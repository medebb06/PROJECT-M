Shader "ProjectM/Aurora"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ColorA ("Alt Renk", Color) = (0.15, 1.0, 0.65, 1)
        _ColorB ("Ust Renk", Color) = (0.25, 0.45, 1.0, 1)
        _Intensity ("Yogunluk", Range(0,3)) = 0.6
        _Speed ("Hiz", Float) = 0.04
        _Scale ("Olcek", Float) = 2.2
        _Curtain ("Perde Sikligi", Float) = 7
        _Height ("Bant Yuksekligi", Range(0.05,1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST; half4 _ColorA; half4 _ColorB; half _Intensity; float _Speed; float _Scale; float _Curtain; half _Height;
            CBUFFER_END

            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; half4 col : COLOR; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half4 col : COLOR; float3 ws : TEXCOORD1; };

            float h21(float2 p){ p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vn(float2 p){ float2 i = floor(p); float2 f = frac(p); f = f*f*(3-2*f);
                return lerp(lerp(h21(i), h21(i+float2(1,0)), f.x), lerp(h21(i+float2(0,1)), h21(i+float2(1,1)), f.x), f.y); }
            float fbm(float2 p){ float v = 0, a = 0.5; for (int k = 0; k < 4; k++){ v += a * vn(p); p *= 2.03; a *= 0.5; } return v; }

            V vert(A v){ V o; o.ws = TransformObjectToWorld(v.pos.xyz); o.pos = TransformWorldToHClip(o.ws); o.uv = v.uv; o.col = v.col; return o; }

            half4 frag(V i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 uv = i.uv;
                float x = uv.x * _Scale;
                // perde ekseni: yavasca kivrilan bir serit
                float bend = fbm(float2(x * 0.8 + t, t * 0.7)) - 0.5;
                float center = 0.45 + bend * 0.35 + sin(x * 1.7 + t * 3.0) * 0.06;
                float d = uv.y - center;
                // altta keskin kenar, yukari dogru uzun sonumlenme (gercek aurora gibi)
                float band = smoothstep(-0.06, 0.0, d) * exp(-max(d, 0) / _Height * 2.2);
                // dikey perde cizgileri
                float rays = fbm(float2(x * _Curtain + bend * 3.0, t * 2.0));
                rays = pow(saturate(rays * 1.6 - 0.25), 1.6);
                float shimmer = 0.75 + 0.25 * sin(t * 40.0 + x * 9.0);
                // yanlarda ve ust/alt kenarda yumusak sonme
                float edge = smoothstep(0.0, 0.18, uv.x) * smoothstep(1.0, 0.82, uv.x) * smoothstep(1.0, 0.75, uv.y) * smoothstep(0.0, 0.12, uv.y);
                half3 col = lerp(_ColorA.rgb, _ColorB.rgb, saturate(d * 2.2 + 0.15));
                half a = band * (0.35 + rays) * shimmer * edge * _Intensity * i.col.a;
                return half4(col * a * i.col.rgb, 0);
            }
            ENDHLSL
        }
    }
}