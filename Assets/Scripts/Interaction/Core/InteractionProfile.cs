using UnityEngine;
using XeptGame.Core;

namespace XeptGame.Interaction
{
    /// <summary>
    /// 交互探测参数（ScriptableObject 资产），与 Motion/Look/View Profile 惯例对称：
    /// 未配置资产时 <see cref="Default"/> 提供运行时默认值，开箱即用。
    /// 热调参：探测每帧直读 Profile 字段（SO 序列化字段改动即生效）。
    /// </summary>
    [CreateAssetMenu(
        menuName = XeptGameConsts.Editor.InteractionProfileMenuName,
        fileName = XeptGameConsts.Editor.InteractionProfileFileName,
        order = XeptGameConsts.Editor.InteractionProfileOrder)]
    public sealed class InteractionProfile : ScriptableObject
    {
        [Header("探测")]
        [Tooltip("最大交互距离（m）")]
        public float maxRange = 3f;

        [Tooltip("探测球体半径（m）；0 = 纯射线（像素级瞄准，较苛刻）")]
        [Range(0f, 1f)]
        public float probeRadius = 0.1f;

        [Tooltip("交互探测碰撞层。小场景可默认全部；大场景建议配置专用 Interactable 层，减少查询命中噪声（避免打到地形/世界几何）")]
        public LayerMask layers = ~0;

        [Tooltip("目标方向与相机朝向的最大夹角（度）；0 = 不限制")]
        [Range(0f, 90f)]
        public float maxAngle = 0f;

        /// <summary>运行时默认配置（未挂资产时使用）。非资产实例，不可在资源库中编辑。</summary>
        public static InteractionProfile Default
        {
            get
            {
                var profile = CreateInstance<InteractionProfile>();
                profile.name = "InteractionProfile_Default";
                return profile;
            }
        }
    }
}
