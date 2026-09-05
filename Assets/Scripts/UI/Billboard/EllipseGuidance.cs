using System;
using UnityEngine;

namespace XeptGame.UI.Billboard
{
    /// <summary>
    /// 出域放置策略：**内切椭圆双态指引**（任务指引成熟做法）——
    /// 界 = <see cref="HudClampArea"/> 的内切椭圆（中心 = 矩形中心，半轴 = 宽/2、高/2，四边中点相切）：
    /// <list type="bullet">
    /// <item><see cref="IsInside"/>：候选点在椭圆内 → 普通标志态（真实投影，marker）；</item>
    /// <item>出域（椭圆外/出视口/背后）：<see cref="ClampToBoundary"/> 沿方向射线与椭圆求交——箭头停在
    /// 椭圆线上连续滑动（无矩形角落的方向歧义/位置跳角）；核心输出 <c>Direction</c> 供业务旋转箭头。</item>
    /// </list>
    /// 椭圆内切于"扣除元素半尺寸的允许矩形"（HudClampArea 由核心按此解析）——目标整体仍留在容器内。
    /// </summary>
    [Serializable]
    public sealed class EllipseGuidance : IHudBillboardClamp
    {
        /// <inheritdoc />
        public bool IsInside(Vector2 localPoint, HudClampArea area)
        {
            if (!TryGetRadii(area, out float rx, out float ry))
            {
                return true; // 边界退化（收敛为点/线）→ 视为域内（落中心）
            }

            var center = area.Center;
            float nx = (localPoint.x - center.x) / rx;
            float ny = (localPoint.y - center.y) / ry;
            return nx * nx + ny * ny <= 1f;
        }

        /// <inheritdoc />
        public Vector2 ClampToBoundary(Vector2 center, Vector2 dir, HudClampArea area)
        {
            if (dir.x == 0f && dir.y == 0f)
            {
                return center;
            }

            if (!TryGetRadii(area, out float rx, out float ry))
            {
                return center; // 退化
            }

            // 椭圆射线求交（中心在椭圆内）：p = center + t·dir 满足 ((t·dx)/rx)² + ((t·dy)/ry)² = 1
            // → t = 1 / √((dx/rx)² + (dy/ry)²)（dir 任意长度均适用）
            float dxr = dir.x / rx;
            float dyr = dir.y / ry;
            float t = 1f / Mathf.Sqrt(dxr * dxr + dyr * dyr);
            return center + dir * t;
        }

        private static bool TryGetRadii(HudClampArea area, out float rx, out float ry)
        {
            rx = (area.MaxX - area.MinX) * 0.5f;
            ry = (area.MaxY - area.MinY) * 0.5f;
            return rx > 1e-6f && ry > 1e-6f;
        }
    }
}
