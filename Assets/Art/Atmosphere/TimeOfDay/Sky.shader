// PROJECT M — Gökyüzü gradyanı (dünya Y'sine göre). Ufuk rengi ufuk çizgisinde, tepe rengi yukarıda.
// Renkleri TimeOfDay verir (saat dilimi). Ufkun altı ufuk renginin koyulaşmış hâli.
Shader "ProjectM/Sky"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Top ("Tepe", Color) = (0.10, 0.14, 0.24, 1)
        _Mid ("Orta", Color) = (0.20, 0.26, 0.38, 1)
        _Horizon ("Ufuk", Color) = (0.42, 0.45, 0.52, 1)
        _Height ("Gökyüzü yüksekliği (birim)", Float) = 22
        _MidPos ("Orta renk konumu (0-1)", Range(0.05,0.95)) = 0.45
        _HorizonY ("Ufuk Y", Float) = 0
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

        Blend One Zero
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Top, _Mid, _Horizon;
            float _Height, _MidPos, _HorizonY;
        CBUFFER_END

        struct Attributes
        {
            float3 positionOS : POSITION;
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            half4 color : COLOR;
            float3 wpos : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings vert(Attributes v)
        {
            Varyings o = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            SetUpSpriteInstanceProperties();
            v.positionOS = UnityFlipSprite(v.positionOS, unity_SpriteProps.xy);
            o.wpos = TransformObjectToWorld(v.positionOS);
            o.positionCS = TransformWorldToHClip(o.wpos);
            o.color = v.color * unity_SpriteColor;
            return o;
        }

        half4 frag(Varyings i) : SV_Target
        {
            float h = (i.wpos.y - _HorizonY) / max(_Height, 0.01);
            half3 c;

            if (h <= 0.0)
            {
                // Ufkun altı: ufuk rengi, aşağı doğru koyulaşır (su/zemin zaten üstünü kapatır).
                c = _Horizon.rgb * saturate(1.0 + h * 0.6);
            }
            else
            {
                float t = saturate(h);
                // Ufka yakın hızlı geçiş (ufuk parıltısı dar bir bant), sonra yumuşak.
                float tm = saturate(t / max(_MidPos, 0.001));
                tm = tm * tm * (3.0 - 2.0 * tm);
                float tt = saturate((t - _MidPos) / max(1.0 - _MidPos, 0.001));
                tt = tt * tt * (3.0 - 2.0 * tt);
                c = lerp(lerp(_Horizon.rgb, _Mid.rgb, tm), _Top.rgb, tt);
            }

            return half4(c * i.color.rgb, 1);
        }

#include "Assets/Art/Atmosphere/Shaders/WorldGray.hlsl"
half4 frag_gray(Varyings pi) : SV_Target { return WorldGray(frag(pi)); }
        ENDHLSL

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag_gray
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
