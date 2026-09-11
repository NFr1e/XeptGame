using Sirenix.OdinInspector;
using UnityEngine;
using XeptGame.Core;

namespace XeptGame.Inv
{
    /// <summary>
    /// 背包侧配置（容量来源，SlotStore_Design.md §6）：只答"这个背包的<b>基础格数</b>是多少"。
    /// 与物品侧 <c>InventoryFacetProfile</c>（归类 + 每格上限）分工不同——前者是<b>容器</b>的配置，后者是<b>物品</b>的配置。
    /// <list type="bullet">
    /// <item><b>容量 = 基础格数 + 各扩容者加成</b>（加法叠加）：本资产只提供"基础"那一段，
    /// 合成在 <c>ContainerCapacity</c>、应用在 <c>SlotStore.ApplyCapacity</c>；</item>
    /// <item><b>0 合法</b>（= 没有背包）：容量下限 0 是裁定过的合法状态；负数只可能是坏数据 → 回退默认（<see cref="ResolvedBaseSlots"/>）；</item>
    /// <item><b>刻意不加载</b>：页签顺序/显示名/排序策略属背包界面决议，届时另立资产或另立字段。</item>
    /// </list>
    /// </summary>
    [CreateAssetMenu(
        menuName = XeptGameConsts.Editor.InventoryProfileMenuName,
        fileName = XeptGameConsts.Editor.InventoryProfileFileName,
        order = XeptGameConsts.Editor.InventoryProfileOrder)]
    public sealed class InventoryProfile : ScriptableObject
    {
        [Tooltip("基础格数（≥0；0 = 没有背包）。扩容者带来的格数由扩容资产叠加，不写在这里")]
        [MinValue(0)]
        public int baseSlots = XeptGameConsts.Inventory.DefaultCapacity;

        /// <summary>归一化基础格数：负值（坏数据）→ 回退默认；0 原样保留（合法的"无背包"）。</summary>
        public int ResolvedBaseSlots => baseSlots < 0 ? XeptGameConsts.Inventory.DefaultCapacity : baseSlots;
    }
}
