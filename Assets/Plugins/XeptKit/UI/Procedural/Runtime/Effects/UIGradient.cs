using UnityEngine;
using UnityEngine.UI;

namespace XeptKit.UI.Procedural
{
    /// <summary>
    /// 渐变类型。
    /// </summary>
    public enum GradientType
    {
        /// <summary>双向线性渐变，支持旋转</summary>
        Linear,
        /// <summary>径向渐变（中心向外）</summary>
        Radial,
        /// <summary>角度渐变（绕中心旋转）</summary>
        Angle,
        /// <summary>四角独立颜色，双线性插值</summary>
        FourCorner
    }

    /// <summary>
    /// 程序化渐变效果。在填充阶段（Priority=0）修改所有顶点的颜色。
    /// 支持 Linear / Radial / Angle / FourCorner 四种模式。
    /// Linear 和 FourCorner 模式 1 段网格即可精确表达；
    /// Radial 和 Angle 模式需要 16 段网格细分以逼近曲线。
    /// </summary>
    [AddComponentMenu("UI/Effects/Gradient", 20)]
    public class UIGradient : BaseEffect
    {
        /// <summary>填充阶段最早执行</summary>
        public override int Priority => 0;

        /// <summary>
        /// 所需网格细分度。Linear/FourCorner 仅需 1 段，
        /// Radial/Angle 需 16 段以通过顶点色逼近渐变曲线。
        /// </summary>
        public override int RequiredSegments
        {
            get
            {
                switch (m_GradientType)
                {
                    case GradientType.Linear:
                    case GradientType.FourCorner:
                        return 1;
                    default:
                        return 16;
                }
            }
        }

        public override void CleanupBeforeDestroy() { }

        #region Serialized Fields

        [SerializeField]
        private GradientType m_GradientType = GradientType.Linear;

        [SerializeField]
        private Color m_Color1 = Color.white;

        [SerializeField]
        private Color m_Color2 = Color.gray;

        [SerializeField]
        private Color m_CornerTopLeft = Color.white;

        [SerializeField]
        private Color m_CornerTopRight = Color.red;

        [SerializeField]
        private Color m_CornerBottomLeft = Color.blue;

        [SerializeField]
        private Color m_CornerBottomRight = Color.green;

        [SerializeField, Range(0f, 360f)]
        private float m_Rotation = 0f;

        [SerializeField]
        private Vector2 m_Center = new Vector2(0.5f, 0.5f);

        #endregion

        #region Public Properties

        public GradientType gradientType
        {
            get => m_GradientType;
            set { m_GradientType = value; Invalidate(); }
        }

        public Color color1
        {
            get => m_Color1;
            set { m_Color1 = value; Invalidate(); }
        }

        public Color color2
        {
            get => m_Color2;
            set { m_Color2 = value; Invalidate(); }
        }

        public Color cornerTopLeft
        {
            get => m_CornerTopLeft;
            set { m_CornerTopLeft = value; Invalidate(); }
        }

        public Color cornerTopRight
        {
            get => m_CornerTopRight;
            set { m_CornerTopRight = value; Invalidate(); }
        }

        public Color cornerBottomLeft
        {
            get => m_CornerBottomLeft;
            set { m_CornerBottomLeft = value; Invalidate(); }
        }

        public Color cornerBottomRight
        {
            get => m_CornerBottomRight;
            set { m_CornerBottomRight = value; Invalidate(); }
        }

        public float rotation
        {
            get => m_Rotation;
            set { m_Rotation = Mathf.Clamp(value, 0f, 360f); Invalidate(); }
        }

        public Vector2 center
        {
            get => m_Center;
            set { m_Center = value; Invalidate(); }
        }

        #endregion

        #region Core Logic

        public override void ModifyMesh(VertexHelper vh, Rect bounds)
        {
            UIVertex vertex = new UIVertex();

            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);

                // 计算顶点在 bounds 中的归一化位置 (0..1)
                Vector2 uv = new Vector2(
                    Mathf.InverseLerp(bounds.xMin, bounds.xMax, vertex.position.x),
                    Mathf.InverseLerp(bounds.yMin, bounds.yMax, vertex.position.y));

                vertex.color = CalculateColor(uv);

                vh.SetUIVertex(vertex, i);
            }
        }

        private Color CalculateColor(Vector2 uv)
        {
            switch (m_GradientType)
            {
                case GradientType.Linear:
                    float t = VertexHelperExtensions.LinearGradientT(uv, m_Rotation);
                    return Color.Lerp(m_Color1, m_Color2, t);

                case GradientType.Radial:
                    float r = VertexHelperExtensions.RadialGradientT(uv, m_Center);
                    return Color.Lerp(m_Color1, m_Color2, r);

                case GradientType.Angle:
                    float a = VertexHelperExtensions.AngleGradientT(uv, m_Center);
                    return Color.Lerp(m_Color1, m_Color2, a);

                case GradientType.FourCorner:
                    return VertexHelperExtensions.BilinearInterpolate(
                        uv,
                        m_CornerBottomLeft, m_CornerBottomRight,
                        m_CornerTopLeft, m_CornerTopRight);

                default:
                    return m_Color1;
            }
        }

        #endregion
    }
}
