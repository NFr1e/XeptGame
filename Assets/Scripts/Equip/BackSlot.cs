using XeptGame.Container;
using XeptGame.Items;

namespace XeptGame.Equip
{
    /// <summary>
    /// 背部槽（<see cref="SlotBase"/> 实现；Item_Instance_Design.md §3）：
    /// <b>槽自己的门控</b>——种类：<b>容器类物品</b>（<see cref="ContainerFacet"/>）；数目：恒 1 单位。
    /// <list type="bullet">
    /// <item>背槽里的实例就是"当前背包"：会话的 <c>Inventory</c> 由它推导（无包 = 背槽空，合法状态）；</item>
    /// <item><b>换包 = 换这个实例</b>，实例内容永不迁移；旧包去向由操作层的目的地决定（WorldDrop 等）。</item>
    /// </list>
    /// </summary>
    public sealed class BackSlot : SlotBase
    {
        public BackSlot() : base(new SlotId((int)BodySlotType.Back))
        {
        }

        /// <inheritdoc />
        public override bool Accepts(ItemDefinition definition)
            => definition != null && definition.HasFacet<ContainerFacet>();

        /// <inheritdoc />
        public override int CapacityFor(ItemDefinition definition)
            => Accepts(definition) ? 1 : 0;
    }
}
