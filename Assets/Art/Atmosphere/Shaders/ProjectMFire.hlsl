#ifndef PROJECTM_FIRE_INCLUDED
#define PROJECTM_FIRE_INCLUDED

// ATES KENAR ISIGI + SAHTE NORMAL MAP (meşaleler, kamp ateşi).
// FireLights.cs her kare yazar: en fazla 4 ateş.
//   _ProjectM_Fire4Pos[k] = (x, y, yaricap, siddet)   siddet: gece * kesik titreme
//   _ProjectM_Fire4Col[k] = (r, g, b, 0)
// Dogrultu dunya uzayinda; doku uzayina ekran turevleriyle (ddx/ddy) cevrilir, boylece
// donmus kemikler, ters cevrilmis parcalar ve Spine atlasinda donuk paketlenmis bolgeler dogru calisir.

float4 _ProjectM_Fire4Pos[4];
float4 _ProjectM_Fire4Col[4];
float _ProjectM_Fire4Count;

// Dunya ofsetini (wx, wy) doku uv ofsetine cevirir.
float2 PMF_WorldToUV(float2 dUVdx, float2 dUVdy, float2 dWdx, float2 dWdy, float2 w)
{
    // [dWdx dWdy] matrisinin tersi ile ekran adimlarina, sonra uv'ye.
    float det = dWdx.x * dWdy.y - dWdy.x * dWdx.y;
    if (abs(det) < 1e-12) return float2(0, 0);
    float sx = ( dWdy.y * w.x - dWdy.x * w.y) / det;
    float sy = (-dWdx.y * w.x + dWdx.x * w.y) / det;
    return dUVdx * sx + dUVdy * sy;
}

// Isiga bakan kenar (keskin rim) + yumusak kubbe (sahte normal) aydinlatmasi.
//   width  : kenar kalinligi (dunya birimi)
//   soft   : kubbe yumusakligi (dunya birimi, kenardan iceri ne kadar yayilsin)
half3 PMF_FireLight(TEXTURE2D_PARAM(tex, samp), float2 uv, float2 posWS, half alpha,
                    float2 dUVdx, float2 dUVdy, float2 dWdx, float2 dWdy, float width, float soft)
{
    half3 acc = 0;
    if (_ProjectM_Fire4Count < 0.5 || alpha < 0.01h) return acc;

    [unroll]
    for (int k = 0; k < 4; k++)
    {
        float4 f = _ProjectM_Fire4Pos[k];
        float2 d = f.xy - posWS;
        float dist = length(d);
        if (k < (int)_ProjectM_Fire4Count && dist < f.z && f.w > 0.001)
        {

        float2 dir = d / max(dist, 1e-4);
        float att = 1.0 - dist / f.z;
        att *= att;
        // Ates siluetin icinde/hemen arkasindaysa (ust uste binme) yuzey patlamasin: sadece kenar kalsin.
        float nearK = smoothstep(0.8, 3.5, dist);

        // Keskin kenar: isiga dogru komsular bossa yanar.
        float2 st = PMF_WorldToUV(dUVdx, dUVdy, dWdx, dWdy, dir * width);
        half a1 = SAMPLE_TEXTURE2D_GRAD(tex, samp, uv + st * 0.5, dUVdx, dUVdy).a;
        half a2 = SAMPLE_TEXTURE2D_GRAD(tex, samp, uv + st, dUVdx, dUVdy).a;
        half rim = saturate(1.0h - (a1 * 0.55h + a2 * 0.45h));

        // Yumusak kubbe (sahte normal map): genis yaricapta alfa egimi -> yuzey normali.
        float2 sx = PMF_WorldToUV(dUVdx, dUVdy, dWdx, dWdy, float2(soft, 0));
        float2 sy = PMF_WorldToUV(dUVdx, dUVdy, dWdx, dWdy, float2(0, soft));
        half gx = SAMPLE_TEXTURE2D_GRAD(tex, samp, uv - sx, dUVdx, dUVdy).a - SAMPLE_TEXTURE2D_GRAD(tex, samp, uv + sx, dUVdx, dUVdy).a;
        half gy = SAMPLE_TEXTURE2D_GRAD(tex, samp, uv - sy, dUVdx, dUVdy).a - SAMPLE_TEXTURE2D_GRAD(tex, samp, uv + sy, dUVdx, dUVdy).a;
        half2 g = half2(gx, gy);                 // disa dogru bakan egim
        half gl = length(g);
        half dome = gl > 0.001h ? saturate(dot(g / gl, (half2)dir)) * saturate(gl * 0.9h) : 0.0h;

        half k1 = rim * lerp(0.45h, 1.0h, (half)nearK) + dome * 0.55h * (half)nearK;
        acc += (half3)_ProjectM_Fire4Col[k].rgb * k1 * (half)(att * f.w);
        }
    }
    // Yumusak tavan: cok parlak noktada beyaza patlamasin.
    acc = acc / (1.0h + acc * 0.6h);
    return acc * alpha;
}

#endif
