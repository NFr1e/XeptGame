using System;

namespace XeptKit.Event
{
    /// <summary>
    /// 同步事件总线：类型安全的发布/订阅，即发即走。
    /// 所有操作（Subscribe / Unsubscribe / Publish / Contains）必须在 Unity 主线程调用。
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// 订阅类型为 <typeparamref name="T"/> 的事件，返回反注册句柄（Dispose 即退订，幂等）。
        /// <paramref name="priority"/> 越大越先执行；同优先级执行顺序无保证。
        /// 重复订阅同一 handler 幂等：仅保留首次，首次传入的 priority 生效。
        /// </summary>
        IDisposable Subscribe<T>(Action<T> handler, int priority = 0);

        /// <summary>按委托退订；返回是否移除成功。handler 未曾订阅时返回 false。</summary>
        bool Unsubscribe<T>(Action<T> handler);

        /// <summary>
        /// 发布事件。handler 按 priority 降序同步执行；单个 handler 异常被捕获
        /// （记日志 + 触发 <see cref="HandlerException"/>），不中断后续 handler，Publish 本身不抛出。
        /// </summary>
        void Publish<T>(T eventData);

        /// <summary>检查 handler 是否已订阅。O(n)，仅调试/防御用，勿入热路径。</summary>
        bool Contains<T>(Action<T> handler);

        /// <summary>可选异常上报钩子：handler 抛出异常时触发（每异常一次）。</summary>
        event Action<Type, Exception> HandlerException;
    }
}
