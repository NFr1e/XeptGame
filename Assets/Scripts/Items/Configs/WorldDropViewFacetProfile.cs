using UnityEngine;
using UnityEngine.Serialization;
using XeptGame.Core;

namespace XeptGame.Items
{
    /// <summary>
    /// **掉落态**世界表现配置（由 <see cref="WorldDropViewFacet"/> 持有；W10）：
    /// <see cref="viewPrefab"/> = 该物品被抛出到世界时用的模板——
    /// **有刚体与物理碰撞体**（在 `DynamicProp` 层，会与地面/玩家碰撞），同时可被 `WorldItem` 交互。
    /// 触发场景：收起失败落地、拾取余量落地、换包交接（都经世界记录 → 视图生成器）。
    /// 为空 → 生成器退化为占位方块 + 告警（I6 定义缺失可逆）；模板要求同 <see cref="WorldViewFacetProfile"/>。
    /// </summary>
    [CreateAssetMenu(
        menuName = XeptGameConsts.Editor.ItemWorldDropViewFacetProfileMenuName,
        fileName = XeptGameConsts.Editor.ItemWorldDropViewFacetProfileFileName,
        order = XeptGameConsts.Editor.ItemWorldDropViewFacetProfileOrder)]
    [Icon(XeptGameConsts.Editor.WorldDropViewFacetProfileIconPath)]
    public sealed class WorldDropViewFacetProfile : ScriptableObject
    {
        [Tooltip("掉落态世界模板（应含 WorldItem + 视觉 + 可检测碰撞体 + 刚体/物理碰撞体）；空 = 退化为占位方块")]
        public GameObject viewPrefab;
    }
}
