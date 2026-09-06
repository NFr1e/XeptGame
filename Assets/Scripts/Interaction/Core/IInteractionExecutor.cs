using System;
using System.Collections.Generic;

namespace XeptGame.Interaction
{
    /// <summary>
    /// 交互执行者（契约 v3）：持有当前**宿主**与其**动作集**。
    /// <see cref="CurrentHost"/> 由选中系统唯一推送（<c>InteractionExecutor.ApplyHost</c>，单写者纪律，含 null）；
    /// 动作集由执行器在宿主推送时与每次动作执行后收集（宿主链上全部 <see cref="IInteractionAction"/>，槽序排序）。
    /// 消费者（提示层动作清单/灰态）依赖本接口；按下执行与输入槽绑定是具体实现职责。
    /// </summary>
    public interface IInteractionExecutor
    {
        /// <summary>当前宿主（选中系统唯一推）；null = 无。</summary>
        ISelectable CurrentHost { get; }

        /// <summary>当前宿主动作集（槽序；可用性每帧查各自 CanInteract）。</summary>
        IReadOnlyList<IInteractionAction> CurrentActions { get; }

        /// <summary>宿主变化事件（负载 <see cref="InteractionHostChangedArgs"/>，含宿主与动作集，可 null）。</summary>
        event Action<InteractionHostChangedArgs> HostChanged;
    }
}
