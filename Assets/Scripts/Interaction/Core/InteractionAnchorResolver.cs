using UnityEngine;

namespace XeptGame.Interaction
{
    /// <summary>
    /// 交互世界锚点解析收口（单判据，供 Prompt/Marker 等表现消费方共用）：
    /// ① 宿主（或其父链）上有 <see cref="IInteractionAnchorProvider"/> 且 AnchorPoint 非空 → 用之；
    /// ② 否则回退宿主自身 Transform（默认行为）。
    /// </summary>
    public static class InteractionAnchorResolver
    {
        public static Transform Resolve(ISelectable host)
        {
            if (host is Component hostComponent)
            {
                var provider = hostComponent.GetComponentInParent<IInteractionAnchorProvider>();
                if (provider != null && provider.AnchorPoint != null)
                {
                    return provider.AnchorPoint;
                }

                return hostComponent.transform;
            }

            return null;
        }
    }
}
