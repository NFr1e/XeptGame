using Sirenix.OdinInspector;
using UnityEngine;
using XeptGame.Core;

namespace XeptGame.Items
{
    /// <summary>
    /// 容器扩容配置（由 <see cref="ContainerCapacityExpanderFacet"/> 持有的共享配置资产，SlotStore_Design.md §6）：
    /// 只答"这件东西给容器加多少格"。容量 = 基础格数 + 各扩容者加成（**加法叠加**，不是优先级择一）。
    /// <list type="bullet">
    /// <item><b>来源身份不用物品</b>：背包是聚合计数、分不清两件同名扩容物 → 来源键取<b>所在槽位</b>
    /// （身体容器单位制、身份稳定）；本资产只提供"加多少"；</item>
    /// <item><b>刻意不加载</b>：适用范围（背包/箱子）等第二个容量容器出现再加；
    /// "可否叠加/是否永久"由<b>怎么使用</b>（装备 vs 消耗）表达，不落配置字段。</item>
    /// </list>
    /// </summary>
    [CreateAssetMenu(
        menuName = XeptGameConsts.Editor.ItemContainerCapacityExpanderFacetProfileMenuName,
        fileName = XeptGameConsts.Editor.ItemContainerCapacityExpanderFacetProfileFileName,
        order = XeptGameConsts.Editor.ItemContainerCapacityExpanderFacetProfileOrder)]
    [Icon(XeptGameConsts.Editor.ContainerCapacityExpanderProfileIconPath)]
    public sealed class ContainerCapacityExpanderProfile : ScriptableObject
    {
        [Tooltip("扩容格数（≥1）：多件扩容者加法叠加")]
        [MinValue(1)]
        public int addedSlots = 1;

        /// <summary>归一化扩容格数：0/负（坏数据）→ 1（扩容者至少加 1 格才有意义）。</summary>
        public int ResolvedAddedSlots => addedSlots < 1 ? 1 : addedSlots;
    }
}
