// Oyuncu saldiri izi (smear) - prosedurel serit mesh icin.
// Renk / parlaklik Impact Mat ile ayni: HDR beyaz (8.57), alpha blend (SrcAlpha / OneMinusSrcAlpha).
// UV.y: 0 = ic kenar (yumusak), 1 = dis kenar (keskin, en parlak). Vertex alpha = kuyruk / sonma.
Shader "ProjectM/SlashSmear"
{
    Properties
    {
        [HDR] _Color ("Color (Impact Mat)", Color) = (8.574, 8.574, 8.574, 1)
        _InnerSoft ("Ic kenar yumusakligi", Range(0.05, 1)) = 0.6
        _OuterSoft ("Dis kenar yumusakligi", Range(0.005, 0.3)) = 0.06
        _CoreBoost ("Dis kenar parlakligi", Range(0, 2)) = 0.6
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "SlashSmear"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _InnerSoft;
                float _OuterSoft;
                float _CoreBoost;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color = IN.color;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float v = IN.uv.y;
                float inner = smoothstep(0.0, _InnerSoft, v);
                float outer = 1.0 - smoothstep(1.0 - _OuterSoft, 1.0, v);
                float a = inner * outer * IN.color.a;
                half3 rgb = _Color.rgb * (1.0 + _CoreBoost * smoothstep(0.55, 1.0, v));
                return half4(rgb, saturate(a * _Color.a));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
