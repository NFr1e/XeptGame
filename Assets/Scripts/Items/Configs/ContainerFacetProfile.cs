using UnityEngine;
using XeptGame.Core;

namespace XeptGame.Items
{
    /// <summary>
    /// 容器能力面配置（由 <see cref="ContainerFacet"/> 持有的共享配置资产；Item_Instance_Design.md §3）：
    /// 回答"这个物品**自带容器**时的容器参数"。
    /// <b>与 <see cref="InventoryItemFacetProfile"/> 分工不同</b>：那条是"物品作为背包条目"的语义
    /// （归类 + 每格上限），本资产是"这个物品自己能不能装东西"的语义（容器基础格数）。
    /// </summary>
    [CreateAssetMenu(menuName = XeptGameConsts.Editor.ContainerFacetProfileMenuName,
                     fileName = XeptGameConsts.Editor.ContainerFacetProfileFileName,
                     order = XeptGameConsts.Editor.ContainerFacetProfileOrder)]
    public sealed class ContainerFacetProfile : ScriptableObject
    {
        [Tooltip("容器基础格数：负数视为坏数据 → 回退常量默认；0 合法（容量下限 0，SlotStore_Design.md §6）")]
        public int baseSlots = XeptGameConsts.Inventory.DefaultCapacity;

        /// <summary>基础格数（负值 = 坏数据 → 回退默认；0 = 无格容器，合法）。</summary>
        public int ResolvedBaseSlots => baseSlots < 0 ? XeptGameConsts.Inventory.DefaultCapacity : baseSlots;
    }
}
