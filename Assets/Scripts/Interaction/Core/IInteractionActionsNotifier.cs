using System;

namespace XeptGame.Interaction
{
    /// <summary>
    /// 动作集变化通知（**可选能力接口**，与 <see cref="IInteractionActionsHost"/> 分离）：
    /// 动作集**成员/槽占用**随宿主离散状态迁移变化的宿主（如火堆燃/灭：{熄灭,添柴} ⇄ {点燃}）
    /// 实现本接口并在迁移点触发 <see cref="ActionsChanged"/>（DP3 事件轨）。
    /// 成员恒定的宿主（单/固定动作集）**无需实现**——能力按需声明，不强迫持有永不触发的事件。
    /// 可用性（灰/亮）不走本接口：每帧查 <see cref="IInteractionAction.CanInteract"/>（DP3 每帧轨）。
    /// </summary>
    public interface IInteractionActionsNotifier
    {
        /// <summary>动作集成员/槽占用变化（离散状态迁移时触发）。</summary>
        event Action ActionsChanged;
    }
}
