using System;

namespace XeptGame.Container
{
    /// <summary>
    /// 槽位身份（SlotStore_Design.md §2）：容器内<b>稳定</b>的槽标识，int 支撑的只读结构体
    /// ——类型安全，避免各容器裸 int 混用。
    /// <list type="bullet">
    /// <item>身体槽：值 = <c>BodySlotType</c> 枚举值（词表留在 <c>XeptGame.Equip</c>）；</item>
    /// <item>背包格：值 = 格号（固定格数数组，扩容只在尾部增删 → 格号恒定）；</item>
    /// <item>槽身份用于槽级事件定位与 UI 精确更新；顺序只有尾部可变的容器才能保证其稳定。</item>
    /// </list>
    /// </summary>
    public readonly struct SlotId : IEquatable<SlotId>
    {
        /// <summary>容器内的槽号（语义由所属容器定义）。</summary>
        public readonly int Value;

        public SlotId(int value) => Value = value;

        public bool Equals(SlotId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is SlotId other && Equals(other);

        public override int GetHashCode() => Value;

        public override string ToString() => "Slot#" + Value;

        public static bool operator ==(SlotId left, SlotId right) => left.Value == right.Value;

        public static bool operator !=(SlotId left, SlotId right) => left.Value != right.Value;
    }
}
