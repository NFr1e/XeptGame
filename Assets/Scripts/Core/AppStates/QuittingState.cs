using XeptKit.Core;

namespace XeptGame
{
    /// <summary>
    /// 业务关闭态（终态）：业务关闭逻辑（存档/上报等；如需异步重写 EnterAsync）。
    /// 不碰框架服务——框架收尾独占 AppEntry.Shutdown()（由 Application.quitting 触发，见设计决议 §4 两段式退出）。
    /// </summary>
    public sealed class QuittingState : AppStateBase
    {
        public override void OnEnter()
        {
            Log.Info("[AppFSM] 进入退出态，执行业务关闭。");
        }
    }
}
