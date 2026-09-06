using UnityEngine;

namespace XeptGame.Interaction
{
    /// <summary>
    /// 通用交互世界锚点组件：作者把自定义锚点（子空物体/指定挂点）拖入 <see cref="anchor"/>——
    /// 提示/标记等表现跟随该点而非宿主根部。
    /// 未指定（anchor 为空或本组件缺失）→ 由 <see cref="InteractionAnchorResolver.Resolve"/> 回退宿主自身
    /// Transform（默认行为，零配置零迁移）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractionAnchorPoint : MonoBehaviour, IInteractionAnchorProvider
    {
        [Tooltip("自定义世界锚点（可拖入子空物体/挂点）；留空 = 回退宿主自身 Transform")]
        [SerializeField] private Transform anchor;

        /// <inheritdoc />
        public Transform AnchorPoint => anchor;
    }
}
