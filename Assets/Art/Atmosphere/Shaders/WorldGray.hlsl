#ifndef PROJECTM_WORLDGRAY_INCLUDED
#define PROJECTM_WORLDGRAY_INCLUDED

// Global grade, set from the WorldColorGrade component. Character shaders do NOT include this file.
// _WorldGray  : 0 = original colours, 1 = black & white
// _WorldGrade : x = brightness-1, y = contrast-1, z = saturation-1, w = vibrance   (all 0 = unchanged)
float _WorldGray;
float4 _WorldGrade;
float4 _WorldGrade2;   // x = black point (+ deeper blacks, - lifted/milky blacks), y = gamma (1 = unchanged, >1 brighter mids)

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

    // contrast around a mid-dark pivot, then brightness
    pmg_rgb = (pmg_rgb - 0.4) * max(0.0, 1.0 + _WorldGrade.y) + 0.4;
    pmg_rgb = pmg_rgb * max(0.0, 1.0 + _WorldGrade.x);
    pmg_rgb = max(pmg_rgb, float3(0.0, 0.0, 0.0));
    pmg_rgb = (pmg_rgb - _WorldGrade2.x) / max(1.0 - _WorldGrade2.x, 0.05);
    pmg_rgb = max(pmg_rgb, float3(0.0, 0.0, 0.0));
    float pmg_gm = (_WorldGrade2.y > 0.001) ? _WorldGrade2.y : 1.0;
    pmg_rgb = pow(pmg_rgb, float3(1.0 / pmg_gm, 1.0 / pmg_gm, 1.0 / pmg_gm));

    float pmg_l2 = dot(pmg_rgb, float3(0.299, 0.587, 0.114));
    half4 pmg_out = pmg_in;
    pmg_out.rgb = (half3)lerp(pmg_rgb, float3(pmg_l2, pmg_l2, pmg_l2), _WorldGray);
    return pmg_out;
}

#endif
