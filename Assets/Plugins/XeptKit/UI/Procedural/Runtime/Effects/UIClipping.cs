using UnityEngine;
using UnityEngine.UI;

namespace XeptKit.UI.Procedural
{
    /// <summary>
    /// 素材裁剪模式。
    /// </summary>
    public enum ClipMode
    {
        /// <summary>不裁剪</summary>
        None,
        /// <summary>等比缩放至覆盖容器，溢出由 SDF 自然裁剪</summary>
        AspectFill,
        /// <summary>手动指定 UV 缩放与偏移</summary>
        Custom
    }

    /// <summary>
    /// 素材裁剪效果 — 通过 UV 变换控制素材在容器内的缩放与位置。
    /// 填充之后、形状之前执行（Priority=10）。只修改 uv0，溢出裁剪交由 SDF Shader 完成。
    /// </summary>
    [AddComponentMenu("UI/Effects/Clipping", 14)]
    public class UIClipping : BaseEffect
    {
        /// <summary>填充之后、形状之前</summary>
        public override int Priority => 10;

        public override int RequiredSegments => 1;

        public override void CleanupBeforeDestroy() { }

        #region Serialized Fields

        [SerializeField]
        private ClipMode m_ClipMode = ClipMode.None;

        [SerializeField, Range(0f, 1f)]
        private float m_AlignmentX = 0.5f;

        [SerializeField, Range(0f, 1f)]
        private float m_AlignmentY = 0.5f;

        [SerializeField]
        private Vector2 m_Scale = Vector2.one;

        [SerializeField]
        private Vector2 m_Offset = Vector2.zero;

        #endregion

        #region Public Properties

        public ClipMode clipMode
        {
            get => m_ClipMode;
            set { m_ClipMode = value; Invalidate(); }
        }

        public float alignmentX
        {
            get => m_AlignmentX;
            set { m_AlignmentX = Mathf.Clamp01(value); Invalidate(); }
        }

        public float alignmentY
        {
            get => m_AlignmentY;
            set { m_AlignmentY = Mathf.Clamp01(value); Invalidate(); }
        }

        public Vector2 scale
        {
            get => m_Scale;
            set { m_Scale = value; Invalidate(); }
        }

        public Vector2 offset
        {
            get => m_Offset;
            set { m_Offset = value; Invalidate(); }
        }

        #endregion

        #region Core Logic

        public override void ModifyMesh(VertexHelper vh, Rect bounds)
        {
            if (m_ClipMode == ClipMode.None)
                return;

            float scaleX, scaleY, offsetX, offsetY;

            if (m_ClipMode == ClipMode.AspectFill)
            {
                if (!ResolveAspectFillParams(bounds, out scaleX, out scaleY, out offsetX, out offsetY))
                    return;
            }
            else // Custom
            {
                scaleX = m_Scale.x;
                scaleY = m_Scale.y;
                offsetX = m_Offset.x;
                offsetY = m_Offset.y;
            }

            // 扫描当前 UV 范围
            Vector2 uvMin = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 uvMax = new Vector2(float.MinValue, float.MinValue);
            UIVertex vertex = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                if (vertex.uv0.x < uvMin.x) uvMin.x = vertex.uv0.x;
                if (vertex.uv0.y < uvMin.y) uvMin.y = vertex.uv0.y;
                if (vertex.uv0.x > uvMax.x) uvMax.x = vertex.uv0.x;
                if (vertex.uv0.y > uvMax.y) uvMax.y = vertex.uv0.y;
            }

            float uvRangeX = uvMax.x - uvMin.x;
            float uvRangeY = uvMax.y - uvMin.y;
            if (uvRangeX <= 0f || uvRangeY <= 0f) return;

            // 逐顶点重映射 UV
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);

                float lx = Mathf.Clamp01(vertex.uv1.z);
                float ly = Mathf.Clamp01(vertex.uv1.w);

                float remapX = offsetX + lx * scaleX;
                float remapY = offsetY + ly * scaleY;

                vertex.uv0.x = uvMin.x + uvRangeX * remapX;
                vertex.uv0.y = uvMin.y + uvRangeY * remapY;

                vh.SetUIVertex(vertex, i);
            }
        }

        /// <summary>
        /// 根据素材与容器纵横比计算 AspectFill 的 UV 裁剪参数。
        /// 返回 false 表示无法计算（Color 模式，无源尺寸）。
        /// </summary>
        private bool ResolveAspectFillParams(Rect bounds, out float scaleX, out float scaleY,
            out float offsetX, out float offsetY)
        {
            scaleX = scaleY = 1f;
            offsetX = offsetY = 0f;

            var img = GetProceduralImage();
            if (img == null) return false;

            float srcW, srcH;

            if (img.sourceType == ProceduralImage.SourceType.Sprite && img.sprite != null)
            {
                srcW = img.sprite.rect.width;
                srcH = img.sprite.rect.height;
            }
            else if (img.sourceType == ProceduralImage.SourceType.Texture && img.texture != null)
            {
                srcW = img.texture.width * img.uvRect.width;
                srcH = img.texture.height * img.uvRect.height;
            }
            else
            {
                return false;
            }

            if (srcW <= 0f || srcH <= 0f || bounds.width <= 0f || bounds.height <= 0f)
                return false;

            float srcAR = srcW / srcH;
            float conAR = bounds.width / bounds.height;

            if (srcAR > conAR) // 横向裁剪
            {
                scaleX = conAR / srcAR;
                scaleY = 1f;
                offsetX = (1f - scaleX) * m_AlignmentX;
                offsetY = 0f;
            }
            else // 纵向裁剪
            {
                scaleX = 1f;
                scaleY = srcAR / conAR;
                offsetX = 0f;
                offsetY = (1f - scaleY) * m_AlignmentY;
            }

            return true;
        }

        #endregion
    }
}
