Shader "ProjectM/FocusBlur"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        TEXTURE2D(_BlurTex);
        float4 _BlurDir;      // xy: texel yonu * boyut
        float4 _Focus0;       // x,y: viewport konumu, z,w: ic / dis yaricap (genislik cinsinden)
        float4 _Focus1;
        float4 _FocusParams;  // x: guc (0..1), y: aspect (w/h), z: aktif oyuncu 1, w: aktif ikinci 1
        float4 _FocusShape;   // x: yatay ic, y: yatay dis, z: dikey ic, w: dikey dis (viewport oraninda)

        half4 FragBlur(Varyings i) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            float2 uv = i.texcoord;
            float2 d = _BlurDir.xy;
            half4 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv) * 0.2270270270h;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + d * 1.3846153846) * 0.3162162162h;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - d * 1.3846153846) * 0.3162162162h;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + d * 3.2307692308) * 0.0702702703h;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - d * 3.2307692308) * 0.0702702703h;
            return c;
        }

        float FocusMask(float2 uv, float4 f)
        {
            float2 e = uv - f.xy;
            e.x *= _FocusParams.y;                       // en-boy duzelt
            float2 q = float2(e.x / _FocusShape.y, e.y / _FocusShape.w);
            float2 q0 = float2(e.x / _FocusShape.x, e.y / _FocusShape.z);
            float dOuter = length(q);
            float dInner = length(q0);
            // ic: tam net, dis: tam bulanik
            float t = saturate((length(float2(e.x / _FocusShape.x, e.y / _FocusShape.z)) - 1.0) / max((_FocusShape.y / _FocusShape.x) - 1.0, 0.001));
            return 1.0 - smoothstep(0.0, 1.0, t);
        }

        half4 FragComposite(Varyings i) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            float2 uv = i.texcoord;
            half4 sharp = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            half4 blur = SAMPLE_TEXTURE2D_X(_BlurTex, sampler_LinearClamp, uv);
            float m = 0.0;
            if (_FocusParams.z > 0.5) m = max(m, FocusMask(uv, _Focus0));
            if (_FocusParams.w > 0.5) m = max(m, FocusMask(uv, _Focus1));
            if (_FocusParams.z < 0.5 && _FocusParams.w < 0.5) m = 0.0;
            float k = (1.0 - m) * _FocusParams.x;
            return lerp(sharp, blur, k);
        }
        ENDHLSL

        Pass
        {
            Name "Blur"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlur
            ENDHLSL
        }
        Pass
        {
            Name "Composite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            ENDHLSL
        }
    }
    Fallback Off
}
