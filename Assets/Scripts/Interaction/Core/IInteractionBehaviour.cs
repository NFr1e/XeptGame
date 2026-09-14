namespace XeptGame.Interaction
{
    /// <summary>
    /// 交互行为（v5，Interaction_Behaviour_Design.md §2 D1）——**一件能做出来的事**，以代码类表达：
    /// "按哪个槽、文案叫什么键、什么样的目标能做这件事、此刻能不能做、做了发生什么"。
    /// <list type="bullet">
    /// <item><b>无状态</b>：行为不持目标引用、不持运行时数据——目标由运行时动作 <see cref="BoundAction"/> 携带，
    /// 当前状态归宿主字段。因此行为实例可安全地放进总名单（<c>InteractionBehaviourCatalog</c>）共享；</item>
    /// <item><b>不进资产</b>：行为是代码契约，不是可配置条目（v3 判据沿用；生态调研结论见
    /// <c>docs/research/interaction-architecture/00-final-report.md</c> R2）；需要按物品调参时另开配置 SO；</item>
    /// <item><b>身份与做法同体</b>：文案键与执行逻辑写在同一个类里，物理上不可能"说一套做一套"。</item>
    /// </list>
    /// 运行时动作（<see cref="BoundAction"/>）把 <see cref="Slot"/>/<see cref="PromptKey"/> 直接转发出去，
    /// 因此动作对象没有独立的标签字段。
    /// </summary>
    public interface IInteractionBehaviour
    {
        /// <summary>占用的输入槽（Primary/Secondary/Hold；同槽互斥由执行器保证）。</summary>
        InputSlot Slot { get; }

        /// <summary>文案键（如 <c>interaction.pickup</c>）；取值为 CSV 里的**完整句子**，本键不存文本。</summary>
        string PromptKey { get; }

        /// <summary>
        /// 这件行为是否适用于该目标（决定"清单里有没有这件事"）：粗粒度、随情况变化 → 宿主据此重建清单。
        /// 目标自身的能力（是不是可持物/容器）与玩家携带事实（有没有包）都在这里读；
        /// <paramref name="carry"/> **可空**（会话未建立 = 视为无包），实现必须容忍 null。
        /// </summary>
        bool CanBuildOn(IInteractionTarget target, ICarryFacts carry);

        /// <summary>此刻能否执行（每帧查询；不得产生副作用）。</summary>
        bool CanAct(IInteractionTarget target, InteractionContext context);

        /// <summary>执行（仅当槽输入命中且 <see cref="CanAct"/> 通过后触发）。</summary>
        void Act(IInteractionTarget target, InteractionContext context);
    }
}
