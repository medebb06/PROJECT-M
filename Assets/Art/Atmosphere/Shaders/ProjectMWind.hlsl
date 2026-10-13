// PROJECT M — Ortak rüzgâr fonksiyonları (SilhouetteRim ve ProjectedShadow aynı hareketi yapsın).
#ifndef PROJECTM_WIND_INCLUDED
#define PROJECTM_WIND_INCLUDED

// Global: AtmosphereLight ayarlar (0 = rüzgâr yok).
float _ProjectM_WindStrength;

// Obje başına (WindSway bileşeni MaterialPropertyBlock ile verir):
//   x = faz, y = objenin dünya taban Y'si, z = dünya yüksekliği, w = hız çarpanı
float4 _WindParams;

// Yükseklik oranı: tabanda 0 (kök sabit), tepede 1.
float WindHeight(float worldY)
{
    return _WindParams.z > 0.0001 ? saturate((worldY - _WindParams.y) / _WindParams.z) : 0.0;
}

// -1..1 arası salınım: iki dalga + yavaş değişen esinti (gust).
float WindSway(float speed)
{
    float t = _Time.y * speed * max(_WindParams.w, 0.01) + _WindParams.x;
    float gust = 0.55 + 0.45 * sin(_Time.y * 0.31 + _WindParams.x * 0.5) * sin(_Time.y * 0.17 + 1.7);
    float s = sin(t) * 0.7 + sin(t * 2.3 + 1.1) * 0.3;
    return s * gust;
}

// Dünya konumunu büker: tepe yana gider ve hafifçe alçalır (yay çizer).
float3 WindBend(float3 w, float amount, float speed, out float h, out float sway)
{
    h = WindHeight(w.y);
    sway = WindSway(speed) * _ProjectM_WindStrength;

    float bend = h * h;
    w.x += sway * bend * amount;
    w.y -= abs(sway) * bend * amount * 0.15;
    return w;
}

#endif
