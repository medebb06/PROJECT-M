#ifndef PROJECTM_WORLDGRAY_INCLUDED
#define PROJECTM_WORLDGRAY_INCLUDED

// Global switch: 0 = original colours, 1 = black & white.
// Set from the WorldColorGrade component. Character shaders do NOT include this file, so they keep their colour.
float _WorldGray;

half4 WorldGray(half4 pmg_in)
{
    float3 pmg_rgb = (float3)pmg_in.rgb;
    float pmg_lum = dot(pmg_rgb, float3(0.299, 0.587, 0.114));
    half4 pmg_out = pmg_in;
    pmg_out.rgb = (half3)lerp(pmg_rgb, float3(pmg_lum, pmg_lum, pmg_lum), _WorldGray);
    return pmg_out;
}

#endif
