using System;
using XeptKit.FSM;

namespace XeptGame.Gameplay
{
    /// <summary>
    /// 游戏流程状态基类（对齐 AppStateBase）：提供类型化上下文访问 + 失败上报辅助。
    /// 自驱转移纪律（同 AppFSM 决议）：完成型转移在状态钩子内发起（EnterAsync/OnEnter，
    /// 经 XeptKit.FSM 的 pending 机制在转移收尾后消费，时序有保证）；异步操作绑定传入的转移令牌
    /// （惯例为 KitLifecycle.GlobalToken）——会话结束自动取消。
    /// 失败收敛：Fail() 上报后**不置位完成门**、本状态停驻——GameLoadingManager 挂在门上，
    /// 由 GameplayManager.Shutdown（AppFSM 转 ErrorState 时收尾，取消编排令牌使其 OCE 退出）收敛。
    /// </summary>
    public abstract class GameplayStateBase : StateBase
    {
        /// <summary>类型化上下文（构造时由 Fsm 注入）。</summary>
        protected GameplayContext Game => Context as GameplayContext;

        /// <summary>失败上报：写 LastError + 发布 GameplayFlowErrorEvent（AppFSM 侧经桥接订阅转 ErrorState）。</summary>
        protected void Fail(string message, Exception exception)
        {
            Game.LastError = exception;
            Game.EventBus.Publish(new GameplayFlowErrorEvent($"[GameplayFSM] {message}", exception));
        }
    }
}
