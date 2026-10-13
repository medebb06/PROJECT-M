// PROJECT M — Sürüklenen sis / pus şeridi (dünya uzayında gürültü). Alfa karışımlı.
// Dikey profil: şeridin ortasında yoğun, kenarlarda söner. Işığa yakın yerde ışığın rengini alır.
Shader "ProjectM/Mist"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _NoiseTex ("Noise", 2D) = "gray" {}
        _Color ("Sis rengi", Color) = (0.6, 0.65, 0.75, 1)
        _Density ("Yoğunluk", Range(0,2)) = 0.5
        _Scale ("Leke ölçeği", Range(0.005,0.2)) = 0.03
        _Speed ("Akış hızı", Float) = 0.15
        _Softness ("Lekelerin yumuşaklığı", Range(0.05,1)) = 0.5
        _Coverage ("Kaplama (0 seyrek, 1 dolu)", Range(0,1)) = 0.55
        _Falloff ("Dikey düşüş", Range(0.3,4)) = 1.4
        _Bottom ("Alt kenar ağırlığı (0 orta, 1 tabana yığılır)", Range(0,1)) = 0.0
        _Parallax ("Kamerayla kayma", Range(0,1)) = 0.2
        _LightGlow ("Işık yakınında parlama", Range(0,2)) = 0.6
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

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

        TEXTURE2D(_NoiseTex);
        SAMPLER(sampler_NoiseTex);

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half _Density, _Softness, _Coverage, _Falloff, _Bottom, _Parallax, _LightGlow;
            float _Scale, _Speed;
        CBUFFER_END

        float4 _ProjectM_MoonPos;     // baskın ışık (güneş/ay) konumu
        float4 _ProjectM_LightColor;  // rgb
        float _ProjectM_SkyTime;

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
            float3 wpos : TEXCOORD1;
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
            o.uv = v.uv;
            o.color = v.color * unity_SpriteColor;
            return o;
        }

        half4 frag(Varyings i) : SV_Target
        {
            float t = _ProjectM_SkyTime * _Speed;
            float x = i.wpos.x - _WorldSpaceCameraPos.x * _Parallax;
            float vy = i.uv.y;

            // Tamamen analitik, çok düşük frekanslı dalgalar: doku yok, kenar/tırtık yok.
            // Birkaç yavaş sinüs üst üste -> yumuşak, yuvarlak pus kümeleri; ters yönlerde akar.
            float u = x * _Scale * 14.0;
            float n = 0.5
                + 0.24 * sin(u * 1.00 + t * 0.30 + vy * 2.0)
                + 0.18 * sin(u * 0.57 - t * 0.22 + 1.7 + vy * 3.1)
                + 0.10 * sin(u * 1.83 + t * 0.45 + 4.0 - vy * 1.6);

            float lo = 1.0 - _Coverage;
            float hw = 0.22 + _Softness * 0.35;
            float dens = smoothstep(lo - hw, lo + hw, n);

            // Dikey profil
            float v = i.uv.y;
            float mid = lerp(0.5, 0.25, _Bottom);
            float prof = saturate(1.0 - abs(v - mid) / max(mid, 1.0 - mid));
            prof = pow(prof, _Falloff);
            prof *= smoothstep(0.0, 0.08, v) * smoothstep(1.0, 0.92, v);   // kenar kesikliği olmasın

            float a = dens * prof * _Density * _Color.a * i.color.a;

            // Işığa yakın sis ışığın rengiyle parlar.
            float dl = (i.wpos.x - _ProjectM_MoonPos.x) / 14.0;
            float glow = exp(-dl * dl) * _LightGlow;
            half3 rgb = _Color.rgb + _ProjectM_LightColor.rgb * glow * 0.5;

            return half4(rgb * i.color.rgb, saturate(a));
        }
        ENDHLSL

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
