using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace XeptKit.Event
{
    /// <summary>
    /// 异步广播-等待事件发布器：广播事件并等待所有 handler 并行执行完毕（屏障语义）。
    /// 典型场景：场景加载后并行初始化多个系统，全部就绪后再继续。
    /// </summary>
    public interface IAsyncEventBus
    {
        /// <summary>订阅异步事件，返回反注册句柄（Dispose 即退订，幂等）。重复订阅同一 handler 幂等。</summary>
        IDisposable Subscribe<T>(Func<T, CancellationToken, UniTask> handler);

        /// <summary>按委托退订；返回是否移除成功。handler 未曾订阅时返回 false。</summary>
        bool Unsubscribe<T>(Func<T, CancellationToken, UniTask> handler);

        /// <summary>
        /// 广播事件：并行启动全部 handler（传入 <paramref name="cancellationToken"/>），等待全部完成后返回。
        /// 单个 handler 异常被捕获（记日志 + 触发 <see cref="HandlerException"/>）；全部完成后抛首个异常
        /// （含 handler 同步启动即抛出的异常）。取消：token 取消时抛 <see cref="OperationCanceledException"/>
        /// （取消不作业务异常上报）。
        /// 线程模型：Subscribe / Unsubscribe / PublishAsync 须在 Unity 主线程调用；handler 可在任意线程完成
        /// （如线程池），总线统一将完成处理（记日志、上报钩子、异常收集与抛出）切回主线程执行。
        /// </summary>
        UniTask PublishAsync<T>(T eventData, CancellationToken cancellationToken = default);

        /// <summary>可选异常上报钩子：handler 抛出异常时触发（每异常一次）。</summary>
        event Action<Type, Exception> HandlerException;
    }
}
