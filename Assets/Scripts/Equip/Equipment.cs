using System;
using System.Collections.Generic;
using XeptGame.Inv;
using XeptGame.Items;
using XeptKit.Core;
using XeptKit.Event;

namespace XeptGame.Equip
{
    /// <summary>
    /// 身体容器（运行时 = 配置驱动的槽占用集，Equip_FPV_Design.md §2.2/§3.2，T2）：
    /// 构造时注入 <see cref="ISlot"/> 列表（槽配置），只答"各槽当前占用"，不含接纳规则（接纳在 ISlot）、
    /// 不持候选列表。
    /// <list type="bullet">
    /// <item><b>槽 = 一等值</b>（<see cref="BodySlotType"/>），事件按槽键控（<see cref="SlotChanged"/>）——
    /// 加槽 = 纯新增，消费方合同零改动；</item>
    /// <item><b>单位制</b>：每槽无视物品可堆叠性、容量恒 1 单位（可堆叠性是背包行存储的属性）；</item>
    /// <item><b>实现 IItemContainer</b>（单位语义容器面，v1 单槽无寻址歧义）——让
    /// <see cref="ContainerTransfer.Move"/> 哑原语真实可用（收起 = Move(body → bag) 等）；
    /// 容器级 <see cref="Changed"/> 与 <see cref="SlotChanged"/> 同一次占用变更双发
    /// （分别服务通用容器消费与按槽消费）；</item>
    /// <item>占用变更仅经行为轴命令（TryAdd/TryRemove = 容器端口，唯一写者纪律由命令层保证）。</item>
    /// </list>
    /// </summary>
    public sealed class Equipment : IItemContainer
    {
        private readonly Dictionary<BodySlotType, ISlot> _slots;
        private readonly Dictionary<BodySlotType, ItemDefinition> _occupancy;

        /// <summary>槽位占用变化（按槽键控；New = null = 槽空；SafeEvent：异常隔离 + 订阅去重）。</summary>
        private readonly SafeEvent<SlotChangeArgs> _slotChanged = new();

        public event Action<SlotChangeArgs> SlotChanged
        {
            add => _slotChanged.Add(value);
            remove => _slotChanged.Remove(value);
        }

        /// <summary>容器级状态轨（IItemContainer；与 SlotChanged 同源双发，负载 = 单位 0↔1；SafeEvent）。</summary>
        private readonly SafeEvent<InventoryChangeArgs> _changed = new();

        public event Action<InventoryChangeArgs> Changed
        {
            add => _changed.Add(value);
            remove => _changed.Remove(value);
        }

        public Equipment(IReadOnlyList<ISlot> slots)
        {
            Guard.NotNull(slots, nameof(slots));
            _slots = new Dictionary<BodySlotType, ISlot>(slots.Count);
            _occupancy = new Dictionary<BodySlotType, ItemDefinition>(slots.Count);

            foreach (var slot in slots)
            {
                Guard.NotNull(slot, nameof(slots));
                if (_slots.ContainsKey(slot.Id))
                {
                    throw new ArgumentException($"身体容器配置了重复槽位：{slot.Id}");
                }

                _slots.Add(slot.Id, slot);
                _occupancy[slot.Id] = null;
            }
        }

        /// <summary>读取某槽当前占用（null = 槽空；槽未配置返回 null）。</summary>
        public ItemDefinition Get(BodySlotType slot) => _occupancy.TryGetValue(slot, out var def) ? def : null;

        /// <summary>槽是否为空。</summary>
        public bool IsEmpty(BodySlotType slot) => Get(slot) == null;

        /// <summary>槽的接纳查询（委托给该槽 ISlot；供命令层做"可持/可入槽"守卫）。</summary>
        public bool SlotAccepts(BodySlotType slot, ItemDefinition definition)
            => _slots.TryGetValue(slot, out var rule) && rule.Accepts(definition);

        // ---- IItemContainer：单位语义容器面 ----

        /// <inheritdoc />
        public IReadOnlyList<ItemStack> Stacks
        {
            get
            {
                if (_occupancy.Count == 0)
                {
                    return Array.Empty<ItemStack>();
                }

                var list = new List<ItemStack>(_occupancy.Count);
                foreach (var pair in _occupancy)
                {
                    if (pair.Value != null)
                    {
                        list.Add(new ItemStack(pair.Value, 1));
                    }
                }

                return list;
            }
        }

        /// <inheritdoc />
        public int CountOf(ItemDefinition definition)
        {
            Guard.NotNullObject(definition, nameof(definition));
            foreach (var pair in _occupancy)
            {
                if (ReferenceEquals(pair.Value, definition))
                {
                    return 1;
                }
            }

            return 0;
        }

        /// <inheritdoc />
        public bool Contains(ItemDefinition definition) => CountOf(definition) > 0;

        /// <summary>
        /// 单位语义 Add：count 必须为 1，且存在"空 + 接纳该物"的槽才成功（原子：失败不改动）。
        /// </summary>
        public bool TryAdd(ItemDefinition definition, int count)
        {
            Guard.NotNullObject(definition, nameof(definition));
            if (count != 1)
            {
                return false;
            }

            foreach (var pair in _slots)
            {
                if (_occupancy[pair.Key] != null)
                {
                    continue;
                }

                if (!pair.Value.Accepts(definition))
                {
                    continue;
                }

                _occupancy[pair.Key] = definition;
                RaiseChanged(pair.Key, null, definition);
                return true;
            }

            return false;
        }

        /// <summary>单位语义 Remove：count 必须为 1 且当前占用该物才成功（原子）。</summary>
        public bool TryRemove(ItemDefinition definition, int count)
        {
            Guard.NotNullObject(definition, nameof(definition));
            if (count != 1)
            {
                return false;
            }

            foreach (var pair in _occupancy)
            {
                if (ReferenceEquals(pair.Value, definition))
                {
                    _occupancy[pair.Key] = null;
                    RaiseChanged(pair.Key, definition, null);
                    return true;
                }
            }

            return false;
        }

        private void RaiseChanged(BodySlotType slot, ItemDefinition oldItem, ItemDefinition newItem)
        {
            _slotChanged.Invoke(new SlotChangeArgs(slot, oldItem, newItem));
            // 容器级负载：占用单位 0↔1（New=0 = 移除语义）
            var item = newItem ?? oldItem;
            if (item != null)
            {
                _changed.Invoke(new InventoryChangeArgs(item, oldItem != null ? 1 : 0, newItem != null ? 1 : 0));
            }
        }
    }
}
