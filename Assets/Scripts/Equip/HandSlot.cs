using XeptGame.Items;

namespace XeptGame.Equip
{
    /// <summary>
    /// 手部槽（<see cref="ISlot"/> 实现，v1 唯一成员，Equip_FPV_Design.md §2.2）：
    /// 单位制槽位（无视物品可堆叠性、容量恒 1 单位——可堆叠性是背包行存储的属性），
    /// 接纳"可持物"（HoldableFacet：武器/工具/可堆叠资源）。
    /// </summary>
    public sealed class HandSlot : ISlot
    {
        /// <inheritdoc />
        public BodySlotType Id => BodySlotType.Hand;

        /// <inheritdoc />
        public bool Accepts(ItemDefinition definition)
            => definition != null && definition.HasFacet<HoldableFacet>();
    }
}
