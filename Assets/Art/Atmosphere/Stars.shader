// PROJECT M — Yıldızlı gökyüzü (prosedürel, toplamalı). Gökyüzü katmanıyla bulutların arasına girer.
// Yıldızlar kameranın ~%2'si kadar kayar (çok uzak), ufka doğru söner, yavaşça pırıldar.
Shader "ProjectM/Stars"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _ColorCool ("Soğuk yıldız", Color) = (0.75, 0.85, 1.0, 1)
        _ColorWarm ("Sıcak yıldız", Color) = (1.0, 0.92, 0.75, 1)
        _Intensity ("Parlaklık", Range(0,6)) = 2.0
        _Density ("Sıklık (birim başına hücre)", Range(0.3,6)) = 1.2
        _Chance ("Hücrede yıldız ihtimali", Range(0,1)) = 0.11
        _Size ("Boyut", Range(0.2,3)) = 1.0
        _TinyLayer ("Küçük soluk yıldızlar", Range(0,1)) = 0.45
        _Twinkle ("Pırıldama", Range(0,1)) = 0.45
        _TwinkleSpeed ("Pırıldama hızı", Range(0,5)) = 1.2
        _FadeStart ("Ufuktan bu kadar yukarıda başlar", Float) = 1.5
        _FadeEnd ("Bu kadar yukarıda tam görünür", Float) = 9
        _Parallax ("Kamerayla kayma", Range(0,0.2)) = 0.02
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

        CBUFFER_START(UnityPerMaterial)
            half4 _ColorCool, _ColorWarm;
            float _Intensity, _Density, _Chance, _Size, _TinyLayer;
            float _Twinkle, _TwinkleSpeed, _FadeStart, _FadeEnd, _Parallax;
        CBUFFER_END

        float4 _ProjectM_LightPos;
        float _ProjectM_SkyTime;

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

        float3 Hash3(float2 p)
        {
            float3 q = float3(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)), dot(p, float2(419.2, 371.9)));
            return frac(sin(q) * 43758.5453);
        }

        half3 Layer(float2 sp, float density, float chance, float size, float t, float seed)
        {
            float2 g = sp * density;
            float2 id = floor(g);
            float2 f = frac(g);
            float3 h = Hash3(id + seed);

            if (h.x > chance)
                return 0;

            float3 h2 = Hash3(id * 1.7 + seed + 11.0);
            float2 c = 0.3 + 0.4 * h2.xy;                       // yıldızın hücre içindeki yeri
            float d = length((f - c)) / density;                // birim cinsinden uzaklık
            float r = size * (0.02 + 0.035 * h2.z * h2.z);      // yarıçap (birim)

            float core = exp(-(d * d) / (r * r));
            float halo = exp(-d / (r * 3.5)) * 0.18 * h2.z;

            float tw = 1.0 - _Twinkle * (0.5 + 0.5 * sin(t * _TwinkleSpeed * (0.4 + h2.x * 1.4) + h2.y * 6.2831));
            float bright = (0.35 + 0.65 * h2.z) * tw;

            half3 col = lerp(_ColorCool.rgb, _ColorWarm.rgb, h.z);
            return col * (core + halo) * bright;
        }

        half4 frag(Varyings i) : SV_Target
        {
            float3 cam = _WorldSpaceCameraPos;
            float2 sp = i.wpos.xy - cam.xy * (1.0 - _Parallax);   // neredeyse kameraya yapışık
            float t = _ProjectM_SkyTime;

            half3 c = Layer(sp, _Density, _Chance, _Size, t, 0.0);
            c += Layer(sp + 31.7, _Density * 2.7, saturate(_Chance * 1.3), _Size * 0.55, t, 7.0) * _TinyLayer;

            // Ufka doğru sön (ufuk = ışık konumunun 3 birim altı).
            float horizon = _ProjectM_LightPos.y - 3.0;
            float fade = smoothstep(horizon + _FadeStart, horizon + _FadeEnd, i.wpos.y);

            return half4(c * _Intensity * fade * i.color.rgb * i.color.a, 0);
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
