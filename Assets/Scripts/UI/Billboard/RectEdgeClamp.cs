using System;
using UnityEngine;

namespace XeptGame.UI.Billboard
{
    /// <summary>
    /// 出域放置策略：**矩形方向性保界**（默认——继承 HudBillboard 已验证行为）——
    /// 界 = 允许矩形（<see cref="HudClampArea"/>）；出界/背后沿方向射线与矩形边框求交：
    /// 斜向目标落真实出界边而非角落。不输出方向语义（Direction 由核心仍按目标方向提供）。
    /// </summary>
    [Serializable]
    public sealed class RectEdgeClamp : IHudBillboardClamp
    {
        /// <inheritdoc />
        public bool IsInside(Vector2 localPoint, HudClampArea area)
            => localPoint.x >= area.MinX && localPoint.x <= area.MaxX
            && localPoint.y >= area.MinY && localPoint.y <= area.MaxY;

        /// <inheritdoc />
        public Vector2 ClampToBoundary(Vector2 center, Vector2 dir, HudClampArea area)
        {
            if (dir.x == 0f && dir.y == 0f)
            {
                return center;
            }

            float t = float.PositiveInfinity;
            if (dir.x > 1e-6f)
            {
                t = Mathf.Min(t, (area.MaxX - center.x) / dir.x);
            }
            else if (dir.x < -1e-6f)
            {
                t = Mathf.Min(t, (area.MinX - center.x) / dir.x);
            }

            if (dir.y > 1e-6f)
            {
                t = Mathf.Min(t, (area.MaxY - center.y) / dir.y);
            }
            else if (dir.y < -1e-6f)
            {
                t = Mathf.Min(t, (area.MinY - center.y) / dir.y);
            }

            if (float.IsPositiveInfinity(t))
            {
                return center; // 退化（矩形收敛为点 / 方向无效）
            }

            return center + dir * t;
        }
    }
}
