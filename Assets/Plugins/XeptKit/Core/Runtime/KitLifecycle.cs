using System;
using System.Threading;

namespace XeptKit.Core
{
    /// <summary>
    /// 全局生命周期设施。由业务组合根显式驱动：
    /// 启动时调用 <see cref="Initialize"/>，会话结束时调用 <see cref="Shutdown"/>。
    /// 仅主线程使用，不做并发设计。
    /// </summary>
    public static class KitLifecycle
    {
        private static CancellationTokenSource _cts;
        private static Action _shutdownCallbacks;

        /// <summary>
        /// 全局取消令牌。未调用 <see cref="Initialize"/> 时访问将抛出 InvalidOperationException（fail-fast）。
        /// 令牌类型统一为 System.Threading.CancellationToken（BCL）。
        /// </summary>
        public static CancellationToken GlobalToken
        {
            get
            {
                if (_cts == null)
                {
                    throw new InvalidOperationException(
                        "KitLifecycle 尚未初始化，请先在组合根调用 KitLifecycle.Initialize()。");
                }

                return _cts.Token;
            }
        }

        /// <summary>
        /// 初始化生命周期设施。幂等：若存在旧 CTS，先 Cancel() 再 Dispose()
        /// （防止绑定旧令牌的异步操作永不取消而泄漏），随后清空旧钩子并重建新 CTS。
        /// </summary>
        public static void Initialize()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
            }

            _shutdownCallbacks = null;
            _cts = new CancellationTokenSource();
        }

        /// <summary>
        /// 关闭生命周期设施：取消全局令牌（绑定它的异步操作随之终止）→ 触发同步钩子。
        /// 同步化设计：不做异步关闭，优雅异步清理由业务组合根自行按序 await 各模块的 IDisposableAsync。
        /// </summary>
        public static void Shutdown()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            var callbacks = _shutdownCallbacks;
            _shutdownCallbacks = null;
            callbacks?.Invoke();
        }

        /// <summary>
        /// 注册会话结束时触发的清理回调，返回反注册句柄。
        /// 触发顺序 FIFO，钩子间不依赖顺序。
        /// </summary>
        public static IDisposable RegisterShutdownCallback(Action callback)
        {
            Guard.NotNull(callback, nameof(callback));
            if (_cts == null)
            {
                throw new InvalidOperationException(
                    "KitLifecycle 尚未初始化或已 Shutdown，无法注册关闭钩子。请先调用 KitLifecycle.Initialize()。");
            }

            _shutdownCallbacks += callback;
            return new ShutdownCallbackHandle(callback);
        }

        private sealed class ShutdownCallbackHandle : IDisposable
        {
            private readonly Action _callback;

            public ShutdownCallbackHandle(Action callback)
            {
                _callback = callback;
            }

            public void Dispose() => _shutdownCallbacks -= _callback;
        }
    }
}
