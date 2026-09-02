using System;

namespace XeptGame.Interaction
{
    /// <summary>
    /// 选中者（契约模型 v2）：产生当前选中目标的生产者。选中严格由 <see cref="ISelectable"/> 门控
    /// （单一门控者、最近解析，见 Interaction_Design.md §4.2）；选中变化经事件推送。
    /// 消费者（提示层/高亮层）依赖本接口而非具体探测器——目标从哪来（玩家相机/AI 目标系统/
    /// 过场强制）与消费者无关。可交互者的分发（推给执行器）不属本接口：见 <see cref="IInteractionExecutor"/>。
    /// </summary>
    public interface ISelector
    {
        /// <summary>当前选中（单一门控者，最近解析）；null = 无目标。</summary>
        ISelectable CurrentSelected { get; }

        /// <summary>
        /// 目标变化事件（负载 <see cref="SelectionChangeArgs"/>，含旧/新选中，可 null——
        /// 镜像 <c>Fsm.StateChangeArgs</c> 命名先例，避免后期业务需求回头补旧值参数）。
        /// </summary>
        event Action<SelectionChangeArgs> SelectionChanged;
    }
}
