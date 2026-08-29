using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using XeptKit.Core;

namespace XeptKit.Input
{
    /// <summary>
    /// <see cref="IInputManager"/> 的默认实现。主线程 only、无锁，全同步。
    /// 按 InputAction 分组管理任务（<see cref="TaskCollection"/>）；分发读任务数组快照（零分配），
    /// 层激活过滤与阻断经 <see cref="HashSet{T}"/>（Type 键）O(1) 检查。
    /// 绑定/解绑重建数组（低频、允许分配），分发读快照（零分配）——对齐 EventBus 模式。
    /// </summary>
    public sealed class InputManager : IInputManager
    {
        private readonly Dictionary<InputAction, TaskCollection> _mapping = new();
        private readonly Dictionary<Type, IInputLayer> _layers = new();
        private readonly HashSet<Type> _inactiveLayers = new();

        /// <inheritdoc />
        public event Action<InputAction, Exception> HandlerException;

        /// <inheritdoc />
        public IDisposable Bind<TLayer>(InputAction action, Action<InputAction.CallbackContext> callback) where TLayer : IInputLayer, new()
        {
            Guard.NotNull(action, nameof(action));
            Guard.NotNull(callback, nameof(callback));

            var layer = GetLayerInstance<TLayer>();

            if (!_mapping.TryGetValue(action, out var collection))
            {
                collection = new TaskCollection(this, action);
                _mapping.Add(action, collection);

                // action 首次有任务时挂三事件；最后一个任务移除时解绑（见 Unbind）
                action.started += OnInputed;
                action.performed += OnInputed;
                action.canceled += OnInputed;
            }

            collection.Add(callback, layer);

            return new BindHandle(() => Unbind<TLayer>(action, callback));
        }

        /// <inheritdoc />
        public bool Unbind<TLayer>(InputAction action, Action<InputAction.CallbackContext> callback)
            where TLayer : IInputLayer, new()
        {
            Guard.NotNull(action, nameof(action));
            Guard.NotNull(callback, nameof(callback));

            if (!_mapping.TryGetValue(action, out var collection))
            {
                return false;
            }

            var layer = GetLayerInstance<TLayer>();
            bool removed = collection.Remove(callback, layer);

            if (collection.IsEmpty)
            {
                action.started -= OnInputed;
                action.performed -= OnInputed;
                action.canceled -= OnInputed;
                _mapping.Remove(action);
            }

            return removed;
        }

        /// <inheritdoc />
        public TLayer GetInputLayer<TLayer>() where TLayer : IInputLayer, new()
            => (TLayer)GetLayerInstance<TLayer>();

        /// <inheritdoc />
        public bool IsLayerActive<TLayer>() where TLayer : IInputLayer, new()
            => !_inactiveLayers.Contains(typeof(TLayer));

        /// <inheritdoc />
        public void SetLayerActive<TLayer>(bool active) where TLayer : IInputLayer, new()
        {
            if (active)
            {
                _inactiveLayers.Remove(typeof(TLayer));
            }
            else
            {
                _inactiveLayers.Add(typeof(TLayer));
            }
        }

        /// <inheritdoc />
        public void Clear()
        {
            foreach (var pair in _mapping)
            {
                pair.Key.started -= OnInputed;
                pair.Key.performed -= OnInputed;
                pair.Key.canceled -= OnInputed;
            }

            _mapping.Clear();
            _layers.Clear();
            _inactiveLayers.Clear();
        }

        /// <summary>
        /// 分发入口：由 InputAction 事件触发，查映射后交给对应任务集合分发。
        /// 三相位（started/performed/canceled）全透传，相位判断归业务（回调内 ctx.phase）。
        /// </summary>
        private void OnInputed(InputAction.CallbackContext ctx)
        {
            if (_mapping.TryGetValue(ctx.action, out var collection))
            {
                collection.Dispatch(ctx);
            }
        }

        private IInputLayer GetLayerInstance<TLayer>() where TLayer : IInputLayer, new()
            => _layers.GetOrAdd(typeof(TLayer), () => new TLayer());

        private void RaiseHandlerException(InputAction action, Exception ex)
        {
            try
            {
                HandlerException?.Invoke(action, ex);
            }
            catch (Exception hookEx)
            {
                Log.Exception(hookEx); // 钩子自身防御：订阅者异常被捕获记日志，不打断分发
            }
        }

        /// <summary>
        /// 单个 action 的任务集合：维护按层优先级降序排列的任务数组。
        /// Add/Remove 重建数组（低频），Dispatch 读快照（零分配）。
        /// </summary>
        private sealed class TaskCollection
        {
            private readonly InputManager _owner;
            private readonly InputAction _action;
            private InputTask[] _entries = Array.Empty<InputTask>();

            public bool IsEmpty => _entries.Length == 0;

            public TaskCollection(InputManager owner, InputAction action)
            {
                _owner = owner;
                _action = action;
            }

            /// <summary>添加任务。同一 (callback, layer) 重复添加仅保留首次（幂等，首次优先级生效）。</summary>
            public void Add(Action<InputAction.CallbackContext> callback, IInputLayer layer)
            {
                for (int i = 0; i < _entries.Length; i++)
                {
                    if (_entries[i].Callback.Equals(callback) && _entries[i].Layer == layer)
                    {
                        return;
                    }
                }

                var newArray = new InputTask[_entries.Length + 1];
                Array.Copy(_entries, newArray, _entries.Length);
                newArray[^1] = new InputTask(callback, layer);

                // 按 priority 降序（值越大越先）。Array.Sort 非稳定，同层顺序无保证。
                Array.Sort(newArray, (a, b) => b.Priority.CompareTo(a.Priority));
                _entries = newArray;
            }

            /// <summary>按 (callback, layer) 精确移除任务（不同层可绑同一 callback）。未命中返回 false。</summary>
            public bool Remove(Action<InputAction.CallbackContext> callback, IInputLayer layer)
            {
                for (int i = 0; i < _entries.Length; i++)
                {
                    if (_entries[i].Callback.Equals(callback) && _entries[i].Layer == layer)
                    {
                        var newArray = new InputTask[_entries.Length - 1];
                        Array.Copy(_entries, 0, newArray, 0, i);
                        Array.Copy(_entries, i + 1, newArray, i, _entries.Length - i - 1);
                        _entries = newArray;
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// 分发（快照遍历 + 层激活过滤 + 阻断）：
            /// - 失活层任务跳过（不执行，其阻断标志自然失效）；
            /// - 激活层任务执行；执行完该层全部任务后，若该层 BlockLowerLayers 为 true → 中断分发
            ///   （其后更低优先级层任务不再执行）。
            /// - 回调异常隔离继续：Log.Exception + HandlerException 钩子（每异常），不中断同组其余回调。
            /// </summary>
            public void Dispatch(InputAction.CallbackContext ctx)
            {
                var entries = _entries; // 快照，回调中增删绑定不影响本次分发
                Type currentLayer = null;
                bool layerActive = true;
                bool blocked = false;

                for (int i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];

                    if (entry.LayerType != currentLayer)
                    {
                        currentLayer = entry.LayerType;

                        if (blocked)
                        {
                            break; // 进入更低层且已被阻断
                        }

                        layerActive = !_owner._inactiveLayers.Contains(currentLayer);
                    }

                    if (!layerActive)
                    {
                        continue; // 失活层任务跳过，不阻断
                    }

                    try
                    {
                        entry.Callback(ctx);
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                        _owner.RaiseHandlerException(_action, ex);
                    }

                    if (entry.Layer.BlockLowerLayers)
                    {
                        blocked = true; // 同层剩余任务继续执行，进入更低层时中断
                    }
                }
            }
        }

        private readonly struct InputTask
        {
            public readonly Action<InputAction.CallbackContext> Callback;
            public readonly IInputLayer Layer;   // 配置读取（BlockLowerLayers 等）
            public readonly Type LayerType;      // 激活检查键（构造时缓存，避免分发时 GetType()）
            public readonly int Priority;        // 排序键（直读字段，避免接口虚调用）

            public InputTask(Action<InputAction.CallbackContext> callback, IInputLayer layer)
            {
                Callback = callback;
                Layer = layer;
                LayerType = layer.GetType();
                Priority = layer.Priority;
            }
        }

        /// <summary>绑定反注册句柄。持解绑闭包，Dispose 执行一次后置空（幂等）。</summary>
        private sealed class BindHandle : IDisposable
        {
            private Action _onDispose;

            public BindHandle(Action onDispose)
            {
                _onDispose = onDispose;
            }

            public void Dispose()
            {
                var onDispose = _onDispose;
                _onDispose = null;
                onDispose?.Invoke();
            }
        }
    }
}
