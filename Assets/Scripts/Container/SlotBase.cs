using System;
using XeptGame.Items;
using XeptKit.Core;
using XeptKit.Event;

namespace XeptGame.Container
{
    /// <summary>
    /// 槽位基类（SlotStore_Design.md §2）——"能装东西的位置"的通用抽象：手部装备槽、背包格、
    /// 将来的箱子格/光标槽共用同一套语义。它是<b>读面 + 写口</b>的合体：
    /// <list type="bullet">
    /// <item><b>槽持自己的状态</b>（空否/装了什么/几个）与<b>自己的门控</b>（种类 + 数目）；</item>
    /// <item><b>写口 internal</b>：<see cref="TryPlace"/> / <see cref="TryTake"/> / <see cref="Clear"/> 只由所属容器调用
    /// ——<b>容器是唯一公开写面</b>（跨容器转移仍只经 <c>ContainerTransfer</c> / 编排器）；
    /// 调用约定 + 可评审的强转是收口手段（与 <c>ItemStack.Count</c> 的 internal set 同一做法）；</item>
    /// <item><b>槽不得自建/销毁单位</b>：单位只经容器口进出，"每个单位只存在于一个容器"不破；</item>
    /// <item><b>接口面刻意最小</b>（对照：MC 的 Slot 携带 UI 坐标与大量钩子，这里只取"身份 + 门控 + 通知"三件事）。</item>
    /// </list>
    /// <b>为何没有单独的只读接口</b>：槽实现只有本基类一族（无第二种实现，也没有跨程序集只读消费方），
    /// `internal` 已挡住跨程序集写入；按"接口按真实消费者生长"的纪律不预抽接口。
    /// 若将来出现第二种槽实现或需要硬只读契约，再抽 <c>ISlot</c> 是机械改动（改两处签名）。
    /// </summary>
    public abstract class SlotBase
    {
        private readonly SafeEvent<SlotChangeArgs> _changed = new();
        private ItemDefinition _item;
        private int _count;

        /// <summary>容器内稳定身份（身体槽 = BodySlotType 值；背包格 = 格号）。</summary>
        public SlotId Id { get; }

        /// <summary>是否为空。</summary>
        public bool IsEmpty => _item == null;

        /// <summary>当前占用（空 = null）。</summary>
        public ItemDefinition Item => _item;

        /// <summary>当前数量（空 = 0；单位制槽恒 1）。</summary>
        public int Count => _count;

        /// <summary>槽级变更（槽位轨；由槽发布，容器原样转发）。</summary>
        public event Action<SlotChangeArgs> Changed
        {
            add => _changed.Add(value);
            remove => _changed.Remove(value);
        }

        protected SlotBase(SlotId id) => Id = id;

        /// <summary>种类门控：该槽是否接纳此物品（实现类回答）。</summary>
        public abstract bool Accepts(ItemDefinition definition);

        /// <summary>数目门控：该槽对此物品的每格上限（<see cref="int.MaxValue"/> = 不约束；实现类回答）。</summary>
        public abstract int CapacityFor(ItemDefinition definition);

        /// <summary>该槽对此物品的剩余可放量（空槽 = 上限；占用同物 = 上限 − 数量；异类/不接纳 = 0）。</summary>
        public int RemainingCapacityFor(ItemDefinition definition)
        {
            if (definition == null || !Accepts(definition))
            {
                return 0;
            }

            var capacity = CapacityFor(definition);
            if (capacity <= 0)
            {
                return 0;
            }

            if (IsEmpty)
            {
                return capacity;
            }

            return ReferenceEquals(_item, definition) ? capacity - _count : 0;
        }

        // ---- 写口（internal：仅所属容器可调，SlotStore_Design.md §2）----

        /// <summary>放入指定数量（原子：门控不过或超上限则整笔失败且不改动）。</summary>
        internal bool TryPlace(ItemDefinition definition, int count)
        {
            Guard.NotNullObject(definition, nameof(definition));
            Guard.True(count > 0, "入槽数量必须为正。");

            if (!Accepts(definition))
            {
                return false;
            }

            if (!IsEmpty && !ReferenceEquals(_item, definition))
            {
                return false;
            }

            var capacity = CapacityFor(definition);
            if (capacity <= 0 || _count + count > capacity)
            {
                return false;
            }

            var oldItem = _item;
            var oldCount = _count;
            _item = definition;
            _count += count;
            RaiseChanged(oldItem, oldCount, _item, _count);
            return true;
        }

        /// <summary>取走指定数量（原子：不足则整笔失败且不改动；扣至 0 即视为空）。</summary>
        internal bool TryTake(int count)
        {
            Guard.True(count > 0, "出槽数量必须为正。");

            if (IsEmpty || _count < count)
            {
                return false;
            }

            var oldItem = _item;
            var oldCount = _count;
            _count -= count;
            if (_count == 0)
            {
                _item = null;
            }

            RaiseChanged(oldItem, oldCount, _item, _count);
            return true;
        }

        /// <summary>清空（幂等；非空时发一条槽级变更）。</summary>
        internal void Clear()
        {
            if (IsEmpty)
            {
                return;
            }

            var oldItem = _item;
            var oldCount = _count;
            _item = null;
            _count = 0;
            RaiseChanged(oldItem, oldCount, null, 0);
        }

        private void RaiseChanged(ItemDefinition oldItem, int oldCount, ItemDefinition newItem, int newCount)
            => _changed.Invoke(new SlotChangeArgs(Id, oldItem, oldCount, newItem, newCount));
    }
}
