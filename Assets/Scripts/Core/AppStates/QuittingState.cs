using XeptKit.Core;
using XeptGame.Game;

namespace XeptGame
{
    /// <summary>
    /// 业务关闭态（终态）：业务关闭逻辑（存档/上报等；如需异步重写 EnterAsync）。
    /// **业务收尾在此完成**：收掉游戏流程（<see cref="GameManager.Shutdown"/>——取消编排令牌 → RunAsync OCE 退出、
    /// Dispose GameplayFSM、清 Gameplay 域总线；幂等，错误路径已收尾则 no-op）。
    /// 不碰框架服务——框架收尾独占 AppEntry.Shutdown()（由 Application.quitting 触发，见设计决议 §4 两段式退出）。
    /// </summary>
    public sealed class QuittingState : AppStateBase
    {
        public override void OnEnter()
        {
            GameManager.Shutdown(); // 业务关闭：游戏流程收尾（正常退出时 GameplayFSM 首次被释放）

            Log.Info("[AppFSM] 进入退出态，执行业务关闭。");
        }
    }
}
