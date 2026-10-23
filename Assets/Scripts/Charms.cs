using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Test charm'ları. TAB = oyunu durdurur, charm menüsü açılır; istediğin charm'ı aç/kapat (1-4 ya da tıkla).
// Süre sınırı yok; seçim oyunlar arasında da kalır.
//   ZEHİR   : vurduğun düşman zehirlenir (vuruş bitince 3.5 sn boyunca can yakar).
//   PATLAMA : ölen düşman patlar, çevresindekilere hasar verir (zincirleme).
//   KAN     : can vuruşundan verdiğin hasarın %35'i kadar can kazanırsın.
//   FIRTINA : saldırı hızı x1.2.
//   AĞIR    : hasar +%30, düşman 3 kat uzağa itilir.
public enum CharmType { Poison, Explosion, Blood, Storm }

public static class Charms
{
    // ANA ŞALTER: false iken charm menüsü (TAB) açılmaz, hiçbir charm etkisi çalışmaz. Demo için kapalı.
    public static readonly bool Enabled = false;

    private static readonly bool[] on = new bool[4];

    public static bool Active(CharmType t) { return Enabled && on[(int)t]; }
    public static void Set(CharmType t, bool v) { on[(int)t] = v; }
    public static void Toggle(CharmType t) { on[(int)t] = !on[(int)t]; }

    public static float StormMult { get { return Active(CharmType.Storm) ? 1.2f : 1f; } }
    
    

    public static string Name(CharmType t)
    {
        switch (t) { case CharmType.Poison: return "ZEHİR"; case CharmType.Explosion: return "PATLAMA"; case CharmType.Blood: return "KAN"; default: return "FIRTINA"; }
    }

    public static string Desc(CharmType t)
    {
        switch (t)
        {
            case CharmType.Poison: return "Vurduğun düşman zehirlenir, 3.5 sn can yanar";
            case CharmType.Explosion: return "Ölen düşman patlar, çevresine hasar verir (zincir)";
            case CharmType.Blood: return "Verdiğin can hasarının %35'i kadar can kazanırsın";
            default: return "Saldırı hızı x1.2";

        }
    }

    public static Color Tint(CharmType t)
    {
        switch (t) { case CharmType.Poison: return new Color(0.45f, 1f, 0.25f, 1f); case CharmType.Explosion: return new Color(1f, 0.55f, 0.15f, 1f); case CharmType.Blood: return new Color(1f, 0.25f, 0.3f, 1f); default: return new Color(1f, 0.95f, 0.3f, 1f); }
    }
}

public class CharmManager : MonoBehaviour
{
    [Header("Zehir")]
    public int poisonTickDamage = 3;   // yığın başına tik hasarı (en çok 8 yığın)
    public float poisonTick = 0.4f;
    public float poisonTime = 3.5f;

    [Header("Patlama")]
    public float explodeRadius = 10f;
    public int explodeDamage = 30;
    public float explodeDelay = 0.12f;

    private static Sprite orb;
    private Font font;
    private bool open;
    private float prevScale = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (!Charms.Enabled) return;

        GameObject go = new GameObject("CharmManager");
        DontDestroyOnLoad(go);
        go.AddComponent<CharmManager>();
    }

    private void OnEnable()
    {
        CombatEvents.EnemyKilled += OnKilled;
        CombatEvents.EnemyHit += OnHit;
        font = GameFont.Or(Resources.Load<Font>("Pixeltype"));
    }

    private void OnDisable()
    {
        CombatEvents.EnemyKilled -= OnKilled;
        CombatEvents.EnemyHit -= OnHit;
        if (open) Close();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab)) { if (open) Close(); else Open(); }
        if (!open) return;
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
        for (int i = 0; i < 4; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) Charms.Toggle((CharmType)i);
    }

    private void Open()
    {
        if (Time.timeScale < 0.01f) return;   // başka bir menü zaten durdurmuş
        open = true;
        prevScale = Time.timeScale;
        Time.timeScale = 0f;
    }

    private void Close()
    {
        open = false;
        Time.timeScale = prevScale <= 0.01f ? 1f : prevScale;
    }

    private void OnKilled(EnemyBase e)
    {
        if (e == null || !Charms.Active(CharmType.Explosion)) return;
        StartCoroutine(Explode(e.transform.position + Vector3.up * 0.8f, e));
    }

    private IEnumerator Explode(Vector3 c, EnemyBase source)
    {
        GameObject ring = new GameObject("CharmExplosion");
        ring.transform.position = c;
        SpriteRenderer sr = ring.AddComponent<SpriteRenderer>();
        sr.sprite = GetOrb();
        sr.sortingOrder = 450;
        float t0 = Time.time;
        float dur = 0.28f;

        yield return new WaitForSeconds(explodeDelay);

        List<EnemyBase> all = EnemyBase.All;
        for (int i = all.Count - 1; i >= 0; i--)
        {
            EnemyBase o = all[i];
            if (o == null || o == source || o.IsDead) continue;
            Vector3 op = o.transform.position;
            if (Mathf.Abs(op.x - c.x) > explodeRadius || Mathf.Abs(op.y - c.y) > 4.5f) continue;
            float dx = op.x - c.x;
            PlayerDamage.HitEnemy(o, new DamageInfo
            {
                source = DamageSource.Ability,
                comboStep = 1,
                healthDamage = explodeDamage,
                direction = new Vector2(dx == 0f ? 1f : Mathf.Sign(dx), 0f),
                hitPosition = op + Vector3.up,
                disallowCrit = true
            });
        }

        while (Time.time - t0 < dur && ring != null)
        {
            float k = (Time.time - t0) / dur;
            ring.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, explodeRadius * 2f, 1f - (1f - k) * (1f - k));
            Color col = Charms.Tint(CharmType.Explosion); col.a = 1f - k;
            sr.color = col;
            yield return null;
        }
        if (ring != null) Destroy(ring);
    }

    private void OnHit(EnemyBase enemy, DamageInfo info, HitResult result)
    {
        if (enemy == null || !result.hit || result.onBalance) return;

        if (Charms.Active(CharmType.Blood) && result.amount > 0)
        {
            GameObject pl = GameObject.FindWithTag("Player");
            Health ph = pl != null ? pl.GetComponent<Health>() : null;
            if (ph != null && !ph.IsDead) ph.Heal(Mathf.Max(1, Mathf.RoundToInt(result.amount * 0.35f)));
        }

        if (Charms.Active(CharmType.Poison) && !enemy.IsDead)
        {
            PoisonStatus ps = enemy.GetComponent<PoisonStatus>();
            if (ps == null) ps = enemy.gameObject.AddComponent<PoisonStatus>();
            ps.Apply(poisonTime, poisonTick, poisonTickDamage);
        }
    }

    private static Sprite GetOrb()
    {
        if (orb != null) return orb;
        int n = 48;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f - 0.5f, n / 2f - 0.5f)) / (n / 2f);
                float a = d > 1f ? 0f : (d > 0.82f ? 1f : (d > 0.55f ? 0.35f : 0.85f));
                float edge = Mathf.Clamp01((1f - d) * 12f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * edge));
            }
        tex.Apply();
        orb = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        return orb;
    }

    private GUIStyle Style(int size, Color c, TextAnchor a)
    {
        GUIStyle st = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = a };
        if (font != null) st.font = font;
        st.normal.textColor = c;
        return st;
    }

    private void OnGUI()
    {
        // Aktif charm'lar sol altta.
        float ly = Screen.height - 40f;
        for (int i = 3; i >= 0; i--)
        {
            CharmType t = (CharmType)i;
            if (!Charms.Active(t)) continue;
            GUI.Label(new Rect(14f, ly, 300f, 26f), Charms.Name(t), Style(22, Charms.Tint(t), TextAnchor.MiddleLeft));
            ly -= 26f;
        }

        if (!open)
        {
            GUI.Label(new Rect(14f, 34f, 300f, 22f), "[TAB] charm menüsü", Style(18, new Color(1f, 1f, 1f, 0.55f), TextAnchor.MiddleLeft));
            return;
        }

        float w = 620f, rowH = 56f, h = 120f + rowH * 4f;
        Rect panel = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.82f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = old;

        GUI.Label(new Rect(panel.x, panel.y + 10f, w, 36f), "CHARM SEÇ", Style(32, Color.white, TextAnchor.MiddleCenter));
        GUI.Label(new Rect(panel.x, panel.y + 46f, w, 24f), "1-4 ya da tıkla   •   TAB / ESC kapat", Style(18, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter));

        for (int i = 0; i < 4; i++)
        {
            CharmType t = (CharmType)i;
            bool on = Charms.Active(t);
            Rect r = new Rect(panel.x + 20f, panel.y + 90f + rowH * i, w - 40f, rowH - 6f);

            GUI.color = on ? new Color(Charms.Tint(t).r, Charms.Tint(t).g, Charms.Tint(t).b, 0.35f) : new Color(1f, 1f, 1f, 0.08f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;

            GUI.Label(new Rect(r.x + 10f, r.y, 40f, r.height), (i + 1).ToString(), Style(26, Color.white, TextAnchor.MiddleLeft));
            GUI.Label(new Rect(r.x + 50f, r.y + 2f, 300f, 26f), Charms.Name(t), Style(26, Charms.Tint(t), TextAnchor.MiddleLeft));
            GUI.Label(new Rect(r.x + 50f, r.y + 26f, 420f, 22f), Charms.Desc(t), Style(18, new Color(1f, 1f, 1f, 0.75f), TextAnchor.MiddleLeft));
            GUI.Label(new Rect(r.xMax - 90f, r.y, 80f, r.height), on ? "AÇIK" : "KAPALI", Style(24, on ? Color.white : new Color(1f, 1f, 1f, 0.4f), TextAnchor.MiddleRight));

            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) Charms.Toggle(t);
        }
    }
}

public class PoisonStatus : MonoBehaviour
{
    public const int MaxStacks = 8;
    public const float StackCooldown = 0.35f;
    private float lastStack = -999f;
    private EnemyBase enemy;
    private Health health;
    private float until, nextTick, tickEvery;
    private int tickDamage, stacks;

    // Her vuruş bir yığın ekler (süre yenilenir); tik hasarı yığınla çarpılır.
    public void Apply(float time, float tick, int dmg)
    {
        if (enemy == null) { enemy = GetComponent<EnemyBase>(); health = GetComponent<Health>(); }
        until = Time.time + time;
        tickEvery = tick; tickDamage = dmg;
        if (Time.time - lastStack >= StackCooldown)
        {
            stacks = Mathf.Min(MaxStacks, stacks + 1);   // yığın hızı saldırı hızından bağımsız (Fırtına ile patlamasın)
            lastStack = Time.time;
        }
        if (nextTick < Time.time) nextTick = Time.time + tick;
    }

    private void Update()
    {
        if (enemy == null || health == null || enemy.IsDead || health.IsDead || Time.time >= until) { Destroy(this); return; }
        if (Time.time < nextTick) return;
        nextTick = Time.time + tickEvery;
        int dealt = tickDamage * stacks;
        // Zırhlı düşmanda zehir önce zırhı aşındırır (yavaşça); zırh kırıksa normal can yakar.
        EnemyBalance armor = enemy.UsesPosture ? GetComponent<EnemyBalance>() : null;
        if (armor != null && !armor.IsBroken) armor.AddBalanceDamage(Mathf.Max(1, Mathf.RoundToInt(dealt * 0.4f)), false);
        else health.TakeDamage(dealt);
        DamageCounter.Add(enemy, dealt);
        if (!health.IsDead) enemy.PlayTintFlash(new Color(0.45f, 1f, 0.25f, 1f), 0.15f);
    }
}
