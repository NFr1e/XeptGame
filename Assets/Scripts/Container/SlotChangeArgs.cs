using XeptGame.Items;

namespace XeptGame.Container
{
    /// <summary>
    /// 槽级变更负载（槽位轨，SlotStore_Design.md §2）：按槽身份键控，带前后物品与数量。
    /// <list type="bullet">
    /// <item>由槽自己发布（<see cref="SlotBase"/>），容器原样转发（不做二次翻译）；</item>
    /// <item>"槽级轨 = 位置/单格变化，聚合轨 = 物品总数变化"两条轨分工的槽侧一半；</item>
    /// <item>数量是通用形态的必需项：单位制槽恒 0/1，堆叠格为实际数量；New = null（NewCount = 0）表示槽被清空。</item>
    /// </list>
    /// </summary>
    public readonly struct SlotChangeArgs
    {
        /// <summary>发生变化的槽身份。</summary>
        public readonly SlotId Slot;

        /// <summary>变化前占用（null = 原为空）。</summary>
        public readonly ItemDefinition Old;

        /// <summary>变化前数量（原为空时 0）。</summary>
        public readonly int OldCount;

        /// <summary>变化后占用（null = 已清空）。</summary>
        public readonly ItemDefinition New;

        /// <summary>变化后数量（0 = 已清空）。</summary>
        public readonly int NewCount;

        public SlotChangeArgs(SlotId slot, ItemDefinition oldItem, int oldCount, ItemDefinition newItem, int newCount)
        {
            Slot = slot;
            Old = oldItem;
            OldCount = oldCount;
            New = newItem;
            NewCount = newCount;
        }
    }
}
