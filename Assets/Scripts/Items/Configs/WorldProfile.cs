using UnityEngine;
using XeptGame.Core;

namespace XeptGame.Items
{

    /// <summary>
    /// 世界载体配置子对象（由 <see cref="WorldFacet"/> 持有）：可拾取载体的模板引用。
    /// 载体含 WorldItem 组件（实现 ISelectable + IInteractionAction：宿主 + Primary 拾取动作）——WorldItem 接线阶段消费；
    /// 有/无可用载体看 <see cref="worldPrefab"/> 是否为空（Object 引用真实可空，判据可靠）。
    /// </summary>
    [CreateAssetMenu(
        menuName = XeptGameConsts.Editor.ItemWorldFacetProfileMenuName,
        fileName = XeptGameConsts.Editor.ItemWorldFacetProfileFileName,
        order = XeptGameConsts.Editor.ItemWorldFacetProfileOrder)]
    public sealed class WorldProfile : ScriptableObject
    {
        [Tooltip("世界可拾取载体模板（含 WorldItem 组件 + 视觉 + Collider）；空 = 无模板可实例化")]
        public GameObject worldPrefab;
    }
}
