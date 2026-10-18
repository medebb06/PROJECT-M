// PROJECT M — Akışkan, yansıtan su yüzeyi (2D Renderer).
// Yansıma: WaterSurface bileşeninin aynalanmış kamerası (_WaterReflTex).
// Dalga: kaydırılan iki gürültü katmanı + derinleştikçe büyüyen bükülme.
// Ay yolu: ayın altında pırıldayan ışık şeridi.
// Halkalar: WaterSurface'in gönderdiği 16 halka (adım, iniş, vuruş, rastgele).
Shader "ProjectM/Water"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _NoiseTex ("Noise (RG dalga, B pırıltı)", 2D) = "gray" {}

        [Header(Renk)]
        _ShallowColor ("Kıyı rengi", Color) = (0.30, 0.29, 0.28, 1)
        _DeepColor ("Derin renk", Color) = (0.07, 0.07, 0.08, 1)
        _ReflectTint ("Yansıma tonu", Color) = (0.62, 0.62, 0.66, 1)
        _ReflStrength ("Yansıma gücü", Range(0,1.5)) = 0.9
        _FadeDistance ("Kararma mesafesi (birim)", Float) = 16
        _ReflFadeDistance ("Yansıma silinme mesafesi (birim, büyük = tüm suda yansır)", Float) = 16
        _Blur ("Derinlikte bulanıklık", Range(0,0.2)) = 0.015

        [Header(Dalga)]
        _WaveAmp ("Dalga bükülmesi", Range(0,1.5)) = 0.12
        _WaveScale ("Dalga sıklığı", Range(0.01,1)) = 0.06
        _WaveSpeed ("Dalga hızı", Range(0,3)) = 0.7
        _Stretch ("Yatay uzama", Range(1,12)) = 5
        _Streaks ("Dalga sırtı parıltısı", Range(0,1)) = 0.08
        _BandContrast ("Bant belirginliği", Range(0,0.5)) = 0.05

        [Header(Kiyi cizgisi)]
        _LineColor ("Çizgi rengi", Color) = (0.75, 0.82, 0.9, 1)
        _LineWidth ("Çizgi kalınlığı", Range(0,1)) = 0.12
        _LineIntensity ("Çizgi gücü", Range(0,2)) = 0.45

        [Header(Ay yolu)]
        _MoonColor ("Pırıltı rengi", Color) = (1.0, 0.86, 0.6, 1)
        _MoonWidth ("Şerit genişliği", Range(0.1,10)) = 1.6
        _MoonSpread ("Aşağı doğru genişleme", Range(0,1)) = 0.12
        _MoonIntensity ("Pırıltı gücü", Range(0,6)) = 1.6
        _MoonGlow ("Yumuşak parıltı", Range(0,1)) = 0.12
        _SparkleSharp ("Pırıltı keskinliği", Range(1,16)) = 5

        [Header(Halkalar)]
        _RippleAspect ("Halka basıklığı", Range(1,10)) = 4.5
        _RippleSpeed ("Halka büyüme hızı", Float) = 2.6
        _RippleLife ("Halka ömrü (sn)", Float) = 1.6
        _RippleWidth ("Halka kalınlığı", Float) = 0.16
        _RippleDistort ("Halka bükülmesi", Range(0,1)) = 0.25
        _RippleLight ("Halka parlaklığı", Range(0,2)) = 0.6
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

        TEXTURE2D(_MainTex);        SAMPLER(sampler_MainTex);
        TEXTURE2D(_NoiseTex);       SAMPLER(sampler_NoiseTex);
        TEXTURE2D(_WaterReflTex);   SAMPLER(sampler_WaterReflTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _ShallowColor, _DeepColor, _ReflectTint, _LineColor, _MoonColor;
            float _ReflStrength, _FadeDistance, _Blur, _ReflFadeDistance;
            float _WaveAmp, _WaveScale, _WaveSpeed, _Stretch, _Streaks, _BandContrast;
            float _LineWidth, _LineIntensity;
            float _MoonWidth, _MoonSpread, _MoonIntensity, _MoonGlow, _SparkleSharp;
            float _RippleAspect, _RippleSpeed, _RippleLife, _RippleWidth, _RippleDistort, _RippleLight;
        CBUFFER_END

        // Globaller (WaterSurface)
        float  _WaterY;
        float  _WaterHasRefl;
        float4 _WaterReflRect;      // minX, minY, 1/genişlik, 1/yükseklik
        float  _WaterMoonX;
        float  _WaterTime;

        // Saat dilimi (TimeOfDay)
        float  _ProjectM_TodOn;
        float4 _ProjectM_WaterMul;     // rgb: su rengi çarpanı
        float4 _ProjectM_LightColor;   // rgb: güneş/ay rengi
        float4 _ProjectM_Glint;        // x: ışık yolu pırıltı çarpanı
        float4 _WaterRipples[16];   // x, derinlik, başlangıç zamanı, güç

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

        float4 SampleRefl(float2 p)
        {
            float2 uv = (p - _WaterReflRect.xy) * _WaterReflRect.zw;
            // Kenarlarda yumuşakça söner (kare kenar görünmesin).
            float2 edge = saturate(min(uv, 1.0 - uv) * 12.0);
            float inside = edge.x * edge.y;
            return float4(SAMPLE_TEXTURE2D(_WaterReflTex, sampler_WaterReflTex, saturate(uv)).rgb, inside);
        }

        half4 frag(Varyings i) : SV_Target
        {
            half4 spr = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;

            float t = _WaterTime * _WaveSpeed;
            float x = i.wpos.x;
            float d = max(0.0, _WaterY - i.wpos.y);            // yüzeyden derinlik (birim)
            float fade = saturate(d / max(_FadeDistance, 0.01));
            float near = 1.0 - fade;

            // Perspektif: ufka yakın dalgalar sık ve ince, aşağı indikçe geniş.
            float ph = log(1.0 + d * 0.9) * 266.0 * _WaveScale;

            // Çok uzun, yatay gürültü (dalga kümeleri). Sadece yavaş değişen büyük leke.
            float2 nq = float2(x * 0.09 / _Stretch, ph * 0.02);
            float2 n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, nq + float2(t * 0.006, -t * 0.02)).rg * 2.0 - 1.0;
            float2 n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, nq * 2.7 + float2(-t * 0.01, -t * 0.035)).rg * 2.0 - 1.0;

            // Yatay dalga bantları: yansıma yatayda kayar (gerçek su gibi), dikeyde neredeyse sabit.
            float bandA = sin(ph * 1.00 - t * 1.30 + n1.x * 2.5);
            float bandB = sin(ph * 2.13 + t * 0.90 + n2.y * 2.0);
            float wave = bandA * 0.65 + bandB * 0.35;

            float grow = 0.15 + 0.85 * saturate(d / (_FadeDistance * 0.5));
            float2 off = float2(wave * _WaveAmp * grow + n1.y * _WaveAmp * 0.35 * grow, 0.0);

            // ---- halkalar ----
            float ringLight = 0.0;   // + sırt (ışık alır), - çukur (gölge)
            [unroll]
            for (int k = 0; k < 16; k++)
            {
                float4 r = _WaterRipples[k];
                float age = _WaterTime - r.z;

                if (age > 0.0 && age < _RippleLife)
                {
                    float2 e = float2(x - r.x, (d - r.y) * _RippleAspect);
                    float dist = length(e);
                    float rad = age * _RippleSpeed * (0.6 + 0.4 * r.w);
                    float life = 1.0 - age / _RippleLife;
                    float band = (dist - rad) / _RippleWidth;
                    float ring = exp(-band * band) * life * life * r.w;
                    float band2 = (dist - rad * 0.55) / (_RippleWidth * 0.8);
                    ring += exp(-band2 * band2) * life * life * life * r.w * 0.5;

                    off.x += (e.x / max(dist, 0.001)) * ring * _RippleDistort;
                    off.y += (e.y / max(dist, 0.001)) / _RippleAspect * ring * _RippleDistort * 0.5;
                    // Halkanın dış yüzü ışığı yakalar, iç yüzü kararır (gerçek dalgacık gibi).
                    float slope = -band * exp(-band * band) * life * life * r.w;
                    ringLight += slope;
                }
            }

            // ---- yansıma ----
            half3 base = lerp(_ShallowColor.rgb, _DeepColor.rgb, fade) * lerp(half3(1,1,1), _ProjectM_WaterMul.rgb, _ProjectM_TodOn);
            half3 lightC = lerp(_MoonColor.rgb, _ProjectM_LightColor.rgb, _ProjectM_TodOn);
            half3 lineC = lerp(_LineColor.rgb, _ProjectM_LightColor.rgb * 0.8h, _ProjectM_TodOn);
            half glint = lerp(1.0h, _ProjectM_Glint.x, _ProjectM_TodOn);
            half3 col = base;

            if (_WaterHasRefl > 0.5)
            {
                float2 p = float2(x + off.x, _WaterY + d + off.y);   // aynalanmış nokta
                float b = _Blur * d;
                float4 rA = SampleRefl(p);
                float4 rB = SampleRefl(p + float2( b, 0));
                float4 rC = SampleRefl(p + float2(-b, 0));
                float4 refl = rA * 0.5 + rB * 0.25 + rC * 0.25;

                // Bant aralarında yansıma hafifçe kırılır (yatay şeritler).
                float slice = 1.0 - _BandContrast + _BandContrast * smoothstep(-0.3, 0.6, bandA);

                half amount = saturate(_ReflStrength) * lerp(1.0, 0.2, saturate(d / max(_ReflFadeDistance, 0.01))) * refl.a;
                col = lerp(base, refl.rgb * _ReflectTint.rgb * slice * max(_ReflStrength, 1.0), amount);
            }

            // ---- ince parlak çizgiler (dalga sırtları) ----
            float crest = pow(saturate(bandA * 0.5 + 0.5), 24.0) * saturate(n2.x * 1.5 + 0.6);
            col += lineC * crest * _Streaks * 0.5 * near;

            // ---- ay yolu: yatay kısa çizgi pırıltıları ----
            float w = _MoonWidth * (1.0 + d * _MoonSpread);
            float across = (x - _WaterMoonX) / w;
            float mask = exp(-across * across);
            // Yumuşak, yatay ışık çizgileri (düşük frekanslı gürültü: pürüz/gren yok).
            float ga = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, float2(x * 0.03 + t * 0.01, ph * 0.04 - t * 0.03)).r;
            float gb = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, float2(x * 0.05 - t * 0.013, ph * 0.07 - t * 0.02)).g;
            float row = pow(saturate(sin(ph * 3.0 - t * 1.2 + ga * 6.0) * 0.5 + 0.5), _SparkleSharp + 1.0);
            float blob = smoothstep(0.45, 0.8, ga * 0.55 + gb * 0.55 - 0.05);
            float sparkle = row * blob * 2.5;
            float vfade = saturate(1.0 - d / (_FadeDistance * 1.2));
            col += lightC * mask * vfade * (sparkle * _MoonIntensity + _MoonGlow) * glint;

            // ---- halka ışığı ----
            // Halka rengi ışıktan: ay yolunda ayın rengi, başka yerde yansıyan gökyüzünün rengi.
            {
                half3 sky = (_WaterHasRefl > 0.5) ? SampleRefl(float2(x, _WaterY + d + 1.5)).rgb : _ShallowColor.rgb;
                half3 lightCol = lerp(sky * 1.2, lightC, saturate(mask * 1.5));
                float hi = max(ringLight, 0.0);
                float lo = max(-ringLight, 0.0);
                col += lightCol * hi * _RippleLight * 0.5 * (0.4 + 0.6 * near);
                col *= 1.0 - lo * _RippleLight * 0.35;
            }

            return half4(col, spr.a);
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
            #pragma target 3.0
            ENDHLSL
        }
    }
}
