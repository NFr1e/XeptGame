using XeptGame.Items;

namespace XeptGame.Container
{
    /// <summary>
    /// 槽级变更负载（槽位轨，SlotStore_Design.md §2；实例字段见 Item_Instance_Design.md §2.2）：
    /// 按槽身份键控，带前后物品与数量，<b>并带前后实例句柄</b>。
    /// <list type="bullet">
    /// <item>由槽自己发布（<see cref="SlotBase"/>），容器原样转发（不做二次翻译）；</item>
    /// <item>"槽级轨 = 位置/单格变化，聚合轨 = 物品总数变化"两条轨分工的槽侧一半；</item>
    /// <item>数量是通用形态的必需项：单位制槽与实例行恒 0/1，堆叠格为实际数量；New = null（NewCount = 0）表示槽被清空；</item>
    /// <item><b><see cref="OldInstance"/> / <see cref="NewInstance"/> 可空</b>：非空表示该侧是"有状态实例"行
    /// （数量恒 1、不参与合并），UI/表现层据此区分"一格木头"与"一个背包"。</item>
    /// </list>
    /// </summary>
    public readonly struct SlotChangeArgs
    {
        /// <summary>发生变化的槽身份。</summary>
        public readonly SlotId Slot;

        /// <summary>变化前占用（null = 原为空）。</summary>
        public readonly ItemDefinition Old;

        /// <summary>变化前数量（原为空时 0；实例行恒 1）。</summary>
        public readonly int OldCount;

        /// <summary>变化前持有的实例（null = 无状态行或原为空）。</summary>
        public readonly ItemInstance OldInstance;

        /// <summary>变化后占用（null = 已清空）。</summary>
        public readonly ItemDefinition New;

        /// <summary>变化后数量（0 = 已清空；实例行恒 1）。</summary>
        public readonly int NewCount;

        /// <summary>变化后持有的实例（null = 无状态行或已清空）。</summary>
        public readonly ItemInstance NewInstance;

        public SlotChangeArgs(SlotId slot, ItemDefinition oldItem, int oldCount, ItemDefinition newItem, int newCount)
            : this(slot, oldItem, oldCount, null, newItem, newCount, null)
        {
        }

        public SlotChangeArgs(SlotId slot,
                              ItemDefinition oldItem, int oldCount, ItemInstance oldInstance,
                              ItemDefinition newItem, int newCount, ItemInstance newInstance)
        {
            Slot = slot;
            Old = oldItem;
            OldCount = oldCount;
            OldInstance = oldInstance;
            New = newItem;
            NewCount = newCount;
            NewInstance = newInstance;
        }
    }
}
