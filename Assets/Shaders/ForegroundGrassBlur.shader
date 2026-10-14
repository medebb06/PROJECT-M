
Shader "Custom/ForegroundGrassBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BlurSize ("Blur Amount (Pixels)", Range(0, 5)) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _BlurSize;
            CBUFFER_END

            float _ProjectM_TodOn;
            float4 _ProjectM_FgColor;   // rgb: yaprak rengi, a: opaklık
            float4 _ProjectM_FgRim;     // rgb: kenar ışığı rengi*şiddet

            float4 _MainTex_TexelSize;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS =
                    TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 stepUV = _MainTex_TexelSize.xy * _BlurSize;

                half4 col = SAMPLE_TEXTURE2D(
                    _MainTex, sampler_MainTex, input.uv
                ) * 4.0;

                col += SAMPLE_TEXTURE2D(
                    _MainTex, sampler_MainTex,
                    input.uv + float2(stepUV.x, 0)
                );

                col += SAMPLE_TEXTURE2D(
                    _MainTex, sampler_MainTex,
                    input.uv - float2(stepUV.x, 0)
                );

                col += SAMPLE_TEXTURE2D(
                    _MainTex, sampler_MainTex,
                    input.uv + float2(0, stepUV.y)
                );

                col += SAMPLE_TEXTURE2D(
                    _MainTex, sampler_MainTex,
                    input.uv - float2(0, stepUV.y)
                );

                col += SAMPLE_TEXTURE2D(
                    _MainTex, sampler_MainTex,
                    input.uv + stepUV
                );

                col += SAMPLE_TEXTURE2D(
                    _MainTex, sampler_MainTex,
                    input.uv - stepUV
                );

                col += SAMPLE_TEXTURE2D(
                    _MainTex, sampler_MainTex,
                    input.uv + float2(stepUV.x, -stepUV.y)
                );

                col += SAMPLE_TEXTURE2D(
                    _MainTex, sampler_MainTex,
                    input.uv + float2(-stepUV.x, stepUV.y)
                );

                col /= 12.0;

                // Saat dilimi: düz siyah yerine renkli yaprak + ışık gelen kenarda ince parlak çizgi.
                if (_ProjectM_TodOn > 0.5)
                {
                    half a0 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                    half aUp = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0, stepUV.y * 2.0)).a;
                    half aSide = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(-stepUV.x * 1.5, stepUV.y)).a;
                    half rim = saturate(a0 - min(aUp, aSide));

                    half lum = dot(col.rgb, half3(0.333, 0.333, 0.333));
                    half3 leaf = _ProjectM_FgColor.rgb * (1.0 + lum * 2.0);
                    col.rgb = leaf + _ProjectM_FgRim.rgb * rim;
                    col.a *= _ProjectM_FgColor.a;
                }

                col *= input.color;

                return col;
            }
            ENDHLSL
        }
    }
}
