Shader "XeptKit/UI/ProceduralImage"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [PerRendererData] _StrokeColor ("Stroke Color", Color) = (0,0,0,1)
        [PerRendererData] _StrokeWidth ("Stroke Width", Float) = 0
        [PerRendererData] _StrokePosition ("Stroke Position", Float) = 0
        [PerRendererData] _StrokeUseGraphicAlpha ("Use Graphic Alpha", Float) = 1
        [PerRendererData] _StrokeCanvasGroupAlpha ("CanvasGroup Alpha", Float) = 1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off  Lighting Off  ZWrite Off  ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float4 uv0    : TEXCOORD0;
                float4 uv1    : TEXCOORD1;
                float4 uv2    : TEXCOORD2;
                float4 uv3    : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex         : SV_POSITION;
                fixed4 color          : COLOR;
                float4 worldPosition  : TEXCOORD0;
                float4 radius         : TEXCOORD1;
                float2 texcoord       : TEXCOORD2;
                float2 rectSize       : TEXCOORD3;
                float2 localCoord     : TEXCOORD4;
                float  pixelScale     : TEXCOORD5;
                float  smoothness     : TEXCOORD6;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float2 _ClipSoftness;
            float4 _MainTex_ST;
            fixed4 _StrokeColor;
            float  _StrokeWidth;
            float  _StrokePosition;
            float  _StrokeUseGraphicAlpha;
            float  _StrokeCanvasGroupAlpha;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                OUT.rectSize = IN.uv1.xy;
                OUT.localCoord = IN.uv1.zw;
                OUT.texcoord = TRANSFORM_TEX(IN.uv0, _MainTex);
                OUT.radius = IN.uv2;

                OUT.pixelScale = clamp(IN.uv3.y, 1.0 / 2048.0, 2048.0);
                OUT.smoothness = IN.uv3.z;

                // 顶点色颜色空间由 C# 侧统一处理（ProceduralImage.OnPopulateMesh）：
                // Canvas.vertexColorAlwaysGammaSpace=true 时顶点色保持 gamma（C++ 不转换），
                // 转换责任在 shader/C# 侧；此处不做 shader 宏判断——宏环境受
                // vertexColorAlwaysGammaSpace 影响（true 时 UI 变体按 gamma 编译），不可靠。
                OUT.color = IN.color * _Color;
                return OUT;
            }

            // SDF for rounded rectangle with optional superellipse (squircle) corners.
            // smoothness = 0: standard arc (G1), smoothness = 0.6: Apple squircle (approx G2).
            float RoundedRectSDF(float2 pos, float4 r, float2 rectSize, float smoothness)
            {
                float4 p = float4(pos.x, pos.y, rectSize.x - pos.x, rectSize.y - pos.y);
                float edgeDist = min(min(p.x, p.y), min(p.z, p.w));
                // 角落索引与 C# 侧 m_CornerRadii 语义对齐（x=TL, y=TR, z=BL, w=BR）。
                // 样本缺陷修复：原实现角3/4 用 (p.zy, r.z)/(p.xy, r.w)，实为
                // (右下, r.z)/(左下, r.w)——z/w 与 BL/BR 相反，导致四角字段控制交叉。
                // p = (距左, 距下, 距右, 距上)：BL = (p.x, p.y)，BR = (p.z, p.y)。
                bool4 inCorner = bool4(
                    all(p.xw < r.x), all(p.zw < r.y),
                    all(p.xy < r.z), all(p.zy < r.w));
                float n = 2.0 + smoothness * 3.0;
                float4 cornerDist;
                if (n <= 2.001)
                {
                    cornerDist = float4(
                        r.x - length(p.xw - r.x), r.y - length(p.zw - r.y),
                        r.z - length(p.xy - r.z), r.w - length(p.zy - r.w));
                }
                else
                {
                    cornerDist = float4(
                        r.x - pow(pow(abs(p.xw.x - r.x), n) + pow(abs(p.xw.y - r.x), n), 1.0 / n),
                        r.y - pow(pow(abs(p.zw.x - r.y), n) + pow(abs(p.zw.y - r.y), n), 1.0 / n),
                        r.z - pow(pow(abs(p.xy.x - r.z), n) + pow(abs(p.xy.y - r.z), n), 1.0 / n),
                        r.w - pow(pow(abs(p.zy.x - r.w), n) + pow(abs(p.zy.y - r.w), n), 1.0 / n));
                }
                float4 blended = inCorner * cornerDist + (1 - inCorner) * edgeDist;
                return any(inCorner)
                    ? min(min(blended.x, blended.y), min(blended.z, blended.w))
                    : edgeDist;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 fillColor = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                float clipAlpha = 1.0;

                #ifdef UNITY_UI_CLIP_RECT
                if (_ClipSoftness.x > 0.001 || _ClipSoftness.y > 0.001)
                {
                    float2 s = _ClipSoftness;
                    float2 pos = IN.worldPosition.xy;

                    float2 lo, hi;

                    lo.x = s.x > 0.001
                        ? smoothstep(_ClipRect.x - s.x, _ClipRect.x + s.x, pos.x)
                        : step(_ClipRect.x, pos.x);
                    lo.y = s.y > 0.001
                        ? smoothstep(_ClipRect.y - s.y, _ClipRect.y + s.y, pos.y)
                        : step(_ClipRect.y, pos.y);

                    hi.x = s.x > 0.001
                        ? smoothstep(_ClipRect.z + s.x, _ClipRect.z - s.x, pos.x)
                        : step(pos.x, _ClipRect.z);
                    hi.y = s.y > 0.001
                        ? smoothstep(_ClipRect.w + s.y, _ClipRect.w - s.y, pos.y)
                        : step(pos.y, _ClipRect.w);

                    clipAlpha = lo.x * lo.y * hi.x * hi.y;
                }
                else
                {
                    clipAlpha = UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                }
                #endif

                float d = RoundedRectSDF(IN.localCoord * IN.rectSize, IN.radius, IN.rectSize, IN.smoothness);

                float fillEdge = d;

                // Stroke: SDF-based band-pass
                float strokeA = 0.0;
                if (_StrokeWidth > 0.001)
                {
                    float w = _StrokeWidth;
                    float ps = IN.pixelScale;
                    float aa = 1.0 / ps;

                    float graphicAlpha = _StrokeUseGraphicAlpha > 0.5
                        ? IN.color.a
                        : _StrokeCanvasGroupAlpha;

                    if (_StrokePosition < 0.5)
                    {
                        // Inside: stroke in [0, w], fill starts at w
                        fillEdge = d - w;
                        strokeA = saturate(d * ps) * saturate((w + aa - d) * ps) * _StrokeColor.a * graphicAlpha;
                    }
                    else if (_StrokePosition < 1.5)
                    {
                        // Center: stroke in [-w/2, w/2]
                        float hw = w * 0.5;
                        fillEdge = d - hw;
                        strokeA = saturate((d + hw) * ps) * saturate((hw + aa - d) * ps) * _StrokeColor.a * graphicAlpha;
                    }
                    else
                    {
                        // Outside: stroke in [-w, 0]
                        strokeA = saturate((d + w) * ps) * saturate((-d + aa) * ps) * _StrokeColor.a * graphicAlpha;
                    }
                }

                float fillA = saturate(fillEdge * IN.pixelScale) * fillColor.a;

                // Stroke under fill composite
                float outA = fillA + strokeA * (1.0 - fillA);
                float3 outRGB = (fillColor.rgb * fillA + _StrokeColor.rgb * strokeA * (1.0 - fillA)) / max(outA, 0.0001);

                float totalA = outA * clipAlpha;
                half4 color = half4(outRGB, totalA);

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                if (color.a <= 0.0) discard;
                return color;
            }
            ENDCG
        }
    }
}
