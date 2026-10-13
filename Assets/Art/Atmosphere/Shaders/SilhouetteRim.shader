// PROJECT M — Siluet + kenar ışığı (rim) + atmosferik pus.
// Normal map gerektirmez: sprite'ın alfa kenarını ışık kaynağına doğru tarar,
// ışığa bakan kenarı yakar. Ağaç, çalı, ön plan yaprakları için.
Shader "ProjectM/SilhouetteRim"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint (siluet rengi)", Color) = (1,1,1,1)

        [Header(Atmospheric Haze)]
        _HazeColor ("Haze Color", Color) = (0.75,0.70,0.65,1)
        _Haze ("Haze (uzaklık)", Range(0,1)) = 0
        _HazeBottom ("Haze Bottom (yerde sis)", Range(0,1)) = 0

        [Header(Rim Light)]
        [HDR] _RimColor ("Rim Color", Color) = (1.4,1.1,0.8,1)
        _RimIntensity ("Rim Intensity", Range(0,4)) = 1
        _RimWidth ("Rim Width (px)", Range(0,64)) = 10
        _RimTop ("Extra Top Rim", Range(0,1)) = 0.35
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
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half4 _HazeColor;
            half _Haze;
            half _HazeBottom;
            half4 _RimColor;
            half _RimIntensity;
            half _RimWidth;
            half _RimTop;
        CBUFFER_END

        // Işık konumu (dünya XY). AtmosphereLight her kare ayarlar (kamerayı takip eder).
        float4 _ProjectM_LightPos;

        // Sprite'ın gerçek doku boyutu (SpriteRenderer her çizimde ayarlar; materyal CBUFFER'ında olmamalı).
        float4 _MainTex_TexelSize;

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
            float3 positionWS : TEXCOORD1;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings vert(Attributes v)
        {
            Varyings o = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

            SetUpSpriteInstanceProperties();
            v.positionOS = UnityFlipSprite(v.positionOS, unity_SpriteProps.xy);

            o.positionWS = TransformObjectToWorld(v.positionOS);
            o.positionCS = TransformWorldToHClip(o.positionWS);
            o.uv = v.uv;
            o.color = v.color * _Color * unity_SpriteColor;
            return o;
        }

        half4 frag(Varyings i) : SV_Target
        {
            half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
            half4 c = tex * i.color;

            if (c.a <= 0.001h)
                discard;

            // Atmosferik pus: uzaktakiler ve alt kısım havaya karışır.
            half haze = saturate(_Haze + _HazeBottom * (1.0h - i.uv.y));
            half3 rgb = lerp(c.rgb, _HazeColor.rgb, haze);

            // Kenar ışığı: ışığa doğru bakan komşu pikseller boşsa kenar yanar.
            float2 dir = _ProjectM_LightPos.xy - i.positionWS.xy;
            float len = max(length(dir), 1e-4);
            dir /= len;
            dir = normalize(dir + float2(0, _RimTop)); // üst kenarlar biraz daha yansın

            float2 stepUV = dir * _MainTex_TexelSize.xy * _RimWidth;

            half a1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + stepUV * 0.33).a;
            half a2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + stepUV * 0.66).a;
            half a3 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + stepUV).a;

            half empty = 1.0h - (a1 * 0.5h + a2 * 0.3h + a3 * 0.2h);
            half rim = saturate(empty) * tex.a;

            rgb += _RimColor.rgb * rim * _RimIntensity;

            return half4(rgb, c.a);
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

    Fallback "Sprites/Default"
}
