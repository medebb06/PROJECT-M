using UnityEngine;

/// <summary>
/// Bulutlar: (1) gunun saatine gore renk (sabah pembe, ogle beyaz, aksam turuncu, gece mavi-gri),
/// (2) oyunda cok yavas yatay suzulme (her bulut farkli hizda, uc uca sarilir).
/// [WORLD]/Clouds objesine konur; cocuk SpriteRenderer'lari boyar/kaydirir.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(10050)]
public class CloudLife : MonoBehaviour
{
    [Header("Suzulme (sadece Play)")]
    public bool drift = true;
    [Tooltip("Ortalama hiz, birim/sn.")]
    public float speed = 0.35f;
    [Tooltip("Suzulme alani (yerel x). Disina cikan bulut diger uca sarilir.")]
    public float minX = -75f;
    public float maxX = 95f;

    [Header("Renk")]
    public bool tintByTime = true;
    [Range(0f, 1f)] public float dayAlpha = 0.45f;

    [Header("Daglar (gece koyulasir)")]
    public Transform mountains;
    [Range(0.1f,1f)] public float mountainNight = 0.3f;
    public Color farColor = new Color(0.78f,0.80f,0.95f);
    public Color midColor = new Color(0.62f,0.70f,0.88f);
    public Color nearColor = new Color(0.46f,0.60f,0.76f);

    private TimeOfDay tod;

    private struct Stop
    {
        public float h; public Color c; public float a;
        public Stop(float h, Color c, float a) { this.h = h; this.c = c; this.a = a; }
    }

    private static readonly Stop[] Stops =
    {
        new Stop(0f,    new Color(0.62f, 0.70f, 0.95f), 0.80f),
        new Stop(5f,    new Color(0.70f, 0.66f, 0.92f), 0.80f),
        new Stop(6.5f,  new Color(1.00f, 0.76f, 0.70f), 0.85f),
        new Stop(8.5f,  new Color(1.00f, 0.95f, 0.90f), 0.95f),
        new Stop(12f,   new Color(1.00f, 1.00f, 1.00f), 1.00f),
        new Stop(16f,   new Color(1.00f, 0.97f, 0.90f), 0.95f),
        new Stop(18f,   new Color(1.00f, 0.62f, 0.46f), 0.90f),
        new Stop(19.5f, new Color(0.72f, 0.50f, 0.70f), 0.75f),
        new Stop(21f,   new Color(0.60f, 0.68f, 0.94f), 0.80f),
        new Stop(24f,   new Color(0.62f, 0.70f, 0.95f), 0.80f),
    };

    private void LateUpdate()
    {
        if (tod == null)
            tod = FindObjectOfType<TimeOfDay>();

        if (tintByTime && tod != null)
            { Tint(Mathf.Repeat(tod.hour, 24f)); TintMountains(Mathf.Repeat(tod.hour, 24f)); }

        if (drift && Application.isPlaying)
            Drift();
    }

    private void Tint(float h)
    {
        int i = 0;

        while (i < Stops.Length - 2 && h > Stops[i + 1].h)
            i++;

        Stop a = Stops[i], b = Stops[i + 1];
        float t = Mathf.InverseLerp(a.h, b.h, h);
        Color c = Color.Lerp(a.c, b.c, t);
        c.a = Mathf.Lerp(a.a, b.a, t) * dayAlpha;

        foreach (SpriteRenderer r in GetComponentsInChildren<SpriteRenderer>(true))
            r.color = c;
    }

    private void TintMountains(float h)
    {
        if (mountains == null) { var g = GameObject.Find("MountainsBG"); if (g != null) mountains = g.transform; if (mountains == null) return; }
        float night = h <= 5f ? 1f : h < 7f ? 1f - Mathf.InverseLerp(5f, 7f, h) : h <= 19f ? 0f : h < 21f ? Mathf.InverseLerp(19f, 21f, h) : 1f;
        float f = Mathf.Lerp(1f, mountainNight, night);
        foreach (SpriteRenderer r in mountains.GetComponentsInChildren<SpriteRenderer>(true))
        {
            Transform t = r.transform; while (t.parent != null && t.parent != mountains) t = t.parent;
            Color b = t.name == "Far" ? farColor : t.name == "Mid" ? midColor : nearColor;
            r.color = new Color(b.r * f, b.g * f, b.b * Mathf.Lerp(1f, 1.15f, night) * f, 1f);
        }
    }

    private void Drift()
    {
        int n = transform.childCount;

        for (int i = 0; i < n; i++)
        {
            Transform t = transform.GetChild(i);
            float k = 0.6f + 0.8f * (Mathf.Abs(Mathf.Sin((i + 1) * 12.9898f) * 43758.5453f) % 1f);
            Vector3 p = t.localPosition;
            p.x += speed * k * Time.deltaTime;

            if (p.x > maxX)
                p.x -= (maxX - minX);

            t.localPosition = p;
        }
    }
}
