using UnityEngine;
using UnityEngine.UI;

namespace XeptKit.UI.Procedural
{
    /// <summary>
    /// 描边位置。
    /// </summary>
    public enum StrokePosition
    {
        Inside,
        Center,
        Outside
    }

    /// <summary>
    /// 程序化 SDF 描边效果。
    /// 在形状阶段之后执行（Priority=60），通过修改 Shader 参数实现。
    /// 描边跟随容器几何形状（矩形/圆角），三种位置：Inside / Center / Outside。
    /// </summary>
    [AddComponentMenu("UI/Effects/Stroke", 24)]
    public class UIStroke : BaseEffect
    {
        public override int Priority => 60;

        public override int RequiredSegments => 1;

        public override bool RequiresCustomShader => true;

        public override float FalloffDistance
        {
            get
            {
                if (!isActiveAndEnabled || m_Width <= 0f) return 0f;

                switch (m_Position)
                {
                    case StrokePosition.Outside: return m_Width + m_AntiAliasing;
                    case StrokePosition.Center: return m_Width * 0.5f + m_AntiAliasing;
                    default: return m_AntiAliasing;
                }
            }
        }

        public override float AntiAliasingDistance => m_AntiAliasing;

        #region Serialized Fields

        [SerializeField]
        private StrokePosition m_Position = StrokePosition.Outside;

        [SerializeField, Min(0f)]
        private float m_Width = 2f;

        [SerializeField]
        private Color m_Color = Color.black;

        [SerializeField]
        private bool m_UseGraphicAlpha = true;

        [SerializeField, Range(0.1f, 4f)]
        private float m_AntiAliasing = 0.5f;

        #endregion

        #region Public Properties

        public StrokePosition position
        {
            get => m_Position;
            set { m_Position = value; Invalidate(); }
        }

        public float width
        {
            get => m_Width;
            set { m_Width = Mathf.Max(0f, value); Invalidate(); }
        }

        public Color color
        {
            get => m_Color;
            set { m_Color = value; Invalidate(); }
        }

        public bool useGraphicAlpha
        {
            get => m_UseGraphicAlpha;
            set { m_UseGraphicAlpha = value; Invalidate(); }
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
            ApplyStrokeParams();
        }

        private void ApplyStrokeParams()
        {
            if (m_Width <= 0f) return;

            var img = GetProceduralImage();
            if (img == null) return;

            var mat = img.material;
            // 材质守卫：仅自定义 SDF Shader（或用户提供的同名 Shader 材质）设置参数，防污染共享/默认材质
            if (mat == null || mat.shader == null || mat.shader.name != ProceduralImage.ProceduralShaderName) return;

            mat.SetFloat("_StrokeWidth", m_Width);
            mat.SetColor("_StrokeColor", m_Color);
            mat.SetFloat("_StrokePosition", (float)m_Position);
            mat.SetFloat("_StrokeUseGraphicAlpha", m_UseGraphicAlpha ? 1f : 0f);
            mat.SetFloat("_StrokeCanvasGroupAlpha", img.canvasRenderer.GetAlpha());
        }

        #endregion

        #region Cleanup

        internal void ResetStrokeParams()
        {
            var img = GetProceduralImage();
            if (img == null) return;

            var mat = img.material;
            if (mat == null || mat.shader == null || mat.shader.name != ProceduralImage.ProceduralShaderName) return;

            mat.SetFloat("_StrokeWidth", 0f);
        }

        /// <summary>Editor 清理：重置材质 _StrokeWidth 防止残留</summary>
        public override void CleanupBeforeDestroy()
        {
            ResetStrokeParams();
        }

        #endregion

        #region Unity Lifecycle

        protected override void OnEnable()
        {
            base.OnEnable();
            ApplyStrokeParams();
        }

        protected override void OnDisable()
        {
            ResetStrokeParams();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            ResetStrokeParams();
            base.OnDestroy();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (isActiveAndEnabled) ApplyStrokeParams();
        }
#endif

        #endregion
    }
}
