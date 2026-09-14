using UnityEngine;
using UnityEngine.Serialization;
using XeptGame.Core;

namespace XeptGame.Items
{
    /// <summary>
    /// **生成态**世界表现配置（由 <see cref="WorldViewFacet"/> 持有；W10）：
    /// <see cref="viewPrefab"/> = 世界生成器把该物品作为**静态资源**放进世界时用的模板
    /// （无刚体、无物理碰撞体，只有 `Interactable` 层上的探测碰撞体，可被 `WorldItem` 交互）。
    /// 为空 → 消费方退化为占位方块 + 告警（I6 定义缺失可逆）。
    /// 模板要求：含 `WorldItem`（世界载体契约）+ 视觉 + **可检测碰撞体**，并在 `WorldItem.view` 里连好视图根
    /// （WorldItem_Design.md §7 W8）。
    /// <b>不参与</b>"能否拾取 / 能否抛出"——那由结构事实与"有没有世界掉落口"决定（见 <see cref="WorldViewFacet"/>）。
    /// </summary>
    [CreateAssetMenu(
        menuName = XeptGameConsts.Editor.ItemWorldViewFacetProfileMenuName,
        fileName = XeptGameConsts.Editor.ItemWorldViewFacetProfileFileName,
        order = XeptGameConsts.Editor.ItemWorldViewFacetProfileOrder)]
    [Icon(XeptGameConsts.Editor.WorldViewFacetProfileIconPath)]
    public sealed class WorldViewFacetProfile : ScriptableObject
    {
        [Tooltip("生成态世界模板（应含 WorldItem + 视觉 + 可检测碰撞体；视图根在 WorldItem.view 上）；空 = 退化为占位方块")]
        public GameObject viewPrefab;
    }
}
