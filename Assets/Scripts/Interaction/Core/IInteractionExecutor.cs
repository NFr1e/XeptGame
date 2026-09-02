using System;

namespace XeptGame.Interaction
{
    /// <summary>
    /// 交互执行者（契约模型 v2）：持有当前可交互者。
    /// <see cref="CurrentInteractable"/> 由选中系统**唯一推送**（单写者纪律，含 null）——
    /// 执行器不感知目标从哪来（探测/选中是选中系统职责）；交互者变化经事件推送。
    /// 消费者（提示层内容/灰态、"手持物"HUD）依赖本接口；按下执行与输入绑定是具体实现职责。
    /// </summary>
    public interface IInteractionExecutor
    {
        /// <summary>当前可交互者（选中系统唯一推）；null = 无。设置时发布 <see cref="InteractableChanged"/>。</summary>
        IInteractable CurrentInteractable { get; set; }

        /// <summary>交互者变化事件（负载 <see cref="InteractableChangeArgs"/>，含旧/新交互者，可 null）。</summary>
        event Action<InteractableChangeArgs> InteractableChanged;
    }
}
