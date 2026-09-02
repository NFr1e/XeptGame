using UnityEngine;

namespace XeptGame.Interaction
{
    /// <summary>
    /// 探测快照（只读结构体）：一次实际判定的探测数据记录，供调试可视化（Gizmos）使用。
    /// 由 <see cref="InteractionResolver"/> 在解析时同步记录——画的是实际判定用的那次探测，
    /// 调试组件（<c>InteractionProbeDebug</c>）不重复探测，只消费快照。
    /// </summary>
    public readonly struct ProbeInfo
    {
        /// <summary>探测起点（世界空间）。</summary>
        public readonly Vector3 Origin;

        /// <summary>探测方向（世界空间，单位向量）。</summary>
        public readonly Vector3 Direction;

        /// <summary>探测球体半径（0 = 纯射线）。</summary>
        public readonly float Radius;

        /// <summary>最大探测距离（m）。</summary>
        public readonly float MaxDistance;

        /// <summary>是否命中碰撞体。</summary>
        public readonly bool Hit;

        /// <summary>命中点（世界空间；未命中为 zero）。</summary>
        public readonly Vector3 HitPoint;

        /// <summary>命中距离（m；未命中为 0）。</summary>
        public readonly float HitDistance;

        /// <summary>命中点法线（未命中为 zero）。</summary>
        public readonly Vector3 Normal;

        /// <summary>命中目标对象但选中被拒（CanSelect=false 或角度过滤失败）。</summary>
        public readonly bool Invalid;

        /// <summary>语义：已选中目标（含可交互目标）。</summary>
        public readonly bool Semantic;

        public ProbeInfo(
            Vector3 origin, Vector3 direction, float radius, float maxDistance,
            bool hit = false, Vector3 hitPoint = default, float hitDistance = 0f,
            Vector3 normal = default, bool invalid = false, bool semantic = false)
        {
            Origin = origin;
            Direction = direction;
            Radius = radius;
            MaxDistance = maxDistance;
            Hit = hit;
            HitPoint = hitPoint;
            HitDistance = hitDistance;
            Normal = normal;
            Invalid = invalid;
            Semantic = semantic;
        }

        /// <summary>未命中快照（终点 = 起点沿方向到最大距离，供调试可视化）。</summary>
        public static ProbeInfo Miss(Vector3 origin, Vector3 direction, float radius, float maxDistance)
            => new(origin, direction, radius, maxDistance);
    }
}
