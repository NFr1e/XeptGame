using XeptGame.Items;

namespace XeptGame.Equip
{
    /// <summary>
    /// 身体容器槽变更负载（只读结构体，Equip_FPV_Design.md §3.2）：
    /// 按槽键控（Slot + Old/New），New = null = 槽空。消费方（EquipViewModule/未来 UI）按
    /// <see cref="BodySlotType"/> 订阅，槽数增长不改本负载形状。
    /// </summary>
    public readonly struct SlotChangeArgs
    {
        /// <summary>发生占用的槽位。</summary>
        public readonly BodySlotType Slot;

        /// <summary>变化前占用（null = 原为空）。</summary>
        public readonly ItemDefinition Old;

        /// <summary>变化后占用（null = 已清空）。</summary>
        public readonly ItemDefinition New;

        public SlotChangeArgs(BodySlotType slot, ItemDefinition oldItem, ItemDefinition newItem)
        {
            Slot = slot;
            Old = oldItem;
            New = newItem;
        }
    }
}
