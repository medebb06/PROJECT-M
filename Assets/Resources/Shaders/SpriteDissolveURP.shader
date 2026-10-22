// Dissolve shader'inin URP / Sprite uyumlu surumu.
// Orijinal "Ultimate 10+ Shaders/Dissolve" built-in surface shader oldugu icin URP'de (pembe) calismaz.
// Mantik ayni: noise < cutoff olan pikseller kesilir, kenarda parlak (HDR) cizgi kalir.
// Noise procedural (dokusuz) uretilir: her sprite ve mesh ile calisir.
Shader "Ultimate 10+ Shaders/Dissolve Sprite URP"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _NoiseScale ("Noise Scale", Float) = 1.1
        _Cutoff ("Cut off", Range(0, 1)) = 0
        _EdgeWidth ("Edge Width", Range(0.001, 0.5)) = 0.2
        [HDR] _EdgeColor ("Edge Color", Color) = (3, 1.6, 0.5, 1)
        [Toggle] _VertexDissolve ("Dissolve from vertex alpha (mesh parcalari)", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Dissolve"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _EdgeColor;
                float _NoiseScale;
                float _Cutoff;
                float _EdgeWidth;
                float _VertexDissolve;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 objXY : TEXCOORD1;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float VNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                return VNoise(p) * 0.75 + VNoise(p * 2.03 + 7.1) * 0.2 + VNoise(p * 4.1 + 3.7) * 0.05;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                OUT.objXY = IN.positionOS.xy;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // 0..1'e yayilmis noise
                float n = saturate((Fbm(IN.objXY * _NoiseScale) - 0.05) / 0.9);

                // Mesh parcalarinda ilerleme vertex alpha'dan gelir (1 -> 0).
                float cut = lerp(_Cutoff, 1.0 - IN.color.a, _VertexDissolve) * 1.002;

                clip(n - cut);

                float edge = (cut > 0.0005) ? (1.0 - saturate((n - cut) / _EdgeWidth)) : 0.0;

                half3 baseRgb = tex.rgb * _Color.rgb * IN.color.rgb;
                half3 rgb = lerp(baseRgb, _EdgeColor.rgb, edge);

                half a = tex.a * _Color.a * lerp(IN.color.a, 1.0, _VertexDissolve);

                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
