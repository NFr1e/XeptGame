using UnityEngine;
using UnityEngine.UI;

namespace XeptKit.UI.Procedural
{
    /// <summary>
    /// 圆角半径模式。
    /// </summary>
    public enum RadiusMode
    {
        /// <summary>绝对单位（Canvas 像素 / 世界单位）</summary>
        World,
        /// <summary>相对 min(width, height)/2 的百分比（0–100）。50% = 完美圆角</summary>
        Percentage
    }

    /// <summary>
    /// 程序化圆角效果 — GPU SDF 实现。
    /// 不修改顶点位置，将四角半径编码到 uv2 通道，
    /// 由 XeptKit/UI/ProceduralImage Shader 在像素着色器中通过 SDF 裁剪。
    /// </summary>
    [AddComponentMenu("UI/Effects/Rounded Corners", 21)]
    public class UIRoundedCorners : BaseEffect
    {
        public override int Priority => 50;

        /// <summary>GPU SDF 处理形状，CPU 无需细分网格</summary>
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

            // 编码到 uv2 通道，交由 Shader 执行 SDF 裁剪
            UIVertex vert = new UIVertex();
            Vector4 encodedUV2 = r;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vert, i);
                vert.uv2 = encodedUV2;
                vh.SetUIVertex(vert, i);
            }
        }

        #endregion
    }
}
