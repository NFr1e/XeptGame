using System.Collections.Generic;

namespace XeptGame.Interaction
{
    /// <summary>
    /// 交互动作集读取（被选中节点实现即成为"动作集权威"）：
    /// <see cref="Actions"/> = 动作唯一来源（宿主内部 List/IInteractionAction，成员 = 列表存在）。
    /// 纯 C# 动作经宿主构造注入（无 Unity 生命周期纠缠）。
    /// **成员变化通知是可选能力**：动作集动态变化的宿主（火堆）另实现
    /// <see cref="IInteractionActionsNotifier"/>；成员恒定的宿主（WorldItem/DebugInteractable）**不实现**，
    /// 避免被迫持有永不触发的事件（能力按需声明；CS0067 是接口强迫的气味）。
    /// 执行器读取本接口，并仅在宿主实现 Notifier 时订阅成员事件。
    /// </summary>
    public interface IInteractionActionsHost
    {
        /// <summary>当前动作集（只读视图；成员增删由宿主内部管理）。</summary>
        IReadOnlyList<IInteractionAction> Actions { get; }
    }
}
