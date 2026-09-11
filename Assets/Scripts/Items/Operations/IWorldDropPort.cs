using XeptGame.Items;

namespace XeptGame.Items.Operations
{
    /// <summary>
    /// 世界掉落口（Item_Instance_Design.md §5.3）：一次装配、两种落地——<b>整包</b>（实例）与<b>一堆</b>（无状态）。
    /// <list type="bullet">
    /// <item>实现方（<c>WorldDropDestination</c>）负责"往哪落"（落点来自场景侧锚点），业务侧只问"能不能接"；</item>
    /// <item>失败时必须给出类型化原因，调用方据此<b>回滚</b>（不丢东西）。</item>
    /// </list>
    /// </summary>
    public interface IWorldDropPort : ICarrierDestination
    {
        /// <summary>把一堆无状态物品落到世界（收起失败时的"放走"路径等）。</summary>
        bool TryAcceptStack(ItemDefinition definition, int count, out string reason);
    }
}
