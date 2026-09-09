using UnityEngine;
using XeptGame.Core;

namespace XeptGame.Items
{
    /// <summary>
    /// 握持配置子对象（由 <see cref="HoldableFacet"/> 持有）：握持类别 + 手里视觉模型。
    /// 由能力面的字段初始化创建（条目添加即非空）——<see cref="category"/> 默认 Handheld 只是
    /// **参数初值**，不承载"是否存在该能力"的语义（存在性看 Facet 条目）。
    /// 若将来需要表驱动批量调参（武器手感）或跨物品共享配置，可整段提为独立 SO 引用而不改消费者
    /// （ItemLoop_Design.md §6 B 化路径）。
    /// </summary>
    [CreateAssetMenu(
        menuName = XeptGameConsts.Editor.ItemHoldableFacetProfileMenuName,
        fileName = XeptGameConsts.Editor.ItemHoldableFacetProfileFileName,
        order = XeptGameConsts.Editor.ItemHoldableFacetProfileOrder)]
    public sealed class HoldProfile : ScriptableObject
    {
        [Tooltip("握持类别：决定 FPV 挂点与默认握持姿势/动画层")]
        public HoldCategory category = HoldCategory.Handheld;

        [Tooltip("手里的视觉模型；为空时回退世界模型（FPV 阶段实现回退策略）")]
        public GameObject viewPrefab;

        [Tooltip("拿放时长参数（随本握持配置共享，供物品差异调参；全 0 = 未配置 → 读取时回退默认）")]
        public EquipTiming timing;

        /// <summary>解析后的拿放时长：未配置（全 0 / 旧资产反序列化）时回退 <see cref="EquipTiming.Default"/>；行为只收解析后的值。</summary>
        public EquipTiming ResolvedTiming => timing.IsSet ? timing : EquipTiming.Default;
    }

    /// <summary>
    /// 握持类别：FPV 手部表现（挂点选择 + 默认握持姿势/动画层）的决策输入（Equip + FPV 阶段消费）。
    /// 本类别是 <see cref="HoldProfile"/> 的参数值，**不独立表达能力存在**——能力由能力面
    /// （HoldableFacet）的条目存在表达（ItemLoop_Design.md §3.2）。
    /// </summary>
    public enum HoldCategory
    {
        /// <summary>未指定/不通过持物系统（保留位；正常可持物用 Handheld/TwoHanded）。</summary>
        None = 0,

        /// <summary>单手小件（石头/药水…，握于掌心）。</summary>
        Handheld = 1,

        /// <summary>双手长杆（斧/棍/枪械…）。</summary>
        TwoHanded = 2,
    }
}
