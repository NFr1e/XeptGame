namespace XeptGame.Interaction
{
    /// <summary>
    /// 交互动作（v3，演进自 v2 IInteractable；Interaction_Prompt_V3_Design.md §2）：
    /// 一个对象可持**多个**动作组件（同对象多实例，如熄灭/添柴/点燃），每个动作自带
    /// 输入槽、提示文案与可用性门控——宿主（被选中节点）只负责聚合，不解释动作语义。
    /// <list type="bullet">
    /// <item><b>输入身份</b>：<see cref="Slot"/> 声明动作占用的语义槽（不持物理键，见 InputSlot）；</item>
    /// <item><b>提示文案键</b>：<see cref="PromptKey"/> 供提示 UI 解析（键 → CSV 里的完整句子；
    /// v5 修订，见 Interaction_Behaviour_Design.md §2 D3）；</item>
    /// <item><b>可用性</b>：<see cref="CanInteract"/> 每帧门控（冷却/状态/耗尽等，沿用 v2 语义，无副作用）；</item>
    /// <item><b>执行</b>：<see cref="Interact"/> 由输入命中该槽且门控通过时触发。</item>
    /// </list>
    /// 同槽互斥规则：同一宿主动作集里，一个槽同时最多一个可用动作（多可用才需菜单/轮换，见决议 §2.2）。
    /// </summary>
    public interface IInteractionAction
    {
        /// <summary>动作占用的输入槽。</summary>
        InputSlot Slot { get; }

        /// <summary>文案键（如 <c>interaction.pickup</c>）；文本在 CSV，键本身不存文本。实现方一律转发自行为类。</summary>
        string PromptKey { get; }

        /// <summary>当前是否可执行（每帧查询；不得产生副作用）。</summary>
        bool CanInteract(InteractionContext context);

        /// <summary>执行动作（仅当该槽输入命中且 <see cref="CanInteract"/> 通过后触发）。</summary>
        void Interact(InteractionContext context);
    }
}
