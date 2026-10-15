using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RAGE BARI (eski infazın yerine). Sağlık barının hemen altında ince MAVİ çubuk.
/// Düşman öldürdükçe dolar. Dolunca E: kısa odaklanma (ağır çekim) → düz zeminde
/// çok hızlı başlayıp yavaşlayarak duran ezici bir hamle + büyük alan şoku.
/// Oyun başlayınca kendiliğinden kurulur.
/// </summary>
public class RageMeter : MonoBehaviour
{
    public static RageMeter Instance { get; private set; }

    /// <summary>true: eski infaz (E) kapalı, E rage'i tetikler.</summary>
    public static bool ReplacesExecute = true;

    /// <summary>Test: menuden acilinca bar hep dolu, sinirsiz rage.</summary>
    public static bool Unlimited;

    [Header("Dolma (1 = dolu)")]
    [Range(0f, 1f)] public float killFill = 0.34f;       // 3 öldürme
    [Range(0f, 1f)] public float bigKillFill = 0.6f;     // maxHP >= 250
    public const bool StartFull = true;   // koddan sabit: sahnede eski serialize deger ezmesin

    [Header("Tuş")]
    public KeyCode key = KeyCode.E;

    [Header("Odak")]
    public float focusTime = 0.55f;                       // gerçek sn
    [Range(0.02f, 1f)] public float focusTimeScale = 0.07f;

    [Header("Hamle (hızlı başlar, yavaşlayarak durur)")]
    public float rushDistance = 18f;
    public float rushTime = 0.8f;                        // gerçek sn
    [Range(0.01f, 0.4f)] public float accelPortion = 0.08f; // hizlanma suresi (toplamin orani)
    [Range(1f, 4f)] public float decelPower = 2.2f;         // yavaslama egrisinin keskinligi
    public float recoverTime = 0.6f;
    [Tooltip("Durunca alan hasarinin gecikmesi (sn). Bu sure boyunca alan isaretlenir.")]
    public float shockDelay = 0.7f;
    [Tooltip("Kilic koyma animasyonu suresi (sn); alan vurusuyla biter. Kucuk = hizli.")]
    public float sheathDur = 0.65f;

    [Header("Execute animasyonu (Resources/RageExec kareleri)")]
    [Tooltip("Karenin boyu = collider boyu x bu. Karakter buyuk/kucuk gelirse degistir.")]
    public float execFrameScale = 1.1f;

    [Header("Hasar")]
    public float shockRadius = 7.5f;
    public float shockHeight = 3f;                        // zeminde kalanlar
    [Range(0f, 1f)] public float bossPercent = 0.25f;
    public int minDamage = 150;

    [Header("Yerleşim (1080p)")]
    public float marginX = 28f, marginY = 34f, width = 280f, height = 4f;

    [Header("Hazir: kilic parlamasi")]
    public Color auraColor = new Color(1f, 1f, 1f, 1f);        // Impact materyalinin rengi: beyaz, HDR
    [Range(0.5f, 80f)] public float bladeIntensity = 30f;
    [Range(0f, 3f)] public float haloSize = 1.4f;        // kilicin cevresindeki isik halesi
    [Range(0f, 20f)] public float haloIntensity = 5f;   // HDR (bloom ile parlar)

    public float Fill { get; private set; }
    public bool IsFull => Fill >= 0.999f;
    public bool Busy { get; set; }

    float shown, flash, lastWarn;
    RageAura aura;
    PlayerController lastPlayer;
    PlayerController player;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindAnyObjectByType<RageMeter>() != null) return;
        var go = new GameObject("RageMeter");
        DontDestroyOnLoad(go);
        go.AddComponent<RageMeter>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Fill = StartFull ? 1f : 0f;
    }

    void OnEnable()
    {
        CombatEvents.EnemyKilled += OnKilled;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnScene;
    }

    void OnDisable()
    {
        CombatEvents.EnemyKilled -= OnKilled;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnScene;
    }

    void OnScene(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
    {
        Fill = StartFull ? 1f : 0f;
        shown = Fill;
        Busy = false;
        player = null;
    }

    void OnKilled(EnemyController e)
    {
        if (Busy) return;   // rage hamlesinin öldürmeleri barı doldurmaz
        Health h = e != null ? e.GetComponent<Health>() : null;
        bool big = h != null && h.MaxHealth >= 250;
        bool was = IsFull;
        Fill = Mathf.Clamp01(Fill + (big ? bigKillFill : killFill));
        if (!was && IsFull)
        {
            flash = 1f;
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (player != null)
                CombatCallout.Popup(player.transform.position + Vector3.up * 2.6f, "RAGE HAZIR  [E]", new Color(0.45f, 0.75f, 1f), 1f);
        }
    }

    void Update()
    {
        if (Unlimited) Fill = 1f;
        shown = Mathf.MoveTowards(shown, Fill, Time.unscaledDeltaTime * 1.5f);
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != lastPlayer) { lastPlayer = pc; if (pc != null) { Fill = 1f; shown = 1f; Busy = false; } }
        if (player == null) player = pc;
        bool wantAura = IsFull && !Busy && player != null;
        if (wantAura && aura == null) aura = RageAura.Create(this, player);
        else if (!wantAura && aura != null) { Destroy(aura.gameObject); aura = null; }
        if (flash > 0f) flash -= Time.unscaledDeltaTime * 1.5f;

        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (player == null || Busy || !Input.GetKeyDown(key)) return;
        if (!player.canControl || player.inputLocked || player.isDashing) return;

        if (!IsFull)
        {
            if (Time.unscaledTime > lastWarn + 0.9f)
            {
                lastWarn = Time.unscaledTime;
                CombatCallout.Popup(player.transform.position + Vector3.up * 2.6f, "RAGE DOLU DEĞİL", new Color(0.7f, 0.75f, 0.85f), 0.8f);
            }
            return;
        }

        PlayerCombatController combat = player.GetComponent<PlayerCombatController>();
        if (combat != null) combat.CancelAttack();

        if (!Unlimited) Fill = 0f;
        Busy = true;
        player.stateMachine.ChangeState(new RageSlamState(player, player.stateMachine, this));
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        float s = Screen.height / 1080f;
        float x = marginX * s, y = marginY * s, w = width * s, h = Mathf.Max(2f, height * s);
        Texture2D t = Texture2D.whiteTexture;
        float pulse = IsFull ? 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 9f) : 1f;

        GUI.color = new Color(0.4f, 0.65f, 1f, 0.16f);
        GUI.DrawTexture(new Rect(x, y, w, h), t);

        Color c = IsFull ? Color.Lerp(new Color(0.35f, 0.65f, 1f), new Color(0.8f, 0.95f, 1f), 1f - pulse) : new Color(0.3f, 0.6f, 1f, 0.95f);
        GUI.color = c;
        GUI.DrawTexture(new Rect(x, y, w * shown, h), t);

        if (flash > 0f)
        {
            GUI.color = new Color(0.8f, 0.95f, 1f, flash * 0.5f);
            GUI.DrawTexture(new Rect(x - 3f * s, y - 3f * s, w + 6f * s, h + 6f * s), t);
        }
        GUI.color = Color.white;
    }
}

/// <summary>Rage hamlesi: odak (ağır çekim) → ease-out ezici atılış + şok dalgası → toparlanma.</summary>
public class RageSlamState : IPlayerState
{
    readonly PlayerController player;
    readonly PlayerStateMachine sm;
    readonly RageMeter rage;

    enum Phase { Focus, Slam, Recover }
    Phase phase;
    float timer, ghostTimer, gravity, dir;
    bool sheathStarted;
    Vector2 start;
    readonly HashSet<EnemyController> hitOnPath = new HashSet<EnemyController>();

    readonly List<Collider2D> mine = new List<Collider2D>();
    readonly List<Collider2D> ignored = new List<Collider2D>();

    void IgnoreEnemies()
    {
        player.GetComponentsInChildren(mine);
        for (int i = 0; i < EnemyController.All.Count; i++)
        {
            EnemyController e = EnemyController.All[i];
            if (e == null) continue;
            foreach (Collider2D c in e.GetComponentsInChildren<Collider2D>())
            {
                if (c == null || c.isTrigger) continue;
                foreach (Collider2D m in mine)
                    if (m != null && !m.isTrigger) Physics2D.IgnoreCollision(m, c, true);
                ignored.Add(c);
            }
        }
    }

    void RestoreEnemies()
    {
        foreach (Collider2D c in ignored)
            if (c != null)
                foreach (Collider2D m in mine)
                    if (m != null) Physics2D.IgnoreCollision(m, c, false);
        ignored.Clear();
    }

    Transform ring; SpriteRenderer ringSr; static Sprite dot;
    RageExecView view;

    public RageSlamState(PlayerController p, PlayerStateMachine s, RageMeter r) { player = p; sm = s; rage = r; }

    public void Enter()
    {
        gravity = player.rb.gravityScale;
        player.rb.gravityScale = 0f;
        player.rb.linearVelocity = Vector2.zero;
        player.canControl = false;
        player.isInvincible = true;
        dir = player.facingDir < 0f ? -1f : 1f;
        start = player.rb.position;
        phase = Phase.Focus;
        timer = 0f;
        MakeRing();
        view = RageExecView.Create(player, rage.execFrameScale);
        IgnoreEnemies();
        prevType = player.rb.bodyType;
        player.rb.bodyType = RigidbodyType2D.Kinematic;
    }
    RigidbodyType2D prevType;

    public void Update()
    {
        float dt = Time.unscaledDeltaTime;
        timer += dt;

        switch (phase)
        {
            case Phase.Focus:
            {
                float p = Mathf.Clamp01(timer / rage.focusTime);
                HitStop.Request(0.12f, rage.focusTimeScale, 8);
                player.rb.linearVelocity = Vector2.zero;
                if (ring != null)
                {
                    float r = Mathf.Lerp(5f, 0.6f, p * p);          // içeri toplanan halka
                    ring.position = player.transform.position + Vector3.up * 0.9f;
                    ring.localScale = Vector3.one * r;
                    ringSr.color = new Color(0.4f, 0.7f, 1f, 0.15f + 0.5f * p);
                }
                if (view != null)
                {
                    // Gerilme: kinda, hafif geri cekilip cokme
                    float e = p * p * (3f - 2f * p);
                    view.Show(RageExecView.A, new Vector2(-0.35f * dir * e, 0f), 1f - 0.06f * e, dir);
                }
                if (timer >= rage.focusTime) BeginSlam();
                break;
            }
            case Phase.Slam:
            {
                float t = Mathf.Clamp01(timer / rage.rushTime);
                Vector2 want = start + new Vector2(dir * rage.rushDistance * Profile(t), 0f);
                Vector2 cur = player.rb.position;
                float step = Mathf.Abs(want.x - cur.x);
                bool blocked = false;
                if (step > 0.0001f)
                {
                    float allowed = ClearDistance(new Vector2(dir, 0f), step);
                    if (allowed < step) { step = allowed; blocked = true; }
                }
                Vector2 pos = new Vector2(cur.x + dir * step, start.y);
                player.rb.position = pos;
                player.transform.position = new Vector3(pos.x, pos.y, player.transform.position.z);
                player.rb.linearVelocity = Vector2.zero;

                if (view != null)
                {
                    int f = t < 0.14f ? RageExecView.S : RageExecView.L;
                    view.Show(f, Vector2.zero, 1f, dir);
                }

                HitPath(pos);

                ghostTimer -= dt;
                if (ghostTimer <= 0f) { ghostTimer = 0.016f; SpawnGhost(); }

                if (t >= 1f || blocked) Impact();
                break;
            }
            case Phase.Recover:
                player.rb.linearVelocity = Vector2.zero;
                if (wavePending && timer >= rage.shockDelay) DoShock();
                // Smearsiz karede bekle; alan vurusundan 'sheathDur' once kilic koyma animasyonu baslar ve vurusla biter.
                if (!sheathStarted && timer >= rage.shockDelay - rage.sheathDur)
                {
                    sheathStarted = true;
                    if (view != null) { view.End(); view = null; }
                    var bridge = player.GetComponentInChildren<RonniSkin.RonniPlayerBridge>(true);
                    if (bridge != null) bridge.BeginSheathe(rage.sheathDur);
                }
                if (view != null) view.Show(RageExecView.L, Vector2.zero, 1f, dir);
                if (!wavePending && timer >= rage.shockDelay + 0.08f) Finish();
                break;
        }
    }

    public void FixedUpdate() { player.rb.linearVelocity = Vector2.zero; }

    // Hiz profili: hizli ivmelenme, sonra yumusak yavaslama. Normalize edilmis 0..1 konum.
    float norm = -1f;
    float Vel(float t)
    {
        float a = Mathf.Max(0.001f, rage.accelPortion);
        float ramp = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / a));
        return ramp * Mathf.Pow(1f - t, rage.decelPower);
    }
    float Profile(float t)
    {
        const int N = 200;
        if (norm < 0f)
        {
            norm = 0f;
            for (int i = 0; i < N; i++) norm += Vel((i + 0.5f) / N);
        }
        int n = Mathf.Max(1, Mathf.CeilToInt(t * N));
        float sum = 0f;
        for (int i = 0; i < n; i++)
        {
            float a = (float)i / N;
            float b = Mathf.Min(t, (float)(i + 1) / N);
            if (b > a) sum += Vel((a + b) * 0.5f) * (b - a) * N;
        }
        return Mathf.Clamp01(sum / norm);
    }

    readonly List<RaycastHit2D> castHits = new List<RaycastHit2D>();
    float ClearDistance(Vector2 d, float dist)
    {
        var f = new ContactFilter2D { useTriggers = false };
        f.useLayerMask = false;
        castHits.Clear();
        player.rb.Cast(d, f, castHits, dist);
        float best = dist;
        foreach (var h in castHits)
        {
            if (h.collider == null || h.collider.isTrigger) continue;
            if (h.collider.GetComponentInParent<EnemyController>() != null) continue;
            if (Mathf.Abs(h.normal.x) < 0.5f) continue;   // zemin / tavan
            best = Mathf.Min(best, Mathf.Max(0f, h.distance - 0.02f));
        }
        return best;
    }

    void BeginSlam()
    {
        phase = Phase.Slam;
        timer = 0f;
        start = player.rb.position;
        if (ring != null) ring.localScale = Vector3.zero;
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.25f);
    }

    // Atılış yolundaki düşmanlar ezilir.
    void HitPath(Vector2 pos)
    {
        for (int i = 0; i < EnemyController.All.Count; i++)
        {
            EnemyController e = EnemyController.All[i];
            if (e == null || e.IsDead || hitOnPath.Contains(e)) continue;
            Vector2 d = (Vector2)e.transform.position - pos;
            if (Mathf.Abs(d.x) < 2f && Mathf.Abs(d.y) < rage.shockHeight)
            {
                hitOnPath.Add(e);
                Smash(e, 0.6f);
            }
        }
    }

    bool wavePending;

    // Duruş: alan isaretlenir (gecikmeli), hasar shockDelay sonra vurur.
    void Impact()
    {
        phase = Phase.Recover;
        timer = 0f;
        wavePending = true;
        Vector2 p = player.rb.position;
        ShockVfx.Spawn(p, rage.shockRadius, rage.shockDelay, true);   // uyari alani
        HitStop.Request(0.06f, 0.2f, 6);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.4f);
    }

    // Gecikmeli alan hasari: yaricap icindeki (zeminde) herkes.
    void DoShock()
    {
        wavePending = false;
        Vector2 p = player.rb.position;

        for (int i = 0; i < EnemyController.All.Count; i++)
        {
            EnemyController e = EnemyController.All[i];
            if (e == null || e.IsDead) continue;
            Vector2 d = (Vector2)e.transform.position - p;
            if (Mathf.Abs(d.x) <= rage.shockRadius && Mathf.Abs(d.y) <= rage.shockHeight)
                Smash(e, 1f);
        }

        HitStop.Request(0.14f, 0.04f, 10);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(1.2f);
        CombatCallout.Popup(p + Vector2.up * 2.6f, "RAGE!", new Color(0.55f, 0.8f, 1f), 1.4f);
        ShockVfx.Spawn(p, rage.shockRadius, 0.5f, false);
    }

    void Smash(EnemyController e, float power)
    {
        Health h = e.GetComponent<Health>();
        if (h == null) return;

        Vector2 away = (Vector2)e.transform.position - player.rb.position;
        BossController boss = e.GetComponent<BossController>();

        int dmg = boss != null
            ? Mathf.Max(1, Mathf.RoundToInt(h.MaxHealth * rage.bossPercent * power))
            : Mathf.Max(rage.minDamage, h.MaxHealth);

        e.ApplyAttackHit(away.sqrMagnitude < 0.01f ? new Vector2(dir, 0f) : away, true, 3);
        h.TakeDamage(dmg);
    }

    void SpawnGhost()
    {
        if (!player.afterImagePrefab || !player.playerSprite) return;
        GameObject obj = Object.Instantiate(player.afterImagePrefab, player.transform.position, Quaternion.identity);
        AfterImage g = obj.GetComponent<AfterImage>();
        if (g == null) return;
        Vector3 sc = player.modelPivot != null ? player.modelPivot.localScale : Vector3.one;
        sc.x = Mathf.Abs(sc.x);
        g.Init(player.playerSprite.sprite, sc, player.playerSprite.flipX);
        g.fadeSpeed = 2.2f;
    }

    void MakeRing()
    {
        if (dot == null)
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float r = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    float a = Mathf.Clamp01((1f - r) * 4f) * Mathf.Clamp01(r * 3f - 1.6f + 1f);
                    a = r > 1f ? 0f : Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(r - 0.85f) * 6f), 1.5f) + (1f - r) * 0.15f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            dot = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n / 2f);
        }
        var go = new GameObject("RageRing");
        ring = go.transform;
        ringSr = go.AddComponent<SpriteRenderer>();
        ringSr.sprite = dot;
        ringSr.sortingOrder = 95;
        Shader sh = Shader.Find("Sprites/Default");
        if (sh != null) ringSr.sharedMaterial = new Material(sh);
        ringSr.color = new Color(0.4f, 0.7f, 1f, 0f);
    }

    void Cleanup()
    {
        if (view != null) { view.End(); view = null; }
        if (ring != null) Object.Destroy(ring.gameObject);
        ring = null;
    }

    void Finish()
    {
        Exit();
        if (player.IsGrounded()) sm.ChangeState(new GroundedState(player, sm));
        else sm.ChangeState(new AirState(player, sm));
    }

    public void Exit()
    {
        Cleanup();
        RestoreEnemies();
        player.rb.bodyType = prevType;
        player.rb.gravityScale = gravity;
        player.isInvincible = false;
        player.canControl = true;
        rage.Busy = false;
    }
}

/// <summary>Zeminde yayılan mavi şok dalgası.</summary>
public class ShockVfx : MonoBehaviour
{
    static Sprite px;
    SpriteRenderer a, b;
    float t, radius, life = 0.5f;
    bool telegraph;
    Vector2 origin;

    public static void Spawn(Vector2 at, float radius, float life = 0.5f, bool telegraph = false)
    {
        var go = new GameObject(telegraph ? "RageShockWarn" : "RageShock");
        var s = go.AddComponent<ShockVfx>();
        s.origin = at; s.radius = radius; s.life = Mathf.Max(0.05f, life); s.telegraph = telegraph;
        go.transform.position = at;
        if (px == null)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white); tex.Apply();
            px = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
        Shader sh = Shader.Find("Sprites/Default");
        Material m = sh != null ? new Material(sh) : null;
        s.a = Part(go.transform, m, 92);
        s.b = Part(go.transform, m, 93);
    }

    static SpriteRenderer Part(Transform p, Material m, int order)
    {
        var g = new GameObject("p");
        g.transform.SetParent(p, false);
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = px; sr.sortingOrder = order;
        if (m != null) sr.sharedMaterial = m;
        return sr;
    }

    void Update()
    {
        t += Time.unscaledDeltaTime / life;
        if (telegraph)
        {
            float k = Mathf.Clamp01(t);
            float w0 = radius * 2f;
            a.transform.localScale = new Vector3(w0, 0.35f + 0.25f * k, 1f);
            a.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            a.color = new Color(0.4f, 0.7f, 1f, 0.10f + 0.30f * k * k);
            // kenar cizgileri: dolum
            b.transform.localScale = new Vector3(w0 * k, 0.06f, 1f);
            b.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            b.color = new Color(0.9f, 0.98f, 1f, 0.35f + 0.5f * k);
            if (t >= 1f) Destroy(gameObject);
            return;
        }
        float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
        float w = radius * 2f * e;
        float al = 1f - Mathf.Clamp01(t);
        a.transform.localScale = new Vector3(w, 0.5f + 1.2f * (1f - e), 1f);
        a.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        a.color = new Color(0.4f, 0.7f, 1f, 0.45f * al);
        b.transform.localScale = new Vector3(w * 0.9f, 0.09f, 1f);
        b.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        b.color = new Color(0.9f, 0.98f, 1f, 0.95f * al);
        if (t >= 1f) Destroy(gameObject);
    }
}


/// <summary>
/// Bar doluyken kilicin (blade/katana/sword) KENDI materyali, Impact efektinin HDR rengiyle
/// parlayan toplamali materyalle degisir (ek kopya/overlay YOK). Bar bosalinca eski materyal geri gelir.
/// </summary>
public class RageAura : MonoBehaviour
{
    RageMeter rage; PlayerController player;
    Shader shader;
    static readonly int GlowId = Shader.PropertyToID("_GlowColor");

    class Entry { public Renderer src; public Material orig; public Material glow; }
    readonly List<Entry> list = new List<Entry>();
    readonly HashSet<Renderer> known = new HashSet<Renderer>();
    float nextScan;
    Transform haloT; SpriteRenderer halo; Material haloMat; static Sprite haloSprite;

    void EnsureHalo()
    {
        if (halo != null) return;
        if (haloSprite == null)
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    float a = d >= 1f ? 0f : Mathf.Pow(1f - d, 2.2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            haloSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n / 2f);   // 2 birim capli
        }
        var go = new GameObject("~RageHalo");
        haloT = go.transform;
        halo = go.AddComponent<SpriteRenderer>();
        halo.sprite = haloSprite;
        haloMat = new Material(shader);
        haloMat.mainTexture = haloSprite.texture;
        halo.sharedMaterial = haloMat;
    }

    public static RageAura Create(RageMeter r, PlayerController p)
    {
        var go = new GameObject("RageAura");
        var a = go.AddComponent<RageAura>();
        a.rage = r; a.player = p;
        a.shader = Resources.Load<Shader>("AtkGlow/SpriteGlowAdd");
        if (a.shader == null) a.shader = Shader.Find("ProjectM/SpriteGlowAdd");
        if (a.shader == null) { Debug.LogWarning("RageAura: SpriteGlowAdd shader bulunamadi"); Destroy(go); return null; }
        return a;
    }

    static bool NameMatch(string n)
    {
        if (string.IsNullOrEmpty(n)) return false;
        n = n.ToLowerInvariant();
        return n.Contains("blade") || n.Contains("katana") || n.Contains("sword");
    }

    static bool Match(Renderer r)
    {
        var sr = r as SpriteRenderer;
        if (sr != null) return (sr.sprite != null && NameMatch(sr.sprite.name)) || NameMatch(sr.gameObject.name);
        if (r is MeshRenderer) return NameMatch(r.gameObject.name);
        return false;
    }

    void Scan()
    {
        foreach (var r in player.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || known.Contains(r) || r.gameObject.name.StartsWith("~")) continue;
            if (!(r is MeshRenderer) && !(r is SpriteRenderer)) continue;
            if (!Match(r)) continue;
            known.Add(r);

            Material orig = r.sharedMaterial;
            if (orig == null) continue;
            var glow = new Material(shader);
            glow.mainTexture = orig.mainTexture;     // CharacterRim vb. dokuyu bu materyalden okur
            list.Add(new Entry { src = r, orig = orig, glow = glow });
        }
    }

    void LateUpdate()
    {
        if (player == null) { Destroy(gameObject); return; }

        if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + 0.25f; Scan(); }

        float pulse = 1f + 0.2f * Mathf.Sin(Time.unscaledTime * 7f);
        Color c = rage.auraColor;
        float k = rage.bladeIntensity * pulse;
        Color hdr = new Color(c.r * k, c.g * k, c.b * k, 1f);

        for (int i = list.Count - 1; i >= 0; i--)
        {
            Entry e = list[i];
            if (e.src == null) { if (e.glow != null) Destroy(e.glow); list.RemoveAt(i); continue; }
            e.glow.SetColor(GlowId, hdr);
            if (e.src.sharedMaterial != e.glow) e.src.sharedMaterial = e.glow;
        }

        // Kilicin cevresine yuvarlak yumusak isik hale (kare degil).
        bool any = false; Bounds b = new Bounds(); int order = 0; int layer = 0;
        foreach (var e in list)
        {
            Renderer r = e.src;
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (r.bounds.size.sqrMagnitude < 0.0001f || r.bounds.size.magnitude > 8f) continue;
            if (!any) { b = r.bounds; any = true; order = r.sortingOrder; layer = r.sortingLayerID; }
            else { b.Encapsulate(r.bounds); order = Mathf.Min(order, r.sortingOrder); }
        }
        if (any && rage.haloSize > 0f)
        {
            EnsureHalo();
            halo.enabled = true;
            float d = Mathf.Max(b.size.x, b.size.y) * rage.haloSize;
            haloT.position = b.center;
            haloT.localScale = Vector3.one * (d * 0.5f);   // sprite capi 2 birim
            halo.sortingLayerID = layer;
            halo.sortingOrder = order - 1;
            float hk = rage.haloIntensity * pulse;
            haloMat.SetColor(GlowId, new Color(c.r * hk, c.g * hk, c.b * hk, 1f));
            halo.color = new Color(1f, 1f, 1f, 0.55f);
        }
        else if (halo != null) halo.enabled = false;
    }

    void OnDestroy()
    {
        foreach (var e in list)
        {
            if (e == null) continue;
            if (e.src != null && e.orig != null) e.src.sharedMaterial = e.orig;
            if (e.glow != null) Destroy(e.glow);
        }
        list.Clear();
        if (haloT != null) Destroy(haloT.gameObject);
        if (haloMat != null) Destroy(haloMat);
    }
}


/// <summary>
/// Execute animasyonu: karakterin tum cizimlerini gizler, yerine Resources/RageExec kareleri
/// (A=kinda gerilmis, S=smearli saldiri, L=smearsiz saldiri, P=geri cekilmis durus) gosterir.
/// </summary>
[DefaultExecutionOrder(11000)]
public class RageExecView : MonoBehaviour
{
    public const int A = 0, S = 1, L = 2, P = 3;
    // Kirpilmis karelerde ayak ortasi (px) ve P karesinin yuksekligi.
    static readonly float[] FeetX = { 341f, 481f, 254f, 290f };
    const float PHeightPx = 757f;
    static readonly string[] Names = { "RageExec/exec_a", "RageExec/exec_s", "RageExec/exec_l", "RageExec/exec_p" };

    PlayerController player;
    SpriteRenderer sr;
    Sprite[] sprites = new Sprite[4];
    readonly List<Renderer> hidden = new List<Renderer>();
    int shown = -1;
    float baseScale = 1f;
    Vector2 offset; float scaleY = 1f, face = 1f;
    int sortLayer, sortOrder;

    public static RageExecView Create(PlayerController p, float scaleMul)
    {
        var go = new GameObject("~RageExecView");
        var v = go.AddComponent<RageExecView>();
        v.player = p;

        // Sira / katman: oyuncunun en ustteki cizicisi
        int top = int.MinValue; v.sortLayer = 0;
        foreach (var r in p.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r is ParticleSystemRenderer || r.gameObject.name.StartsWith("~")) continue;
            if (r.sortingOrder > top) { top = r.sortingOrder; v.sortLayer = r.sortingLayerID; }
        }
        v.sortOrder = (top == int.MinValue ? 0 : top) + 1;

        float colH = p.col != null ? p.col.bounds.size.y : 1.8f;
        float ppu = PHeightPx / Mathf.Max(0.2f, colH * scaleMul);

        for (int i = 0; i < 4; i++)
        {
            Texture2D tex = Resources.Load<Texture2D>(Names[i]);
            if (tex == null) { Debug.LogWarning("RageExec: " + Names[i] + " bulunamadi, eski gorunum kullanilacak"); Destroy(go); return null; }
            v.sprites[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(FeetX[i] / tex.width, 0f), ppu);
        }

        v.sr = go.AddComponent<SpriteRenderer>();
        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh != null) v.sr.sharedMaterial = new Material(sh);
        v.sr.enabled = false;
        return v;
    }

    public void Show(int frame, Vector2 off, float sy, float dir)
    {
        shown = frame; offset = off; scaleY = sy; face = dir < 0f ? -1f : 1f;
        Apply();
    }

    void Apply()
    {
        if (sr == null || player == null || shown < 0) return;
        sr.sprite = sprites[shown];
        sr.enabled = true;
        sr.sortingLayerID = sortLayer;
        sr.sortingOrder = sortOrder;
        float feetY = player.col != null ? player.col.bounds.min.y : player.transform.position.y;
        transform.position = new Vector3(player.transform.position.x + offset.x, feetY + offset.y, player.transform.position.z);
        transform.localScale = new Vector3(face, scaleY, 1f);   // sprite saga bakiyor
    }

    void LateUpdate()
    {
        if (player == null) { Destroy(gameObject); return; }
        // Karakterin kendi cizimlerini her kare gizle (Bridge her kare acabilir).
        foreach (var r in player.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r == sr || r is ParticleSystemRenderer || r.gameObject.name.StartsWith("~")) continue;
            if (!r.forceRenderingOff) { r.forceRenderingOff = true; hidden.Add(r); }
        }
        Apply();
    }

    public void End()
    {
        foreach (var r in hidden) if (r != null) r.forceRenderingOff = false;
        hidden.Clear();
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        foreach (var r in hidden) if (r != null) r.forceRenderingOff = false;
        hidden.Clear();
    }
}
