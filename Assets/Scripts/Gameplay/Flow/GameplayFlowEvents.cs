using System;
using XeptKit.FSM;

namespace XeptGame.Gameplay
{
    /// <summary>
    /// 游戏流程状态变化事件（GameplayFlow_Design.md §3.2）：由 GameplayManager 在 Fsm.StateChanged 时
    /// 发布到 EventBus。场景侧（装配器）订阅本事件响应"玩法已开始/离开"等流程节点——
    /// 装配器**不持 FSM 实例**（接缝纪律），状态感知完全经本事件。
    /// 负载 = 新/旧状态（From 可为 null——初始进入）。
    /// </summary>
    public readonly struct GameplayStateChangedEvent
    {
        /// <summary>新状态。</summary>
        public readonly StateBase To;

        /// <summary>旧状态（null = 初始进入）。</summary>
        public readonly StateBase From;

        public GameplayStateChangedEvent(StateBase to, StateBase from)
        {
            To = to;
            From = from;
        }
    }

    /// <summary>
    /// 游戏流程错误事件（GameplayFlow_Design.md §3.2）：由失败状态（如 Loading）发布到 EventBus。
    /// AppFSM 侧订阅 → 写 AppContext.LastError + 转 ErrorState（AppFSM 不管理 GameplayFSM 生命周期，
    /// 但**接收死亡信号**）。
    /// </summary>
    public readonly struct GameplayFlowErrorEvent
    {
        /// <summary>错误消息（含来源标注）。</summary>
        public readonly string Message;

        /// <summary>原始异常（可 null）。</summary>
        public readonly Exception Exception;

        public GameplayFlowErrorEvent(string message, Exception exception)
        {
            Message = message;
            Exception = exception;
        }
    }
}
