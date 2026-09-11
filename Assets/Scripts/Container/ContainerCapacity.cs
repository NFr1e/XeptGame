using System;
using System.Collections.Generic;
using XeptKit.Core;
using XeptKit.Event;

namespace XeptGame.Container
{
    /// <summary>
    /// 容器容量合成（SlotStore_Design.md §6"容量来源（多来源合成）"）：<b>算</b>在这里，<b>应用</b>在 <c>SlotStore</c>。
    /// <list type="bullet">
    /// <item><b>解析值 = 基础格数 + 各来源加成</b>（加法叠加）——对照：<c>OverrideValue&lt;T&gt;</c> 是"最高优先级择一"，
    /// 与扩容语义相反，故不适用；</item>
    /// <item><b>来源以键标识</b>，键的含义由调用方决定：装备式扩容建议用<b>身体槽位</b>（单位制、身份稳定；
    /// 背包聚合计数无法区分两件同名扩容物）；消耗式永久扩容建议直接计入 <see cref="SetBaseSlots"/>；</item>
    /// <item>同键重复设置 = 覆盖更新（后写生效）；移除未注册键 = no-op；</item>
    /// <item>解析值<b>实际变化</b>时才发 <see cref="Changed"/>（幂等：同值设置不通知）——容器据此应用（<c>ApplyCapacity</c>）。</item>
    /// </list>
    /// 只做算术与通知：不持物品、不认识装备或背包（来源由域侧提供）。主线程 only。
    /// </summary>
    public sealed class ContainerCapacity
    {
        private readonly Dictionary<object, int> _sources = new();
        private readonly SafeEvent<int> _changed = new();
        private int _baseSlots;

        public ContainerCapacity(int baseSlots)
        {
            Guard.InRange(baseSlots, 0, int.MaxValue, nameof(baseSlots));
            _baseSlots = baseSlots;
        }

        /// <summary>解析格数变化（参数 = 新的解析格数；仅实际变化时触发）。</summary>
        public event Action<int> Changed
        {
            add => _changed.Add(value);
            remove => _changed.Remove(value);
        }

        /// <summary>基础格数（不含扩容者）。</summary>
        public int BaseSlots => _baseSlots;

        /// <summary>当前登记的来源数量。</summary>
        public int SourceCount => _sources.Count;

        /// <summary>解析后的格数（基础 + 各来源；long 累加防溢出，封顶 <see cref="int.MaxValue"/>）。</summary>
        public int Resolved
        {
            get
            {
                long total = _baseSlots;
                foreach (var delta in _sources.Values)
                {
                    total += delta;
                }

                return total > int.MaxValue ? int.MaxValue : (int)total;
            }
        }

        /// <summary>设置基础格数（幂等；变化才通知）。</summary>
        public bool SetBaseSlots(int baseSlots)
        {
            Guard.InRange(baseSlots, 0, int.MaxValue, nameof(baseSlots));
            if (_baseSlots == baseSlots)
            {
                return false;
            }

            var before = Resolved;
            _baseSlots = baseSlots;
            return Notify(before);
        }

        /// <summary>登记/更新一个来源的加成格数（键重复 = 覆盖更新；幂等）。</summary>
        public bool SetSource(object key, int addedSlots)
        {
            Guard.NotNull(key, nameof(key));
            Guard.InRange(addedSlots, 1, int.MaxValue, nameof(addedSlots));

            var before = Resolved;
            _sources[key] = addedSlots;
            return Notify(before);
        }

        /// <summary>移除一个来源（未登记 = no-op；幂等）。</summary>
        public bool RemoveSource(object key)
        {
            Guard.NotNull(key, nameof(key));

            var before = Resolved;
            if (!_sources.Remove(key))
            {
                return false;
            }

            Notify(before);
            return true;
        }

        private bool Notify(int before)
        {
            var after = Resolved;
            if (after == before)
            {
                return false;
            }

            _changed.Invoke(after);
            return true;
        }
    }
}
