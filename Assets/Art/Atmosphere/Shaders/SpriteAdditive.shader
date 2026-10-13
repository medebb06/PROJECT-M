// PROJECT M — Toplamalı (additive) sprite: ay halesi, ufuk parıltısı, ışık hüzmeleri, toz.
Shader "ProjectM/SpriteAdditive"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Range(0,4)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Blend One One
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color;
            half _Intensity;
        CBUFFER_END

        struct Attributes
        {
            float3 positionOS : POSITION;
            float4 color : COLOR;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            half4 color : COLOR;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings vert(Attributes v)
        {
            Varyings o = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

            SetUpSpriteInstanceProperties();
            v.positionOS = UnityFlipSprite(v.positionOS, unity_SpriteProps.xy);

            o.positionCS = TransformObjectToHClip(v.positionOS);
            o.uv = v.uv;
            o.color = v.color * _Color * unity_SpriteColor;
            return o;
        }

        half4 frag(Varyings i) : SV_Target
        {
            half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
            half3 rgb = t.rgb * t.a * _Intensity;
            return half4(rgb, 0);
        }
        ENDHLSL

        // Tek etiketsiz pass (URP'nin Sprite-Unlit-Default'u gibi): 2D Renderer bunu çizer.
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
