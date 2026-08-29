using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace XeptKit.Scenes
{
    /// <summary>
    /// 场景请求基类（Command 模式抽象 Command）。
    /// 封装状态、进度、取消（linked CTS）与完成信号。
    /// 请求执行方法内部保证所有路径（成功/失败/取消）设置完成源，不留未观察异常。
    /// </summary>
    internal abstract class SceneRequest : IDisposable
    {
        private readonly CancellationTokenSource _cts;

        /// <summary>场景引用。</summary>
        public SceneReference SceneRef { get; }

        /// <summary>队列优先级（越大越先）。</summary>
        public int Priority { get; }

        /// <summary>入队序号（同优先级 FIFO）。</summary>
        public long SequenceNumber { get; }

        /// <summary>当前状态。初始 Pending。</summary>
        public SceneState Status { get; private set; } = SceneState.Pending;

        /// <summary>进度，范围 [0, 1]。</summary>
        public float Progress { get; private set; }

        /// <summary>关联的公开句柄（仅 Load 请求有值；Unload 请求复用已有句柄）。</summary>
        public SceneHandle Handle { get; protected set; }

        protected readonly UniTaskCompletionSource CompletionSource = new();

        /// <summary>完成信号（成功 TrySetResult / 失败 TrySetException / 取消 TrySetCanceled）。</summary>
        public UniTask CompletionTask => CompletionSource.Task;

        /// <summary>取消令牌（调用方令牌 + 清场/清除取消 linked）。供执行检查与管理器跳过已取消请求。</summary>
        internal CancellationToken Token => _cts.Token;

        protected SceneRequest(
            SceneReference sceneRef,
            int priority,
            long sequenceNumber,
            CancellationToken cancellationToken)
        {
            SceneRef = sceneRef;
            Priority = priority;
            SequenceNumber = sequenceNumber;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        /// <summary>请求取消（清场 / Clear 触发；调用方令牌取消经 linked CTS 自动触发）。幂等。</summary>
        public void Cancel() => _cts.Cancel();

        /// <summary>排队中被取消（未执行）：直接以取消结束。加载请求覆写以同步完成句柄完成源（§5 全终态完成）。</summary>
        public virtual void CancelBeforeExecution() => CompletionSource.TrySetCanceled();

        /// <summary>
        /// 释放 linked CTS（解除对调用方令牌的取消订阅回调）。请求结束后由管理器收尾路径调用；
        /// 释放后不得再访问 <see cref="Token"/>。幂等。
        /// </summary>
        public void Dispose() => _cts.Dispose();

        /// <summary>执行场景操作。子类实现具体逻辑；内部须保证所有路径设置完成源。</summary>
        internal abstract UniTask ExecuteAsync();

        protected void SetStatus(SceneState newStatus)
        {
            Status = newStatus;
            if (Handle != null)
            {
                Handle.State = newStatus;
            }
        }

        protected void SetProgress(float progress)
        {
            Progress = progress;
            if (Handle != null)
            {
                Handle.Progress = progress;
            }
        }

        /// <summary>设置结果。失败时若已取消则改为取消结束（取消优先于失败——取消是正常流程）。</summary>
        protected void SetResult(bool success, string operation, string error = null)
        {
            if (success)
            {
                CompletionSource.TrySetResult();
            }
            else if (Token.IsCancellationRequested)
            {
                CompletionSource.TrySetCanceled();
            }
            else
            {
                CompletionSource.TrySetException(
                    new SceneOperationException(SceneRef, operation, error ?? $"Scene operation failed: {SceneRef.SceneName}"));
            }
        }

        public override string ToString() =>
            $"{GetType().Name}({SceneRef.SceneName}, {Status}, Priority={Priority})";
    }
}
