using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
using XeptKit.Core;
using XeptKit.Event;

namespace XeptKit.Scenes
{
    /// <summary>
    /// <see cref="IScenesManager"/> 的默认实现。
    /// 基于优先级队列 + 可配置并发度的场景加载引擎（链式补位：入队即调度、完成时补位）。
    /// </summary>
    /// <remarks>
    /// 构造注入事件总线（必传，向该总线同步广播场景生命周期事件）；
    /// 主场景唯一权威在管理器内部（_mainScene），句柄不承载主场景标记；
    /// 卸载请求优先级恒为 int.MaxValue（先于一切加载）。
    /// 同一会话内建议仅一个管理器实例驱动场景操作；实例由业务组合根创建并持有
    /// （本库不设模块门面，见 docs/DESIGN.md §6）。
    /// </remarks>
    public sealed class ScenesManager : IScenesManager
    {
        private readonly IEventBus _eventBus;
        private readonly int _maxConcurrentLoads;

        // 请求队列：按 (Priority DESC, Sequence ASC) 有序
        private readonly List<SceneRequest> _pending = new();
        private readonly List<SceneRequest> _active = new();
        private long _sequence;

        // 已加载场景追踪 + 主场景权威
        private readonly List<SceneHandle> _loadedScenes = new();
        private readonly ReadOnlyCollection<SceneHandle> _loadedScenesView;
        private SceneHandle _mainScene;

        // 切换门闩（评审修订 P1）：并发切换不支持，fail-fast——入口同步抛 InvalidOperationException；
        // _transitionEntered 区分过渡进入是否成功（决议 C：进入失败不执行 ExitAsync）
        private bool _switchInProgress;
        private bool _transitionEntered;

        /// <summary>
        /// 创建场景管理器实例。
        /// </summary>
        /// <param name="eventBus">场景生命周期事件广播目标（必传，Guard.NotNull）。</param>
        /// <param name="maxConcurrentLoads">最大同时执行请求数。1 为严格串行（推荐默认）。</param>
        public ScenesManager(IEventBus eventBus, int maxConcurrentLoads = 1)
        {
            Guard.NotNull(eventBus, nameof(eventBus));
            Guard.InRange(maxConcurrentLoads, 1, int.MaxValue, nameof(maxConcurrentLoads));

            _eventBus = eventBus;
            _maxConcurrentLoads = maxConcurrentLoads;
            _loadedScenesView = _loadedScenes.AsReadOnly();
        }

        // ============================================================
        // 加载 / 卸载 / 切换
        // ============================================================

        /// <inheritdoc />
        public async UniTask<SceneHandle> LoadSceneAsync(
            SceneReference sceneRef,
            SceneLoadOptions options = default,
            CancellationToken cancellationToken = default)
        {
            Guard.True(sceneRef.IsValid, $"SceneReference 无效（路径为空）：{sceneRef}");

            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            var request = new SceneLoadRequest(sceneRef, options, _eventBus, NextSequence(), cancellationToken);
            Enqueue(request);

            await request.CompletionTask.AttachExternalCancellation(cancellationToken);
            return request.Handle;
        }

        /// <inheritdoc />
        public async UniTask UnloadSceneAsync(
            SceneHandle handle,
            CancellationToken cancellationToken = default)
        {
            Guard.NotNull(handle, nameof(handle));

            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            var request = new SceneUnloadRequest(handle, _eventBus, NextSequence(), cancellationToken);
            Enqueue(request);

            await request.CompletionTask.AttachExternalCancellation(cancellationToken);
        }

        /// <inheritdoc />
        public UniTask<SceneHandle> SwitchMainSceneAsync(
            SceneReference sceneRef,
            ISceneTransition transition = null,
            CancellationToken cancellationToken = default)
        {
            // 并发切换不支持（评审修订 P1，fail-fast）：切换是"清场 → 卸载 → 加载 → 设活跃"的复合流程，
            // 管理器只对单个 Load/Unload 请求做队列串行，不提供切换级串行化——两个切换会在各自 await 之间
            // 交错（互相取消对方的新主场景加载、重复卸载同一旧场景、返回句柄与最终 _mainScene 失实）。
            // 不做静默排队：排队切换会让第二次调用"迟到生效"，意图陈旧且延迟不可预期；业务层负责防抖/互斥。
            // 门闩检查置于 async 方法之外——async 方法体内抛出的异常会被捕获进返回的 UniTask 而非同步抛出，
            // 故此处同步抛 InvalidOperationException（fail-fast），不进入复合流程。
            if (_switchInProgress)
            {
                throw new InvalidOperationException(
                    "[ScenesManager] SwitchMainSceneAsync already in progress — concurrent switches are not supported " +
                    "(fail-fast); debounce/mutex at business layer (e.g. UI double-click).");
            }

            return SwitchMainSceneCoreAsync(sceneRef, transition, cancellationToken);
        }

        /// <summary>切换复合流程本体（门闩已在 <see cref="SwitchMainSceneAsync"/> 入口校验并放行，此处直接置位）。</summary>
        private async UniTask<SceneHandle> SwitchMainSceneCoreAsync(
            SceneReference sceneRef,
            ISceneTransition transition,
            CancellationToken cancellationToken)
        {
            Guard.True(sceneRef.IsValid, $"SceneReference 无效（路径为空）：{sceneRef}");

            _switchInProgress = true;
            _transitionEntered = false;

            try
            {
                // 1. 过渡进入（遮住画面，在清场/卸载之前；不响应切换令牌，见 ISceneTransition）
                //    进入成功才标记 _transitionEntered——EnterAsync 失败直接上抛、不执行 ExitAsync（§8/§10 决议 C）
                if (transition != null)
                {
                    await transition.EnterAsync();
                    _transitionEntered = true;
                }

                // 0. 清场（决议 A）：取消全部非持久加载请求并等待其终止
                await CancelNonPersistentLoadsAsync();

                // 2. 卸载全部非持久场景（此时已无排队加载残留）
                var toUnload = new List<SceneHandle>();
                for (int i = 0; i < _loadedScenes.Count; i++)
                {
                    if (!_loadedScenes[i].IsPersistent)
                    {
                        toUnload.Add(_loadedScenes[i]);
                    }
                }

                var unloadTasks = new List<UniTask>(toUnload.Count);
                foreach (var handle in toUnload)
                {
                    unloadTasks.Add(UnloadSceneAsync(handle, cancellationToken));
                }

                if (unloadTasks.Count > 0)
                {
                    await UniTask.WhenAll(unloadTasks);
                }

                // 3. 加载新主场景
                SceneHandle newMainScene = await LoadSceneAsync(
                    sceneRef,
                    new SceneLoadOptions(isMainScene: true),
                    cancellationToken);

                // 4. 设为 Unity 活跃场景
                if (newMainScene.Scene.isLoaded)
                {
                    SceneManager.SetActiveScene(newMainScene.Scene);
                }

                return newMainScene;
            }
            finally
            {
                // 5. 过渡退出（揭开画面；仅进入成功才执行；不响应切换令牌）
                //    门闩保持至过渡退出完成（评审修订 P2）：若先释放门闩，第二次切换会在第一次淡出
                //    尚未结束时进入并开始新的过渡/清场/加载——两次过渡效果重叠、闪帧或暴露中间状态。
                try
                {
                    if (_transitionEntered)
                    {
                        await transition.ExitAsync();
                    }
                }
                finally
                {
                    // 6. 门闩释放——ExitAsync 抛异常也必须释放（内层 finally），不永久锁死；失败/取消路径同样释放
                    _switchInProgress = false;
                }
            }
        }

        // ============================================================
        // 场景组
        // ============================================================

        /// <inheritdoc />
        public async UniTask<SceneGroupHandle> LoadSceneGroupAsync(
            SceneGroup group,
            CancellationToken cancellationToken = default)
        {
            Guard.NotNull(group, nameof(group));
            Guard.True(group.Entries != null && group.Entries.Length > 0, "SceneGroup 无条目。");

            var entries = group.Entries;
            var tasks = new UniTask<SceneHandle>[entries.Length];
            var handles = new List<SceneHandle>(entries.Length);

            // 组内主场景唯一化（决议 §8 运行时宽容"首个生效"）：仅首个 IsMainScene 条目携带标记，
            // 保证管理器 _mainScene 与组句柄 MainScene 一致（都取首个）；多余标记仅计数用于 Warning。
            bool mainSceneAssigned = false;
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                Guard.True(entry.SceneRef.IsValid, $"SceneGroup 条目 [{i}] 的场景引用无效。");

                bool isMain = entry.IsMainScene && !mainSceneAssigned;
                if (entry.IsMainScene)
                {
                    mainSceneAssigned = true;
                }

                var options = new SceneLoadOptions(
                    isMainScene: isMain,
                    persistent: entry.Persistent);

                tasks[i] = LoadSceneAsync(entry.SceneRef, options, cancellationToken);
            }

            // 全部入队后并行等待；任一条目失败 WhenAll 抛异常（已加载条目保留，不回滚）
            var results = await UniTask.WhenAll(tasks);
            handles.AddRange(results);

            // 组内主场景：首个 IsMainScene 条目的句柄；无标记由 SceneGroupHandle 内部兜底
            SceneHandle mainScene = null;
            int mainSceneCount = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].IsMainScene)
                {
                    if (mainScene == null)
                    {
                        mainScene = handles[i];
                    }
                    mainSceneCount++;
                }
            }

            if (mainSceneCount > 1)
            {
                // 运行时宽容：首个生效 + 信息性提示（编辑期由 SceneGroupEditor 强制）
                Log.Warning($"SceneGroup '{group.name}' 有 {mainSceneCount} 个 IsMainScene 条目，仅首个生效。");
            }

            return new SceneGroupHandle(handles, mainScene);
        }

        /// <inheritdoc />
        public async UniTask UnloadSceneGroupAsync(
            SceneGroupHandle groupHandle,
            CancellationToken cancellationToken = default)
        {
            Guard.NotNull(groupHandle, nameof(groupHandle));

            var unloadTasks = new List<UniTask>();

            foreach (var handle in groupHandle.Handles)
            {
                // 幂等：只卸载仍在追踪中且非 Unloaded 的场景
                if (_loadedScenes.Contains(handle) && handle.State != SceneState.Unloaded)
                {
                    unloadTasks.Add(UnloadSceneAsync(handle, cancellationToken));
                }
            }

            if (unloadTasks.Count > 0)
            {
                await UniTask.WhenAll(unloadTasks);
            }
        }

        // ============================================================
        // 查询
        // ============================================================

        /// <inheritdoc />
        public IReadOnlyList<SceneHandle> LoadedScenes => _loadedScenesView;

        /// <inheritdoc />
        public SceneHandle CurrentMainScene => _mainScene;

        /// <inheritdoc />
        public bool SceneExists(SceneReference sceneRef)
        {
            if (!sceneRef.IsValid) return false;
            return SceneUtility.GetBuildIndexByScenePath(sceneRef.ScenePath) >= 0;
        }

        /// <inheritdoc />
        public bool IsSceneLoaded(SceneReference sceneRef)
        {
            for (int i = 0; i < _loadedScenes.Count; i++)
            {
                if (_loadedScenes[i].SceneRef == sceneRef &&
                    _loadedScenes[i].State == SceneState.Active)
                {
                    return true;
                }
            }
            return false;
        }

        /// <inheritdoc />
        public void SetActiveScene(SceneHandle handle)
        {
            Guard.NotNull(handle, nameof(handle));
            handle.SetAsActiveScene();
        }

        /// <inheritdoc />
        public float OverallProgress
        {
            get
            {
                float sum = 0f;
                int count = 0;
                for (int i = 0; i < _loadedScenes.Count; i++)
                {
                    var handle = _loadedScenes[i];
                    if (handle.State is SceneState.Pending
                                     or SceneState.Loading
                                     or SceneState.Ready
                                     or SceneState.Activating)
                    {
                        sum += handle.Progress;
                        count++;
                    }
                }
                return count == 0 ? 1f : sum / count;
            }
        }

        /// <inheritdoc />
        public int PendingRequestCount => _pending.Count;

        /// <inheritdoc />
        public void Clear()
        {
            // 取消全部请求；pending 立即释放，active 由 ExecuteAndCleanup 收尾时释放
            for (int i = 0; i < _active.Count; i++)
            {
                _active[i].Cancel();
            }
            for (int i = 0; i < _pending.Count; i++)
            {
                _pending[i].Cancel();
                _pending[i].Dispose();
            }

            _pending.Clear();
            _active.Clear();
            _loadedScenes.Clear();
            _mainScene = null;
        }

        // ============================================================
        // 内部：队列管理（链式补位）
        // ============================================================

        private long NextSequence() => ++_sequence;

        private void Enqueue(SceneRequest request)
        {
            // 仅 Load 请求将句柄加入已加载追踪（其 Handle 恒非 null）；Unload 请求复用已有句柄
            if (request is SceneLoadRequest)
            {
                _loadedScenes.Add(request.Handle);
            }

            InsertSorted(request);
            TryStartNext();
        }

        /// <summary>按 (Priority DESC, Sequence ASC) 插入有序队列。</summary>
        private void InsertSorted(SceneRequest request)
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                int priorityDiff = request.Priority.CompareTo(_pending[i].Priority); // 降序
                if (priorityDiff > 0)
                {
                    _pending.Insert(i, request);
                    return;
                }
                if (priorityDiff == 0 && request.SequenceNumber < _pending[i].SequenceNumber)
                {
                    _pending.Insert(i, request);
                    return;
                }
            }
            _pending.Add(request);
        }

        /// <summary>链式补位：填满空闲槽位（跳过已取消请求）。</summary>
        private void TryStartNext()
        {
            while (_active.Count < _maxConcurrentLoads)
            {
                if (_pending.Count == 0) return;

                SceneRequest request = _pending[0];
                _pending.RemoveAt(0);

                if (request.Token.IsCancellationRequested)
                {
                    // 排队中取消：以取消结束、清理追踪并释放请求
                    request.CancelBeforeExecution();
                    UpdateTracking(request);
                    request.Dispose();
                    continue;
                }

                _active.Add(request);
                ExecuteAndCleanup(request).Forget();
            }
        }

        /// <summary>执行请求并在收尾时补位（fire-and-forget，异常隔离，无未观察异常——请求内部保证设置完成源）。</summary>
        private async UniTask ExecuteAndCleanup(SceneRequest request)
        {
            try
            {
                await request.ExecuteAsync();
            }
            finally
            {
                _active.Remove(request);
                UpdateTracking(request);
                request.Dispose();
                TryStartNext();
            }
        }

        /// <summary>
        /// 按请求结果更新追踪（幂等，Clear/清场并发安全）：
        /// 加载——仅 Active 保留；失败/取消移除。卸载——Unloaded（成功/取消收尾）移除；Failed 保留（场景仍在）。
        /// </summary>
        private void UpdateTracking(SceneRequest request)
        {
            if (request.Handle == null) return;

            switch (request)
            {
                case SceneLoadRequest loadRequest:
                    if (request.Status == SceneState.Active)
                    {
                        if (loadRequest.IsMainScene)
                        {
                            _mainScene = request.Handle;
                        }
                    }
                    else
                    {
                        _loadedScenes.Remove(request.Handle);
                    }
                    break;

                case SceneUnloadRequest _:
                    if (request.Status == SceneState.Unloaded)
                    {
                        _loadedScenes.Remove(request.Handle);
                        if (ReferenceEquals(_mainScene, request.Handle))
                        {
                            _mainScene = null;
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// 清场（决议 A）：取消全部非持久加载请求（排队中 + 执行中）并等待其终止，
        /// 随后才进入卸载阶段——保证切换后无旧场景加载残留、无双重卸载竞态。
        /// </summary>
        private async UniTask CancelNonPersistentLoadsAsync()
        {
            var nonPersistentLoads = new List<SceneRequest>();

            for (int i = 0; i < _pending.Count; i++)
            {
                if (IsNonPersistentLoad(_pending[i])) nonPersistentLoads.Add(_pending[i]);
            }
            for (int i = 0; i < _active.Count; i++)
            {
                if (IsNonPersistentLoad(_active[i])) nonPersistentLoads.Add(_active[i]);
            }

            if (nonPersistentLoads.Count == 0) return;

            foreach (var request in nonPersistentLoads)
            {
                // pending：直接移除并以取消结束；active：触发取消清理路径
                if (_pending.Remove(request))
                {
                    request.CancelBeforeExecution();
                    UpdateTracking(request);
                    request.Dispose();
                }
                else
                {
                    request.Cancel();
                }
            }

            // 等待被取消的 active 请求终止（走收敛 + 卸载残留清理后以取消结束）
            foreach (var request in nonPersistentLoads)
            {
                try
                {
                    await request.CompletionTask;
                }
                catch (OperationCanceledException)
                {
                    // 取消是预期结果
                }
            }
        }

        private static bool IsNonPersistentLoad(SceneRequest request) =>
            request is SceneLoadRequest loadRequest &&
            loadRequest.Handle != null &&
            !loadRequest.Handle.IsPersistent;
    }
}
