// Ruh efekti (SoulSystem): tek geçişte additive çizim. Mod, uv2.x ile seçilir:
//   0 = kuyruk şeridi (uv.y: kenardan merkeze), 1 = yumuşak parlama noktası, 2 = çubuk (kılıç parlaması), 3 = halka
// Vertex rengi 0..1 aralığındadır; HDR parlaklık _Intensity ile verilir (Bloom'u besler).
Shader "ProjectM/SoulFx"
{
    Properties
    {
        _Intensity ("Parlaklik (HDR)", Range(0, 12)) = 2.6
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
            Name "SoulFx"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float mode : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color = IN.color;
                OUT.uv = IN.uv;
                OUT.mode = IN.uv2.x;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float a = 0.0;
                float m = IN.mode;

                if (m < 0.5)
                {
                    // Kuyruk: kenarlara doğru yumuşak, ortada ince parlak çekirdek.
                    float c = 1.0 - abs(IN.uv.y * 2.0 - 1.0);
                    a = pow(saturate(c), 1.7) * 0.8 + pow(saturate(c), 6.0) * 0.9;
                }
                else if (m < 1.5)
                {
                    // Parlama noktası: geniş hale + keskin çekirdek.
                    float d = length(IN.uv - 0.5) * 2.0;
                    float edge = 1.0 - smoothstep(0.82, 1.0, d);
                    a = (exp(-d * d * 4.2) * 0.75 + pow(saturate(1.0 - d), 5.0) * 1.2) * edge;
                }
                else if (m < 2.5)
                {
                    // Çubuk: enine yumuşak, boyuna uçlara doğru sönen.
                    float c = 1.0 - abs(IN.uv.y * 2.0 - 1.0);
                    float along = pow(saturate(sin(IN.uv.x * 3.14159265)), 0.55);
                    a = (pow(saturate(c), 1.5) * 0.7 + pow(saturate(c), 5.0) * 0.9) * along;
                }
                else
                {
                    // Halka: yarıçap ~0.8 civarında ince, yumuşak bant.
                    float d = length(IN.uv - 0.5) * 2.0;
                    float x = (d - 0.78) / 0.14;
                    a = exp(-x * x) * (1.0 - smoothstep(0.9, 1.0, d));
                }

                half3 rgb = IN.color.rgb * (a * IN.color.a * _Intensity);
                return half4(rgb, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
