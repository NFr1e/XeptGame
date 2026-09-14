using Sirenix.OdinInspector;
using UnityEngine;
using XeptGame.Core;

namespace XeptGame.Items
{
    /// <summary>
    /// 物品背包面配置（由 <see cref="InventoryItemFacet"/> 持有的共享配置资产，Inventory_Facet_Design.md §3）：
    /// 只答两件事——这个物品**陈列到背包哪一页**、**每格最多几个**。
    /// <list type="bullet">
    /// <item><b>category = 背包归类的唯一标准</b>：语义判断（这东西是什么性质）归分类面，陈列归属归本字段，
    /// 两者正交、不构成多真源（Inventory_Facet_Design.md §4）；</item>
    /// <item><b>maxStack = 格语义，不是容器语义</b>：只决定"摆成几格"，不限制背包里能有多少个（容量属另一件事，
    /// 是背包侧配置的迟到项）。因此容量生效之前它**不改变任何容器写入语义**——背包容器、容器端口与转移原语都不读它；</item>
    /// <item><b>共享与复用</b>：资产可跨物品共享（如"资源·上限 20"），共享粒度天然是 (category, maxStack) 组合；</item>
    /// <item><b>刻意不加载字段</b>：无 stackable 布尔（<see cref="maxStack"/> = 1 已表达）、无图标/显示名（本体已有）、
    /// 无排序权重（背包侧决策）、无可见开关（安全性靠兜底页，不靠开关）。</item>
    /// </list>
    /// 归一化读取：消费方一律读 <see cref="ResolvedMaxStack"/>，不直接读字段（沿用 <see cref="HoldableFacetProfile.ResolvedTiming"/> 先例）。
    /// </summary>
    [CreateAssetMenu(
        menuName = XeptGameConsts.Editor.InventoryItemFacetProfileMenuName,
        fileName = XeptGameConsts.Editor.InventoryItemFacetProfileFileName,
        order = XeptGameConsts.Editor.InventoryItemFacetProfileOrder)]
    [Icon(XeptGameConsts.Editor.InventoryItemFacetProfileIconPath)]
    public sealed class InventoryItemFacetProfile : ScriptableObject
    {
        [Tooltip("背包归类（唯一标准）：决定物品默认陈列在哪一页；None = 未分类（落兜底页）")]
        public InventoryCategory category = InventoryCategory.None;

        [Tooltip("每格上限（≥1）：1 = 不可堆叠；须显式声明，不设魔法默认值")]
        [MinValue(1)]
        public int maxStack = 1;

        /// <summary>归一化每格上限：0/负只可能来自坏数据（旧资产/手改）→ 兜底 1，沿用"必须显式声明"立场。</summary>
        public int ResolvedMaxStack => maxStack < 1 ? 1 : maxStack;
    }

    /// <summary>
    /// 背包归类（<see cref="InventoryItemFacetProfile.category"/> 的取值域，Inventory_Facet_Design.md §3）：
    /// **纯展示桶**——只回答"陈列在哪一页"，不回答"这东西是什么性质"（后者归分类面，两者正交）。
    /// <list type="bullet">
    /// <item><b>编号稳定纪律</b>：取值编号随资产持久化，插入新取值只能取新编号，**禁止重排或复用**；</item>
    /// <item><b>初版值域</b>随背包界面决议收敛；新增取值 = 改本枚举一行，背包侧页签未覆盖的取值落兜底页；</item>
    /// <item>不允许往本枚举塞标志位：多语义由分类面/能力面的条目组合表达。</item>
    /// </list>
    /// </summary>
    public enum InventoryCategory
    {
        /// <summary>未分类/未指定（落"其他"兜底页——绝不让已拥有的物品从界面消失）。</summary>
        None = 0,

        /// <summary>资源（原始材料：石头/木头/纤维…）。</summary>
        Resource = 1,

        /// <summary>消耗品（直接使用即生效：药品/绷带/精力补充剂…）。</summary>
        Consumable = 2,

        /// <summary>武器。</summary>
        Weapon = 3,

        /// <summary>工具（斧/镐…）。</summary>
        Tool = 4,

        /// <summary>建材（建造用加工材料）。</summary>
        Material = 5,
    }
}
