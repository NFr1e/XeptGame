namespace XeptGame.Interaction.Behaviours
{
    /// <summary>
    /// 调试目标端口（桥接层）：仅调试对照物 <c>DebugInteractable</c> 实现，
    /// 用于覆盖验证"选中门控 / 动作冷却门控 / 灰态"。
    /// </summary>
    public interface IDebugTarget : IInteractionTarget
    {
        /// <summary>是否处于交互冷却期（可用性门控数据源）。</summary>
        bool IsCoolingDown { get; }

        /// <summary>执行一次交互。</summary>
        bool TryInteract();
    }
}
