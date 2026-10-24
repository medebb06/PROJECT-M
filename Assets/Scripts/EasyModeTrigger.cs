using System.Collections;
using UnityEngine;

// KOLAY MOD TETİKLEYİCİSİ
// Dünyada duran, büyük, kendi ekseninde dönen kırmızı "engellenemez saldırı" yıldızı
// (düşmanın kılıç sapındaki uyarı yıldızının büyük hali). Kılıçla KESİNCE Kolay Mod başlar:
// Kuronami'nin aynısı (sonsuz döngü, 20 öldürmede charm seçimi) ama hasar yok, düşmanlar seyrek gelir.
//
// Konum = bu objenin konumu. Boyut = bu objenin Scale'i (dünya birimi; düşmanın yıldızı 3.5).
// Kuronami / Kolay Mod açıkken ya da kesildikten sonra gizlenir; ana menüye dönünce yeniden görünür.
[RequireComponent(typeof(SpriteRenderer))]
public class EasyModeTrigger : MonoBehaviour
{
    public enum Mode { Easy, Kuronami }

    [Header("Mod")]
    [Tooltip("Easy (mavi yıldız) = Kolay Mod, Kuronami (kırmızı yıldız) = Kuronami. Sprite'ı da buna göre ver.")]
    public Mode mode = Mode.Easy;

    [Header("Görünüm")]
    [Tooltip("Boşsa Resources/EnemyAlerts/alert_unblockable_red yüklenir.")]
    public Sprite starSprite;
    [Tooltip("Saniyede derece (eksi = saat yönü). Düşmanın yıldızı -240.")]
    public float spinDegPerSec = -80f;
    [Range(0f, 0.3f)] public float pulse = 0.08f;
    public float pulseSpeed = 3f;
    public int sortingOrder = 500;
    [Tooltip("Yıldızın arkasındaki yumuşak kırmızı ışıma.")]
    public Color glowColor = new Color(1f, 0.1f, 0.06f, 0.32f);
    public float glowScale = 1.5f;

    [Header("Kesme")]
    [Tooltip("Kılıç kutusuyla çarpışma yarıçapı = yıldız boyutu x bu oran.")]
    public float hitRadiusFraction = 0.42f;
    [Tooltip("Kesildikten kaç sn sonra mod başlasın (gerçek sn).")]
    public float startDelay = 0.6f;

    private SpriteRenderer sr;
    private SpriteRenderer glow;
    private Vector3 baseScale = Vector3.one;
    private bool cut;
    private float t0;

    private static Material unlitMat;
    private static Sprite glowSprite;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (starSprite == null) starSprite = Resources.Load<Sprite>(mode == Mode.Easy ? "EnemyAlerts/alert_combo_blue" : "EnemyAlerts/alert_unblockable_red");
        if (sr.sprite == null) sr.sprite = starSprite;
        sr.sortingOrder = sortingOrder;
        baseScale = transform.localScale;
        // (ışıltı katmanı kaldırıldı: yıldız sahnedeki varsayılan ışıklı materyalle çizilir, düşman uyarı yıldızı gibi)
    }

    private void OnEnable()
    {
        PlayerWeapon.Swung += OnSwing;
        t0 = Time.unscaledTime;
    }

    private void OnDisable()
    {
        PlayerWeapon.Swung -= OnSwing;
    }

    private static Material UnlitMaterial()
    {
        if (unlitMat != null) return unlitMat;
        Shader s = Shader.Find("Sprites/Default");
        if (s == null) return null;
        unlitMat = new Material(s);
        return unlitMat;
    }

    private static Sprite GlowSprite()
    {
        if (glowSprite != null) return glowSprite;
        const int n = 128;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float r = n * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);   // 1 birim çap
        return glowSprite;
    }

    private void BuildGlow()
    {
        Transform existing = transform.Find("Glow");
        GameObject go = existing != null ? existing.gameObject : new GameObject("Glow");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one * glowScale;
        glow = go.GetComponent<SpriteRenderer>();
        if (glow == null) glow = go.AddComponent<SpriteRenderer>();
        glow.sprite = GlowSprite();
        glow.sharedMaterial = UnlitMaterial();
        glow.sortingLayerID = sr.sortingLayerID;
        glow.sortingOrder = sortingOrder - 1;
        glow.color = glowColor;
    }

    private void Update()
    {
        if (cut) return;

        bool show = !Charms.WaveMode;
        if (sr.enabled != show) { sr.enabled = show; if (glow != null) glow.enabled = show; }
        if (!show) return;

        float t = Time.unscaledTime - t0;
        transform.rotation = Quaternion.Euler(0f, 0f, spinDegPerSec * t);   // kendi ekseninde döner
        transform.localScale = baseScale * (1f + pulse * Mathf.Sin(t * pulseSpeed));

        Color g = glowColor;
        g.a *= 0.75f + 0.25f * Mathf.Sin(t * pulseSpeed * 1.7f);
        if (glow != null) glow.color = g;
    }

    // Oyuncu kılıç savurdu: kılıç kutusu yıldızın çevresine (daire) değdiyse yıldız kesilir.
    private void OnSwing(Vector2 center, Vector2 box, Vector2 dir)
    {
        if (cut || sr == null || !sr.enabled || Charms.WaveMode) return;

        Vector2 c = transform.position;
        Vector2 half = box * 0.5f;
        Vector2 closest = new Vector2(
            Mathf.Clamp(c.x, center.x - half.x, center.x + half.x),
            Mathf.Clamp(c.y, center.y - half.y, center.y + half.y));

        float r = hitRadiusFraction * Mathf.Max(baseScale.x, baseScale.y);
        if ((closest - c).sqrMagnitude > r * r) return;

        Cut(dir);
    }

    public void Cut(Vector2 dir)
    {
        if (cut) return;
        cut = true;

        Vector3 p = transform.position;
        if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;

        if (CombatVFXManager.Instance != null) CombatVFXManager.Instance.PlayHealthHit(p, dir);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(1.2f);
        HitClickAudio.PlayEnemyHealthHit();
        CombatCallout.Popup(p + Vector3.up * (baseScale.y * 0.65f), (mode == Mode.Easy ? "KOLAY MOD" : "KURONAMI"), (mode == Mode.Easy ? new Color(0.4f, 0.7f, 1f) : new Color(1f, 0.35f, 0.3f)), 1.6f);

        StartCoroutine(Shatter());
    }

    // Beyaz çakar, hızlanarak dönüp büyür ve söner; startDelay dolunca mod başlar.
    private IEnumerator Shatter()
    {
        const float fade = 0.45f;
        float start = Time.unscaledTime;
        Color c0 = sr.color;
        Color g0 = glow != null ? glow.color : Color.clear;
        float angle = transform.eulerAngles.z;
        bool started = false;

        while (true)
        {
            float t = Time.unscaledTime - start;
            float k = Mathf.Clamp01(t / fade);

            angle += spinDegPerSec * 3f * Time.unscaledDeltaTime;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            transform.localScale = baseScale * Mathf.Lerp(1f, 1.7f, 1f - (1f - k) * (1f - k));

            Color c = Color.Lerp(Color.white, c0, Mathf.Clamp01(k * 6f));
            c.a = c0.a * (1f - k);
            sr.color = c;

            Color g = g0;
            g.a = g0.a * (1f - k);
            if (glow != null) glow.color = g;

            if (!started && t >= startDelay)
            {
                started = true;
                BeginMode();
            }

            if (k >= 1f && started) break;
            yield return null;
        }

        sr.enabled = false;
        if (glow != null) glow.enabled = false;
    }

    private void BeginMode()
    {
        EnemyArena arena = FindAnyObjectByType<EnemyArena>();
        if (arena != null) { if (mode == Mode.Easy) arena.BeginEasy(); else arena.BeginKuronami(); }
        else Debug.LogWarning("MOD TETİKLEYİCİ: sahnede EnemyArena yok, mod başlatılamadı.");
    }
}
