#ifndef PROJECTM_WORLDGRAY_INCLUDED
#define PROJECTM_WORLDGRAY_INCLUDED

// Global grade, set from the WorldColorGrade component. Character shaders do NOT include this file.
// _WorldGray  : 0 = original colours, 1 = black & white
// _WorldGrade : x = brightness-1, y = contrast-1, z = saturation-1, w = vibrance   (all 0 = unchanged)
float _WorldGray;
float4 _WorldGrade;
float4 _WorldGrade2;   // x = black point (+ deeper blacks, - lifted/milky blacks), y = gamma (1 = unchanged, >1 brighter mids)
float4 _WorldShadow;   // rgb = tint offset for shadows
float4 _WorldMid;      // rgb = tint offset for midtones
float4 _WorldHigh;     // rgb = tint offset for highlights
float4 _WorldHue1;     // x,y,z = saturation delta for red, yellow, green
float4 _WorldHue2;     // x,y,z = saturation delta for cyan, blue, magenta

// Yakin objelere ince ay parlamasi (gece). _ProjectM_NightRim = gecelik guc (0 gunduz), renk = ay rengi.
float _ProjectM_NightRim;
float4 _ProjectM_NearRimColor;

// Sprite'in dis hattinda ~widthPx ekran pikseli kalinliginda ince kenar (0..1). lightDir: isiga bakan yon (kenar o tarafta daha parlak).
half PMG_ThinRim(TEXTURE2D_PARAM(pmg_tex, pmg_smp), float2 pmg_uv, half pmg_selfA, float2 pmg_lightDir, float pmg_widthPx)
{
    float2 pmg_px = max(fwidth(pmg_uv), float2(1e-6, 1e-6)) * pmg_widthPx;
    half pmg_ar = SAMPLE_TEXTURE2D(pmg_tex, pmg_smp, pmg_uv + float2(pmg_px.x, 0)).a;
    half pmg_al = SAMPLE_TEXTURE2D(pmg_tex, pmg_smp, pmg_uv - float2(pmg_px.x, 0)).a;
    half pmg_au = SAMPLE_TEXTURE2D(pmg_tex, pmg_smp, pmg_uv + float2(0, pmg_px.y)).a;
    half pmg_ad = SAMPLE_TEXTURE2D(pmg_tex, pmg_smp, pmg_uv - float2(0, pmg_px.y)).a;
    half pmg_edge = saturate((1.0h - (pmg_ar + pmg_al + pmg_au + pmg_ad) * 0.25h) * 2.0h) * pmg_selfA;
    float2 pmg_out = -float2(pmg_ar - pmg_al, pmg_au - pmg_ad);
    float pmg_ol = length(pmg_out);
    half pmg_bias = 0.55h + 0.45h * (pmg_ol > 1e-4 ? saturate(dot(pmg_out / pmg_ol, pmg_lightDir)) : 0.5h);
    return pmg_edge * pmg_bias;
}

half4 WorldGray(half4 pmg_in)
{
    float3 pmg_rgb = (float3)pmg_in.rgb;
    float pmg_lum = dot(pmg_rgb, float3(0.299, 0.587, 0.114));

    // saturation + vibrance (vibrance boosts dull colours more than already vivid ones)
    float pmg_mx = max(max(pmg_rgb.x, pmg_rgb.y), pmg_rgb.z);
    float pmg_mn = min(min(pmg_rgb.x, pmg_rgb.y), pmg_rgb.z);
    float pmg_s0 = (pmg_mx - pmg_mn) / max(pmg_mx, 0.0001);
    float pmg_k = max(0.0, 1.0 + _WorldGrade.z) * (1.0 + _WorldGrade.w * (1.0 - pmg_s0));
    pmg_rgb = pmg_lum + (pmg_rgb - pmg_lum) * pmg_k;

    // per-hue saturation (red, yellow, green, cyan, blue, magenta)
    if (abs(_WorldHue1.x) + abs(_WorldHue1.y) + abs(_WorldHue1.z) + abs(_WorldHue2.x) + abs(_WorldHue2.y) + abs(_WorldHue2.z) > 0.0001)
    {
        float pmg_hx = max(max(pmg_rgb.x, pmg_rgb.y), pmg_rgb.z);
        float pmg_hn = min(min(pmg_rgb.x, pmg_rgb.y), pmg_rgb.z);
        float pmg_d = pmg_hx - pmg_hn;
        float pmg_h = 0.0;
        if (pmg_d > 0.0001)
        {
            if (pmg_hx == pmg_rgb.x) pmg_h = fmod((pmg_rgb.y - pmg_rgb.z) / pmg_d + 6.0, 6.0);
            else if (pmg_hx == pmg_rgb.y) pmg_h = (pmg_rgb.z - pmg_rgb.x) / pmg_d + 2.0;
            else pmg_h = (pmg_rgb.x - pmg_rgb.y) / pmg_d + 4.0;
        }
        float pmg_w0 = saturate(1.0 - min(pmg_h, 6.0 - pmg_h));
        float pmg_w1 = saturate(1.0 - abs(pmg_h - 1.0));
        float pmg_w2 = saturate(1.0 - abs(pmg_h - 2.0));
        float pmg_w3 = saturate(1.0 - abs(pmg_h - 3.0));
        float pmg_w4 = saturate(1.0 - abs(pmg_h - 4.0));
        float pmg_w5 = saturate(1.0 - abs(pmg_h - 5.0));
        float pmg_dl = pmg_w0 * _WorldHue1.x + pmg_w1 * _WorldHue1.y + pmg_w2 * _WorldHue1.z + pmg_w3 * _WorldHue2.x + pmg_w4 * _WorldHue2.y + pmg_w5 * _WorldHue2.z;
        float pmg_hl = dot(pmg_rgb, float3(0.299, 0.587, 0.114));
        pmg_rgb = pmg_hl + (pmg_rgb - pmg_hl) * max(0.0, 1.0 + pmg_dl);
    }

    // contrast around a mid-dark pivot, then brightness
    pmg_rgb = (pmg_rgb - 0.4) * max(0.0, 1.0 + _WorldGrade.y) + 0.4;
    pmg_rgb = pmg_rgb * max(0.0, 1.0 + _WorldGrade.x);
    pmg_rgb = max(pmg_rgb, float3(0.0, 0.0, 0.0));
    pmg_rgb = (pmg_rgb - _WorldGrade2.x) / max(1.0 - _WorldGrade2.x, 0.05);
    pmg_rgb = max(pmg_rgb, float3(0.0, 0.0, 0.0));
    float pmg_gm = (_WorldGrade2.y > 0.001) ? _WorldGrade2.y : 1.0;
    pmg_rgb = pow(pmg_rgb, float3(1.0 / pmg_gm, 1.0 / pmg_gm, 1.0 / pmg_gm));

    // shadows / midtones / highlights colour balance
    float pmg_lb = saturate(dot(pmg_rgb, float3(0.299, 0.587, 0.114)));
    float pmg_sh = 1.0 - smoothstep(0.0, 0.5, pmg_lb);
    float pmg_hi = smoothstep(0.5, 1.0, pmg_lb);
    float pmg_md = 1.0 - pmg_sh - pmg_hi;
    pmg_rgb += _WorldShadow.rgb * pmg_sh + _WorldMid.rgb * pmg_md + _WorldHigh.rgb * pmg_hi;
    pmg_rgb = max(pmg_rgb, float3(0.0, 0.0, 0.0));

    float pmg_l2 = dot(pmg_rgb, float3(0.299, 0.587, 0.114));
    half4 pmg_out = pmg_in;
    pmg_out.rgb = (half3)lerp(pmg_rgb, float3(pmg_l2, pmg_l2, pmg_l2), _WorldGray);
    return pmg_out;
}

#endif
