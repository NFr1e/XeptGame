using System;
using XeptKit.Core;

namespace XeptKit.Event
{
    /// <summary>
    /// 类型化"对象事件"安全广播原语（SafeEvent 决议）：语义 ≈ C# event，但内置<b>逐监听异常隔离</b>与订阅去重。
    /// <list type="bullet">
    /// <item>与 <see cref="EventBus"/> 分工：SafeEvent 供<b>单个事件源对象</b>（容器 / 行为 / 组件的事件字段）使用；
    /// <see cref="EventBus"/> 供<b>域级类型广播</b>（多源多消费者、需要优先级/句柄/跨对象共享）——需要优先级请用 EventBus
    /// （SafeEvent 按订阅顺序执行）；</item>
    /// <item>handler null：<see cref="Add"/> 抛 ArgumentNull（对齐 EventBus.Subscribe）；<see cref="Remove"/> null → false；</item>
    /// <item>重复订阅同一 handler 幂等去重（对齐 EventBus，仅保留首次）；</item>
    /// <item><see cref="Invoke"/> 数组快照迭代 + 逐监听 try/catch（<see cref="Log.Exception"/>），不向发布方抛——
    /// 回调中 Add/Remove 不影响本轮；允许重入 Invoke；</item>
    /// <item>主线程 only、无锁（同 EventBus 约定）；<see cref="Subscribe"/> 返回幂等退订句柄（对齐 EventBus 句柄惯用）。</item>
    /// </list>
    /// </summary>
    public sealed class SafeEvent<T>
    {
        private Action<T>[] _handlers = Array.Empty<Action<T>>();

        /// <summary>订阅（幂等去重）；null 抛。</summary>
        public void Add(Action<T> handler)
        {
            Guard.NotNull(handler, nameof(handler));
            if (IndexOf(_handlers, handler) >= 0)
            {
                return;
            }

            _handlers = Append(_handlers, handler);
        }

        /// <summary>退订；未订阅或 null → false。</summary>
        public bool Remove(Action<T> handler)
        {
            if (handler == null)
            {
                return false;
            }

            int index = IndexOf(_handlers, handler);
            if (index < 0)
            {
                return false;
            }

            _handlers = RemoveAt(_handlers, index);
            return true;
        }

        /// <summary>订阅并返回退订句柄（Dispose 幂等）。</summary>
        public IDisposable Subscribe(Action<T> handler)
        {
            Add(handler);
            return new SubscriptionHandle(() => Remove(handler));
        }

        /// <summary>广播：快照迭代 + 逐监听异常隔离，不向外抛。</summary>
        public void Invoke(T value)
        {
            var snapshot = _handlers;
            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    snapshot[i](value);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex);
                }
            }
        }

        /// <summary>清空全部订阅（事件源销毁/Dispose 时调用，防僵尸订阅）。</summary>
        public void Clear() => _handlers = Array.Empty<Action<T>>();

        private static int IndexOf(Action<T>[] handlers, Action<T> handler)
        {
            for (int i = 0; i < handlers.Length; i++)
            {
                // Delegate.Equals：同方法 + 同 target 即相等——方法组每次转换为新委托实例，引用比较不可靠
                // （对齐 EventBus.HandlerCollection 的判定语义）。
                if (handlers[i].Equals(handler))
                {
                    return i;
                }
            }

            return -1;
        }

        private static Action<T>[] Append(Action<T>[] source, Action<T> handler)
        {
            var next = new Action<T>[source.Length + 1];
            Array.Copy(source, next, source.Length);
            next[^1] = handler;
            return next;
        }

        private static Action<T>[] RemoveAt(Action<T>[] source, int index)
        {
            var next = new Action<T>[source.Length - 1];
            Array.Copy(source, 0, next, 0, index);
            Array.Copy(source, index + 1, next, index, source.Length - index - 1);
            return next;
        }
    }
}
