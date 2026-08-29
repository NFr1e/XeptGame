using UnityEngine;
using UnityEngine.UI;

namespace XeptKit.UI.Procedural
{
    /// <summary>
    /// G2 曲率连续圆角效果 — 基于超椭圆 (Superellipse) SDF。
    ///
    /// 与 UIRoundedCorners 不同，本效果使用 Ln 范数替代 L2 范数（圆弧），
    /// 使曲率在直边交界处平滑过渡（κ: 0 → 1/R），消除标准圆角的"折点感"。
    ///
    /// smoothness = 0 时退化为标准圆弧（等效 UIRoundedCorners），
    /// smoothness = 0.6 时近似 Apple 连续圆角风格。
    /// </summary>
    [AddComponentMenu("UI/Effects/Squircle Corners", 22)]
    public class UISquircleCorners : BaseEffect
    {
        public override int Priority => 50;

        public override int RequiredSegments => 1;

        public override bool RequiresCustomShader => true;

        public override EffectGroup ExclusiveGroup => EffectGroup.Shape;

        public override void CleanupBeforeDestroy() { }

        public override float FalloffDistance => m_AntiAliasing;
        public override float AntiAliasingDistance => m_AntiAliasing;

        #region Serialized Fields

        [SerializeField]
        private RadiusMode m_RadiusMode = RadiusMode.World;

        [SerializeField]
        private bool m_Uniform = true;

        [SerializeField, Min(0f)]
        private float m_Radius = 16f;

        [SerializeField]
        private Vector4 m_CornerRadii = new Vector4(16f, 16f, 16f, 16f);

        [SerializeField, Range(0.1f, 4f)]
        private float m_AntiAliasing = 0.5f;

        [SerializeField, Range(0f, 1f)]
        private float m_Smoothness = 0.6f;

        #endregion

        #region Public Properties

        public RadiusMode radiusMode
        {
            get => m_RadiusMode;
            set { m_RadiusMode = value; Invalidate(); }
        }

        public bool uniform
        {
            get => m_Uniform;
            set { m_Uniform = value; Invalidate(); }
        }

        public float radius
        {
            get => m_Radius;
            set { m_Radius = Mathf.Max(0f, value); Invalidate(); }
        }

        /// <summary>x=TL, y=TR, z=BL, w=BR</summary>
        public Vector4 cornerRadii
        {
            get => m_CornerRadii;
            set { m_CornerRadii = value; Invalidate(); }
        }

        public float antiAliasing
        {
            get => m_AntiAliasing;
            set { m_AntiAliasing = Mathf.Clamp(value, 0.1f, 4f); Invalidate(); }
        }

        /// <summary>
        /// 超椭圆指数平滑度。
        /// 0 = 标准圆弧 (G1)，0.6 ≈ Apple squircle (G2 近似)，1 = 最强曲线过渡。
        /// </summary>
        public float smoothness
        {
            get => m_Smoothness;
            set { m_Smoothness = Mathf.Clamp01(value); Invalidate(); }
        }

        #endregion

        #region Core Logic

        public override void ModifyMesh(VertexHelper vh, Rect bounds)
        {
            Vector4 r = m_Uniform
                ? new Vector4(m_Radius, m_Radius, m_Radius, m_Radius)
                : m_CornerRadii;

            // 百分比模式：转换为世界单位
            if (m_RadiusMode == RadiusMode.Percentage)
            {
                float halfMinDim = Mathf.Min(bounds.width, bounds.height) * 0.5f;
                r.x = halfMinDim * Mathf.Clamp(r.x, 0f, 100f) / 100f;
                r.y = halfMinDim * Mathf.Clamp(r.y, 0f, 100f) / 100f;
                r.z = halfMinDim * Mathf.Clamp(r.z, 0f, 100f) / 100f;
                r.w = halfMinDim * Mathf.Clamp(r.w, 0f, 100f) / 100f;
            }

            // 限制半径不超过矩形一半
            float maxR = Mathf.Min(bounds.width * 0.5f, bounds.height * 0.5f);
            r.x = Mathf.Clamp(r.x, 0f, maxR);
            r.y = Mathf.Clamp(r.y, 0f, maxR);
            r.z = Mathf.Clamp(r.z, 0f, maxR);
            r.w = Mathf.Clamp(r.w, 0f, maxR);

            // 编码到 uv2（半径）+ uv3.z（smoothness），交由 Shader 执行 SDF 裁剪
            UIVertex vert = new UIVertex();
            Vector4 encodedUV2 = r;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vert, i);
                vert.uv2 = encodedUV2;
                vert.uv3.z = m_Smoothness;
                vh.SetUIVertex(vert, i);
            }
        }

        #endregion
    }
}
