using System;
using System.Collections.Generic;

namespace XeptGame.Interaction
{
    /// <summary>
    /// 交互动作集宿主（可选能力接口，被选中节点实现即成为"动作集权威"）：
    /// <see cref="Actions"/> = 动作唯一来源（宿主内部 List/IInteractionAction，成员 = 列表存在，
    /// 不再依赖组件 enable/扫描）；<see cref="ActionsChanged"/> 在**成员/槽占用变化**（离散状态迁移，
    /// DP3 事件轨）时触发。纯 C# 动作经宿主构造注入（无 Unity 生命周期纠缠）。
    /// 执行器在宿主推送时读本接口并订阅事件；未实现本接口的宿主 = 无动作（纯选中/观察）。
    /// </summary>
    public interface IInteractionActionsHost
    {
        /// <summary>当前动作集（只读视图；成员增删由宿主内部管理并触发 <see cref="ActionsChanged"/>）。</summary>
        IReadOnlyList<IInteractionAction> Actions { get; }

        /// <summary>动作集成员/槽占用变化（离散状态迁移时触发）。</summary>
        event Action ActionsChanged;
    }
}
