namespace XeptGame.Interaction
{
    /// <summary>
    /// 交互物显示信息契约（可选）：提示层/广告牌消费，未实现时消费方回退默认（Host.name + 默认文案）。
    /// 数据与显示分离：本契约只提供数据，信息组装是 FormLogic 职责，表现机制（投影器）不负责任何内容
    /// （见 docs/modules/UI_WorldBillboard_Design.md §1.2 职责边界）。
    /// </summary>
    public interface IInteractionLabel
    {
        /// <summary>显示名（Prompt 标题）。</summary>
        string DisplayName { get; }

        /// <summary>提示文案（如"按 E 拾取"）；null/空 = 消费方用默认文案。</summary>
        string HintText { get; }
    }
}
