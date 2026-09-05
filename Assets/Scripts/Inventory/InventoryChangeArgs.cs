using XeptGame.Items;

namespace XeptGame.Inv
{
    /// <summary>
    /// 背包变更负载（只读结构体）：单条变更的完整语义，消费方（背包 UI 行刷新）免反向推导
    /// ——镜像 Interaction SelectionChangeArgs 先例（ItemLoop_Design.md §4.3）。
    /// </summary>
    public readonly struct InventoryChangeArgs
    {
        /// <summary>发生变更的物品。</summary>
        public readonly ItemDefinition Item;

        /// <summary>变化前数量（该行新增时为 0）。</summary>
        public readonly int OldCount;

        /// <summary>变化后数量（0 = 该行被移除）。</summary>
        public readonly int NewCount;

        public InventoryChangeArgs(ItemDefinition item, int oldCount, int newCount)
        {
            Item = item;
            OldCount = oldCount;
            NewCount = newCount;
        }
    }
}
