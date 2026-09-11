using XeptGame.Container;
using XeptGame.Items;

namespace XeptGame.Equip
{
    /// <summary>
    /// 手部槽（<see cref="SlotBase"/> 实现，v1 唯一身体槽，Equip_FPV_Design.md §2.2；SlotStore_Design.md §2）：
    /// <b>槽自己的门控</b>——种类：可持物（<see cref="HoldableFacet"/>）；数目：恒 1 单位（单位制，无视可堆叠性；
    /// 可堆叠性是背包行的属性）。状态与变更事件由基类提供，本类只回答"接纳什么、能放多少"。
    /// </summary>
    public sealed class HandSlot : SlotBase
    {
        public HandSlot() : base(new SlotId((int)BodySlotType.Hand))
        {
        }

        /// <inheritdoc />
        public override bool Accepts(ItemDefinition definition)
            => definition != null && definition.HasFacet<HoldableFacet>();

        /// <inheritdoc />
        public override int CapacityFor(ItemDefinition definition)
            => Accepts(definition) ? 1 : 0;
    }
}
