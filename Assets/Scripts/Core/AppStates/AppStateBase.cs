using XeptKit.FSM;

namespace XeptGame
{
    /// <summary>
    /// 应用状态基类：提供类型化上下文访问。
    /// 自驱转移纪律（见 AppFSM 设计决议）：完成型转移在状态钩子内发起（EnterAsync/OnEnter，
    /// 经 XeptKit.FSM 的 pending 机制在转移收尾后消费，时序有保证）；异步操作绑定
    /// KitLifecycle.GlobalToken——会话结束自动取消，杜绝"退出后异步续延复活发起转移"。
    /// </summary>
    public abstract class AppStateBase : StateBase
    {
        /// <summary>类型化上下文（构造时由 Fsm 注入）。</summary>
        protected AppContext App => Context as AppContext;
    }
}
