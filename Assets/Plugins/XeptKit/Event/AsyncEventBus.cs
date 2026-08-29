using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using XeptKit.Core;

namespace XeptKit.Event
{
    /// <summary>
    /// <see cref="IAsyncEventBus"/> 的默认实现：广播-等待并行执行（屏障语义）。
    /// 总线 API 主线程 only、无锁（Subscribe / Unsubscribe / PublishAsync 须在主线程调用）；
    /// handler 可在任意线程完成（如线程池），总线将 handler 完成处理统一切回主线程
    /// （记日志 + 上报钩子 + 异常收集 + 最终抛出），线程模型不因 handler 完成线程而破坏。
    /// PublishAsync 采用观察包装 + WhenAll：每个 handler 任务经 Observe 捕获业务异常
    /// （记日志 + 上报 + 收集），吞掉后让 WhenAll 正常完成，全部完成后抛首个业务异常
    /// （含 handler 同步启动即抛出的异常）；OperationCanceledException 放行（取消不误报）。
    /// </summary>
    public sealed class AsyncEventBus : IAsyncEventBus, ISubscriptionClearable
    {
        private readonly Dictionary<Type, PublisherSubscribers> _subscribers = new Dictionary<Type, PublisherSubscribers>();

        /// <inheritdoc />
        public event Action<Type, Exception> HandlerException;

        public IDisposable Subscribe<T>(Func<T, CancellationToken, UniTask> handler)
        {
            Guard.NotNull(handler, nameof(handler));

            _subscribers.GetOrAdd(typeof(T), () => new PublisherSubscribers()).Add(handler);
            return new SubscriptionHandle(() => Remove(typeof(T), handler));
        }

        public bool Unsubscribe<T>(Func<T, CancellationToken, UniTask> handler)
        {
            Guard.NotNull(handler, nameof(handler));
            return Remove(typeof(T), handler);
        }

        public async UniTask PublishAsync<T>(T eventData, CancellationToken cancellationToken = default)
        {
            if (!_subscribers.TryGetValue(typeof(T), out var subscribers))
            {
                return;
            }

            var snapshot = subscribers.Snapshot();
            if (snapshot.Length == 0)
            {
                return;
            }

            // 收集业务异常（供最后抛首个）；主线程 only，无并发写
            var exceptions = new List<Exception>();
            var tasks = new List<UniTask>(snapshot.Length);

            for (int i = 0; i < snapshot.Length; i++)
            {
                var handler = (Func<T, CancellationToken, UniTask>)snapshot[i];

                UniTask task;
                try
                {
                    task = handler(eventData, cancellationToken);
                }
                catch (Exception ex)
                {
                    // 同步启动异常：记日志 + 上报 + 收集，跳过该 handler，不阻止其他 handler 启动；
                    // 全部完成后随首个异常一并抛出（契约：调用方有责感知失败）
                    Log.Exception(ex);
                    RaiseHandlerException(typeof(T), ex);
                    exceptions.Add(ex);
                    continue;
                }

                tasks.Add(Observe(task, typeof(T), exceptions));
            }

            try
            {
                // 等待全部完成；等待受 ct 控制（即使 handler 忽略 token，取消时也抛 OCE）
                await UniTask.WhenAll(tasks).AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                // handler 可能在线程池等非主线程完成：统一回到主线程后，再读取异常列表并抛出。
                // 成功路径与 OCE 路径均保证在主线程收尾，调用方 await PublishAsync 的后续也在主线程恢复。
                await UniTask.SwitchToMainThread();
            }

            if (exceptions.Count > 0)
            {
                throw exceptions[0];
            }
        }

        public void Clear() => _subscribers.Clear();

        private bool Remove(Type eventType, Delegate handler)
        {
            if (!_subscribers.TryGetValue(eventType, out var subscribers))
            {
                return false;
            }

            bool removed = subscribers.Remove(handler);
            if (subscribers.Count == 0)
            {
                _subscribers.Remove(eventType);
            }

            return removed;
        }

        /// <summary>
        /// 观察单个 handler 任务：捕获业务异常上报并收集，吞掉避免 WhenAll 短路；OCE 放行。
        /// handler 可能在线程池等非主线程完成：先切回主线程再做日志 / 钩子 / 收集，
        /// 避免 List 并发写与异常钩子线程违规（主线程 only 模型）。
        /// </summary>
        private async UniTask Observe(UniTask task, Type eventType, List<Exception> exceptions)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                throw; // 取消（无论是否因 ct 所致）放行，不作业务异常上报
            }
            catch (Exception ex)
            {
                await UniTask.SwitchToMainThread(); // 统一回主线程（主线程上为同步直通）
                Log.Exception(ex);
                RaiseHandlerException(eventType, ex);
                exceptions.Add(ex);
            }
        }

        private void RaiseHandlerException(Type type, Exception ex)
        {
            try
            {
                HandlerException?.Invoke(type, ex);
            }
            catch (Exception hookEx)
            {
                Log.Exception(hookEx);
            }
        }

        /// <summary>类型擦除的订阅者列表，存储 Func&lt;T, CancellationToken, UniTask&gt; 委托。</summary>
        private sealed class PublisherSubscribers
        {
            private Delegate[] _handlers = Array.Empty<Delegate>();

            public int Count => _handlers.Length;

            public void Add(Delegate handler)
            {
                for (int i = 0; i < _handlers.Length; i++)
                {
                    if (_handlers[i].Equals(handler))
                    {
                        return;
                    }
                }

                var newArray = new Delegate[_handlers.Length + 1];
                Array.Copy(_handlers, newArray, _handlers.Length);
                newArray[^1] = handler;
                _handlers = newArray;
            }

            public bool Remove(Delegate handler)
            {
                for (int i = 0; i < _handlers.Length; i++)
                {
                    if (_handlers[i].Equals(handler))
                    {
                        var newArray = new Delegate[_handlers.Length - 1];
                        Array.Copy(_handlers, 0, newArray, 0, i);
                        Array.Copy(_handlers, i + 1, newArray, i, _handlers.Length - i - 1);
                        _handlers = newArray;
                        return true;
                    }
                }

                return false;
            }

            /// <summary>返回当前订阅者快照，PublishAsync 期间零分配。</summary>
            public Delegate[] Snapshot() => _handlers;
        }
    }
}
