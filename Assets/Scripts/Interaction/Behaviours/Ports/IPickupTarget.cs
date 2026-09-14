using XeptGame.Items.Operations;

namespace XeptGame.Interaction.Behaviours
{
    /// <summary>
    /// 可拾取目标端口（**桥接层**，Interaction_Behaviour_Design.md §4 不变量的精确边界）：
    /// 行为需要"拾取"这类物品域动作时，经本端口向目标发起；端口由**物品域自愿实现**
    /// （<c>WorldItem</c>），交互核心契约（<c>Interaction/Core</c>）不认识物品。
    /// <list type="bullet">
    /// <item><b>能力声明</b>：<see cref="IsHoldable"/>（可持物面）/ <see cref="IsCarrier"/>（容器面）——
    /// 由目标按自己的物品定义回答，行为据此决定"我该不该出现在清单里"；</item>
    /// <item><b>请求</b>：<see cref="RequestPickup"/> 沿用物品域的 <c>PickupIntent</c> 分流
    /// （点按 = 入包；长按 = 到身上：可持物上一手 / 容器背上换包）。</item>
    /// </list>
    /// </summary>
    public interface IPickupTarget : IInteractionTarget
    {
        /// <summary>此刻能否发起拾取（源可用、未占用、还有内容）。</summary>
        bool CanPickup { get; }

        /// <summary>目标是不是"可持物"（物品定义挂 <c>HoldableFacet</c>）。</summary>
        bool IsHoldable { get; }

        /// <summary>目标是不是"容器/背包"（物品定义挂 <c>ContainerFacet</c>）。</summary>
        bool IsCarrier { get; }

        /// <summary>发起拾取请求（null = 会话未就绪等拒收情形）。</summary>
        OperationReceipt RequestPickup(PickupIntent intent);
    }
}
