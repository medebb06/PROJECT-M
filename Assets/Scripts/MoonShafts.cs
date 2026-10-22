using UnityEngine;

/// <summary>
/// AY IŞIĞI HÜZMELERİ: aydan ağaç aralarına inen yumuşak, çapraz ışık şeritleri.
/// Toplamalı (additive) sprite'lar; yavaşça nefes alır, bulut geçiyormuş gibi kısılıp açılır,
/// sadece gece görünür (_ProjectM_NightRim). Yağmur çizgileri hüzmenin içinden geçerken parlar gibi durur.
/// [ATMOSPHERE] altında bir objeye ekle (oyunda kurulur).
/// </summary>
public class MoonShafts : MonoBehaviour
{
    public Transform moon;
    [Range(0f, 1f)] public float intensity = 0.22f;
    public Color color = new Color(0.62f, 0.76f, 1f, 1f);
    public int sortingOrder = -35;
    public float length = 34f;

    [System.Serializable]
    public struct Shaft
    {
        public float angle;     // derece, 0 = dik asagi, + = saga dogru
        public float width;
        public float offsetX;   // aya gore yatay kayma
        public float alpha;
        public float speed;
    }

    public Shaft[] shafts =
    {
        new Shaft { angle = -14f, width = 4.5f, offsetX = -1.5f, alpha = 1.0f, speed = 0.21f },
        new Shaft { angle = -6f,  width = 2.2f, offsetX =  1.2f, alpha = 0.8f, speed = 0.33f },
        new Shaft { angle = 6f,   width = 6.5f, offsetX =  3.5f, alpha = 0.55f, speed = 0.17f },
        new Shaft { angle = 15f,  width = 3.0f, offsetX =  6.0f, alpha = 0.6f, speed = 0.27f },
    };

    private SpriteRenderer[] srs;
    private static Sprite shaftSprite;

    private void Start()
    {
        if (moon == null)
        {
            GameObject m = GameObject.Find("Moon");
            if (m != null) moon = m.transform;
        }

        Shader sh = Shader.Find("ProjectM/SpriteAdditive");
        Material mat = sh != null ? new Material(sh) { name = "MoonShaft" } : null;

        srs = new SpriteRenderer[shafts.Length];
        for (int i = 0; i < shafts.Length; i++)
        {
            GameObject go = new GameObject("~MoonShaft" + i);
            go.transform.SetParent(transform, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ShaftSprite();
            if (mat != null) sr.sharedMaterial = mat;
            sr.sortingOrder = sortingOrder;
            srs[i] = sr;
        }
    }

    private static Sprite ShaftSprite()
    {
        if (shaftSprite != null) return shaftSprite;

        const int W = 64, H = 256;
        Texture2D t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        for (int y = 0; y < H; y++)
        {
            float v = y / (H - 1f);                         // 1 = ust (ay), 0 = alt
            float along = Mathf.SmoothStep(0f, 1f, v * 3f) * Mathf.Pow(v, 0.6f);   // ust guclu, asagi soner
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W * 2f - 1f;
                float spread = Mathf.Lerp(1.6f, 0.9f, v);  // asagida genisler
                float across = Mathf.Exp(-(u * u) * 4.5f * spread);
                float a = across * along;
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        t.Apply(false, true);
        // Pivot ust ortada: sprite aydan asagi uzanir.
        shaftSprite = Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 1f), H / 1f);
        return shaftSprite;
    }

    private void LateUpdate()
    {
        if (srs == null) return;

        Vector3 mp = moon != null ? moon.position : (Vector3)(Vector4)Shader.GetGlobalVector("_ProjectM_MoonPos");
        float night = Shader.GetGlobalFloat("_ProjectM_NightRim");
        float t = Time.time;

        for (int i = 0; i < srs.Length; i++)
        {
            Shaft s = shafts[i];
            SpriteRenderer sr = srs[i];

            sr.transform.position = new Vector3(mp.x + s.offsetX, mp.y + 1f, 0f);
            sr.transform.rotation = Quaternion.Euler(0f, 0f, s.angle);
            // 1 birim sprite yuksekligi -> length; genislik: 64/256 orani duzeltilir.
            sr.transform.localScale = new Vector3(s.width / 0.25f, length, 1f);

            // Bulut gecisi: yavas, duzensiz nefes.
            float breathe = 0.55f + 0.45f * Mathf.PerlinNoise(t * s.speed, i * 3.7f);
            float a = intensity * s.alpha * breathe * night;
            sr.color = new Color(color.r, color.g, color.b, a);
            sr.enabled = a > 0.002f;
        }
    }
}
