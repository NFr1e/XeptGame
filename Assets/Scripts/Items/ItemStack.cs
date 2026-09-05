namespace XeptGame.Items
{
    /// <summary>
    /// 背包内的一叠物品（引用语义）：{ Definition, Count }。
    /// 引用语义便于在稳定顺序列表中就地改数量（ItemLoop_Design.md §4.2）；
    /// Count 仅 Inventory 可改（internal），对外只读。
    /// </summary>
    public sealed class ItemStack
    {
        /// <summary>物品定义（内容键；共享资产只读引用）。</summary>
        public ItemDefinition Definition { get; }

        /// <summary>当前数量（&gt;= 1；扣至 0 的行由 Inventory 移除）。</summary>
        public int Count { get; internal set; }

        internal ItemStack(ItemDefinition definition, int count)
        {
            Definition = definition;
            Count = count;
        }
    }
}
