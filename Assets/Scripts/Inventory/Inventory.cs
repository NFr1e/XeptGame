using System;
using XeptGame.Container;
using XeptGame.Core;
using XeptGame.Items;

namespace XeptGame.Inv
{
    /// <summary>
    /// 背包容器（SlotStore_Design.md §1/§6）＝槽容器 <see cref="SlotStore"/> 的<b>背包域特化</b>。
    /// <list type="bullet">
    /// <item><b>兼两副面孔</b>：物品面 = <see cref="SlotContainer.Stacks"/>（聚合行，给编排器/世界/调试）；
    /// 槽位面 = <see cref="SlotContainer.Slots"/>、<see cref="SlotContainer.TryPlaceAt"/>、
    /// <see cref="SlotStore.TryCompact"/>、<see cref="SlotStore.ApplyCapacity"/>（给背包界面与归位）；</item>
    /// <item><b>容量 = 基础格数 + 扩容者加成</b>：基础来自 <see cref="InventoryProfile"/>（缺省用常量），
    /// 扩容者经 <see cref="SetCapacitySource"/> / <see cref="RemoveCapacitySource"/> 以<b>槽位为键</b>登记；
    /// 合成在 <see cref="ContainerCapacity"/>，应用走 <c>ApplyCapacity</c> 既有路径（尾部增删 + 压缩/丢弃）；</item>
    /// <item><b>无自有状态</b>：占用事实全在格子里；命名收口随之落地——<c>Stacks</c> 归物品聚合、<c>Slots</c> 归槽位；</item>
    /// <item><b>物品面零改动</b>：<c>IItemContainer</c> 端口的聚合语义与事件形状保持（编排器/世界/测试不受影响）。</item>
    /// <item><b>容量入口纪律</b>：域侧改容量一律走 <see cref="SetCapacitySource"/> / <see cref="RemoveCapacitySource"/>
    /// （或构造时给 profile）；继承来的 <c>SlotStore.ApplyCapacity</c> 是低层"应用到指定格数"的机制口，
    /// 直接调用会让实际格数与合成值脱钩（仅机制/测试使用）。</item>
    /// </list>
    /// </summary>
    public sealed class Inventory : SlotStore, IItemContainer
    {
        /// <summary>容量合成（基础 + 各扩容来源）；解析值变化时自动应用到本容器。</summary>
        private readonly ContainerCapacity _capacity;

        public Inventory(int capacity = XeptGameConsts.Inventory.DefaultCapacity,
                         Action<ItemDefinition, int> discardSink = null)
            : base(capacity, discardSink)
        {
            _capacity = new ContainerCapacity(capacity);
            _capacity.Changed += OnCapacityResolved;
        }

        /// <summary>按背包侧配置装配（<paramref name="profile"/> 为 null = 用常量默认），窗口给显式装配点。</summary>
        public Inventory(InventoryProfile profile, Action<ItemDefinition, int> discardSink = null)
            : this(profile != null ? profile.ResolvedBaseSlots : XeptGameConsts.Inventory.DefaultCapacity, discardSink)
        {
        }

        /// <summary>当前登记的容量来源数量（装备式扩容者数量）。</summary>
        public int CapacitySourceCount => _capacity.SourceCount;

        /// <summary>
        /// 应用背包侧配置（<b>幂等</b>）：把 <see cref="InventoryProfile.ResolvedBaseSlots"/> 作为容量基准；
        /// <paramref name="profile"/> 为 null 时回退常量默认。容量变化走既有扩缩容路径（尾部追加 / 压缩 + 溢出丢弃）。
        /// 装配点（<c>InventoryCapacityModule</c>）在会话建立后调用——会话创建早于基座场景加载，故为后置注入；
        /// 重复调用安全（同值不动作）。
        /// </summary>
        public bool ApplyProfile(InventoryProfile profile)
            => _capacity.SetBaseSlots(profile != null ? profile.ResolvedBaseSlots : XeptGameConsts.Inventory.DefaultCapacity);

        /// <summary>
        /// 登记/更新一个扩容来源（<b>键 = 所在槽位</b>，见 <see cref="ContainerCapacity"/>；重复键覆盖更新）；
        /// 解析格数变化时自动应用（扩容追加空格 / 缩容压缩 + 溢出丢弃）。
        /// </summary>
        public bool SetCapacitySource(object key, int addedSlots) => _capacity.SetSource(key, addedSlots);

        /// <summary>移除一个扩容来源（卸载扩容者 → 自动缩容，溢出按丢弃出口处理）。</summary>
        public bool RemoveCapacitySource(object key) => _capacity.RemoveSource(key);

        private void OnCapacityResolved(int resolved) => ApplyCapacity(resolved);
    }
}
