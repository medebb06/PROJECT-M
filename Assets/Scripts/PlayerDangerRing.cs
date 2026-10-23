using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "DIKKAT" HARESI: bir dusman saldirisi (windup / uyari) sirasinda oyuncuya ISABET edecek menzildeyse,
/// oyuncunun govdesinde disaridan iceri dogru kuculen halkalar surekli belirir (yagmur damlasinin sudaki izinin tersi).
/// EnemyUnit uyari baslayinca PlayerDangerRing.Warn(...) cagirir; kendi kendine olusur, sahneye eklemen gerekmez.
/// </summary>
public class PlayerDangerRing : MonoBehaviour
{
    [Header("Gorunum")]
    public Color normalColor = new Color(1f, 0.32f, 0.26f, 1f);
    public Color unblockableColor = new Color(1f, 0.82f, 0.25f, 1f);
    [Tooltip("Halkanin disaridaki baslangic boyutu (dunya birimi, yatay yaricap).")]
    public float outerRadius = 1.35f;
    [Tooltip("Halka govdeye varinca kalan yaricap.")]
    public float innerRadius = 0.18f;
    [Tooltip("Dikey/yatay oran (1 = tam daire).")]
    public float aspect = 1f;
    public int ringCount = 3;
    [Tooltip("Bir halkanin disaridan iceri inme suresi (sn).")]
    public float period = 0.42f;
    [Range(0f, 1f)] public float maxAlpha = 0.45f;
    [Tooltip("Menzile ek pay (oyuncu menzile yakinken de goster).")]
    public float reachPadding = 0.7f;

    class Warner { public EnemyUnit unit; public AttackDef atk; public float t; }
    static readonly List<Warner> warners = new List<Warner>();
    static PlayerDangerRing inst;

    SpriteRenderer[] rings;
    PlayerController player;
    Collider2D playerCol;
    int playerLayer; int playerMinOrder; float orderCheckT = -1f;
    float strength;           // 0..1 yumusak gecis
    bool unblockable;
    float phase;
    static Sprite ringSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { warners.Clear(); inst = null; ringSprite = null; }

    public static bool Enabled = false;   // simdilik kapali (true yapinca halka geri gelir)

    public static void Warn(EnemyUnit unit, AttackDef atk)
    {
        if (!Enabled) return;
        if (unit == null || atk == null) return;
        if (inst == null)
        {
            var go = new GameObject("PlayerDangerRing");
            inst = go.AddComponent<PlayerDangerRing>();
        }
        warners.RemoveAll(w => w.unit == unit);
        warners.Add(new Warner { unit = unit, atk = atk, t = 0f });
    }

    static Sprite MakeRingSprite()
    {
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float r = n * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                // ince halka d ~ 0.9, yumusak kenar
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.88f) / 0.1f);
                a = a * a * (3f - 2f * a);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);   // 1 birim cap
    }

    void Awake()
    {
        if (inst == null) inst = this;
        if (ringSprite == null) ringSprite = MakeRingSprite();
        var mat = new Material(Shader.Find("Sprites/Default"));
        rings = new SpriteRenderer[Mathf.Max(1, ringCount)];
        for (int i = 0; i < rings.Length; i++)
        {
            var go = new GameObject("Ring" + i);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ringSprite;
            sr.sharedMaterial = mat;
            sr.enabled = false;
            rings[i] = sr;
        }
    }

    bool Danger(out bool unb)
    {
        unb = false;
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (player == null) return false;
        float px = player.transform.position.x;
        bool any = false;
        for (int i = warners.Count - 1; i >= 0; i--)
        {
            var w = warners[i];
            if (w.unit == null || w.unit.IsDead || !w.unit.IsInWindup) { warners.RemoveAt(i); continue; }
            float dx = Mathf.Abs(px - w.unit.transform.position.x);
            if (dx <= w.atk.hitReach + reachPadding)
            {
                any = true;
                if (w.atk.unblockable) unb = true;
            }
        }
        return any;
    }

    void LateUpdate()
    {
        bool unb;
        bool danger = Danger(out unb);
        if (danger) unblockable = unb;
        strength = Mathf.MoveTowards(strength, danger ? 1f : 0f, Time.deltaTime * (danger ? 8f : 6f));

        if (strength <= 0.001f || player == null)
        {
            for (int i = 0; i < rings.Length; i++) if (rings[i].enabled) rings[i].enabled = false;
            if (!danger) phase = 0f;
            return;
        }

        if (playerCol == null) playerCol = player.GetComponent<Collider2D>();
        if (Time.time - orderCheckT > 0.5f)   // halka oyuncunun en arkadaki parcasinin da arkasinda kalsin
        {
            orderCheckT = Time.time;
            int mn = int.MaxValue; int ly = 0;
            foreach (var r in player.GetComponentsInChildren<Renderer>())
            {
                if (r == null || !r.enabled || r.transform.IsChildOf(transform)) continue;
                if (r.sortingOrder < mn) { mn = r.sortingOrder; ly = r.sortingLayerID; }
            }
            if (mn != int.MaxValue) { playerMinOrder = mn; playerLayer = ly; }
        }
        Vector3 c = playerCol != null ? (Vector3)playerCol.bounds.center : player.transform.position + Vector3.up;
        c.z = player.transform.position.z;
        transform.position = c;

        int layer = playerLayer;
        int order = playerMinOrder - 1;
        Color baseCol = unblockable ? unblockableColor : normalColor;

        phase += Time.deltaTime / Mathf.Max(0.05f, period);
        for (int i = 0; i < rings.Length; i++)
        {
            float p = Mathf.Repeat(phase + (float)i / rings.Length, 1f);   // 0 = disarida, 1 = iceride
            float rad = Mathf.Lerp(outerRadius, innerRadius, p);
            // disarida belirir, ortada en parlak, govdeye varinca erir
            float a = Mathf.Clamp01(p / 0.2f) * Mathf.Clamp01((1f - p) / 0.35f) * maxAlpha * strength;
            var sr = rings[i];
            sr.enabled = a > 0.01f;
            sr.sortingLayerID = layer;
            sr.sortingOrder = order;
            sr.transform.localScale = new Vector3(rad * 2f, rad * 2f * aspect, 1f);
            baseCol.a = a;
            sr.color = baseCol;
        }
    }
}
