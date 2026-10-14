// PROJECT M — Saldırı smear'ı için parlak (HDR) toplamalı sprite katmanı.
// Impact prefab'ındaki gibi bloom ile parlar; renk _GlowColor (HDR), saydamlık sprite rengi alfasından.
Shader "ProjectM/SpriteGlowAdd"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        [HDR] _GlowColor ("Glow Color", Color) = (1.2, 2.2, 4.5, 1)
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

        Blend SrcAlpha One
        Cull Off
        ZWrite Off

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
                half4 _GlowColor;
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

                SetUpSpriteInstanceProperties();
                float3 pos = UnityFlipSprite(v.positionOS, unity_SpriteProps.xy);

                o.positionCS = TransformObjectToHClip(pos);
                o.uv = v.uv;
                o.color = v.color * unity_SpriteColor;   // Unity 6: SpriteRenderer.color burada gelir (Sprite-Unlit-Default ile aynı)
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half a = t.a * i.color.a;
                return half4(t.rgb * _GlowColor.rgb * i.color.rgb, a);
            }
            ENDHLSL
        }
    }
}
