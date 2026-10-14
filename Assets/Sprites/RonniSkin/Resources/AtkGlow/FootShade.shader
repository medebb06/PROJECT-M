// PROJECT M — Karakterin ayaklarını zemine oturtan "alt gövde gölgelenmesi".
// Karakterin silüeti üstüne, ayak çizgisinden yukarı doğru azalan koyu mavi bir katman çizer.
// Alfa = sprite/mesh alfası x yükseklik gradyanı; yani sadece karakterin kendi piksellerini etkiler,
// arka planı karartmaz. Stencil (bit 32): parçalar üst üste binince gölge iki kat koyulaşmasın.
Shader "ProjectM/FootShade"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _ShadeColor ("Shade Color", Color) = (0.03, 0.06, 0.14, 1)
        _FeetY ("Feet Y (world)", Float) = 0
        _GradHeight ("Gradient Height (world)", Float) = 1
        _Strength ("Strength", Range(0,1)) = 0.5
        _Power ("Falloff Power", Range(0.3,4)) = 1.4
        _IsSprite ("Is Sprite", Float) = 1
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

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Stencil
        {
            Ref 32
            ReadMask 32
            WriteMask 32
            Comp NotEqual
            Pass Replace
        }

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
                half4 _ShadeColor;
                float _FeetY;
                float _GradHeight;
                half _Strength;
                half _Power;
                float _IsSprite;
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
                float worldY : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 pos = v.positionOS;
                half4 col = v.color;

                if (_IsSprite > 0.5)
                {
                    SetUpSpriteInstanceProperties();
                    pos = UnityFlipSprite(pos, unity_SpriteProps.xy);
                    col = v.color * unity_SpriteColor;
                }

                float3 w = TransformObjectToWorld(pos);
                o.worldY = w.y;
                o.positionCS = TransformWorldToHClip(w);
                o.uv = v.uv;
                o.color = col;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a * i.color.a;
                clip(a - 0.04);

                float h = saturate(1.0 - (i.worldY - _FeetY) / max(0.0001, _GradHeight));
                half g = pow(h, _Power) * _Strength;
                return half4(_ShadeColor.rgb, a * g);
            }
            ENDHLSL
        }
    }
}
