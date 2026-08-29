using UnityEngine;

namespace XeptKit.UI.Procedural
{
    /// <summary>
    /// 渐变计算的工具函数。供 UIGradient 使用。
    /// </summary>
    internal static class VertexHelperExtensions
    {
        /// <summary>
        /// 线性渐变 t 值：将归一化 UV 沿 rotation 方向投影到 [0, 1]。
        /// </summary>
        public static float LinearGradientT(Vector2 uv, float rotationDeg)
        {
            float rad = rotationDeg * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            float t = Vector2.Dot(uv - new Vector2(0.5f, 0.5f), dir) + 0.5f;
            return Mathf.Clamp01(t);
        }

        /// <summary>
        /// 径向渐变 t 值：从 center 到当前 uv 的距离，归一化到 [0, 1]。
        /// </summary>
        public static float RadialGradientT(Vector2 uv, Vector2 center)
        {
            float dist = Vector2.Distance(uv, center);
            float maxDist = Mathf.Max(
                Vector2.Distance(center, new Vector2(0f, 0f)),
                Vector2.Distance(center, new Vector2(1f, 0f)),
                Vector2.Distance(center, new Vector2(0f, 1f)),
                Vector2.Distance(center, new Vector2(1f, 1f))
            );
            if (maxDist < 0.0001f) return 0f;
            return Mathf.Clamp01(dist / maxDist);
        }

        /// <summary>
        /// 角度渐变 t 值：从 center 出发的极角，归一化到 [0, 1]。
        /// </summary>
        public static float AngleGradientT(Vector2 uv, Vector2 center)
        {
            Vector2 delta = uv - center;
            float angle = Mathf.Atan2(delta.y, delta.x);
            float t = (angle + Mathf.PI) / (2f * Mathf.PI);
            return t;
        }

        /// <summary>
        /// 双线性插值四个角颜色。UV 为归一化坐标 (0..1, 0..1)。
        /// </summary>
        public static Color BilinearInterpolate(
            Vector2 uv,
            Color bottomLeft, Color bottomRight,
            Color topLeft, Color topRight)
        {
            Color bottom = Color.Lerp(bottomLeft, bottomRight, uv.x);
            Color top = Color.Lerp(topLeft, topRight, uv.x);
            return Color.Lerp(bottom, top, uv.y);
        }
    }
}
