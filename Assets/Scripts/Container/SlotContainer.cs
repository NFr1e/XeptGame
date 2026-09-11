using System;
using System.Collections.Generic;
using XeptGame.Items;
using XeptKit.Core;
using XeptKit.Event;

namespace XeptGame.Container
{
    /// <summary>
    /// 槽容器共享机制（SlotStore_Design.md §3）：容器 = 槽的管理者——持槽集合、做分配与聚合、发两条事件轨。
    /// <list type="bullet">
    /// <item><b>状态在槽、容器不另存</b>：占用事实唯一来源是各槽；</item>
    /// <item><b>全量语义</b>：<see cref="TryAdd"/> 装不下则整批拒绝、零改动（"部分接受"是容量生效时的迟到项）；
    /// <see cref="TryRemove"/> 原子（不足不改动）——<c>ContainerTransfer</c> 依赖这两条做回滚；</item>
    /// <item><b>两条事件轨各一个发布点</b>：槽发槽级轨（<see cref="SlotChanged"/> 由本类原样转发，不做二次翻译）；
    /// 聚合轨（<see cref="Changed"/>）在本类<b>提交点</b>按物品算完前后总数再发（子类用 <see cref="PublishChanged"/> 发布）；</item>
    /// <item><b>提交门</b>：整批变更（含两级事件派发）期间写请求一律返回 false——与 EB §8"提交区内的新请求返回 Busy"
    /// 同一纪律；子类（SlotStore 的扩缩容/压缩）用 <see cref="TryBeginMutation"/> / <see cref="EndMutation"/> 共用同一道门；</item>
    /// <item><b>分配规则</b>（单一实现 <see cref="PlaceWithin"/>）：按槽序升序，先并入同物品未满槽、再占用空槽；
    /// 新增分配与压缩/缩容共用同一套放置算法；移除按槽序（<see cref="TakeOut"/> 报告被清空的槽）。</item>
    /// <item><b>两种载荷与两个计数口径</b>（Item_Instance_Design.md §2.2）：无状态堆叠行与<b>实例行</b>（各计 1）；
    /// 公开聚合面（<see cref="CountOf"/> / <see cref="Stacks"/>）<b>包含</b>实例行，而<b>按定义寻址的写入跳过实例行</b>
    /// ——写可行性读 <see cref="CountOfCore"/>（可写口径），事件负载读 <see cref="TotalCountOfCore"/>（聚合口径）；
    /// 实例按格寻址：<see cref="TryPlaceInstanceAt"/> / <see cref="TryTakeInstanceAt"/>。</item>
    /// </list>
    /// 消费者：<c>Equipment</c>（身体域）与 <c>SlotStore</c>（机制层/背包域）。
    /// </summary>
    public abstract class SlotContainer
    {
        private readonly List<SlotBase> _slots;

        /// <summary>聚合轨（容器级状态轨；提交点发布，负载含前后总数）。</summary>
        private readonly SafeEvent<ContainerChangeArgs> _changed = new();

        /// <summary>槽级轨（由槽发布，本类原样转发）。</summary>
        private readonly SafeEvent<SlotChangeArgs> _slotChanged = new();

        private bool _mutating;

        protected SlotContainer(IReadOnlyList<SlotBase> slots)
        {
            Guard.NotNull(slots, nameof(slots));

            _slots = new List<SlotBase>(slots.Count);
            var ids = new HashSet<int>();
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                Guard.NotNull(slot, "slots[" + i + "]");
                if (!ids.Add(slot.Id.Value))
                {
                    throw new ArgumentException("容器配置了重复的槽位身份：" + slot.Id);
                }

                _slots.Add(slot);
                slot.Changed += OnSlotChanged;
            }
        }

        /// <summary>槽集合（只读视图；顺序 = 分配与展示顺序）。写口在各槽的 internal 成员上，只有本容器可调。</summary>
        public IReadOnlyList<SlotBase> Slots => _slots;

        /// <summary>聚合轨（容器级状态轨）：每次已提交变更按物品发一条。</summary>
        public event Action<ContainerChangeArgs> Changed
        {
            add => _changed.Add(value);
            remove => _changed.Remove(value);
        }

        /// <summary>槽级轨：由槽发布、本容器原样转发（含前后数量与槽身份）。</summary>
        public event Action<SlotChangeArgs> SlotChanged
        {
            add => _slotChanged.Add(value);
            remove => _slotChanged.Remove(value);
        }

        /// <summary>
        /// 某物品在当前容器内的<b>聚合</b>总数（跨槽求和；无 = 0）：<b>包含实例行</b>（每个实例计 1），
        /// 因此"我有几个背包"查得到（Item_Instance_Design.md §2.2）。
        /// 注意与写入口径 <see cref="CountOfCore"/> 的区别——后者只数可写的无状态数量。
        /// </summary>
        public int CountOf(ItemDefinition definition)
        {
            Guard.NotNullObject(definition, nameof(definition));
            return TotalCountOfCore(definition);
        }

        /// <summary>是否持有某物品（数量 &gt; 0）。</summary>
        public bool Contains(ItemDefinition definition) => CountOf(definition) > 0;

        /// <summary>物品面聚合视图（首现序、同物品合并）——供通用容器消费者与调试件使用。</summary>
        public IReadOnlyList<ItemStack> Stacks
        {
            get
            {
                List<ItemStack> rows = null;
                for (int i = 0; i < _slots.Count; i++)
                {
                    var slot = _slots[i];
                    if (slot.IsEmpty)
                    {
                        continue;
                    }

                    rows ??= new List<ItemStack>();
                    var existing = FindRow(rows, slot.Item);
                    if (existing != null)
                    {
                        existing.Count += slot.Count;
                    }
                    else
                    {
                        rows.Add(new ItemStack(slot.Item, slot.Count));
                    }
                }

                return rows ?? (IReadOnlyList<ItemStack>)Array.Empty<ItemStack>();
            }
        }

        /// <summary>
        /// 放入指定数量（全量语义：所有槽的剩余可放量之和不足则整批拒绝、零改动）。
        /// 分配顺序 = 槽序升序（先并入同物品未满槽、再占用空槽）。
        /// </summary>
        public bool TryAdd(ItemDefinition definition, int count)
        {
            Guard.NotNullObject(definition, nameof(definition));
            if (count <= 0 || !TryBeginMutation())
            {
                return false;
            }

            try
            {
                if (TotalRemainingCapacity(definition) < count)
                {
                    return false;
                }

                var before = TotalCountOfCore(definition);
                PlaceWithin(definition, count, _slots.Count);
                PublishChanged(definition, before, before + count);
                return true;
            }
            finally
            {
                EndMutation();
            }
        }

        /// <summary>移除指定数量（原子：总量不足则失败且零改动；按槽序扣减，扣至 0 即清槽）。</summary>
        public bool TryRemove(ItemDefinition definition, int count) => TryRemove(definition, count, out _);

        /// <summary>
        /// 移除指定数量并报告"被清空的槽"（归位提示取值用）：原子失败时 <paramref name="emptiedCell"/> 为 default；
        /// 若本次移除清空了多个槽，报告其中最后一个（调用方按需自行甄别）。
        /// </summary>
        public bool TryRemove(ItemDefinition definition, int count, out SlotId emptiedCell)
        {
            emptiedCell = default;
            Guard.NotNullObject(definition, nameof(definition));
            if (count <= 0 || !TryBeginMutation())
            {
                return false;
            }

            try
            {
                // 可行性按"可写口径"判断（实例行不可按定义扣减）；事件按"聚合口径"报告前后总数
                if (CountOfCore(definition) < count)
                {
                    return false;
                }

                var before = TotalCountOfCore(definition);
                TakeOut(definition, count, out emptiedCell);
                PublishChanged(definition, before, before - count);
                return true;
            }
            finally
            {
                EndMutation();
            }
        }

        /// <summary>
        /// 定向放入指定槽（归位用；原子：槽不存在、不接纳或余量不足则整笔失败且不改动）。
        /// </summary>
        public bool TryPlaceAt(SlotId cell, ItemDefinition definition, int count)
        {
            Guard.NotNullObject(definition, nameof(definition));
            if (count <= 0 || !TryBeginMutation())
            {
                return false;
            }

            try
            {
                var slot = FindSlot(cell);
                if (slot == null || slot.RemainingCapacityFor(definition) < count)
                {
                    return false;
                }

                var before = CountOfCore(definition);
                if (!slot.TryPlace(definition, count))
                {
                    return false;
                }

                PublishChanged(definition, before, before + count);
                return true;
            }
            finally
            {
                EndMutation();
            }
        }

        /// <summary>
        /// 定向放入一个<b>实例</b>（按格寻址；Item_Instance_Design.md §2.3）：目标格必须存在、完全为空且接纳其定义，
        /// 否则整笔失败且零改动。实例数量恒 1（不变量 I2），聚合轨按"实例各计 1"报告前后总数。
        /// </summary>
        public bool TryPlaceInstanceAt(SlotId cell, ItemInstance instance)
        {
            Guard.NotNull(instance, nameof(instance));
            if (!TryBeginMutation())
            {
                return false;
            }

            try
            {
                var slot = FindSlot(cell);
                if (slot == null)
                {
                    return false;
                }

                var before = TotalCountOfCore(instance.Definition);
                if (!slot.TryPlaceInstance(instance))
                {
                    return false;
                }

                PublishChanged(instance.Definition, before, before + 1);
                return true;
            }
            finally
            {
                EndMutation();
            }
        }

        /// <summary>
        /// 从指定格取出一个<b>实例</b>（按格寻址）：该格没持实例则失败且零改动。
        /// 取出的实例仍持有身份——调用方负责把它交给另一个容器或出口（"一个实例一个位置"，不变量 I1）。
        /// </summary>
        public bool TryTakeInstanceAt(SlotId cell, out ItemInstance instance)
        {
            instance = null;
            if (!TryBeginMutation())
            {
                return false;
            }

            try
            {
                var slot = FindSlot(cell);
                if (slot == null || !slot.HasInstance)
                {
                    return false;
                }

                var before = TotalCountOfCore(slot.Instance.Definition);
                if (!slot.TryTakeInstance(out instance))
                {
                    instance = null;
                    return false;
                }

                PublishChanged(instance.Definition, before, before - 1);
                return true;
            }
            finally
            {
                EndMutation();
            }
        }

        /// <summary>
        /// 把实例放进<b>最靠前的可用空格</b>（"随便找个格子"的实例入口；世界背包放进当前背包等）。
        /// 找不到可用格 = false 且零改动；聚合轨按"实例各计 1"报告。
        /// </summary>
        public bool TryPlaceInstance(ItemInstance instance)
        {
            Guard.NotNull(instance, nameof(instance));
            if (!TryBeginMutation())
            {
                return false;
            }

            try
            {
                var before = TotalCountOfCore(instance.Definition);
                if (!PlaceInstanceWithin(instance, _slots.Count))
                {
                    return false;
                }

                PublishChanged(instance.Definition, before, before + 1);
                return true;
            }
            finally
            {
                EndMutation();
            }
        }

        /// <summary>槽集合（可写视图）——仅供子类做容量增删（<c>SlotStore</c> 扩缩容）。</summary>
        protected IReadOnlyList<SlotBase> SlotList => _slots;

        // ---- protected 内部件：不加门、不发聚合轨，由公开面或子类在门内调用（SlotStore_Design.md §3/§6）----

        /// <summary>进入提交区（可重入拒绝）：失败表示已有变更在进行中。</summary>
        protected bool TryBeginMutation()
        {
            if (_mutating)
            {
                return false;
            }

            _mutating = true;
            return true;
        }

        /// <summary>退出提交区。</summary>
        protected void EndMutation() => _mutating = false;

        /// <summary>
        /// 跨槽求和（不加门、不校验空引用）——<b>可写口径</b>：只数无状态行、<b>跳过实例行</b>。
        /// 用于按定义寻址的写入可行性判断（实例行的身份不可按定义定位，Item_Instance_Design.md §2.2）。
        /// </summary>
        protected int CountOfCore(ItemDefinition definition)
        {
            var total = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.HasInstance || !ReferenceEquals(slot.Item, definition))
                {
                    continue;
                }

                total += slot.Count;
            }

            return total;
        }

        /// <summary>
        /// 跨槽求和（不加门、不校验空引用）——<b>聚合口径</b>：无状态行按数量、<b>实例行各计 1</b>。
        /// 与公开的 <see cref="CountOf"/> / <see cref="Stacks"/> 一致，也是聚合轨
        /// （<see cref="ContainerChangeArgs"/>）报告前后总数的口径。
        /// </summary>
        protected int TotalCountOfCore(ItemDefinition definition)
        {
            var total = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!ReferenceEquals(slot.Item, definition))
                {
                    continue;
                }

                total += slot.HasInstance ? 1 : slot.Count;
            }

            return total;
        }

        /// <summary>
        /// 全容器对该物品的剩余可放量之和（long 累加：无上限格返回 <see cref="int.MaxValue"/>，
        /// 多格相加会溢出 int）。
        /// </summary>
        protected long TotalRemainingCapacity(ItemDefinition definition)
        {
            long total = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                total += _slots[i].RemainingCapacityFor(definition);
            }

            return total;
        }

        /// <summary>
        /// 常规分配（单一实现）：在 <paramref name="limit"/> 个槽内，先并入同物品未满槽、再占用空槽（槽序升序），
        /// 返回实际放入量。分配不足时返回小于 <paramref name="count"/> 的值（调用方决定是否视为失败）。
        /// </summary>
        protected int PlaceWithin(ItemDefinition definition, int count, int limit)
        {
            var remaining = count;

            for (int pass = 0; pass < 2 && remaining > 0; pass++)
            {
                for (int i = 0; i < limit && remaining > 0; i++)
                {
                    var slot = _slots[i];
                    if (slot.HasInstance)
                    {
                        continue; // 实例行不参与按定义的合并/分配（不变量 I2）
                    }

                    var merge = pass == 0;
                    if (merge ? (slot.IsEmpty || !ReferenceEquals(slot.Item, definition)) : !slot.IsEmpty)
                    {
                        continue;
                    }

                    var place = Math.Min(remaining, slot.RemainingCapacityFor(definition));
                    if (place <= 0)
                    {
                        continue;
                    }

                    if (!slot.TryPlace(definition, place))
                    {
                        throw new InvalidOperationException("槽分配失败：剩余可放量与实际不符（" + slot.Id + "）。");
                    }

                    remaining -= place;
                }
            }

            return count - remaining;
        }

        /// <summary>按槽序扣减（不加门），返回实际取走量与最后一个被清空的槽。</summary>
        protected int TakeOut(ItemDefinition definition, int count, out SlotId emptiedCell)
        {
            emptiedCell = default;
            var remaining = count;

            for (int i = 0; i < _slots.Count && remaining > 0; i++)
            {
                var slot = _slots[i];
                if (slot.HasInstance || !ReferenceEquals(slot.Item, definition))
                {
                    continue; // 实例行不参与按定义的扣减（不变量 I2）
                }

                var take = Math.Min(remaining, slot.Count);
                if (!slot.TryTake(take))
                {
                    throw new InvalidOperationException("槽扣减失败：数量与实际不符（" + slot.Id + "）。");
                }

                if (slot.IsEmpty)
                {
                    emptiedCell = slot.Id;
                }

                remaining -= take;
            }

            return count - remaining;
        }

        /// <summary>
        /// 把一个实例放进 [0, <paramref name="limit"/>) 内<b>最靠前的空格</b>（机制内部：缩容/压缩安置实例行；
        /// 不加门、不发聚合轨）。放不下返回 false，此时容器内容未被改动。
        /// </summary>
        protected bool PlaceInstanceWithin(ItemInstance instance, int limit)
        {
            Guard.NotNull(instance, nameof(instance));

            for (int i = 0; i < limit && i < _slots.Count; i++)
            {
                if (_slots[i].TryPlaceInstance(instance))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>按槽身份查找（无 = null）。</summary>
        protected SlotBase FindSlot(SlotId cell)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Id == cell)
                {
                    return _slots[i];
                }
            }

            return null;
        }

        /// <summary>尾部追加一个槽（订阅其变更；SlotStore 扩容用）。</summary>
        protected void AppendSlot(SlotBase slot)
        {
            Guard.NotNull(slot, nameof(slot));
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Id.Value == slot.Id.Value)
                {
                    throw new ArgumentException("容器配置了重复的槽位身份：" + slot.Id);
                }
            }

            _slots.Add(slot);
            slot.Changed += OnSlotChanged;
        }

        /// <summary>移除尾部槽（退订；调用方负责先把内容搬空或丢弃——SlotStore 缩容用）。</summary>
        protected void RemoveTrailingSlot()
        {
            if (_slots.Count == 0)
            {
                return;
            }

            var slot = _slots[_slots.Count - 1];
            slot.Changed -= OnSlotChanged;
            _slots.RemoveAt(_slots.Count - 1);
        }

        /// <summary>发布聚合轨（提交点调用；SlotStore 在缩容丢弃时逐物品发布）。</summary>
        protected void PublishChanged(ItemDefinition item, int oldCount, int newCount)
            => _changed.Invoke(new ContainerChangeArgs(item, oldCount, newCount));

        private static ItemStack FindRow(List<ItemStack> rows, ItemDefinition definition)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (ReferenceEquals(rows[i].Definition, definition))
                {
                    return rows[i];
                }
            }

            return null;
        }

        private void OnSlotChanged(SlotChangeArgs args) => _slotChanged.Invoke(args);
    }
}
