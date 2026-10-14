// PROJECT M — Karakterlerin dış kenarında arkadan gelen ışık (rim).
// Karakterin bir kopyası ışığa doğru birkaç piksel kaydırılıp karakterin ARKASINA,
// toplamalı (additive) çizilir. Karakter önünü örttüğü için sadece siluetin ışığa
// bakan dış kenarından taşan ince ışık görünür; iç parçalarda çizgi oluşmaz.
// Stencil (bit 64): parçalar üst üste binince ışık iki kat parlamasın.
Shader "ProjectM/CharacterRim"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        [HDR] _RimColor ("Rim Color", Color) = (1,0.8,0.55,1)
        _Intensity ("Intensity", Range(0,4)) = 0.6
        _Offset ("Offset (world XY)", Vector) = (0.05,0.03,0,0)
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

        Blend One One
        Cull Off
        ZWrite Off

        Stencil
        {
            Ref 64
            ReadMask 64
            WriteMask 64
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
                half4 _RimColor;
                half _Intensity;
                float4 _Offset;
                float _IsSprite;
                half _AlphaCut;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
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
                w.xy += _Offset.xy;

                o.positionCS = TransformWorldToHClip(w);
                o.uv = v.uv;
                o.color = v.color * unity_SpriteColor;   // Unity 6: SpriteRenderer.color burada gelir (Sprite-Unlit-Default ile aynı)
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                clip(a - _AlphaCut);
                // Sprite kopyası kaynağın saydamlığını taşır (kaybolan / yanıp sönen karakterle birlikte söner).
                half k = _IsSprite > 0.5 ? i.color.a : 1.0;
                return half4(_RimColor.rgb * (_Intensity * k), 0);
            }
            ENDHLSL
        }
    }
}
