using System;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;
using XeptKit.Core;

namespace XeptKit.UI.Procedural
{
    /// <summary>
    /// 程序化 UI 图像组件。
    /// 统一替代 UnityEngine.UI.Image 和 UnityEngine.UI.RawImage，
    /// 支持三种数据源模式：纯色(Color)、精灵(Sprite)、纹理(Texture)。
    /// 通过 EffectPipeline 叠加圆角、渐变、描边等效果。
    ///
    /// 当管线中存在需要自定义 Shader 的效果（如圆角）时，
    /// 自动切换到 XeptKit/UI/ProceduralImage Shader（GPU SDF 渲染）。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    [RequireComponent(typeof(EffectPipeline))]
    [AddComponentMenu("UI/Procedural Image", 13)]
    public class ProceduralImage : MaskableGraphic
    {
        /// <summary>自定义 Shader 名称（与 UIStroke 等效果共享，须与 Shaders/ProceduralImage.shader 一致）</summary>
        internal const string ProceduralShaderName = "XeptKit/UI/ProceduralImage";

        #region Static

        /// <summary>自定义 Shader 缓存，避免 Shader.Find 每帧全局查找</summary>
        private static Shader s_ProceduralShader;

        /// <summary>Shader 缺失警告已上报标志（每会话一次，域重载重置）</summary>
        private static bool s_ShaderMissingWarned;

        private static Shader ProceduralShader
        {
            get
            {
                if (s_ProceduralShader == null)
                    s_ProceduralShader = Shader.Find(ProceduralShaderName);
                return s_ProceduralShader;
            }
        }

        /// <summary>
        /// Shader 未找到（未随构建包含等）时一次性警告——效果静默回退默认材质，
        /// 提示帮助排查，避免无提示的视觉失效。
        /// </summary>
        private static void WarnShaderMissing()
        {
            if (s_ShaderMissingWarned) return;
            s_ShaderMissingWarned = true;
            Log.Warning(
                $"[ProceduralImage] Shader '{ProceduralShaderName}' 未找到（未随构建包含？）。" +
                "圆角/描边等 SDF 效果将失效并回退默认材质。");
        }

        #endregion

        #region Enums

        public enum SourceType
        {
            Color,
            Sprite,
            Texture
        }

        #endregion

        #region Serialized Fields

        [SerializeField]
        private SourceType m_SourceType = SourceType.Color;

        [SerializeField]
        private Sprite m_Sprite;

        [SerializeField]
        private Texture m_Texture;

        [SerializeField]
        private Rect m_UVRect = new Rect(0f, 0f, 1f, 1f);

        [SerializeField]
        private bool m_PreserveAspect = false;

        #endregion

        #region Public Properties

        public SourceType sourceType
        {
            get => m_SourceType;
            set
            {
                if (m_SourceType == value) return;
                m_SourceType = value;
                UpdateMaterialTexture();
                SetAllDirty();
            }
        }

        public Sprite sprite
        {
            get => m_Sprite;
            set
            {
                if (m_Sprite == value) return;
                m_Sprite = value;
                if (m_SourceType == SourceType.Sprite)
                {
                    UpdateMaterialTexture();
                    SetAllDirty();
                }
            }
        }

        public Texture texture
        {
            get => m_Texture;
            set
            {
                if (m_Texture == value) return;
                m_Texture = value;
                if (m_SourceType == SourceType.Texture)
                {
                    UpdateMaterialTexture();
                    SetAllDirty();
                }
            }
        }

        public Rect uvRect
        {
            get => m_UVRect;
            set
            {
                if (m_UVRect == value) return;
                m_UVRect = value;
                if (m_SourceType == SourceType.Texture)
                    SetVerticesDirty();
            }
        }

        public bool preserveAspect
        {
            get => m_PreserveAspect;
            set
            {
                if (m_PreserveAspect == value) return;
                m_PreserveAspect = value;
                SetVerticesDirty();
            }
        }

        #endregion

        #region Material Management

        [NonSerialized]
        private Material m_CustomMaterialInstance;

        /// <summary>EffectPipeline 引用缓存，避免 OnPopulateMesh 中重复 GetComponent</summary>
        [NonSerialized]
        private EffectPipeline m_CachedPipeline;

        private EffectPipeline pipeline
        {
            get
            {
                if (m_CachedPipeline == null)
                    TryGetComponent(out m_CachedPipeline);
                return m_CachedPipeline;
            }
        }

        private Material customMaterial
        {
            get
            {
                Shader shader = ProceduralShader;
                if (shader == null)
                {
                    WarnShaderMissing();
                    return defaultMaterial;
                }

                if (m_CustomMaterialInstance == null || m_CustomMaterialInstance.shader != shader)
                {
                    DestroyCustomMaterial();

                    m_CustomMaterialInstance = new Material(shader)
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }

                return m_CustomMaterialInstance;
            }
        }

        /// <summary>
        /// 动态路由 mainTexture：根据 sourceType 返回正确的纹理。
        /// </summary>
        public override Texture mainTexture
        {
            get
            {
                switch (m_SourceType)
                {
                    case SourceType.Sprite:
                        if (m_Sprite != null) return m_Sprite.texture;
                        break;
                    case SourceType.Texture:
                        if (m_Texture != null) return m_Texture;
                        break;
                }
                if (material != null && material.mainTexture != null)
                    return material.mainTexture;
                return s_WhiteTexture;
            }
        }

        /// <summary>
        /// 材质选择：
        /// - 用户指定材质 → 使用用户材质
        /// - 需要自定义 Shader（圆角/描边） → XeptKit/UI/ProceduralImage
        /// - 默认 → UI/Default
        /// </summary>
        public override Material material
        {
            get
            {
                if (m_Material != null)
                    return m_Material;

                var p = pipeline;
                if (p != null && p.RequiresCustomShader)
                    return customMaterial;

                return defaultMaterial;
            }
            set => base.material = value;
        }

        protected override void UpdateMaterial()
        {
            base.UpdateMaterial();

            if (m_SourceType == SourceType.Sprite && m_Sprite != null)
                canvasRenderer.SetAlphaTexture(m_Sprite.associatedAlphaSplitTexture);
            else
                canvasRenderer.SetAlphaTexture(null);

            UpdateClipSoftness();
        }

        [NonSerialized]
        private RectMask2D m_CachedMask;

        /// <summary>
        /// 向材质写入 RectMask2D 的 softness（软裁剪）。
        /// 仅自定义 SDF 材质生效——defaultMaterial 为共享材质，对其写入属性会污染其他使用者。
        /// </summary>
        private void UpdateClipSoftness()
        {
            Material mat = material;
            if (mat == null || mat.shader == null || mat.shader.name != ProceduralShaderName)
                return;

            if (m_CachedMask == null)
                m_CachedMask = GetComponentInParent<RectMask2D>();

            Vector2 softness = m_CachedMask != null ? m_CachedMask.softness : Vector2.zero;
            mat.SetVector("_ClipSoftness", softness);
        }

        /// <summary>
        /// 当父级 RectMask2D 的裁剪区域或 Softness 变化时，
        /// Unity 会通过 IClippable.SetClipRect 通知子元素。
        /// 重写它以标记 material dirty，确保 _ClipSoftness 在下一帧同步。
        /// </summary>
        public override void SetClipRect(Rect value, bool validRect)
        {
            base.SetClipRect(value, validRect);
            SetMaterialDirty();
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            m_CachedMask = null; // 父级变化时失效缓存
            m_CachedCanvas = null;
            SetupCanvasShaderChannels();
        }

        private void UpdateMaterialTexture()
        {
            SetMaterialDirty();
        }

        #endregion

        #region Canvas Shader Channel Setup

        /// <summary>
        /// 确保 Canvas 启用了额外的 UV 通道（uv1/uv2/uv3），
        /// 用于向自定义 Shader 传递圆角半径、描边宽度等参数。
        /// </summary>
        private void SetupCanvasShaderChannels()
        {
            Canvas c = GetComponentInParent<Canvas>();
            if (c != null)
            {
                c.additionalShaderChannels |=
                    AdditionalCanvasShaderChannels.TexCoord1 |
                    AdditionalCanvasShaderChannels.TexCoord2 |
                    AdditionalCanvasShaderChannels.TexCoord3;
            }
        }

        #endregion

        #region Native Size

        public override void SetNativeSize()
        {
            switch (m_SourceType)
            {
                case SourceType.Sprite:
                    if (m_Sprite != null)
                    {
                        float w = m_Sprite.rect.width;
                        float h = m_Sprite.rect.height;
                        rectTransform.anchorMax = rectTransform.anchorMin;
                        rectTransform.sizeDelta = new Vector2(w, h);
                        SetAllDirty();
                    }
                    break;
                case SourceType.Texture:
                    if (m_Texture != null)
                    {
                        int w = Mathf.RoundToInt(m_Texture.width * m_UVRect.width);
                        int h = Mathf.RoundToInt(m_Texture.height * m_UVRect.height);
                        rectTransform.anchorMax = rectTransform.anchorMin;
                        rectTransform.sizeDelta = new Vector2(w, h);
                    }
                    break;
            }
        }

        #endregion

        #region Mesh Generation

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect bounds = GetPixelAdjustedRect();
            EffectPipeline p = pipeline;

            // 1. 生成基础网格
            Rect meshBounds;
            switch (m_SourceType)
            {
                case SourceType.Color:
                    meshBounds = GenerateColorMesh(vh, bounds, p);
                    break;
                case SourceType.Sprite:
                    meshBounds = GenerateSpriteMesh(vh, bounds, p);
                    break;
                case SourceType.Texture:
                    meshBounds = GenerateTextureMesh(vh, bounds, p);
                    break;
                default:
                    meshBounds = bounds;
                    break;
            }

            // 2. 获取 falloff 距离（由形状效果提供）
            float falloff = p != null ? p.GetFalloffDistance() : 0f;
            float aaDistance = p != null ? p.GetAntiAliasingDistance() : 1f;

            // 3. 扩展顶点 + 初始化 UV 通道
            ExpandVerticesAndInitUVChannels(vh, meshBounds, falloff, aaDistance);

            // 4. 效果管线处理
            if (p != null)
                p.ProcessMesh(vh, meshBounds);

            // 5. 顶点色颜色空间处理（详见 ConvertVertexColorsToLinearIfNeeded）
            ConvertVertexColorsToLinearIfNeeded(vh);
        }

        /// <summary>
        /// 顶点色颜色空间对齐（与内置 UI/Default 行为一致）：
        /// <see cref="Canvas.vertexColorAlwaysGammaSpace"/> = true 时，CanvasRenderer 不把顶点色
        /// 从 sRGB 转线性（转换责任转移到 shader/C# 侧）。
        /// 本模块的 ProceduralImage shader 不做 shader 宏判断（宏环境受该属性影响不可靠），
        /// 故由 C# 侧补偿——但**仅限该 shader 路径**：
        /// 渲染材质为 ProceduralImage SDF shader 时才转（该 shader 不转换顶点色，须补偿）；
        /// UI/Default（无效果）与用户自定义 shader 由各自 shader 处理 gamma 语义，此处跳过，
        /// 否则双重转换导致颜色变深（无效果时与标准色不一致）。
        /// </summary>
        private void ConvertVertexColorsToLinearIfNeeded(VertexHelper vh)
        {
            if (QualitySettings.activeColorSpace != ColorSpace.Linear)
                return;

            Canvas canvas = GetCanvas();
            if (canvas == null || !canvas.vertexColorAlwaysGammaSpace)
                return;

            Material mat = material;
            if (mat == null || mat.shader == null || mat.shader.name != ProceduralShaderName)
                return;

            UIVertex vert = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vert, i);
                Color32 c = vert.color;
                vert.color = new Color32(
                    (byte)(UIGammaToLinear(c.r / 255f) * 255f + 0.5f),
                    (byte)(UIGammaToLinear(c.g / 255f) * 255f + 0.5f),
                    (byte)(UIGammaToLinear(c.b / 255f) * 255f + 0.5f),
                    c.a);
                vh.SetUIVertex(vert, i);
            }
        }

        /// <summary>
        /// 复刻 UnityUI.cginc 的 <c>UIGammaToLinear</c>（分段近似，精度优于 0.5/255）——
        /// 与内置 UI/Default shader 的顶点色转换曲线严格一致，避免同一 Canvas 下与内置元素的微差。
        /// </summary>
        private static float UIGammaToLinear(float value)
        {
            // split = 18.5 / 255 = 0.0725490
            if (value < 0.0725490f)
                return 0.0849710f * value - 0.000163029f;

            return value * (value * (value * 0.265885f + 0.736584f) - 0.00980184f) + 0.00319697f;
        }

        [NonSerialized]
        private Canvas m_CachedCanvas;

        private Canvas GetCanvas()
        {
            if (m_CachedCanvas == null)
                m_CachedCanvas = GetComponentInParent<Canvas>();
            return m_CachedCanvas;
        }

        /// <summary>
        /// 生成纯色基础网格。
        /// </summary>
        private Rect GenerateColorMesh(VertexHelper vh, Rect bounds, EffectPipeline p)
        {
            int segments = p != null ? p.GetMaxRequiredSegments() : 1;
            BuildSubdividedQuad(vh, bounds, segments, color,
                Vector2.zero, Vector2.one);
            return bounds;
        }

        /// <summary>
        /// 生成 Sprite 基础网格。对齐 UnityEngine.UI.Image.GenerateSimpleSprite + GetDrawingDimensions 行为。
        /// </summary>
        private Rect GenerateSpriteMesh(VertexHelper vh, Rect bounds, EffectPipeline p)
        {
            if (m_Sprite == null)
                return GenerateColorMesh(vh, bounds, p);

            Rect adjustedBounds = ApplySpritePadding(bounds, m_Sprite);

            Vector2 spriteSize = new Vector2(m_Sprite.rect.width, m_Sprite.rect.height);
            if (m_PreserveAspect && spriteSize.sqrMagnitude > 0f)
                adjustedBounds = PreserveAspectRatio(adjustedBounds, spriteSize);

            Vector4 outerUV = DataUtility.GetOuterUV(m_Sprite);
            Vector2 uvMin = new Vector2(outerUV.x, outerUV.y);
            Vector2 uvMax = new Vector2(outerUV.z, outerUV.w);

            int segments = p != null ? p.GetMaxRequiredSegments() : 1;
            BuildSubdividedQuad(vh, adjustedBounds, segments, color, uvMin, uvMax);
            return adjustedBounds;
        }

        /// <summary>
        /// 将 sprite.border 作为网格内缩应用，对齐 UnityEngine.UI.Image.GetDrawingDimensions 的 padding 行为。
        /// </summary>
        private static Rect ApplySpritePadding(Rect bounds, Sprite sprite)
        {
            Vector4 border = DataUtility.GetPadding(sprite);
            float spriteW = sprite.rect.width;
            float spriteH = sprite.rect.height;

            if (spriteW <= 0f || spriteH <= 0f)
                return bounds;

            float padLeft = border.x / spriteW;
            float padBottom = border.y / spriteH;
            float padRight = border.z / spriteW;
            float padTop = border.w / spriteH;

            return new Rect(
                bounds.x + bounds.width * padLeft,
                bounds.y + bounds.height * padBottom,
                bounds.width * (1f - padLeft - padRight),
                bounds.height * (1f - padBottom - padTop));
        }

        /// <summary>
        /// 生成 Texture 基础网格。
        /// </summary>
        private Rect GenerateTextureMesh(VertexHelper vh, Rect bounds, EffectPipeline p)
        {
            if (m_Texture == null)
                return GenerateColorMesh(vh, bounds, p);

            Rect adjustedBounds = bounds;
            Vector2 texSize = new Vector2(m_Texture.width, m_Texture.height);
            if (m_PreserveAspect && texSize.sqrMagnitude > 0f)
                adjustedBounds = PreserveAspectRatio(bounds, texSize);

            Vector2 uvMin = new Vector2(m_UVRect.xMin, m_UVRect.yMin);
            Vector2 uvMax = new Vector2(m_UVRect.xMax, m_UVRect.yMax);

            int segments = p != null ? p.GetMaxRequiredSegments() : 1;
            BuildSubdividedQuad(vh, adjustedBounds, segments, color, uvMin, uvMax);
            return adjustedBounds;
        }

        /// <summary>
        /// 构建 segments×segments 的细分四边形。
        /// </summary>
        private static void BuildSubdividedQuad(
            VertexHelper vh, Rect bounds, int segments,
            Color baseColor, Vector2 uvMin, Vector2 uvMax)
        {
            segments = Mathf.Max(1, segments);
            Color32 c32 = baseColor;

            float xMin = bounds.xMin;
            float xMax = bounds.xMax;
            float yMin = bounds.yMin;
            float yMax = bounds.yMax;

            int vcX = segments + 1;
            int vcY = segments + 1;

            for (int y = 0; y < vcY; y++)
            {
                float tY = (float)y / segments;
                float posY = Mathf.Lerp(yMin, yMax, tY);
                float uvY = Mathf.Lerp(uvMin.y, uvMax.y, tY);

                for (int x = 0; x < vcX; x++)
                {
                    float tX = (float)x / segments;
                    float posX = Mathf.Lerp(xMin, xMax, tX);
                    float uvX = Mathf.Lerp(uvMin.x, uvMax.x, tX);

                    vh.AddVert(
                        new Vector3(posX, posY, 0f),
                        c32,
                        new Vector4(uvX, uvY, 0f, 0f),   // uv0: texture
                        Vector4.zero,                     // uv1: rectSize (set later)
                        Vector4.zero,                     // uv2: radii (set by effects)
                        Vector4.zero);                    // uv3: stroke+AA (set later)
                }
            }

            for (int y = 0; y < segments; y++)
            {
                for (int x = 0; x < segments; x++)
                {
                    int i00 = y * vcX + x;
                    int i10 = i00 + 1;
                    int i01 = (y + 1) * vcX + x;
                    int i11 = i01 + 1;

                    vh.AddTriangle(i00, i10, i11);
                    vh.AddTriangle(i00, i11, i01);
                }
            }
        }

        /// <summary>
        /// 扩展顶点（falloff）并初始化 uv1/uv2/uv3 通道。
        ///
        /// uv1: (originalWidth, originalHeight, localX, localY)
        /// uv2: (0, 0, 0, 0) — 占位，由 UIRoundedCorners 覆盖为四角半径
        /// uv3: (0, pixelScale) — 占位，由 UIStroke 覆盖 strokeWidth
        /// </summary>
        private static void ExpandVerticesAndInitUVChannels(
            VertexHelper vh, Rect originalBounds, float falloff, float aaDistance)
        {
            float origW = originalBounds.width;
            float origH = originalBounds.height;
            float cx = originalBounds.center.x;
            float cy = originalBounds.center.y;

            float pixelScale = 1f / Mathf.Max(0.001f, aaDistance);

            Vector4 defaultUV3 = new Vector4(0f, pixelScale, 0f, 0f);

            UIVertex vert = new UIVertex();
            Vector2 uvMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 uvMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vert, i);
                uvMin.x = Mathf.Min(uvMin.x, vert.uv0.x);
                uvMin.y = Mathf.Min(uvMin.y, vert.uv0.y);
                uvMax.x = Mathf.Max(uvMax.x, vert.uv0.x);
                uvMax.y = Mathf.Max(uvMax.y, vert.uv0.y);
            }

            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vert, i);

                // Falloff 扩展：将顶点沿法线方向向外推
                if (falloff > 0f && origW > 0f && origH > 0f)
                {
                    float nx = (vert.position.x - cx) / (origW * 0.5f);
                    float ny = (vert.position.y - cy) / (origH * 0.5f);
                    vert.position.x += nx * falloff;
                    vert.position.y += ny * falloff;
                }

                float localX = origW > 0f ? (vert.position.x - originalBounds.xMin) / origW : 0f;
                float localY = origH > 0f ? (vert.position.y - originalBounds.yMin) / origH : 0f;

                // uv1: 原始矩形尺寸 + 本地坐标
                vert.uv1 = new Vector4(origW, origH, localX, localY);

                // 纹理采样始终锚定在原始矩形范围（uv0.zw 未使用，置 0）
                vert.uv0 = new Vector4(
                    Mathf.LerpUnclamped(uvMin.x, uvMax.x, localX),
                    Mathf.LerpUnclamped(uvMin.y, uvMax.y, localY),
                    0f,
                    0f);

                // uv3: 初始化（strokeWidth=0, pixelScale）
                vert.uv3 = defaultUV3;

                vh.SetUIVertex(vert, i);
            }
        }

        private Rect PreserveAspectRatio(Rect rect, Vector2 nativeSize)
        {
            float nativeRatio = nativeSize.x / nativeSize.y;
            float rectRatio = rect.width / rect.height;

            if (nativeRatio > rectRatio)
            {
                float oldHeight = rect.height;
                rect.height = rect.width * (1f / nativeRatio);
                rect.y += (oldHeight - rect.height) * rectTransform.pivot.y;
            }
            else
            {
                float oldWidth = rect.width;
                rect.width = rect.height * nativeRatio;
                rect.x += (oldWidth - rect.width) * rectTransform.pivot.x;
            }
            return rect;
        }

        #endregion

        #region Unity Lifecycle

        protected override void OnEnable()
        {
            base.OnEnable();
            m_CachedPipeline = null; // 失效缓存，下次访问时重新获取
            m_CachedMask = null;
            m_CachedCanvas = null;
            UpdateMaterialTexture();
            SetupCanvasShaderChannels();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            DestroyCustomMaterial();
        }

        private void DestroyCustomMaterial()
        {
            if (m_CustomMaterialInstance == null)
                return;

            if (Application.isPlaying)
                Destroy(m_CustomMaterialInstance);
            else
                DestroyImmediate(m_CustomMaterialInstance);

            m_CustomMaterialInstance = null;
        }

        protected override void OnDidApplyAnimationProperties()
        {
            base.OnDidApplyAnimationProperties();
            SetAllDirty();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            m_UVRect.x = Mathf.Clamp01(m_UVRect.x);
            m_UVRect.y = Mathf.Clamp01(m_UVRect.y);
            m_UVRect.width = Mathf.Clamp01(m_UVRect.width);
            m_UVRect.height = Mathf.Clamp01(m_UVRect.height);

            // Inspector 直接改序列化字段（不经属性 setter）、撤销、预制体 Apply 等路径
            // 均经 OnValidate——标记重建保证编辑期所见即所得（uGUI 组件惯例，如 Image）。
            SetAllDirty();
        }
#endif

        #endregion
    }
}
