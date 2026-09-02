using UnityEngine;

namespace XeptGame.Interaction
{
    /// <summary>
    /// 交互探测射线（世界空间，只读结构体）：探测的起点与方向。
    /// 由 <see cref="IInteractionProbeSource"/> 提供，是探测源抽象的载体——
    /// 玩家（相机）、AI（朝向）、脚本（指定位置）各自决定射线来源，解析逻辑共用。
    /// </summary>
    public readonly struct InteractionProbeRay
    {
        /// <summary>探测起点（世界空间）。</summary>
        public readonly Vector3 Origin;

        /// <summary>探测方向（世界空间，应为单位向量；零向量视为无效）。</summary>
        public readonly Vector3 Direction;

        public InteractionProbeRay(Vector3 origin, Vector3 direction)
        {
            Origin = origin;
            Direction = direction;
        }

        /// <summary>方向是否有效（零向量探测无意义，解析器将直接返回空结果）。</summary>
        public bool IsValid => Direction.sqrMagnitude > 1e-6f;
    }
}
