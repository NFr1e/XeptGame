using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
using XeptKit.Event;

namespace XeptKit.Scenes
{
    /// <summary>
    /// 场景卸载请求——Command 模式的具体 Command。
    /// 优先级恒为 int.MaxValue（卸载先于一切加载，决议 B）。
    /// 取消语义：底层卸载无法中止——继续等待卸载完成（收尾 Unloaded），随后以取消结束（调用方 await 抛 OCE）。
    /// </summary>
    internal sealed class SceneUnloadRequest : SceneRequest
    {
        private readonly IEventBus _eventBus;

        public SceneUnloadRequest(
            SceneHandle handle,
            IEventBus eventBus,
            long sequenceNumber,
            CancellationToken cancellationToken)
            : base(handle.SceneRef, int.MaxValue, sequenceNumber, cancellationToken)
        {
            Handle = handle;
            _eventBus = eventBus;
        }

        internal override async UniTask ExecuteAsync()
        {
            if (Token.IsCancellationRequested)
            {
                // 排队中被取消：未执行，直接以取消结束（句柄保持原状，场景仍在）
                CompletionSource.TrySetCanceled();
                return;
            }

            // 检查场景是否仍在加载中/已加载
            Scene targetScene = Handle.Scene;
            if (!targetScene.isLoaded)
            {
                targetScene = SceneManager.GetSceneByPath(SceneRef.ScenePath);
            }

            if (!targetScene.isLoaded)
            {
                // 场景未加载——视为成功（幂等），不广播事件
                SetStatus(SceneState.Unloaded);
                SetResult(true, "Unload");
                return;
            }

            // --- 开始卸载 ---
            SetStatus(SceneState.Unloading);
            _eventBus.Publish(new SceneUnloadingEvent(Handle));

            AsyncOperation op = SceneManager.UnloadSceneAsync(targetScene);
            if (op == null)
            {
                string error = $"Failed to start unloading scene '{SceneRef.SceneName}'.";
                SetStatus(SceneState.Failed);
                SetResult(false, "Unload", error);
                return;
            }

            while (!op.isDone)
            {
                if (Token.IsCancellationRequested)
                {
                    // 取消：底层卸载无法中止——继续等待完成，收尾后以取消结束
                    while (!op.isDone)
                    {
                        await UniTask.Yield();
                    }

                    SetProgress(1.0f);
                    SetStatus(SceneState.Unloaded);
                    CompletionSource.TrySetCanceled();
                    return;
                }

                SetProgress(op.progress);
                await UniTask.Yield();
            }

            // --- 完成 ---
            SetProgress(1.0f);
            SetStatus(SceneState.Unloaded);

            SetResult(true, "Unload");
            _eventBus.Publish(new SceneUnloadedEvent(SceneRef));
        }
    }
}
