using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
using XeptKit.Event;

namespace XeptKit.Scenes
{
    /// <summary>
    /// 场景加载请求——Command 模式的具体 Command。
    /// 封装 Unity SceneManager.LoadSceneAsync 的完整生命周期（Loading → Ready → Activating → Active），
    /// 取消走"收敛至 0.9 → 放行激活 → 卸载残留"清理路径（Unity AsyncOperation 无法真正中止，见 §14 评审修订 P0）。
    /// </summary>
    internal sealed class SceneLoadRequest : SceneRequest
    {
        private readonly SceneLoadOptions _options;
        private readonly IEventBus _eventBus;

        /// <summary>是否设为主场景（供管理器更新主场景权威）。</summary>
        public bool IsMainScene => _options.IsMainScene;

        public SceneLoadRequest(
            SceneReference sceneRef,
            SceneLoadOptions options,
            IEventBus eventBus,
            long sequenceNumber,
            CancellationToken cancellationToken)
            : base(sceneRef, options.Priority, sequenceNumber, cancellationToken)
        {
            _options = options;
            _eventBus = eventBus;

            Handle = new SceneHandle(sceneRef, options.Persistent);
        }

        /// <summary>
        /// 排队中被取消（未执行）：完成请求完成源，并同步完成句柄加载完成源（§5 P1 全终态完成，
        /// 持句柄调用方 WaitForCompletionAsync 抛 OCE，不永久等待）。
        /// </summary>
        public override void CancelBeforeExecution()
        {
            base.CancelBeforeExecution();
            Handle.LoadCompletionSource.TrySetCanceled();
        }

        internal override async UniTask ExecuteAsync()
        {
            if (Token.IsCancellationRequested)
            {
                // 已取消才进入执行：与排队取消同语义（§5 全终态完成）
                CompleteHandleLoadSource(false, null);
                CompletionSource.TrySetCanceled();
                return;
            }

            // --- 阶段 1: 加载至 90% ---
            SetStatus(SceneState.Loading);
            SetProgress(0f);

            AsyncOperation op = SceneManager.LoadSceneAsync(SceneRef.ScenePath, LoadSceneMode.Additive);
            if (op == null)
            {
                string error = $"Failed to start loading scene '{SceneRef.SceneName}' — scene may not be in Build Settings or path is invalid.";

                SetStatus(SceneState.Failed);
                SetResult(false, "Load", error);
                CompleteHandleLoadSource(false, error); // §5 P1：失败终态完成句柄完成源（TrySetException）
                _eventBus.Publish(new SceneLoadFailedEvent(SceneRef, error));

                return;
            }

            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
            {
                if (Token.IsCancellationRequested)
                {
                    await CleanupOnCancelAsync(op);
                    return;
                }

                SetProgress(op.progress);
                _eventBus.Publish(new SceneLoadingEvent(Handle, Progress));
                await UniTask.Yield();
            }

            // --- 阶段 2: 等待激活 ---
            SetProgress(0.9f);
            SetStatus(SceneState.Ready);

            if (!_options.ActivateOnLoad)
            {
                // 等待外部调用 Handle.ActivateAsync()；期间响应取消（走清理路径）
                while (!Handle.ActivationSource.Task.Status.IsCompleted())
                {
                    if (Token.IsCancellationRequested)
                    {
                        await CleanupOnCancelAsync(op);
                        return;
                    }

                    await UniTask.Yield();
                }
            }

            // --- 阶段 3: 激活 ---
            SetStatus(SceneState.Activating);
            op.allowSceneActivation = true;

            while (!op.isDone)
            {
                if (Token.IsCancellationRequested)
                {
                    await CleanupOnCancelAsync(op);
                    return;
                }

                await UniTask.Yield();
            }

            // --- 阶段 4: 完成 ---
            SetProgress(1.0f);
            SetStatus(SceneState.Active);

            Scene loadedScene = SceneManager.GetSceneByPath(SceneRef.ScenePath);
            Handle.Scene = loadedScene;

            // 完成加载完成信号（WaitForCompletionAsync / 组句柄 WaitForCompletionAsync 依赖）
            CompleteHandleLoadSource(true, null);

            SetResult(true, "Load");
            _eventBus.Publish(new SceneLoadedEvent(Handle));
        }

        /// <summary>
        /// 完成句柄加载完成源（§5 P1 全终态完成）：与请求完成源同终态、不漂移——
        /// 成功 TrySetResult / 失败 TrySetException(SceneOperationException) / 取消 TrySetCanceled。
        /// </summary>
        private void CompleteHandleLoadSource(bool success, string error)
        {
            if (success)
            {
                Handle.LoadCompletionSource.TrySetResult();
            }
            else if (Token.IsCancellationRequested)
            {
                Handle.LoadCompletionSource.TrySetCanceled();
            }
            else
            {
                Handle.LoadCompletionSource.TrySetException(
                    new SceneOperationException(SceneRef, "Load", error ?? $"Scene operation failed: {SceneRef.SceneName}"));
            }
        }

        /// <summary>
        /// 取消清理（§14 加载取消契约）：等待底层操作收敛至 0.9 → 放行激活并等待完成 →
        /// 卸载残留场景 → 终态 Unloaded → 以取消结束。
        /// 收敛/激活/卸载等待均不响应取消（避免清理被二次取消打断）。
        /// </summary>
        private async UniTask CleanupOnCancelAsync(AsyncOperation op)
        {
            // 1. 等待收敛至 0.9（不响应取消）
            while (op.progress < 0.9f)
            {
                await UniTask.Yield();
            }

            // 2. 放行激活并等待操作完成（§14 评审修订 P0）：
            //    allowSceneActivation = false 时 op 停在 0.9，场景尚未被 SceneManager 承认已加载
            //    （GetSceneByPath(...).isLoaded == false），直接卸载会被跳过，遗留停驻 0.9 的
            //    AsyncOperation 与驻留内存且不可再回收。必须先放行激活、等 op.isDone——
            //    场景对象经一次 Awake 等初始化后被 SceneManager 承认已加载，方可卸载。
            //    副作用（§17 接受）：取消会短暂激活场景一次。
            op.allowSceneActivation = true;
            while (!op.isDone)
            {
                await UniTask.Yield();
            }

            // 3. 卸载残留场景（此时已激活，SceneManager 承认其已加载）
            Scene scene = SceneManager.GetSceneByPath(SceneRef.ScenePath);
            if (scene.isLoaded)
            {
                AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(scene);
                if (unloadOp != null)
                {
                    while (!unloadOp.isDone)
                    {
                        await UniTask.Yield();
                    }
                }
            }

            // 4. 终态 + 取消结束（句柄完成源同步 TrySetCanceled，§5 P1 全终态完成；
            //    句柄从追踪移除由管理器 UpdateTracking 处理）
            SetStatus(SceneState.Unloaded);
            CompleteHandleLoadSource(false, null);
            CompletionSource.TrySetCanceled();
        }
    }
}
