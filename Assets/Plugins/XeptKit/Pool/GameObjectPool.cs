using System;
using System.Collections.Generic;
using UnityEngine;
using XeptKit.Core;

using Object = UnityEngine.Object;

namespace XeptKit.Pool
{
    /// <summary>
    /// GameObject 对象池。基于 <see cref="Stack{T}"/> 管理闲置实例，基于 <see cref="HashSet{T}"/> 追踪活跃实例，
    /// 支持预热、闲置超时回收、整体销毁与生命周期回调（<see cref="IPoolable"/>）。主线程 only、无锁。
    /// </summary>
    /// <typeparam name="T">池化的 Component 类型</typeparam>
    public sealed class GameObjectPool<T> : IDisposable where T : Component
    {
        private readonly struct PooledInstance
        {
            public readonly T Instance;
            public readonly float ReleaseTime;

            public PooledInstance(T instance, float releaseTime)
            {
                Instance = instance;
                ReleaseTime = releaseTime;
            }
        }

        private readonly Stack<PooledInstance> _inactive = new();
        private readonly HashSet<T> _active = new();
        private readonly T _prefab;
        private readonly Transform _parent;

        /// <summary>当前活跃（已取出未归还）的对象数量。</summary>
        public int ActiveCount => _active.Count;

        /// <summary>当前闲置（已归还可复用）的对象数量。</summary>
        public int InactiveCount => _inactive.Count;

        /// <summary>
        /// 创建 GameObject 对象池。
        /// </summary>
        /// <param name="prefab">用于实例化的组件。不可为 null。</param>
        /// <param name="parent">实例化对象的父级 Transform。可选。</param>
        /// <param name="defaultCapacity">初始预热数量。大于 0 时构造期间自动预热。</param>
        public GameObjectPool(T prefab, Transform parent = null, int defaultCapacity = 0)
        {
            Guard.NotNullObject(prefab, nameof(prefab));
            Guard.InRange(defaultCapacity, 0, int.MaxValue, nameof(defaultCapacity));

            _prefab = prefab;
            _parent = parent;

            if (defaultCapacity > 0)
            {
                Prewarm(defaultCapacity);
            }
        }

        /// <summary>
        /// 从池中获取一个实例。有闲置则复用，否则实例化；返回已激活、已 OnSpawn 的实例。
        /// </summary>
        public T Get()
        {
            T instance;

            if (_inactive.Count > 0)
            {
                instance = _inactive.Pop().Instance;

                // 惰性剔除已被外部 Destroy 的失效对象，防止取用僵尸
                while (instance == null && _inactive.Count > 0)
                {
                    instance = _inactive.Pop().Instance;
                }

                // 全部闲置对象均已失效，回退到实例化
                if (instance == null)
                {
                    instance = InstantiatePrefab();
                }
            }
            else
            {
                instance = InstantiatePrefab();
            }

            instance.gameObject.SetActive(true);
            _active.Add(instance);

            if (instance is IPoolable poolable)
            {
                try
                {
                    poolable.OnSpawn();
                }
                catch (Exception ex)
                {
                    Log.Exception(ex);
                }
            }

            return instance;
        }

        /// <summary>
        /// 将实例归还池中。重复归还、误还他池对象、假 null（外部 Destroy）静默处理。
        /// </summary>
        public void Release(T instance)
        {
            if (instance == null)
            {
                // 假 null：外部 Destroy 的信号，全扫失效引用
                _active.RemoveWhere(item => item == null);
                CleanDestroyedInactive();
                return;
            }

            if (!_active.Remove(instance))
            {
                return; // 重复归还 / 误还他池对象，静默忽略
            }

            if (instance is IPoolable poolable)
            {
                try
                {
                    poolable.OnDespawn();
                }
                catch (Exception ex)
                {
                    Log.Exception(ex);
                }
            }

            instance.gameObject.SetActive(false);

            if (_parent != null)
            {
                instance.transform.SetParent(_parent);
            }

            _inactive.Push(new PooledInstance(instance, Time.time));
        }

        /// <summary>
        /// 预热池，预先实例化指定数量并设为非激活。
        /// </summary>
        public void Prewarm(int count)
        {
            Guard.InRange(count, 0, int.MaxValue, nameof(count));

            for (int i = 0; i < count; i++)
            {
                var instance = InstantiatePrefab();

                if (instance is IPoolable poolable)
                {
                    try
                    {
                        poolable.OnDespawn();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }
                }

                instance.gameObject.SetActive(false);
                _inactive.Push(new PooledInstance(instance, Time.time));
            }
        }

        /// <summary>
        /// 回收闲置时间超过 <paramref name="idleTime"/> 秒的实例（Destroy），并顺带清扫活跃集中的失效引用。
        /// 由业务显式调用（典型经业务定时器周期调用）。
        /// </summary>
        public void Shrink(float idleTime)
        {
            if (idleTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(idleTime), idleTime, "idleTime 必须为非负数。");
            }

            // 顺带全扫活跃集中已被外部 Destroy 的失效引用
            _active.RemoveWhere(item => item == null);

            if (_inactive.Count == 0)
            {
                return;
            }

            float now = Time.time;
            var kept = new Stack<PooledInstance>(_inactive.Count);

            while (_inactive.Count > 0)
            {
                var pooled = _inactive.Pop();
                if (now - pooled.ReleaseTime <= idleTime)
                {
                    kept.Push(pooled);
                }
                else if (pooled.Instance != null)
                {
                    Object.Destroy(pooled.Instance.gameObject);
                }
            }

            // 双栈倒腾恢复原 LIFO 顺序
            while (kept.Count > 0)
            {
                _inactive.Push(kept.Pop());
            }
        }

        /// <summary>
        /// 整体销毁：销毁全部实例（活跃 + 闲置），清空集合。池仍可继续使用。幂等。
        /// </summary>
        public void Clear()
        {
            foreach (var instance in _active)
            {
                if (instance != null)
                {
                    Object.Destroy(instance.gameObject);
                }
            }

            while (_inactive.Count > 0)
            {
                var pooled = _inactive.Pop();
                if (pooled.Instance != null)
                {
                    Object.Destroy(pooled.Instance.gameObject);
                }
            }

            _active.Clear();
        }

        /// <summary>等价 <see cref="Clear"/>，幂等，可进 using / CompositeDisposable。</summary>
        public void Dispose() => Clear();

        private T InstantiatePrefab()
        {
            var instance = Object.Instantiate(_prefab, _parent);
            return instance ?? throw new InvalidOperationException(
                    $"GameObjectPool<{typeof(T).Name}>: Instantiate 返回 null，Prefab 可能已被销毁。");
        }

        /// <summary>从闲置栈中移除所有已被外部 Destroy 的失效对象。</summary>
        private void CleanDestroyedInactive()
        {
            if (_inactive.Count == 0)
            {
                return;
            }

            var valid = new Stack<PooledInstance>(_inactive.Count);
            while (_inactive.Count > 0)
            {
                var entry = _inactive.Pop();
                if (entry.Instance != null)
                {
                    valid.Push(entry);
                }
            }

            // 恢复栈顺序
            while (valid.Count > 0)
            {
                _inactive.Push(valid.Pop());
            }
        }
    }
}
