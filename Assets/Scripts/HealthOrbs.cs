using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CAN KÜRELERİ (sadece SONSUZ MOD): ölen her düşmandan parlak kırmızı küreler düşer. Oyuncu yaklaşınca
/// mıknatıs gibi ona çekilir; her küre oyuncunun canını biraz artırır.
/// Oyun başlayınca kendiliğinden kurulur, sahneye ekleme gerekmez.
/// </summary>
public class HealthOrbs : MonoBehaviour
{
    [Header("Düşme")]
    public bool enabledOrbs = true;
    [Tooltip("Her ölümde düşecek küre sayısı (en az, en çok).")]
    public Vector2Int orbsPerKill = new Vector2Int(2, 3);
    [Tooltip("Boss / elite gibi güçlü düşmanlar bu kadar ek küre bırakır (can ölçeğine göre).")]
    public int extraForBigEnemy = 2;

    [Header("İyileştirme")]
    [Tooltip("Küre başına iyileştirme: oyuncunun maksimum canının yüzdesi.")]
    [Range(0f, 0.2f)] public float healPercentPerOrb = 0.05f;
    public int minHealPerOrb = 1;

    [Header("Mıknatıs")]
    public float magnetRadius = 5f;
    [Tooltip("Düştükten sonra çekilmeye başlamadan önce beklenen süre (sn).")]
    public float magnetDelay = 0.45f;
    public float magnetAccel = 55f;
    public float maxSpeed = 24f;
    public float pickupRadius = 0.5f;
    [Tooltip("Alınmayan küre bu süre sonra kaybolur (sn).")]
    public float lifetime = 25f;

    [Header("Görünüm")]
    public Color coreColor = new Color(1f, 0.28f, 0.28f, 1f);
    public Color glowColor = new Color(1f, 0.15f, 0.2f, 0.55f);
    public float coreSize = 0.26f;
    public float glowSize = 0.85f;

    static HealthOrbs instance;
    static Sprite softSprite;
    static Material spriteMat;

    PlayerController player;
    Health playerHealth;
    readonly List<Orb> orbs = new List<Orb>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindAnyObjectByType<HealthOrbs>() != null)
            return;

        var go = new GameObject("HealthOrbs");
        DontDestroyOnLoad(go);
        go.AddComponent<HealthOrbs>();
    }

    void Awake() { instance = this; }

    void OnEnable() { CombatEvents.EnemyKilled += OnEnemyKilled; }

    void OnDisable()
    {
        CombatEvents.EnemyKilled -= OnEnemyKilled;

        for (int i = 0; i < orbs.Count; i++)
            if (orbs[i] != null && orbs[i].root != null) Destroy(orbs[i].root);

        orbs.Clear();
    }

    class Orb
    {
        public GameObject root;
        public SpriteRenderer core, glow;
        public Vector2 pos, vel;
        public float age, phase;
        public bool magnet;
    }

    void OnEnemyKilled(EnemyController e)
    {
        if (!enabledOrbs || !EndlessMode.Active || e == null) return;

        Vector2 at = e.transform.position;
        var col = e.GetComponentInChildren<Collider2D>();
        if (col != null) at = col.bounds.center;

        int n = Random.Range(orbsPerKill.x, orbsPerKill.y + 1);
        var hp = e.GetComponent<Health>();
        if (hp != null && hp.MaxHealth >= 250) n += extraForBigEnemy;

        for (int i = 0; i < n; i++) Spawn(at);
    }

    void Spawn(Vector2 at)
    {
        EnsureAssets();

        var o = new Orb();
        o.root = new GameObject("HealthOrb");
        o.root.transform.position = at;
        o.pos = at + Random.insideUnitCircle * 0.15f;
        float dir = Random.value < 0.5f ? -1f : 1f;
        o.vel = new Vector2(dir * Random.Range(1.5f, 5.5f), Random.Range(4f, 8f));
        o.phase = Random.value * 6.28f;

        GetSorting(out string layer, out int order);

        var g = new GameObject("glow");
        g.transform.SetParent(o.root.transform, false);
        o.glow = g.AddComponent<SpriteRenderer>();
        o.glow.sprite = softSprite; o.glow.sharedMaterial = spriteMat;
        o.glow.color = glowColor;
        o.glow.sortingLayerName = layer; o.glow.sortingOrder = order;
        g.transform.localScale = Vector3.one * glowSize;

        var c = new GameObject("core");
        c.transform.SetParent(o.root.transform, false);
        o.core = c.AddComponent<SpriteRenderer>();
        o.core.sprite = softSprite; o.core.sharedMaterial = spriteMat;
        o.core.color = coreColor;
        o.core.sortingLayerName = layer; o.core.sortingOrder = order + 1;
        c.transform.localScale = Vector3.one * coreSize;

        orbs.Add(o);
    }

    void GetSorting(out string layer, out int order)
    {
        layer = "Default"; order = 50;

        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (player == null) return;

        foreach (var r in player.GetComponentsInChildren<Renderer>())
        {
            if (r.gameObject.name.StartsWith("~")) continue;
            layer = r.sortingLayerName;
            order = Mathf.Max(order, r.sortingOrder + 3);
        }
    }

    static void EnsureAssets()
    {
        if (softSprite == null)
        {
            const int S = 48;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(S / 2f, S / 2f)) / (S / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    // merkez daha beyaza yakın (parlak çekirdek)
                    float hot = Mathf.Clamp01(1f - d * 2.2f);
                    tex.SetPixel(x, y, new Color(1f, 1f - 0.0f * hot + hot * 0f, 1f, a) * 1f);
                    tex.SetPixel(x, y, new Color(1f, Mathf.Lerp(0.55f, 1f, hot), Mathf.Lerp(0.55f, 1f, hot), a));
                }

            tex.Apply();
            softSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        }

        if (spriteMat == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            spriteMat = new Material(sh) { name = "HealthOrb" };
        }
    }

    void Update()
    {
        if (orbs.Count == 0) return;

        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (player != null && playerHealth == null) playerHealth = player.GetComponent<Health>();

        Vector2 target = Vector2.zero;
        bool canPull = player != null && playerHealth != null && !playerHealth.IsDead;

        if (canPull)
        {
            var pc = player.GetComponent<Collider2D>();
            target = pc != null ? (Vector2)pc.bounds.center : (Vector2)player.transform.position + Vector2.up * 0.5f;
        }

        float dt = Time.deltaTime;

        for (int i = orbs.Count - 1; i >= 0; i--)
        {
            Orb o = orbs[i];

            if (o.root == null) { orbs.RemoveAt(i); continue; }

            o.age += dt;

            if (o.age >= lifetime) { Destroy(o.root); orbs.RemoveAt(i); continue; }

            if (canPull && o.age >= magnetDelay)
            {
                Vector2 to = target - o.pos;
                float dist = to.magnitude;

                if (dist <= pickupRadius)
                {
                    Pickup();
                    Destroy(o.root);
                    orbs.RemoveAt(i);
                    continue;
                }

                if (o.magnet || dist <= magnetRadius)
                {
                    o.magnet = true;
                    // yaklaştıkça hızlanır, hedefe doğru yön hızını yumuşakça çevirir
                    Vector2 want = to / Mathf.Max(0.0001f, dist) * maxSpeed;
                    o.vel = Vector2.MoveTowards(o.vel, want, magnetAccel * dt);
                }
            }

            if (!o.magnet)
                o.vel = Vector2.Lerp(o.vel, Vector2.zero, 1f - Mathf.Exp(-4f * dt));

            o.pos += o.vel * dt;

            float bob = o.magnet ? 0f : Mathf.Sin(Time.time * 3f + o.phase) * 0.06f;
            o.root.transform.position = new Vector3(o.pos.x, o.pos.y + bob, 0f);

            float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 8f + o.phase);
            float fade = Mathf.Clamp01((lifetime - o.age) / 2f);
            float pop = Mathf.Clamp01(o.age / 0.12f);
            o.root.transform.localScale = Vector3.one * (pulse * pop);

            Color gc = glowColor; gc.a *= fade * (0.85f + 0.15f * pulse);
            o.glow.color = gc;
            Color cc = coreColor; cc.a *= fade;
            o.core.color = cc;
        }
    }

    void Pickup()
    {
        int amount = Mathf.Max(minHealPerOrb, Mathf.RoundToInt(playerHealth.MaxHealth * healPercentPerOrb));
        playerHealth.Heal(amount);
    }
}
