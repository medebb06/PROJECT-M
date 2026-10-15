using UnityEngine;

/// <summary>
/// SEKIRO TARZI "ACIK" ISARET: dogru parry yapilan dusman birkac saniye KIRMIZI yanip soner;
/// bu surede ona isabet eden ilk NORMAL vurus onu tek vurusta oldurur (infaz).
/// Duz vurusla postur/can kirmak zor, parry ise oldurucu firsat verir.
/// Boss'a uygulanmaz. Oyun baslayinca kendiliginden kurulur.
/// </summary>
public class ParryMarkSystem : MonoBehaviour
{
    [Tooltip("Isaret suresi (sn). Yeni parry sureyi tazeler.")] public float duration = 3.5f;
    [Tooltip("Parry'den hemen sonra gelen otomatik vuruslar infaz sayilmasin (sn).")] public float grace = 0.2f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindAnyObjectByType<ParryMarkSystem>() != null) return;
        var go = new GameObject("ParryMarkSystem");
        DontDestroyOnLoad(go);
        go.AddComponent<ParryMarkSystem>();
    }

    void OnEnable()
    {
        CombatEvents.ParrySucceeded += OnParry;
        CombatEvents.EnemyHit += OnHit;
    }

    void OnDisable()
    {
        CombatEvents.ParrySucceeded -= OnParry;
        CombatEvents.EnemyHit -= OnHit;
    }

    void OnParry(EnemyController e, bool brokeBalance)
    {
        if (e == null || e.IsDead) return;
        if (e.GetComponent<BossController>() != null) return;

        var m = e.GetComponent<ParryMark>();
        if (m == null) m = e.gameObject.AddComponent<ParryMark>();
        m.Refresh(duration, grace);
    }

    void OnHit(EnemyController e, DamageInfo info, HitResult r)
    {
        if (e == null || !r.hit || e.IsDead) return;

        var m = e.GetComponent<ParryMark>();
        if (m == null || !m.Lethal) return;

        Health h = e.GetComponent<Health>();
        if (h == null || h.IsDead) return;

        m.Consume();
        h.TakeDamage(h.MaxHealth);
        CombatCallout.PopupAbove(e, "INFAZ!", new Color(1f, 0.25f, 0.2f), 1.2f);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.6f);
    }
}

public class ParryMark : MonoBehaviour
{
    static Sprite glow;

    float until, lethalFrom;
    SpriteRenderer sr;
    Transform fx;

    public bool Lethal => enabled && Time.time >= lethalFrom && Time.time < until;

    public static bool IsMarked(EnemyController e)
    {
        if (e == null) return false;
        var m = e.GetComponent<ParryMark>();
        return m != null && m.enabled && Time.time < m.until;
    }

    public void Refresh(float duration, float grace)
    {
        until = Time.time + duration;
        lethalFrom = Time.time + grace;
        enabled = true;
        EnsureFx();
    }

    public void Consume()
    {
        until = 0f;
        if (fx != null) Destroy(fx.gameObject);
        fx = null; sr = null;
        enabled = false;
    }

    void EnsureFx()
    {
        if (fx != null) return;

        if (glow == null)
        {
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(S / 2f, S / 2f)) / (S / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            glow = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        }

        var go = new GameObject("~ParryMark");
        fx = go.transform;
        fx.SetParent(transform, false);

        sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = glow;

        string layer = "Default"; int order = 50;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r.gameObject.name.StartsWith("~")) continue;
            layer = r.sortingLayerName;
            order = Mathf.Max(order, r.sortingOrder + 3);
        }
        sr.sortingLayerName = layer;
        sr.sortingOrder = order;
    }

    void Update()
    {
        if (Time.time >= until)
        {
            Consume();
            return;
        }

        if (fx == null) return;

        // Gövdenin ortasi, gövde boyutuna gore.
        var col = GetComponentInChildren<Collider2D>();
        Vector3 c = col != null ? col.bounds.center : transform.position;
        float size = col != null ? Mathf.Max(col.bounds.size.x, col.bounds.size.y) : 2f;
        fx.position = c;

        // Kirmizi, hizli yanip sonen isik. Sure bitmeye yaklasirken hizlanir.
        float left = until - Time.time;
        float speed = left < 1f ? 14f : 8f;
        float p = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed);
        var a = Mathf.Lerp(0.15f, 0.85f, p);
        sr.color = new Color(1f, 0.08f, 0.05f, a);

        float s = size * Mathf.Lerp(1.5f, 2.1f, p);
        float inv = transform.lossyScale.x != 0f ? 1f : 1f;
        fx.localScale = Vector3.one * s / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x)) * inv;
    }
}
