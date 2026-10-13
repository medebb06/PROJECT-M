// PROJECT M — Karakter şeklinde, yere düşen uzun gölge.
// Arkadan (ufuktan) gelen ışıkla gölge kameraya doğru, yere yatık düşer.
// Vertex'ler dünya uzayında ayak çizgisine göre aynalanıp ezilir ve yana yatırılır.
// Stencil: aynı karakterin üst üste binen parçaları gölgeyi iki kat karartmasın.
Shader "ProjectM/ProjectedShadow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _ShadowColor ("Shadow Color", Color) = (0.05,0.04,0.035,0.55)
        _FeetY ("Feet Y (world)", Float) = 0
        _Skew ("Skew (yana yatma)", Float) = 0.3
        _ScaleY ("Length (boy oranı)", Float) = 0.7
        _Fade ("Fade Height", Float) = 3
        _IsSprite ("Is Sprite", Float) = 1
        _AlphaCut ("Alpha Cut", Range(0,1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Stencil
        {
            Ref 128
            ReadMask 128
            WriteMask 128
            Comp NotEqual
            Pass Replace
        }

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _ShadowColor;
                float _FeetY;
                float _Skew;
                float _ScaleY;
                float _Fade;
                float _IsSprite;
                half _AlphaCut;
            CBUFFER_END

            // Ağaç gölgesi ağaçla birlikte sallansın (WindSway bunları MaterialPropertyBlock ile verir).
            #include "ProjectMWind.hlsl"
            float _ShadowWindAmount;
            float _ShadowWindSpeed;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float h : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 pos = v.positionOS;

                if (_IsSprite > 0.5)
                {
                    SetUpSpriteInstanceProperties();
                    pos = UnityFlipSprite(pos, unity_SpriteProps.xy);
                }

                float3 w = TransformObjectToWorld(pos);

                if (_ShadowWindAmount > 0.0001)
                {
                    float wh, ws;
                    w = WindBend(w, _ShadowWindAmount, _ShadowWindSpeed, wh, ws);
                }

                float h = max(0.0, w.y - _FeetY);

                // Yere yatır: ayak çizgisinin altına aynala, boyu kısalt, yana kaydır.
                w = float3(w.x + h * _Skew, _FeetY - h * _ScaleY, w.z);

                o.positionCS = TransformWorldToHClip(w);
                o.uv = v.uv;
                o.h = h;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                clip(a - _AlphaCut);

                // Ayaktan uzaklaştıkça solar.
                half fade = lerp(1.0h, 0.15h, saturate(i.h / max(_Fade, 0.01)));
                return half4(_ShadowColor.rgb, _ShadowColor.a * fade);
            }
            ENDHLSL
        }
    }
}
