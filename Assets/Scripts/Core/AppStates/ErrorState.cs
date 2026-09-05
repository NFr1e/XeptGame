using XeptKit.Core;
using XeptGame.Game;

namespace XeptGame
{
    /// <summary>
    /// 失败呈现（ErrorState，终态语义）：读取 <see cref="AppContext.LastError"/> 与
    /// <see cref="AppContext.FailureSource"/> 呈现失败原因，提供重试/放弃路径。
    /// **统一收尾点**：进入本状态即收掉游戏流程（<see cref="GameManager.Shutdown"/>）——
    /// 覆盖两类错误：① 启动失败（Initializing/Starting，GameplayFSM 可能未建/半建，Shutdown 幂等 no-op）；
    /// ② 游戏流程失败（GameplayFlowErrorEvent 桥接，GameplayFSM 停驻失败态 + RunAsync 挂门）——
    /// Shutdown 取消本机令牌 → RunAsync OCE 退出（释放 Context 持有，不活到会话结束）+ 同步 Dispose Fsm + 清域总线。
    /// **重试分派**（UI 接线由业务细化）：<see cref="AppFailureSource.Startup"/> → AppManager.RetryLaunch（全量重跑）；
    /// <see cref="AppFailureSource.GameplayFlow"/> → AppManager.RetryGame（ErrorState 已收尾，回 StartingState 重建流程）。
    /// </summary>
    public sealed class ErrorState : AppStateBase
    {
        public override void OnEnter()
        {
            // 收尾游戏流程（幂等：未启动/已收尾则 no-op）——错误=当前会话终止，任何残留编排/FSM 一并清场
            GameManager.Shutdown();

            var source = App.FailureSource == AppFailureSource.GameplayFlow ? "游戏流程" : "应用启动";
            Log.Error($"[AppFSM] {source}失败：{App.LastError?.Message ?? "未知错误"}");
        }
    }
}
