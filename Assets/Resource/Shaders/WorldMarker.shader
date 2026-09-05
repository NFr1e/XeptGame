Shader "XeptGame/UI/WorldMarker"
{
    // 世界空间 Marker shader 基座（UI_WorldMarker_Design.md §6）
    // GPU 顶点 billboard：顶点着色器以物体中心 + 相机位置构造相机平面基向量，本地 Quad 直接映射——
    // 无每帧 C# LookAt。uniform 全属性驱动（无关键字变体），单材质 + MaterialPropertyBlock 表达效果组合；
    // **ZTest 渲染状态除外**（材质级，MPB 无法驱动）——由 WorldBillboard 在 LEqual/Off 两份共享材质间切换。
    Properties
    {
        [NoScaleOffset] _IconTex ("图标", 2D) = "white" {}
        _Tint ("染色", Color) = (1,1,1,1)
        _Alpha ("Alpha", Range(0,1)) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("深度测试", Float) = 4  // 4=LEqual 参与(墙挡) 0=Off(顶层)
        _WorldSize ("世界大小", Float) = 0.4
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "ForwardMarker"
            Tags { "LightMode"="UniversalForward" }

            // 半透明：无光照、不写深度；深度测试开关经 _ZTest（MPB 可覆盖渲染状态）
            ZWrite Off
            ZTest [_ZTest]
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR0;
            };

            TEXTURE2D(_IconTex);
            SAMPLER(sampler_IconTex);
            float4 _IconTex_ST;

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Alpha;
                float _WorldSize;
            CBUFFER_END

            // GPU 顶点 billboard（面向相机，quad 恒平行屏幕）：
            // 把中心转 view 空间，在 view 的 xy 平面（= 屏幕平面）铺本地 Quad ——
            // view 空间 x/y 即相机 right/up，quad 与屏幕完全平行，任意俯仰/转向无自转无倾斜。
            // 尺寸 = 世界大小（view 空间偏移即世界单位，透视近大远小）。
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                // 物体中心（世界）→ view 空间（Quad pivot 在物体原点，忽略自身旋转——billboard 由 shader 全权决定）
                float3 centerWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 centerVS = TransformWorldToView(centerWS);

                // 本地 Quad [-0.5, 0.5]² 铺到 view xy 平面（= 屏幕平面；uv 直接映射图标）
                float2 quadXY = input.positionOS.xy * _WorldSize;
                float3 positionVS = float3(centerVS.xy + quadXY, centerVS.z);
                output.positionHCS = TransformWViewToHClip(positionVS);
                output.uv = input.uv;
                output.color = _Tint;

                // 相机身后剔除：中心在相机后方（view z > 0）→ 推出视锥外不渲染
                if (centerVS.z > 0.0)
                {
                    output.positionHCS = float4(0, 0, 2, 1); // clip 外，不渲染
                }

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_IconTex, sampler_IconTex, input.uv);
                half4 color = tex * input.color;
                color.a *= _Alpha;
                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
