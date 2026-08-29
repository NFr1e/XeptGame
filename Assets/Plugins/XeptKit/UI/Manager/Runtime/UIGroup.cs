using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using XeptKit.Core;
using Object = UnityEngine.Object;

namespace XeptKit.UI.Manager
{
    /// <summary>
    /// 组内执行者（内部实现类，由 <see cref="UIManager"/> 按 <see cref="UIGroupConfig"/> 懒建）：
    /// 本组链表维护、请求执行、休眠池、重入规则、表单挂载到本组根节点。
    /// 跨组秩序（视觉序/焦点/输入可达/遮挡）由管理器统一仲裁（本类仅经 <see cref="UIManager.NotifyStructureChanged"/> 上报结构变更）。
    /// 主线程 only、无锁。
    /// </summary>
    internal sealed class UIGroup
    {
        /// <summary>单例排队重开（Closing 中 Open 同 Entry → 终结后重开）。</summary>
        private sealed class PendingReopen
        {
            public object Args;
            public CancellationToken CancellationToken;
            public readonly UniTaskCompletionSource<FormHandle> CompletionSource =
                new UniTaskCompletionSource<FormHandle>();
        }

        private readonly UIManager _manager;
        private readonly UIGroupConfig _config;

        /// <summary>每 Entry 一条链表（尾 = 该 Entry 组内顶）。</summary>
        private readonly Dictionary<FormEntry, LinkedList<FormInstance>> _chains =
            new Dictionary<FormEntry, LinkedList<FormInstance>>();

        /// <summary>实例 ID → 节点（句柄失效 / 精确关闭 O(1)）。</summary>
        private readonly Dictionary<int, LinkedListNode<FormInstance>> _idToNode =
            new Dictionary<int, LinkedListNode<FormInstance>>();

        /// <summary>关闭中实例注册表（请求时已出链，供后续 CloseAsync join 等待完整关闭；关闭终结时移除）。</summary>
        private readonly Dictionary<int, FormInstance> _closingById =
            new Dictionary<int, FormInstance>();

        /// <summary>休眠实例池（按 Entry 分组；CacheForms 时关闭入池、打开复用）。</summary>
        private readonly Dictionary<FormEntry, Stack<FormInstance>> _dormant =
            new Dictionary<FormEntry, Stack<FormInstance>>();

        /// <summary>单例排队重开（仅保留最新）。</summary>
        private readonly Dictionary<FormEntry, PendingReopen> _pendingReopens =
            new Dictionary<FormEntry, PendingReopen>();

        /// <summary>Entry 最后打开序列（组内跨 Entry 的视觉/焦点序依据：最后打开者在上）。</summary>
        private readonly Dictionary<FormEntry, long> _entryLastSeq =
            new Dictionary<FormEntry, long>();

        private int _nextInstanceId = 1;
        private long _sequence;

        /// <summary>清场标志：Clear 后置位——in-flight 打开/重开在加载后检测并中止（防僵尸表单）。旧 UIGroup 清场后不再被管理器复用（每次 GetGroup 新建），无需复位。</summary>
        private bool _cleared;

        public UIGroup(UIManager manager, UIGroupConfig config)
        {
            _manager = manager;
            _config = config;
        }

        /// <summary>本组配置（管理器仲裁排序读取）。</summary>
        public UIGroupConfig Config => _config;

        // ===============================================================
        // 打开
        // ===============================================================

        public async UniTask<FormHandle> OpenAsync(FormEntry entry, object args, CancellationToken cancellationToken)
        {
            Guard.NotNullObject(entry, nameof(entry));
            Guard.NotNullObject(entry.Group, "entry.Group");
            Guard.NotNullObject(entry.Prefab, "entry.Prefab");

            if (!_config.Singleton)
            {
                return await OpenInstanceCoreAsync(entry, args, cancellationToken);
            }

            // 单例语义
            if (_chains.TryGetValue(entry, out var chain) && chain.Count > 0)
            {
                var top = chain.Last.Value;
                switch (top.State)
                {
                    case FormInstance.LifecycleState.Opening:
                        // 幂等开启（join）：等待进行中打开的完成——兑现「await 完成 = 已完全打开」契约
                        // （返回句柄而非提前完成，调用方在 OnOpenAsync/入场动画结束后才继续）
                        var completion = top.OpenCompletion;
                        if (completion != null)
                        {
                            return await completion.Task
                                .AttachExternalCancellation(cancellationToken);
                        }

                        return top.Handle; // 防御：完成信号缺失（理论不可达）——回退即时返回

                    case FormInstance.LifecycleState.Opened:
                        // 置顶 + 聚焦（不重播入场）；已在顶则忽略
                        BringToTop(top);
                        return top.Handle;

                    case FormInstance.LifecycleState.Closing:
                        // 排队重开：终结后按新请求处理（仅保留最新）；等待其完成
                        var pending = new PendingReopen
                        {
                            Args = args,
                            CancellationToken = cancellationToken,
                        };
                        if (_pendingReopens.TryGetValue(entry, out var superseded))
                        {
                            // 被新请求取代：旧等待方 OCE（不再永久挂起；与 Clear 的取消语义一致）
                            superseded.CompletionSource.TrySetCanceled();
                        }

                        _pendingReopens[entry] = pending;
                        return await pending.CompletionSource.Task
                            .AttachExternalCancellation(cancellationToken);

                    case FormInstance.LifecycleState.Closed:
                        break; // 理论上不在链中（终结即出链），落到新开
                }
            }

            return await OpenInstanceCoreAsync(entry, args, cancellationToken);
        }

        private async UniTask<FormHandle> OpenInstanceCoreAsync(
            FormEntry entry, object args, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested || _cleared)
            {
                // 调用方取消 / 本组已清场：直接中止（清场 = 取消语义，OCE 静默）
                throw new OperationCanceledException(cancellationToken);
            }

            // 休眠复用（CacheForms）
            if (_dormant.TryGetValue(entry, out var stack) && stack.Count > 0)
            {
                var dormant = stack.Pop();
                if (stack.Count == 0)
                {
                    _dormant.Remove(entry);
                }

                if (dormant.View != null) // 防御：休眠实例可能已被外部销毁（假 null）
                {
                    // 重开分配新 ID：每次打开会话独立身份——旧句柄永久失效（不误伤本会话），对齐「ID 不复用」决议
                    dormant.Handle = new FormHandle(entry, AllocateInstanceId());
                    dormant.State = FormInstance.LifecycleState.Opening;
                    dormant.Covered = false; // 重置遮挡去重标志（复用时避免陈旧态误触发 OnReveal）
                    _manager.ApplyUICameraTo(dormant.View); // 相机注入（复用路径）：休眠期间相机可能已变更——重注入（幂等）
                    AddToChain(dormant);
                    _manager.NotifyStructureChanged(); // 入栈即授
                    return await CompleteOpenAsync(dormant, args, cancellationToken);
                }
            }

            // 全新打开
            var go = await _manager.Loader.InstantiateAsync(entry, cancellationToken);

            if (_cleared)
            {
                // 清场发生在加载期间：本实例尚未入链、不会被清场快照销毁——销毁并中止（防僵尸表单）
                Object.Destroy(go);
                throw new OperationCanceledException(cancellationToken);
            }

            var view = go.GetComponent<UIForm>();
            if (view == null)
            {
                // 手动挂载校验（fail-fast）：UIForm 缺失时配置面（转场/相机注入目标）无法落地
                Object.Destroy(go);
                throw new InvalidOperationException(
                    $"[XeptKit.UI.Manager] FormEntry '{entry.name}' 的 Prefab 根节点缺少 UIForm 组件——" +
                    "请手动挂载 UIForm（可在 Inspector 配置转场与相机注入目标）。");
            }

            var groupRoot = _manager.EnsureGroupRoot(_config); // 懒解析：上下文切换/场景重载后自动取当前根（伪空则重建）
            if (groupRoot != null)
            {
                go.transform.SetParent(groupRoot, false); // Group Root 模型：挂到本组根节点
            }

            _manager.ApplyUICameraTo(view); // 相机注入（显式声明制）：对 cameraSpaceCanvas 声明的画布写入推送相机（未声明不注入）
            var logic = go.GetComponent<FormLogicBase>();
            var instance = new FormInstance(new FormHandle(entry, AllocateInstanceId()), view, logic, this);

            AddToChain(instance);
            _manager.NotifyStructureChanged(); // 入栈即授
            return await CompleteOpenAsync(instance, args, cancellationToken);
        }

        /// <summary>打开收尾（全新与休眠复用共用）：OnOpenAsync → EnterAsync → OnOpened → 自动关闭/事件/遮挡刷新。</summary>
        private async UniTask<FormHandle> CompleteOpenAsync(
            FormInstance instance, object args, CancellationToken cancellationToken)
        {
            var openCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            instance.OpenCts = openCts;
            instance.OpenCompletion = new UniTaskCompletionSource<FormHandle>(); // 打开完成信号（单例 Opening join 等待用）
            var entry = instance.Handle.Entry;

            try
            {
                var logic = instance.Logic;
                instance.View.Handle = instance.Handle; // 会话句柄注入视图（身份单源；全新打开与休眠复用统一经此注入新 ID）
                if (logic != null)
                {
                    logic.View = instance.View;     // 视图引用注入（业务经 View.Handle 触达会话身份）
                    logic.Manager = _manager;       // 管理器引用注入（业务经 Manager.Close(View.Handle) 表达关闭意图）
                    await logic.OnOpenAsync(instance.Handle, args, openCts.Token);
                }

                await instance.View.EnterAsync(openCts.Token);
                if (openCts.IsCancellationRequested)
                {
                    // 中止开启：转场已按取消语义跳至最终态，此处显式 OCE 触发清理（截断入场）
                    throw new OperationCanceledException(openCts.Token);
                }

                instance.State = FormInstance.LifecycleState.Opened;

                try
                {
                    logic?.OnOpened();
                }
                catch (Exception ex)
                {
                    Isolate(ex, entry);
                }

                StartAutoClose(instance);
                _manager.PublishFormOpened(instance.Handle);
                _manager.RefreshCoverStates(); // 打开完成后刷新遮挡通知（born-covered 情形）
                instance.OpenCompletion.TrySetResult(instance.Handle);
                return instance.Handle;
            }
            catch (OperationCanceledException)
            {
                // 取消 / 中止开启：移除节点（幂等）+ 销毁 + 焦点重算，OCE 上抛（静默）
                CleanupFailedOpen(instance);
                instance.OpenCompletion.TrySetCanceled();
                throw;
            }
            catch (Exception ex)
            {
                // 开启失败：同上清理，异常传播（fail-fast）
                CleanupFailedOpen(instance);
                instance.OpenCompletion.TrySetException(ex);
                throw;
            }
            finally
            {
                instance.OpenCts = null;
                instance.OpenCompletion = null; // 会话结束清引用（下一会话重建）
                openCts.Dispose();
            }
        }

        /// <summary>开启失败/取消的清理：移除节点（幂等）、销毁实例、结构已变则重仲裁。</summary>
        private void CleanupFailedOpen(FormInstance instance)
        {
            instance.CancelAutoClose();
            bool removed = RemoveFromChain(instance);
            DestroyInstance(instance);
            if (removed)
            {
                _manager.NotifyStructureChanged();
            }
        }

        // ---- 自动关闭 ----

        private void StartAutoClose(FormInstance instance)
        {
            float seconds = instance.Handle.Entry.AutoCloseSeconds;
            if (seconds <= 0f)
            {
                return;
            }

            var cts = new CancellationTokenSource();
            instance.AutoCloseCts = cts;
            _ = AutoCloseAsync(instance, seconds, cts);
        }

        private async UniTask AutoCloseAsync(FormInstance instance, float seconds, CancellationTokenSource cts)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: cts.Token);
                Close(instance.Handle); // 正常关闭流程（幂等：句柄已失效则 no-op）
            }
            catch (OperationCanceledException)
            {
                // 关闭/清场/中止时取消计时——正常流程，静默
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
            finally
            {
                if (ReferenceEquals(instance.AutoCloseCts, cts))
                {
                    instance.AutoCloseCts = null;
                    cts.Dispose();
                }
            }
        }

        // ===============================================================
        // 关闭
        // ===============================================================

        public void Close(FormHandle handle)
        {
            if (TryGetInstance(handle, out var instance))
            {
                CloseInstance(instance);
            }
        }

        public UniTask CloseAsync(FormHandle handle)
        {
            if (TryGetInstance(handle, out var instance))
            {
                return CloseInstance(instance);
            }

            // 已出链但关闭流程进行中（Close/CloseAsync 已发起）→ join 等待完整关闭（含出场动画与终结）
            if (_closingById.TryGetValue(handle.InstanceId, out var closing)
                && closing.CloseCompletion != null)
            {
                return closing.CloseCompletion.Task;
            }

            return UniTask.CompletedTask; // 幂等（已终结/不存在）
        }

        private UniTask CloseInstance(FormInstance instance)
        {
            switch (instance.State)
            {
                case FormInstance.LifecycleState.Opening:
                    // 中止开启：移除节点（句柄随之失效）+ 取消打开令牌；
                    // 销毁由打开流程的清理路径完成（OCE 触发 CleanupFailedOpen）
                    if (RemoveFromChain(instance))
                    {
                        _manager.NotifyStructureChanged();
                    }

                    instance.CancelOpen();
                    return UniTask.CompletedTask;

                case FormInstance.LifecycleState.Closing:
                case FormInstance.LifecycleState.Closed:
                    return UniTask.CompletedTask; // 幂等

                default:
                    break;
            }

            // Opened → 正常关闭：请求时移除节点（句柄随之失效）+ 焦点重算
            instance.State = FormInstance.LifecycleState.Closing;
            RemoveFromChain(instance);
            instance.CancelAutoClose();
            instance.CloseCompletion = new UniTaskCompletionSource(); // 关闭完成信号（CloseAsync join 用）
            _closingById[instance.Handle.InstanceId] = instance;
            _manager.NotifyStructureChanged();

            return CloseInternalAsync(instance);
        }

        private async UniTask CloseInternalAsync(FormInstance instance)
        {
            var entry = instance.Handle.Entry;
            var logic = instance.Logic;

            try
            {
                try
                {
                    if (logic != null)
                    {
                        await logic.OnCloseAsync();
                    }
                }
                catch (Exception ex)
                {
                    Isolate(ex, entry);
                }

                try
                {
                    await instance.View.ExitAsync();
                }
                catch (Exception ex)
                {
                    Isolate(ex, entry);
                }

                try
                {
                    logic?.OnClosed();
                }
                catch (Exception ex)
                {
                    Isolate(ex, entry);
                }
            }
            finally
            {
                // 关闭是必完成操作：无论逻辑/动画异常，终结照常执行
                TerminateInstance(instance);
                instance.State = FormInstance.LifecycleState.Closed;
                _manager.PublishFormClosed(instance.Handle);
                ProcessPendingReopen(entry);

                // 完成关闭信号并注销（join 等待方此刻解除）
                var completion = instance.CloseCompletion;
                instance.CloseCompletion = null;
                _closingById.Remove(instance.Handle.InstanceId);
                completion?.TrySetResult();
            }
        }

        private void TerminateInstance(FormInstance instance)
        {
            if (_config.CacheForms)
            {
                // 休眠：去激活并入池（复用走完整 OnOpenAsync 重置，状态重置责任有主）
                instance.View.gameObject.SetActive(false);
                var stack = _dormant.GetOrAdd(instance.Handle.Entry, () => new Stack<FormInstance>());
                if (_config.CacheCapacity > 0 && stack.Count >= _config.CacheCapacity)
                {
                    // 容量超限：直接销毁（防休眠池无界增长）
                    instance.View.Terminate();
                    return;
                }

                stack.Push(instance);
            }
            else
            {
                instance.View.Terminate(); // 销毁
            }
        }

        private void DestroyInstance(FormInstance instance)
        {
            instance.CancelAutoClose();
            instance.View.Terminate();
        }

        private void ProcessPendingReopen(FormEntry entry)
        {
            if (!_pendingReopens.TryGetValue(entry, out var pending))
            {
                return;
            }

            _pendingReopens.Remove(entry);
            _ = ReopenAsync(entry, pending);
        }

        private async UniTask ReopenAsync(FormEntry entry, PendingReopen pending)
        {
            try
            {
                var handle = await OpenInstanceCoreAsync(entry, pending.Args, pending.CancellationToken);
                pending.CompletionSource.TrySetResult(handle);
            }
            catch (OperationCanceledException)
            {
                pending.CompletionSource.TrySetCanceled();
            }
            catch (Exception ex)
            {
                pending.CompletionSource.TrySetException(ex);
            }
        }

        // ===============================================================
        // 查询 / 单例
        // ===============================================================

        public FormHandle GetOpened(FormEntry entry)
        {
            Guard.NotNullObject(entry, nameof(entry));
            if (_chains.TryGetValue(entry, out var chain) && chain.Count > 0)
            {
                return chain.Last.Value.Handle;
            }

            return default;
        }

        /// <summary>获取该 Entry 最顶实例的用户逻辑组件（未打开返回 null）。</summary>
        public T GetFormLogic<T>(FormEntry entry) where T : FormLogicBase
        {
            Guard.NotNullObject(entry, nameof(entry));
            if (_chains.TryGetValue(entry, out var chain) && chain.Count > 0)
            {
                return chain.Last.Value.Logic as T;
            }

            return null;
        }

        public bool IsValid(FormHandle handle)
            => handle.Entry != null && _idToNode.ContainsKey(handle.InstanceId);

        public bool TryGetInstance(FormHandle handle, out FormInstance instance)
        {
            instance = null;
            if (_idToNode.TryGetValue(handle.InstanceId, out var node) && node.Value != null)
            {
                instance = node.Value;
                return true;
            }

            return false;
        }

        private void BringToTop(FormInstance instance)
        {
            var node = instance.Node;
            var chain = node?.List;
            if (chain == null || chain.Last == node)
            {
                return; // 已在顶
            }

            chain.Remove(node);
            instance.Node = chain.AddLast(instance);
            _manager.NotifyStructureChanged();
        }

        private int AllocateInstanceId() => _nextInstanceId++;

        private void AddToChain(FormInstance instance)
        {
            var entry = instance.Handle.Entry;
            var chain = _chains.GetOrAdd(entry, () => new LinkedList<FormInstance>());
            instance.Node = chain.AddLast(instance);
            _idToNode[instance.Handle.InstanceId] = instance.Node;
            _entryLastSeq[entry] = _sequence++;
        }

        /// <summary>O(1) 移除；空链顺带清理。返回是否实际移除（幂等）。</summary>
        private bool RemoveFromChain(FormInstance instance)
        {
            var node = instance.Node;
            var chain = node?.List;
            if (chain == null)
            {
                return false;
            }

            chain.Remove(node);
            instance.Node = null;
            _idToNode.Remove(instance.Handle.InstanceId);

            if (chain.Count == 0)
            {
                var entry = instance.Handle.Entry;
                _chains.Remove(entry);
                _entryLastSeq.Remove(entry);
            }

            return true;
        }

        // ===============================================================
        // 仲裁数据（管理器统一仲裁时读取）
        // ===============================================================

        /// <summary>
        /// 收集本组存活实例（组内顶 → 底）：多 Entry 按最后打开时刻降序（最后打开者在上），Entry 内尾 → 头。
        /// </summary>
        public void CollectTopToBottom(List<FormInstance> list)
        {
            if (_chains.Count == 0)
            {
                return;
            }

            if (_chains.Count == 1)
            {
                foreach (var chain in _chains.Values)
                {
                    AppendChainTopToBottom(chain, list);
                }

                return;
            }

            var entries = new List<KeyValuePair<FormEntry, long>>(_entryLastSeq.Count);
            foreach (var kv in _entryLastSeq)
            {
                if (_chains.ContainsKey(kv.Key))
                {
                    entries.Add(kv);
                }
            }

            entries.Sort((a, b) => b.Value.CompareTo(a.Value));
            foreach (var kv in entries)
            {
                if (_chains.TryGetValue(kv.Key, out var chain))
                {
                    AppendChainTopToBottom(chain, list);
                }
            }
        }

        private static void AppendChainTopToBottom(LinkedList<FormInstance> chain, List<FormInstance> list)
        {
            var node = chain.Last;
            while (node != null)
            {
                list.Add(node.Value);
                node = node.Previous;
            }
        }

        /// <summary>相机变更刷新（SetUIContext 相机变更契约）：对组内全部实例（链上 + 关闭中 + 休眠）重注入当前相机（幂等）。</summary>
        internal void ReapplyUICamera(Camera camera)
        {
            foreach (var chain in _chains.Values)
            {
                foreach (var instance in chain)
                {
                    if (instance.View != null)
                    {
                        instance.View.ApplyUICamera(camera);
                    }
                }
            }

            foreach (var instance in _closingById.Values)
            {
                if (instance.View != null)
                {
                    instance.View.ApplyUICamera(camera);
                }
            }

            foreach (var stack in _dormant.Values)
            {
                foreach (var instance in stack)
                {
                    if (instance.View != null)
                    {
                        instance.View.ApplyUICamera(camera);
                    }
                }
            }
        }

        // ===============================================================
        // 清理
        // ===============================================================

        public void Clear()
        {
            _cleared = true; // 清场标志：in-flight 打开/重开在加载后检测并中止（防僵尸表单，见 OpenInstanceCoreAsync）

            // 快照全部存活实例：取消/终止在快照上执行——杜绝「取消同步触发打开流程清理并改 _chains」的
            // 任何潜在路径（当前 UniTask 取消延续异步于 PlayerLoop，枚举本安全；快照使安全性局部化，不依赖调度细节）
            var instances = new List<FormInstance>(_idToNode.Count);
            foreach (var chain in _chains.Values)
            {
                foreach (var instance in chain)
                {
                    instances.Add(instance);
                }
            }

            // 取消打开中的（其清理路径自行收尾；视图由下方强制销毁兜底，双销毁安全）
            foreach (var instance in instances)
            {
                instance.CancelOpen();
            }

            // 强制终止全部存活实例（跳过动画、不等待逻辑收尾）
            foreach (var instance in instances)
            {
                instance.CancelAutoClose();
                instance.State = FormInstance.LifecycleState.Closed;
                instance.View.Terminate();
            }

            _chains.Clear();
            _idToNode.Clear();
            _entryLastSeq.Clear();

            // 释放休眠实例
            foreach (var stack in _dormant.Values)
            {
                while (stack.Count > 0)
                {
                    stack.Pop().View.Terminate();
                }
            }

            _dormant.Clear();

            // 未决排队重开：取消等待方
            foreach (var pending in _pendingReopens.Values)
            {
                pending.CompletionSource.TrySetCanceled();
            }

            _pendingReopens.Clear();
        }

        private void Isolate(Exception ex, FormEntry entry)
        {
            Log.Exception(ex);
            _manager.RaiseFormException(entry, ex);
        }
    }
}
