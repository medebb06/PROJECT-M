// PROJECT M — Karakter isigi (Ronni, Demirci, Spine).
// Gorunum Sprite-Unlit gibi kalir (gece rengi bozulmaz), uzerine:
//   - mesale/fener isigi (2D isik dokusundan, global isik cikarilarak)
//   - ates kenar isigi + sahte normal map (alfa egiminden yumusak kubbe): atese bakan
//     kenar ve yuzeyler kesik kesik titreyen sicak isikla yanar.
Shader "ProjectM/CharacterLit"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [Toggle] _PMA ("Doku premultiplied (Spine)", Float) = 0
        [Toggle] _RcOn ("Yeniden renk acik", Float) = 0
        _RcHue ("Ton kaydirma (derece)", Range(-180,180)) = 0
        _RcSatMul ("Doygunluk carpani", Range(0,6)) = 1
        _RcSatAdd ("Doygunluk ekle", Range(-1,1)) = 0
        _RcGamma ("Parlaklik gamma", Range(0.2,4)) = 1
        _RcValMul ("Parlaklik carpani", Range(0,8)) = 1
        [Toggle] _RcMaskOn ("Sadece belirli tonu etkile", Float) = 0
        _RcMaskHue ("Maske ton merkezi (0-1)", Range(0,1)) = 0
        _RcMaskRange ("Maske genisligi (0-0.5)", Range(0,0.5)) = 0.06
        _RcMaskSoft ("Maske yumusakligi (0-0.5)", Range(0,0.5)) = 0.03
        _PointLit ("Mesale isigi dolgu", Range(0,3)) = 0.7
        _FireRim ("Ates kenar isigi", Range(0,4)) = 1.8
        _FireRimWidth ("Kenar kalinligi (birim)", Range(0.01,0.5)) = 0.09
        _FireSoft ("Kubbe yumusakligi (birim, sahte normal)", Range(0.02,1)) = 0.3

        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Blend One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_0 __

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightVariables.hlsl"
            #include "ProjectMFire.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _ProjectM_Ambient2D;

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _PMA;
                half _PointLit;
                half _FireRim;
                half _FireRimWidth;
                half _FireSoft;
                half _RcOn;
                half _RcHue;
                half _RcSatMul;
                half _RcSatAdd;
                half _RcGamma;
                half _RcValMul;
                half _RcMaskOn;
                half _RcMaskHue;
                half _RcMaskRange;
                half _RcMaskSoft;
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
                float3 positionWS : TEXCOORD1;
                float2 lightUV : TEXCOORD2;
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
                o.lightUV = ComputeScreenPos(o.positionCS / o.positionCS.w).xy;
                return o;
            }

            // Ronni palet sistemi: HSV tabanli yeniden renklendirme (golge/cizgi detayi korunur).
            half3 PMRecolor(half3 c0)
            {
                float3 c = c0;
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;
                float3 hsv = float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
                float3 hsv0 = hsv;
                hsv.x = frac(hsv.x + _RcHue / 360.0);
                hsv.y = saturate(hsv.y * _RcSatMul + _RcSatAdd);
                hsv.z = saturate(pow(max(hsv.z, 1e-5), _RcGamma) * _RcValMul);
                float3 rgb = saturate(abs(frac(hsv.x + float3(1.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0);
                float3 outc = hsv.z * lerp(float3(1, 1, 1), rgb, hsv.y);
                if (_RcMaskOn > 0.5)
                {
                    float dh = abs(frac(hsv0.x - _RcMaskHue + 0.5) - 0.5);
                    float w = 1.0 - smoothstep(_RcMaskRange, _RcMaskRange + max(_RcMaskSoft, 0.0001), dh);
                    w *= smoothstep(0.08, 0.2, hsv0.y);
                    outc = lerp(c, outc, w);
                }
                return half3(outc);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 dUVdx = ddx(i.uv), dUVdy = ddy(i.uv);
                float2 dWdx = ddx(i.positionWS.xy), dWdy = ddy(i.positionWS.xy);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                if (_RcOn > 0.5h) tex.rgb = PMRecolor(tex.rgb);
                half4 c = tex * i.color;
                if (_PMA < 0.5h) c.rgb *= c.a;      // duz alfa -> premultiplied
                if (c.a <= 0.002h) discard;

                half3 albedo = c.rgb / max(c.a, 0.001h);
                half3 add = 0;

            #if USE_SHAPE_LIGHT_TYPE_0
                if (_PointLit > 0.001h)
                {
                    half3 L0 = SAMPLE_TEXTURE2D(_ShapeLightTexture0, sampler_ShapeLightTexture0, i.lightUV).rgb
                             * _ShapeLightBlendFactors0.x * max(_HDREmulationScale, 1.0h);
                    half3 pt = max(L0 - (half3)_ProjectM_Ambient2D.rgb, 0.0h);
                    pt = pt / (1.0h + pt);          // mesalenin dibinde bile beyaza patlamaz
                    add += albedo * pt * _PointLit;
                }
            #endif

                if (_FireRim > 0.001h)
                    add += PMF_FireLight(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.uv, i.positionWS.xy, 1.0h,
                                         dUVdx, dUVdy, dWdx, dWdy, _FireRimWidth, _FireSoft)
                           * lerp(half3(1, 1, 1), albedo * 1.6h + 0.25h, 0.45h) * _FireRim;

                c.rgb += add * c.a;
                // Toplam tavan: orijinal rengin en fazla ~1.8 kati (bloom'u tetikleyip parlamasin).
                c.rgb = min(c.rgb, max(c.rgb - add * c.a, 0.0h) * 1.35h + 0.08h * c.a);
                return c;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
