using UnityEngine;

/// <summary>
/// PARRY ÇATIŞMA EFEKTİ (kodla çizilir, sprite/prefab gerekmez).
///
/// Amaç OKUNURLUK: normal vuruş kırmızı/turuncu "kan" dili kullanır, parry ise
/// soğuk beyaz-mavi + altın bir ÇATIŞMA dili: dört uçlu yıldız flaşı, genişleyen
/// halka ve yukarı doğru saçılan kıvılcım çizgileri. Biçim de renk de farklı,
/// bu yüzden bir bakışta "bu parry" diye okunur.
///
/// Zaman: gerçek zamanla (unscaled) oynar. Parry hit-stop'u zamanı neredeyse
/// durdurduğu için, efekt tam o donuk anda görünür.
/// </summary>
public class ParryVFX : MonoBehaviour
{
    // =========================================================
    // API
    // =========================================================

    /// <summary>
    /// Kılıçların çarpıştığı yer: oyuncu ile düşmanın collider merkezlerinin ortası.
    /// </summary>
    public static Vector3 ContactPoint(Transform enemy)
    {
        Vector3 e = CenterOf(enemy);

        if (cachedPlayer == null)
            cachedPlayer = FindFirstObjectByType<PlayerController>();

        if (cachedPlayer == null)
            return e;

        Vector3 p = CenterOf(cachedPlayer.transform);

        return (e + p) * 0.5f;
    }

    public static void Play(
        Vector3 position,
        Vector2 direction,
        string sortingLayer,
        int sortingOrder,
        float scale = 1f
    )
    {
        if (!Application.isPlaying)
            return;

        GameObject root = new GameObject("ParryVFX");
        root.transform.position = position;

        ParryVFX fx = root.AddComponent<ParryVFX>();
        fx.Build(direction, sortingLayer, sortingOrder, scale);
    }

    // =========================================================
    // AYARLAR
    // =========================================================

    private const float Duration = 0.28f;

    private static readonly Color StarStart = Color.white;
    private static readonly Color StarEnd = new Color(1f, 0.82f, 0.35f);
    private static readonly Color RingColor = new Color(0.62f, 0.93f, 1f);
    private static readonly Color SparkColor = new Color(1f, 0.95f, 0.75f);

    private const int SparkCount = 10;

    // =========================================================
    // İÇ
    // =========================================================

    private static PlayerController cachedPlayer;

    private static Sprite starSprite;
    private static Sprite ringSprite;
    private static Sprite lineSprite;
    private static Material spriteMaterial;

    private SpriteRenderer star;
    private SpriteRenderer ring;

    private SpriteRenderer[] sparks;
    private Vector2[] sparkDirs;
    private float[] sparkReach;
    private float[] sparkLength;

    private float age;
    private float scale = 1f;

    private static Vector3 CenterOf(Transform t)
    {
        if (t == null)
            return Vector3.zero;

        Collider2D c = t.GetComponent<Collider2D>();

        if (c == null)
            c = t.GetComponentInChildren<Collider2D>();

        return c != null ? c.bounds.center : t.position + Vector3.up;
    }

    private void Build(
        Vector2 direction,
        string sortingLayer,
        int sortingOrder,
        float s
    )
    {
        scale = Mathf.Max(0.1f, s);

        EnsureAssets();

        ring = MakeRenderer("Ring", ringSprite, sortingLayer, sortingOrder);
        star = MakeRenderer("Star", starSprite, sortingLayer, sortingOrder + 2);

        sparks = new SpriteRenderer[SparkCount];
        sparkDirs = new Vector2[SparkCount];
        sparkReach = new float[SparkCount];
        sparkLength = new float[SparkCount];

        for (int i = 0; i < SparkCount; i++)
        {
            sparks[i] = MakeRenderer("Spark" + i, lineSprite, sortingLayer, sortingOrder + 1);

            // 8 çizgi yukarı doğru yelpaze (-84..+84 derece), 2 çizgi yatay.
            float angle;

            if (i < 8)
                angle = 90f - 84f + 24f * i + Random.Range(-7f, 7f);
            else
                angle = i == 8 ? Random.Range(-6f, 6f) : 180f + Random.Range(-6f, 6f);

            float rad = angle * Mathf.Deg2Rad;

            sparkDirs[i] = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            sparkReach[i] = Random.Range(1.6f, 2.8f) * (i >= 8 ? 1.15f : 1f);
            sparkLength[i] = Random.Range(0.7f, 1.2f);

            sparks[i].transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        Apply(0f);
    }

    private SpriteRenderer MakeRenderer(
        string objName,
        Sprite sprite,
        string sortingLayer,
        int sortingOrder
    )
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(transform, false);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();

        sr.sprite = sprite;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = sortingOrder;

        if (spriteMaterial != null)
            sr.sharedMaterial = spriteMaterial;

        return sr;
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;

        Apply(Mathf.Clamp01(age / Duration));

        if (age >= Duration)
            Destroy(gameObject);
    }

    private static float OutQuad(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }

    private void Apply(float t)
    {
        // ---------------- YILDIZ FLAŞI (ilk ~%50) ----------------

        float ts = Mathf.Clamp01(t / 0.5f);

        float starScale = Mathf.Lerp(0.75f, 1.7f, OutQuad(ts)) * scale * 0.5f;

        star.transform.localScale = new Vector3(starScale, starScale, 1f);

        Color sc = Color.Lerp(StarStart, StarEnd, ts);
        sc.a = ts < 0.25f ? 1f : Mathf.Clamp01(1f - (ts - 0.25f) / 0.75f);
        star.color = sc;

        // ---------------- HALKA ----------------

        float tr = Mathf.Clamp01(t / 0.85f);

        float ringScale = Mathf.Lerp(0.12f, 0.95f, OutQuad(tr)) * scale;

        ring.transform.localScale = new Vector3(ringScale, ringScale, 1f);

        Color rc = RingColor;
        rc.a = 0.9f * Mathf.Pow(1f - tr, 1.5f);
        ring.color = rc;

        // ---------------- KIVILCIMLAR ----------------

        float move = OutQuad(t);

        for (int i = 0; i < SparkCount; i++)
        {
            float start = 0.35f;
            float dist = start + (sparkReach[i] - start) * move;

            sparks[i].transform.localPosition =
                (Vector3)(sparkDirs[i] * dist * scale);

            float len = sparkLength[i] * Mathf.Lerp(1f, 0.1f, t) * scale;

            sparks[i].transform.localScale = new Vector3(len, 0.09f * scale, 1f);

            Color c = SparkColor;
            c.a = 1f - t * t;
            sparks[i].color = c;
        }
    }

    // =========================================================
    // KODLA ÇİZİLEN SPRITE'LAR (bir kez üretilir)
    // =========================================================

    private static void EnsureAssets()
    {
        if (spriteMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");

            if (shader != null)
                spriteMaterial = new Material(shader);
        }

        if (starSprite == null)
            starSprite = BuildStar();

        if (ringSprite == null)
            ringSprite = BuildRing();

        if (lineSprite == null)
            lineSprite = BuildLine();
    }

    private static Texture2D NewTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        return tex;
    }

    // Dört uçlu yıldız: sqrt(|x|) + sqrt(|y|) <= 1 (ince belli, uzun uçlu).
    private static Sprite BuildStar()
    {
        const int size = 33;
        int c = size / 2;

        Texture2D tex = NewTexture(size, size);
        Color32[] px = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float ax = Mathf.Abs(x - c) / (float)c;
                float ay = Mathf.Abs(y - c) / (float)c;

                bool on = Mathf.Sqrt(ax) + Mathf.Sqrt(ay) <= 1f;

                px[y * size + x] = on ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        }

        tex.SetPixels32(px);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
    }

    // İnce halka.
    private static Sprite BuildRing()
    {
        const int size = 64;
        float c = (size - 1) * 0.5f;

        Texture2D tex = NewTexture(size, size);
        Color32[] px = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));

                bool on = d >= 27.5f && d <= 30.5f;

                px[y * size + x] = on ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        }

        tex.SetPixels32(px);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
    }

    // 1 birim uzunluk, 1 birim kalınlık; pivot sol-orta (ölçekle uzunluk/kalınlık verilir).
    private static Sprite BuildLine()
    {
        const int size = 4;

        Texture2D tex = NewTexture(size, size);
        Color32[] px = new Color32[size * size];

        for (int i = 0; i < px.Length; i++)
            px[i] = new Color32(255, 255, 255, 255);

        tex.SetPixels32(px);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0f, 0.5f), 4f);
    }
}
