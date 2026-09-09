using System;
using System.Collections.Generic;
using XeptKit.Core;
using XeptKit.Event;
using XeptGame.Items;

namespace XeptGame.Inv
{
    /// <summary>
    /// 背包容器（纯 C#，ItemLoop_Design.md §4）：只回答"我有什么、各多少、增删查"。
    /// <list type="bullet">
    /// <item><b>列表背包 = 聚合计数</b>：稳定顺序（首次加入序，UI 按序渲染）列表，同定义合并就地；
    /// 无槽位、无总容量、无堆叠上限（E1/E2 决策——迟到清单见 ItemLoop_Design.md §6）；</item>
    /// <item><b>TryRemove 原子</b>："有就扣"是弹药消耗/丢弃/未来合成消耗的公共语义，不足时返回 false 且不改动；</item>
    /// <item><b>状态变更细粒度推送</b>：<see cref="Changed"/> 每单条变更发一次（负载含前后数量，NewCount=0 = 行移除）；
    /// 一次性播报（拾取提示浮字）是事件非状态，走 GameplaySession 会话域 EventBus，不经本类（两轨分离，§4.3）；</item>
    /// <item>零场景依赖（唯一引擎耦合 = Definition 的 SO 引用）→ XeptGame.Tests EditMode 可单测。</item>
    /// </list>
    /// 宿主：由 GameplaySessionContext 持有（一轮域根，GameplaySession_Domain_Design.md）。
    /// </summary>
    public sealed class Inventory : IItemContainer
    {
        private readonly List<ItemStack> _slots = new();

        /// <summary>变更事件（SafeEvent：异常隔离 + 订阅去重）：每次实际变更推送一条（负载含 OldCount/NewCount）。</summary>
        private readonly SafeEvent<InventoryChangeArgs> _changed = new();

        public event Action<InventoryChangeArgs> Changed
        {
            add => _changed.Add(value);
            remove => _changed.Remove(value);
        }

        /// <summary>当前内容（稳定顺序：首次加入序；同定义合并不移动）。</summary>
        public IReadOnlyList<ItemStack> Slots => _slots;

        /// <summary>查询某定义的总数量（无 = 0）。</summary>
        public int CountOf(ItemDefinition definition)
        {
            Guard.NotNullObject(definition, nameof(definition));
            var slot = FindSlot(definition);
            return slot?.Count ?? 0;
        }

        /// <summary>是否持有某定义（数量 &gt; 0）。</summary>
        public bool Contains(ItemDefinition definition) => CountOf(definition) > 0;

        /// <summary>
        /// 添加物品：同定义已存在 → 合并就地；否则按加入序追加新行。
        /// 每次实际变化推送一条 <see cref="Changed"/>。
        /// </summary>
        public void Add(ItemDefinition definition, int count)
        {
            Guard.NotNullObject(definition, nameof(definition));
            Guard.True(count > 0, "添加数量必须为正。");

            var slot = FindSlot(definition);
            if (slot != null)
            {
                var old = slot.Count;
                slot.Count += count;
                _changed.Invoke(new InventoryChangeArgs(definition, old, slot.Count));
                return;
            }

            _slots.Add(new ItemStack(definition, count));
            _changed.Invoke(new InventoryChangeArgs(definition, 0, count));
        }

        /// <summary>
        /// 尝试移除指定数量（原子）：存在且数量足够 → 扣减（扣至 0 移除该行）并返回 true；
        /// 否则返回 false 且**不做任何改动**、不推送事件。
        /// </summary>
        public bool TryRemove(ItemDefinition definition, int count)
        {
            Guard.NotNullObject(definition, nameof(definition));
            Guard.True(count > 0, "移除数量必须为正。");

            var slot = FindSlot(definition);
            if (slot == null || slot.Count < count)
            {
                return false;
            }

            var old = slot.Count;
            var remaining = old - count;
            if (remaining == 0)
            {
                _slots.Remove(slot);
                _changed.Invoke(new InventoryChangeArgs(definition, old, 0));
            }
            else
            {
                slot.Count = remaining;
                _changed.Invoke(new InventoryChangeArgs(definition, old, remaining));
            }

            return true;
        }

        /// <summary>清空：逐行移除并推送（每行一条，NewCount = 0）。</summary>
        public void Clear()
        {
            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                var slot = _slots[i];
                _slots.RemoveAt(i);
                _changed.Invoke(new InventoryChangeArgs(slot.Definition, slot.Count, 0));
            }
        }

        private ItemStack FindSlot(ItemDefinition definition)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (ReferenceEquals(_slots[i].Definition, definition))
                {
                    return _slots[i];
                }
            }

            return null;
        }

        // ---- IItemContainer 端口实现（Equip_FPV_Design.md §3.1，T1）：语义零改动 ----
        // Stacks = Slots 同源（显式实现，不扩充既有公开 API）；TryAdd = Add 的端口形态（行容器恒成功）。

        IReadOnlyList<ItemStack> IItemContainer.Stacks => _slots;

        bool IItemContainer.TryAdd(ItemDefinition definition, int count)
        {
            Add(definition, count);
            return true;
        }
    }
}
