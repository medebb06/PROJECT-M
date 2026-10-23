// Kılıcın metal parçasına (ve kınlı durumda kılıç parçasına) ikinci materyal olarak eklenen ruh parlaması.
// Parçanın kendi dokusunu ve köşe alfasını kullanır: parlama TAM kılıcın şeklini izler, animasyonla birlikte hareket eder.
// SoulFx (SoulSystem.cs) her kare _Glow / _Tint değerlerini sürer.
Shader "ProjectM/SoulBlade"
{
    Properties
    {
        _MainTex ("Parça dokusu", 2D) = "white" {}
        _Glow ("Parlaklik (HDR)", Range(0, 12)) = 0
        _Tint ("Renk", Color) = (0.30, 0.85, 1.00, 1)
        _Core ("Cekirdek rengi", Color) = (0.80, 1.00, 1.00, 1)
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
        }

        Cull Off
        ZWrite Off
        Blend One One

        Pass
        {
            Name "SoulBlade"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Glow;
                float4 _Tint;
                float4 _Core;
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
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color = IN.color;
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                float a = tex.a * IN.color.a;
                if (a <= 0.002 || _Glow <= 0.001) return half4(0, 0, 0, 0);

                float lum = dot(tex.rgb, float3(0.30, 0.59, 0.11));

                // Sadece metal: kırmızı sap (tsuka) ve siyah kulp/tsuba parlamaz.
                float redHilt = saturate((tex.r - max(tex.g, tex.b) - 0.05) * 6.0);
                float dark = 1.0 - smoothstep(0.08, 0.20, lum);
                float metal = saturate(1.0 - redHilt) * (1.0 - dark * 0.9);

                // Kenar çizgisi: alfa komşuluğundan iç kenar (bıçağın hatlarında daha parlak)
                float2 o = float2(0.018, 0.018);
                float a1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(o.x, 0)).a;
                float a2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(o.x, 0)).a;
                float a3 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0, o.y)).a;
                float a4 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(0, o.y)).a;
                float rim = saturate(1.0 - min(min(a1, a2), min(a3, a4)) / max(tex.a, 0.001));

                // Bıçak boyunca akan ışık dalgası (ruh akıyormuş gibi); düz renk yerine hareketli degrade
                float along = IN.uv.x * 0.8 + (1.0 - IN.uv.y) * 0.6;
                float wave = sin(along * 9.0 - _Time.y * 2.6) * 0.5 + 0.5;
                float shimmer = pow(saturate(wave), 3.0);

                float body = 0.20 + 0.55 * lum + 0.55 * shimmer + 1.1 * rim;
                float3 col = lerp(_Tint.rgb, _Core.rgb, saturate(lum * 0.8 + shimmer * 0.7 + rim * 0.9));
                float3 rgb = col * body * metal * a * _Glow;
                return half4(rgb, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
