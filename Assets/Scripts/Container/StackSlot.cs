using XeptGame.Items;

namespace XeptGame.Container
{
    /// <summary>
    /// 可堆叠物品槽（背包格/箱子格的通用实现，SlotStore_Design.md §2/§6）：
    /// <list type="bullet">
    /// <item><b>种类门控恒真</b>：任何物品都能进格（"参与背包系统"由 <see cref="InventoryFacet"/> 另行表达分类与上限）；</item>
    /// <item><b>数目门控 = 物品声明的每格上限</b>：读 <c>InventoryFacet.profile.ResolvedMaxStack</c>；
    /// 未挂面或未配置 = <see cref="int.MaxValue"/>（不约束，兼容既有内容，零迁移）。</item>
    /// </list>
    /// </summary>
    public sealed class StackSlot : SlotBase
    {
        public StackSlot(int index) : base(new SlotId(index))
        {
        }

        /// <inheritdoc />
        public override bool Accepts(ItemDefinition definition) => definition != null;

        /// <inheritdoc />
        public override int CapacityFor(ItemDefinition definition)
        {
            if (definition == null)
            {
                return 0;
            }

            var facet = definition.GetFacet<InventoryFacet>();
            return facet?.profile?.ResolvedMaxStack ?? int.MaxValue;
        }
    }
}
