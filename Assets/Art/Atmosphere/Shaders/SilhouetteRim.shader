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

        [Header(Rounded Light (normal map))]
        [NoScaleOffset] _NormalMap ("Normal Map (otomatik, sprite'tan)", 2D) = "bump" {}
        _NormalIntensity ("Rounded Light Intensity", Range(0,4)) = 0
        _NormalPower ("Rounded Light Falloff", Range(0.5,6)) = 1.6
        _LightBack ("Light From Behind", Range(0,2)) = 0.35
        _CoreShadow ("Core Shadow (ışıksız taraf)", Range(0,1)) = 0.2

        [Header(Wind)]
        _WindAmount ("Sway Amount (tepe, dünya birimi)", Range(0,2)) = 0
        _WindSpeed ("Sway Speed", Range(0,6)) = 1
        _FlutterAmount ("Leaf Flutter (px)", Range(0,8)) = 0
        _FlutterSpeed ("Leaf Flutter Speed", Range(0,12)) = 3
        _WindLight ("Light Reacts To Sway", Range(0,2)) = 0.6

        [Header(Moon (bulutlar icin))]
        _UseMoon ("Isik kaynagi: 0 = ufuk isigi, 1 = ay", Range(0,1)) = 0
        _MoonRimRadius ("Ay isiginin etkisi (birim)", Float) = 16
        _MoonRimMin ("Aydan uzakta kenar isigi (0-1)", Range(0,1)) = 0.3
        _MoonFade ("Ayin yakininda saydamlasma", Range(0,1)) = 0
        _MoonFadeRadius ("Saydamlasma yaricapi (birim)", Float) = 5
        _NearRim ("Yakin ay parlamasi (0 = yok)", Range(0,2)) = 0

        [Header(Cloth Sway)]
        _SwayAmp ("Bez sallama (uv binde, 0 = kapali)", Range(0,12)) = 0
        _SwaySpeed ("Bez sallama hizi", Range(0,4)) = 1.2

        [Header(Depth Blur)]
        _Blur ("Blur (ekran pikseli, DepthBlur ayarlar)", Range(0,8)) = 0

        [Header(Mesale ve noktasal isik)]
        _PointLit ("Noktasal isik tepkisi (0 = etkilenmez)", Range(0,3)) = 1
        _FireRim ("Ates kenar isigi (mesaleye bakan kenarlar)", Range(0,4)) = 1.4
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
#include "Assets/Art/Atmosphere/Shaders/WorldGray.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightVariables.hlsl"

        // Global Light 2D'nin etkin rengi (renk * siddet). Ambient2DSync her kare ayarlar.
        // Isik dokusundan bu cikarilinca geriye sadece mesale/fener gibi noktasal isiklar kalir.
        float4 _ProjectM_Ambient2D;


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
            half _NormalIntensity;
            half _NormalPower;
            half _LightBack;
            half _CoreShadow;
            half _WindAmount;
            half _WindSpeed;
            half _FlutterAmount;
            half _FlutterSpeed;
            half _WindLight;
            half _UseMoon;
            half _MoonRimRadius;
            half _MoonRimMin;
            half _MoonFade;
            half _MoonFadeRadius;
            half _NearRim;
            half _Blur;
            half _SwayAmp;
            half _SwaySpeed;
            half _PointLit;
            half _FireRim;
        CBUFFER_END

        #include "ProjectMWind.hlsl"
        #include "ProjectMFire.hlsl"

        TEXTURE2D(_NormalMap);
        SAMPLER(sampler_NormalMap);

        // Işık konumu (dünya XY). AtmosphereLight her kare ayarlar (kamerayı takip eder).
        float4 _ProjectM_LightPos;
        float4 _ProjectM_MoonPos;   // AtmosphereLight/TimeOfDay: baskın ışığın (güneş/ay) dünya konumu

        // Saat dilimi (TimeOfDay). _ProjectM_TodOn = 0 ise hiçbiri uygulanmaz.
        float _ProjectM_TodOn;
        float4 _ProjectM_BodyTint;    // rgb: gövde çarpanı
        float4 _ProjectM_BodyLift;    // rgb: gövdeye eklenen (gündüz siluetler açılır)
        float4 _ProjectM_HazeMul;     // rgb: pus rengi çarpanı
        float4 _ProjectM_LightColor;  // rgb: ışık rengi, a: kenar ışığı gücü

        // Sprite'ın gerçek doku boyutu (SpriteRenderer her çizimde ayarlar; materyal CBUFFER'ında olmamalı).
        float4 _MainTex_TexelSize;

        // Derinlik bulanıklığı: ekran pikseli yarıçaplı disk blur (premultiplied, kenarlarda kararma yok).
        half4 PM_BlurSample(float2 uv)
        {
            half4 c0 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
            if (_Blur < 0.02h) return c0;
            float2 px = max(fwidth(uv), float2(1e-6, 1e-6)) * _Blur;
            half4 acc = half4(c0.rgb * c0.a, c0.a);
            [unroll] for (int k = 0; k < 16; k++)
            {
                float r = sqrt((k + 0.5) / 16.0);
                float th = k * 2.39996323;
                half4 s = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(cos(th), sin(th)) * r * px);
                acc += half4(s.rgb * s.a, s.a);
            }
            acc /= 17.0h;
            return half4(acc.rgb / max(acc.a, 0.001h), acc.a);
        }

        bool PM_IsCloth(half4 s)
        {
            half l = dot(s.rgb, half3(0.299h, 0.587h, 0.114h));
            return s.a > 0.5h && s.b > s.r * 1.3h + 0.01h && l < 0.16h;
        }

        // Sadece bez (koyu mavi-yesil) piksellerini sallar; direk, sandik, balta vs. sabit kalir.
        half4 PM_SwaySample(float2 uv)
        {
            half4 o = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
            if (o.a > 0.5h && !PM_IsCloth(o)) return o;
            float w = smoothstep(0.12, 0.85, uv.y);
            float t = _Time.y * _SwaySpeed;
            float2 d = float2(sin(t + uv.y * 5.0 + uv.x * 7.0) * 0.7 + sin(t * 2.3 + uv.x * 13.0) * 0.3,
                              cos(t * 0.8 + uv.x * 6.0) * 0.25) * (_SwayAmp * 0.001) * w;
            half4 s = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + d);
            return PM_IsCloth(s) ? s : half4(0, 0, 0, 0);
        }

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
            float2 flip : TEXCOORD2;
            float2 wind : TEXCOORD3;   // x = yükseklik oranı, y = salınım (-1..1)
            float2 lightUV : TEXCOORD4;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings vert(Attributes v)
        {
            Varyings o = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

            SetUpSpriteInstanceProperties();
            v.positionOS = UnityFlipSprite(v.positionOS, unity_SpriteProps.xy);

            float3 w = TransformObjectToWorld(v.positionOS);

            // Rüzgâr: kök sabit, tepe yana salınır (obje başına faz/hız WindSway'den).
            float h = 0, sway = 0;
            if (_WindAmount > 0.0001)
                w = WindBend(w, _WindAmount, _WindSpeed, h, sway);

            o.wind = float2(h, sway);
            o.positionWS = w;
            o.positionCS = TransformWorldToHClip(o.positionWS);
            o.uv = v.uv;
            o.color = v.color * _Color * unity_SpriteColor;
            o.flip = unity_SpriteProps.xy;
            o.lightUV = ComputeScreenPos(o.positionCS / o.positionCS.w).xy;
            return o;
        }

        half4 frag(Varyings i) : SV_Target
        {
            // Ates kenar isigi icin ekran turevleri (discard'dan ONCE).
            float2 pmfUVdx = ddx(i.uv), pmfUVdy = ddy(i.uv);
            float2 pmfWdx = ddx(i.positionWS.xy), pmfWdy = ddy(i.positionWS.xy);

            // Yaprak titremesi: üst kısımlar küçük, düzensiz kıpırdar.
            float2 uv = i.uv;
            half flutter = 0;

            if (_FlutterAmount > 0.001h && i.wind.x > 0.001)
            {
                float ft = _Time.y * _FlutterSpeed * max(_WindParams.w, 0.01) + _WindParams.x * 3.1;
                float fx = sin(ft + i.positionWS.x * 2.1 + i.positionWS.y * 1.3) * cos(ft * 0.7 + i.positionWS.y * 2.7);
                float fy = sin(ft * 1.3 + i.positionWS.x * 1.7 + 0.8) * 0.6;
                flutter = fx;
                uv += float2(fx, fy) * _MainTex_TexelSize.xy * _FlutterAmount * i.wind.x * max(_ProjectM_WindStrength, 0);
            }

            half4 tex = (_SwayAmp > 0.001h) ? PM_SwaySample(uv) : PM_BlurSample(uv);
            half4 c = tex * i.color;

            if (c.a <= 0.001h)
                discard;

            // Atmosferik pus: uzaktakiler ve alt kısım havaya karışır.
            half haze = saturate(_Haze + _HazeBottom * (1.0h - uv.y));
            half3 body = lerp(c.rgb, c.rgb * _ProjectM_BodyTint.rgb + _ProjectM_BodyLift.rgb, _ProjectM_TodOn);
            half3 hazeC = lerp(_HazeColor.rgb, _HazeColor.rgb * _ProjectM_HazeMul.rgb, _ProjectM_TodOn);
            half3 rgb = lerp(body, hazeC, haze);

            // Kenar ışığı: ışığa doğru bakan komşu pikseller boşsa kenar yanar.
            float2 lightXY = lerp(_ProjectM_LightPos.xy, _ProjectM_MoonPos.xy, _UseMoon);
            float2 dir = lightXY - i.positionWS.xy;
            float len = max(length(dir), 1e-4);
            dir /= len;

            // Bulutlar: ışık aya yaklaştıkça güçlenir (arkadan aydınlanma / gümüş kenar), uzakta sönük.
            half moonK = lerp(1.0h, lerp(_MoonRimMin, 1.0h, pow(saturate(1.0 - len / max(_MoonRimRadius, 0.01)), 1.5)), _UseMoon);
            dir = normalize(dir + float2(0, _RimTop)); // üst kenarlar biraz daha yansın

            float2 stepUV = dir * _MainTex_TexelSize.xy * _RimWidth;

            half a1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + stepUV * 0.33).a;
            half a2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + stepUV * 0.66).a;
            half a3 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + stepUV).a;

            half empty = 1.0h - (a1 * 0.5h + a2 * 0.3h + a3 * 0.2h);
            half rim = saturate(empty) * tex.a * (1.0h - saturate(_Blur * 0.35h));

            // Işığa doğru eğilince kenar parlar, ışıktan uzaklaşınca söner.
            half towardLight = (half)(i.wind.y * i.wind.x * dir.x);
            half windGlow = saturate(1.0h + _WindLight * towardLight * 1.5h + _WindLight * flutter * 0.15h * i.wind.x);

            // Kenar ışığı rengi saat dilimine göre: ışığın rengi, materyalin parlaklığı korunarak.
            half rimLum = dot(_RimColor.rgb, half3(0.3h, 0.59h, 0.11h));
            half3 rimRGB = lerp(_RimColor.rgb, _ProjectM_LightColor.rgb * (rimLum * _ProjectM_LightColor.a), _ProjectM_TodOn);

            rgb += rimRGB * rim * _RimIntensity * windGlow * moonK;
            if (_NearRim > 0.001h && _ProjectM_NightRim > 0.001)
                rgb += _ProjectM_NearRimColor.rgb * PMG_ThinRim(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), uv, tex.a, float2(0, 1), 2.0) * _NearRim * _ProjectM_NightRim * 0.7;

            // Yuvarlak ışık: sprite'tan üretilen kubbe normal map'i, arkadan/yandan gelen ışıkla.
            // Işığa bakan kenarlar yumuşakça parlar, ortası karanlık kalır -> hacim hissi.
            if (_NormalIntensity > 0.001h)
            {
                half3 n = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv).xyz * 2.0h - 1.0h;
                n.xy *= i.flip;                       // çevrilmiş sprite'ta normal de çevrilir

                // Eğilen yaprak yüzeyi ışığa göre döner: ışık tepkisi doğal olarak değişir.
                n.x += (half)(i.wind.y * i.wind.x) * _WindLight * 0.15h;   // titreme yok: yüzey dalgalanmaz
                n = normalize(n);

                half3 L = normalize(half3(dir, -_LightBack));
                half lit = pow(saturate(dot(n, L)), _NormalPower);

                // Işığın tersine bakan yamaç biraz koyulaşır.
                half away = saturate(dot(n.xy, -dir)) * (1.0h - n.z);
                rgb *= 1.0h - _CoreShadow * away;

                rgb += rimRGB * lit * _NormalIntensity * tex.a * windGlow * moonK;
            }

            // Ates kenar isigi: ay isigi gibi ama mesaleye bakan kenarlar yanar, alev titredikce kesik kesik;
            // genis yaricapta alfa egiminden yumusak kubbe (sahte normal map). ProjectMFire.hlsl
            if (_FireRim > 0.001h)
                rgb += PMF_FireLight(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), uv, i.positionWS.xy, tex.a,
                                     pmfUVdx, pmfUVdy, pmfWdx, pmfWdy, 0.18, 0.45) * _FireRim * (1.0h - haze * 0.85h);

            // Mesale/fener isigi: 2D isik dokusundan global isik cikarilir, kalan noktasal isik
            // siluetin kendi rengiyle carpilip eklenir (gece mesale yakinindaki agac/esya sicak aydinlanir,
            // golge dusuren nesnelerin arkasi karanlik kalir). Pus arttikca etkisi azalir.
#if USE_SHAPE_LIGHT_TYPE_0
            if (_PointLit > 0.001h)
            {
                half3 L0 = SAMPLE_TEXTURE2D(_ShapeLightTexture0, sampler_ShapeLightTexture0, i.lightUV).rgb
                         * _ShapeLightBlendFactors0.x * max(_HDREmulationScale, 1.0h);
                half3 pt = max(L0 - (half3)_ProjectM_Ambient2D.rgb, 0.0h);
                pt = pt / (1.0h + pt * 0.5h);
                rgb += c.rgb * pt * _PointLit * (1.0h - haze * 0.85h);
            }
#endif

            // Ayın önünden geçen bulut yarı saydamlaşır: ay içinden sızar.
            half fadeA = 1.0h - _MoonFade * (1.0h - smoothstep(0.0h, _MoonFadeRadius, (half)len)) * _UseMoon;

            return WorldGray(half4(rgb, c.a * fadeA));
        }
        ENDHLSL

        // 2D Renderer'in isikli pass'i: isik dokularini (mesale vs.) okuyabilmek icin.
        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_0 __
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
