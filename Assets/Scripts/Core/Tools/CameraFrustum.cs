using UnityEngine;

namespace XeptGame.Core
{
    /// <summary>点相对相机视锥的可见性分类（前/后 + 视口内/外）。</summary>
    public enum FrustumVisibility
    {
        /// <summary>在相机背后（沿 forward 的 z ≤ 0）。</summary>
        Behind,

        /// <summary>前方且投影落在视口内（含 margin 裕量）。</summary>
        OnScreen,

        /// <summary>前方但投影出视口（视锥侧/上下面外）。</summary>
        OffScreen,
    }

    /// <summary>
    /// 相机视锥判定（纯静态工具，无状态/无场景依赖）：任何消费方（HudBillboard 钳制、任务箭头、AI 可见性、
    /// 流式加载）传 相机 + 世界点 即得统一判定——**单一判据，防各处几何漂移**。与 UI/Canvas/分辨率无关。
    /// 语义：
    /// <list type="bullet">
    /// <item>前/后：世界点在相机本地系沿 forward 的 z（<see cref="Transform.InverseTransformPoint"/>），z ≤ 0 = 背后；</item>
    /// <item>视口：前方点经投影矩阵得 NDC（[-1,1]²）判 On/OffScreen；<paramref name="margin"/> 为 NDC 裕量
    /// （像素级边缘外扩需按需换算，默认 0 = 恰好视口边算 OnScreen）。</item>
    /// </list>
    /// </summary>
    public static class CameraFrustum
    {
        /// <summary>分类：Behind / OnScreen / OffScreen。相机无效（null/未就绪）按 Behind 处理（不可见，调用方空安全）。</summary>
        public static FrustumVisibility Classify(Camera camera, Vector3 worldPoint, float margin = 0f)
        {
            if (camera == null || !camera.isActiveAndEnabled)
            {
                return FrustumVisibility.Behind;
            }

            if (!IsInFront(camera, worldPoint))
            {
                return FrustumVisibility.Behind;
            }

            // 前方点：投影 → NDC（clip.w 携带前/后符号，x/y 的 [-1,1] 即视口内/外）
            var clip = camera.projectionMatrix * camera.worldToCameraMatrix
                     * new Vector4(worldPoint.x, worldPoint.y, worldPoint.z, 1f);
            if (Mathf.Abs(clip.w) < 1e-6f)
            {
                return FrustumVisibility.Behind; // 退化（近平面上）——保守按不可见
            }

            float ndcX = clip.x / clip.w;
            float ndcY = clip.y / clip.w;
            float m = Mathf.Max(0f, margin);

            return ndcX >= -1f - m && ndcX <= 1f + m && ndcY >= -1f - m && ndcY <= 1f + m
                ? FrustumVisibility.OnScreen
                : FrustumVisibility.OffScreen;
        }

        /// <summary>是否前方且投影落在视口内（<see cref="FrustumVisibility.OnScreen"/>）。</summary>
        public static bool IsOnScreen(Camera camera, Vector3 worldPoint, float margin = 0f)
            => Classify(camera, worldPoint, margin) == FrustumVisibility.OnScreen;

        /// <summary>是否在相机前方（沿 forward 的 z &gt; 0；不含横向）。背后判定的唯一判据来源。</summary>
        public static bool IsInFront(Camera camera, Vector3 worldPoint)
            => camera != null && camera.transform.InverseTransformPoint(worldPoint).z > 0f;
    }
}
