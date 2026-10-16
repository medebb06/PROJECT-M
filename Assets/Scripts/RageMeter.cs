using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RAGE BARI (eski infazın yerine). Sağlık barının hemen altında ince MAVİ çubuk.
/// Düşman öldürdükçe dolar. Dolunca E: kısa odaklanma (ağır çekim) → düz zeminde
/// en uygun hedefe atılıp tek vuruş (3 segment, E başına 1 segment).
/// Oyun başlayınca kendiliğinden kurulur.
/// </summary>
public enum RageAbility { Lunge = 0, Flame = 1 }

public class RageMeter : MonoBehaviour
{
    // ---- Yetenek secimi (ana menuden) ----
    const string PrefKey = "RageAbility";
    static int abilityCache = -1;
    public static RageAbility Selected
    {
        get
        {
            if (abilityCache < 0) abilityCache = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, 1);
            return (RageAbility)abilityCache;
        }
        set { abilityCache = (int)value; PlayerPrefs.SetInt(PrefKey, abilityCache); PlayerPrefs.Save(); }
    }
    /// <summary>Gecerli yetenek (tutorial her zaman atilma ogretir).</summary>
    public static RageAbility Current => TutorialMode.Active ? RageAbility.Lunge : Selected;
    public static string AbilityLabel(RageAbility a) => a == RageAbility.Flame ? "ALEV KILICI" : "ATILMA";
    public static void CycleAbility() { Selected = Selected == RageAbility.Lunge ? RageAbility.Flame : RageAbility.Lunge; }

    [Header("Alev Kilici (yetenek)")]
    public float flameDuration = 9f;                       // sn
    [Tooltip("Yanik tam yigina (maxBurnStacks) ulasinca hizlar; her vurus 1 yigin ekler.")]
    public int maxBurnStacks = 16;
    [Tooltip("Bu kadar yiginda hiz = burnBalancePerSec/burnHealthPerSec. Daha cok yiginda daha da artar.")]
    public int burnRefStacks = 6;
    public float burnBalancePerSec = 0.5f;               // max posturun orani / sn (0.5 = ~2 sn'de bir bar)
    public float burnHealthPerSec = 0.12f;                // max canin orani / sn
    public float burnTime = 2.5f;                          // her vurusta yenilenir
    public Color flameColor = new Color(1f, 0.06f, 0.01f, 1f);   // parlak kirmizi
    public float FlameLeft { get; private set; }
    public bool FlameActive => FlameLeft > 0f;

    public static RageMeter Instance { get; private set; }

    /// <summary>true: eski infaz (E) kapalı, E rage'i tetikler.</summary>
    public static bool ReplacesExecute = true;

    /// <summary>Test: menuden acilinca bar hep dolu, sinirsiz rage.</summary>
    public static bool Unlimited = false;   // kapali baslar (menu > TEST ile acilabilir)

    [Header("Dolma (1 = dolu)")]
    [Range(0f, 1f)] public float killFill = 0.34f;       // 1 segment
    [Range(0f, 1f)] public float bigKillFill = 0.67f;     // maxHP >= 250
    public const bool StartFull = true;   // koddan sabit: sahnede eski serialize deger ezmesin

    [Header("Tuş")]
    public KeyCode key = KeyCode.E;

    [Header("Atilis (tek hedefe)")]
    public float windupTime = 0.14f;                      // gercek sn, kisa gerilme
    public float lungeSpeed = 70f;                        // birim/sn
    public float maxLungeTime = 0.45f;
    public float strikeGap = 1.3f;                        // hedefe bu kadar yaklasinca vurur
    public float maxRange = 14f;                          // hedef arama mesafesi
    public float maxHeightDiff = 3.5f;
    public float recoverTime = 0.3f;
    [Tooltip("Kilic koyma animasyonu suresi (sn).")]
    public float sheathDur = 0.3f;

    [Header("Execute animasyonu (Resources/RageExec kareleri)")]
    [Tooltip("Karenin boyu = collider boyu x bu. Karakter buyuk/kucuk gelirse degistir.")]
    public float execFrameScale = 1.1f;

    [Header("Hasar")]
    [Range(0f, 1f)] public float postureFrac = 0.5f;      // isaretsiz hedef: max posturun bu kadari
    [Range(0f, 1f)] public float healthFrac = 0.25f;      // isaretsiz hedef: max canin bu kadari
    [Range(0f, 1f)] public float bossPercent = 0.2f;
    public int minDamage = 150;

    [Header("Yerleşim (1080p)")]
    public float marginX = 28f, marginY = 26f, width = 280f, height = 4f;

    [Header("Hazir: kilic parlamasi")]
    public Color auraColor = new Color(1f, 1f, 1f, 1f);        // Impact materyalinin rengi: beyaz, HDR
    [Range(0.5f, 80f)] public float bladeIntensity = 30f;
    [Range(0f, 3f)] public float haloSize = 1.4f;        // kilicin cevresindeki isik halesi
    [Range(0f, 20f)] public float haloIntensity = 5f;   // HDR (bloom ile parlar)

    public float Fill { get; private set; }
    public const int Segments = 3;
    public const float SegCost = 1f / Segments;
    public bool IsFull => Fill >= SegCost - 0.001f;    // en az 1 segment = E kullanilabilir
    public bool Busy { get; set; }
    public float ignoreKillsUntil;

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
        CombatEvents.ParrySucceeded += OnParry;
        CombatEvents.EnemyHit += OnEnemyHit;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnScene;
    }

    void OnDisable()
    {
        CombatEvents.EnemyKilled -= OnKilled;
        CombatEvents.ParrySucceeded -= OnParry;
        CombatEvents.EnemyHit -= OnEnemyHit;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnScene;
    }

    struct BurnStack { public int count; public float last; }
    readonly Dictionary<EnemyController, BurnStack> burnStacks = new Dictionary<EnemyController, BurnStack>();

    void OnEnemyHit(EnemyController e, DamageInfo info, HitResult r)
    {
        if (!FlameActive || e == null || !r.hit || e.IsDead) return;
        // Vurdukca yiginlanir: hiz = max hiz * yigin / maxBurnStacks. Yanik sonunda yigin sifirlanir.
        burnStacks.TryGetValue(e, out BurnStack bs);
        if (Time.time - bs.last > burnTime) bs.count = 0;
        bs.count = Mathf.Min(Mathf.Max(1, maxBurnStacks), bs.count + 1);
        bs.last = Time.time;
        burnStacks[e] = bs;
        float k = bs.count / (float)Mathf.Max(1, burnRefStacks);
        EnemyStatus.Get(e).ApplyBurn(burnBalancePerSec * k, burnHealthPerSec * k, burnTime);
        if (burnStacks.Count > 40)
        {
            var dead = new List<EnemyController>();
            foreach (var kv in burnStacks) if (kv.Key == null || kv.Key.IsDead) dead.Add(kv.Key);
            foreach (var d in dead) if (d != null) burnStacks.Remove(d);
        }
    }

    void OnScene(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
    {
        FlameLeft = 0f;
        Fill = StartFull ? 1f : 0f;
        shown = Fill;
        Busy = false;
        player = null;
    }

    [Range(0f, 1f)] public float parryFill = 0.12f;        // her basarili parry
    [Range(0f, 1f)] public float parryBreakFill = 0.2f;    // dengeyi kiran parry

    void OnParry(EnemyController e, bool brokeBalance)
    {
        if (Busy) return;
        bool was = IsFull;
        Fill = Mathf.Clamp01(Fill + (brokeBalance ? parryBreakFill : parryFill));
        if (!was && IsFull)
        {
            flash = 1f;
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (player != null)
                CombatCallout.Popup(player.transform.position + Vector3.up * 2.6f, "HAZIR  [E]", new Color(0.45f, 0.75f, 1f), 1f);
        }
    }

    void OnKilled(EnemyController e)
    {
        if (Busy || Time.unscaledTime < ignoreKillsUntil) return;   // rage hamlesinin öldürmeleri barı doldurmaz
        Health h = e != null ? e.GetComponent<Health>() : null;
        bool big = h != null && h.MaxHealth >= 250;
        bool was = IsFull;
        Fill = Mathf.Clamp01(Fill + (big ? bigKillFill : killFill));
        if (!was && IsFull)
        {
            flash = 1f;
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (player != null)
                CombatCallout.Popup(player.transform.position + Vector3.up * 2.6f, "HAZIR  [E]", new Color(0.45f, 0.75f, 1f), 1f);
        }
    }

    void Update()
    {
        if (Unlimited) Fill = 1f;
        shown = Mathf.MoveTowards(shown, Fill, Time.unscaledDeltaTime * 1.5f);
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != lastPlayer) { lastPlayer = pc; if (pc != null) { Fill = 1f; shown = 1f; Busy = false; } }
        if (player == null) player = pc;
        if (FlameLeft > 0f) FlameLeft = Mathf.Max(0f, FlameLeft - Time.unscaledDeltaTime);
        bool wantAura = (IsFull || FlameActive) && !Busy && player != null;
        if (wantAura && aura == null) aura = RageAura.Create(this, player);
        else if (!wantAura && aura != null) { Destroy(aura.gameObject); aura = null; }
        if (flash > 0f) flash -= Time.unscaledDeltaTime * 1.5f;

        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (player == null || Busy || !MobileInput.GetKeyDown(key)) return;
        // Her seyi boler: saldiri, dash, hasar sersemlemesi, havada... Sadece menu aciksa / oluysen calismaz.
        if (MainMenu.Active) return;
        Health ph = player.GetComponent<Health>();
        if (ph != null && ph.IsDead) return;

        if (!IsFull)
        {
            if (Time.unscaledTime > lastWarn + 0.9f)
            {
                lastWarn = Time.unscaledTime;
                CombatCallout.Popup(player.transform.position + Vector3.up * 2.6f, "RAGE DOLU DEĞİL", new Color(0.7f, 0.75f, 0.85f), 0.8f);
            }
            return;
        }

        if (Current == RageAbility.Flame)
        {
            if (FlameActive) return;
            // Alev Kilici tum bari harcar (tek bar).
            if (!Unlimited && Fill < 0.999f)
            {
                if (Time.unscaledTime > lastWarn + 0.9f)
                {
                    lastWarn = Time.unscaledTime;
                    CombatCallout.Popup(player.transform.position + Vector3.up * 2.6f, "BAR DOLU DEĞİL", new Color(0.7f, 0.75f, 0.85f), 0.8f);
                }
                return;
            }
            if (!Unlimited) Fill = 0f;
            FlameLeft = flameDuration;
            CombatCallout.Popup(player.transform.position + Vector3.up * 2.6f, "ALEV!", new Color(1f, 0.2f, 0.08f), 1.2f);
            if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.3f);
            return;
        }

        EnemyController tgt = PickTarget();
        if (tgt == null)
        {
            // Hedef yok: bir sey olmaz, bar harcanmaz.
            if (Time.unscaledTime > lastWarn + 0.9f)
            {
                lastWarn = Time.unscaledTime;
                CombatCallout.Popup(player.transform.position + Vector3.up * 2.6f, "HEDEF YOK", new Color(0.7f, 0.75f, 0.85f), 0.8f);
            }
            return;
        }

        PlayerCombatController combat = player.GetComponent<PlayerCombatController>();
        if (combat != null) combat.CancelAttack();

        if (!Unlimited) Fill = Mathf.Max(0f, Fill - SegCost);
        Busy = true;
        player.stateMachine.ChangeState(new RageSlamState(player, player.stateMachine, this, tgt));
    }

    /// <summary>Oncelik: parry isaretli > posture kirik > en yakin (menzil icinde).</summary>
    EnemyController PickTarget()
    {
        Vector2 pp = player.transform.position;
        EnemyController best = null; int bestRank = -1; float bestD = float.MaxValue;
        for (int i = 0; i < EnemyController.All.Count; i++)
        {
            EnemyController e = EnemyController.All[i];
            if (e == null || e.IsDead || !e.isActiveAndEnabled) continue;
            Vector2 d = (Vector2)e.transform.position - pp;
            if (Mathf.Abs(d.x) > maxRange || Mathf.Abs(d.y) > maxHeightDiff) continue;
            EnemyBalance bal = e.GetComponent<EnemyBalance>();
            int rank = ParryMark.IsMarked(e) ? 2 : (bal != null && bal.IsBroken ? 1 : 0);
            float dist = Mathf.Abs(d.x);
            if (rank > bestRank || (rank == bestRank && dist < bestD)) { best = e; bestRank = rank; bestD = dist; }
        }
        return best;
    }

    void LateUpdate()
    {
        if (MainMenu.Active) return;
        float s = HudCanvas.Scale();
        float w = width * s, h = Mathf.Max(2f, height * s);
        // SOL ALT: marginY = ekranin altindan uzaklik; sinematik serit alttan da yukseldigi icin bar yukari kayar.
        Rect sa = HudCanvas.SafeArea();
        float x = sa.x + marginX * s, y = sa.yMax - marginY * s - h - RunCameraZoom.TopBarPixels;
        w = Mathf.Min(w, sa.width - marginX * s * 2f);
        float pulse = IsFull ? 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 9f) : 1f;

        HudCanvas.Fill(new Rect(x, y, w, h), new Color(0.4f, 0.65f, 1f, 0.16f));

        Color c = FlameActive ? Color.Lerp(new Color(1f, 0.1f, 0.02f), new Color(1f, 0.4f, 0.12f), 1f - pulse)
            : IsFull ? Color.Lerp(new Color(0.35f, 0.65f, 1f), new Color(0.8f, 0.95f, 1f), 1f - pulse) : new Color(0.3f, 0.6f, 1f, 0.95f);
        HudCanvas.Fill(new Rect(x, y, w * shown, h), c);

        for (int i = 1; i < Segments; i++)
            HudCanvas.Fill(new Rect(x + w * i / Segments - 1f * s, y, 2f * s, h), new Color(0.02f, 0.04f, 0.08f, 0.95f));

        if (flash > 0f)
        {
            HudCanvas.Fill(new Rect(x - 3f * s, y - 3f * s, w + 6f * s, h + 6f * s), new Color(0.8f, 0.95f, 1f, flash * 0.5f));
        }
    }
}

/// <summary>E: en uygun hedefe atılıp TEK vuruş. Çizgisel/alansal hasar yok.</summary>
public class RageSlamState : IPlayerState
{
    readonly PlayerController player;
    readonly PlayerStateMachine sm;
    readonly RageMeter rage;
    readonly EnemyController target;

    enum Phase { Windup, Lunge, Strike, Recover }
    Phase phase;
    float timer, ghostTimer, gravity, dir;
    RigidbodyType2D prevType;
    RageExecView view;
    bool sheathed;

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

    public RageSlamState(PlayerController p, PlayerStateMachine s, RageMeter r, EnemyController t) { player = p; sm = s; rage = r; target = t; }

    void Face(float d)
    {
        dir = d < 0f ? -1f : 1f;
        player.facingDir = dir;
        if (player.playerSprite != null) player.playerSprite.flipX = dir < 0f;
    }

    public void Enter()
    {
        gravity = player.rb.gravityScale;
        player.rb.gravityScale = 0f;
        player.rb.linearVelocity = Vector2.zero;
        player.canControl = false;
        player.isInvincible = true;
        Face(target.transform.position.x - player.transform.position.x);
        phase = Phase.Windup;
        timer = 0f;
        view = RageExecView.Create(player, rage.execFrameScale);
        IgnoreEnemies();
        prevType = player.rb.bodyType;
        player.rb.bodyType = RigidbodyType2D.Kinematic;
    }

    public void Update()
    {
        float dt = Time.unscaledDeltaTime;
        timer += dt;
        if (target == null || target.IsDead)
        {
            if (phase == Phase.Windup || phase == Phase.Lunge) { Finish(); return; }
        }

        switch (phase)
        {
            case Phase.Windup:
            {
                float p = Mathf.Clamp01(timer / rage.windupTime);
                HitStop.Request(0.08f, 0.15f, 8);
                player.rb.linearVelocity = Vector2.zero;
                if (target != null) Face(target.transform.position.x - player.transform.position.x);
                if (view != null)
                {
                    float e = p * p * (3f - 2f * p);
                    view.Show(RageExecView.A, new Vector2(-0.3f * dir * e, 0f), 1f - 0.05f * e, dir);
                }
                if (timer >= rage.windupTime) { phase = Phase.Lunge; timer = 0f; if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.2f); }
                break;
            }
            case Phase.Lunge:
            {
                Vector2 cur = player.rb.position;
                float dx = target.transform.position.x - cur.x;
                Face(dx);
                float gap = Mathf.Abs(dx) - rage.strikeGap;
                float step = Mathf.Min(rage.lungeSpeed * dt, Mathf.Max(0f, gap));
                bool blocked = false;
                if (step > 0.0001f)
                {
                    float allowed = ClearDistance(new Vector2(dir, 0f), step);
                    if (allowed < step) { step = allowed; blocked = true; }
                }
                Vector2 pos = new Vector2(cur.x + dir * step, cur.y);
                player.rb.position = pos;
                player.transform.position = new Vector3(pos.x, pos.y, player.transform.position.z);
                player.rb.linearVelocity = Vector2.zero;
                if (view != null) view.Show(timer < 0.05f ? RageExecView.S : RageExecView.L, Vector2.zero, 1f, dir);

                ghostTimer -= dt;
                if (ghostTimer <= 0f) { ghostTimer = 0.016f; SpawnGhost(); }

                bool arrived = gap <= 0.02f;
                if (arrived || blocked || timer >= rage.maxLungeTime)
                {
                    if (Mathf.Abs(target.transform.position.x - pos.x) <= rage.strikeGap + 1.6f) Strike();
                    else Finish();
                }
                break;
            }
            case Phase.Strike:
                player.rb.linearVelocity = Vector2.zero;
                if (view != null) view.Show(RageExecView.S, Vector2.zero, 1f, dir);
                if (timer >= 0.1f) { phase = Phase.Recover; timer = 0f; }
                break;
            case Phase.Recover:
            {
                player.rb.linearVelocity = Vector2.zero;
                if (!sheathed)
                {
                    sheathed = true;
                    if (view != null) { view.End(); view = null; }
                    var bridge = player.GetComponentInChildren<RonniSkin.RonniPlayerBridge>(true);
                    if (bridge != null) bridge.BeginSheathe(rage.sheathDur);
                }
                if (timer > 0.05f && (player.dashBufferTimer > 0f || MobileInput.GetKeyDown(KeyCode.LeftShift) || MobileInput.GetKeyDown(KeyCode.Space) || MobileInput.GetMouseButtonDown(0)))
                {
                    var br = player.GetComponentInChildren<RonniSkin.RonniPlayerBridge>(true);
                    if (br != null) br.CancelSheathe();
                    Finish();
                    break;
                }
                if (timer >= rage.recoverTime) Finish();
                break;
            }
        }
    }

    public void FixedUpdate() { player.rb.linearVelocity = Vector2.zero; }

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

    void Strike()
    {
        phase = Phase.Strike;
        timer = 0f;
        rage.ignoreKillsUntil = Time.unscaledTime + 0.4f;
        Vector2 p = player.rb.position;
        if (target != null && !target.IsDead) Smash(target, p);
        HitStop.Request(0.12f, 0.05f, 10);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.8f);
    }

    void Smash(EnemyController e, Vector2 from)
    {
        Health h = e.GetComponent<Health>();
        if (h == null) return;
        Vector2 hitDir = new Vector2(dir, 0f);
        BossController boss = e.GetComponent<BossController>();
        EnemyBalance bal = e.GetComponent<EnemyBalance>();
        bool lethal = boss == null && (ParryMark.IsMarked(e) || (bal != null && bal.IsBroken));

        e.ApplyAttackHit(hitDir, true, 3);
        if (lethal)
        {
            h.TakeDamage(Mathf.Max(rage.minDamage, h.MaxHealth));
            CombatCallout.PopupAbove(e.transform, "INFAZ!", new Color(1f, 0.35f, 0.3f), 1.2f);
            return;
        }
        // Isaretsiz / kirik olmayan: posturun yarisi + olculu can hasari.
        if (bal != null) bal.AddBalanceDamage(Mathf.RoundToInt(bal.MaxBalance * rage.postureFrac), false);
        float frac = boss != null ? rage.bossPercent : rage.healthFrac;
        if (!h.IsDead) h.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(h.MaxHealth * frac)));
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

    void Finish()
    {
        Exit();
        if (player.IsGrounded()) sm.ChangeState(new GroundedState(player, sm));
        else sm.ChangeState(new AirState(player, sm));
    }

    public void Exit()
    {
        if (view != null) { view.End(); view = null; }
        RestoreEnemies();
        player.rb.bodyType = prevType;
        player.rb.gravityScale = gravity;
        player.isInvincible = false;
        player.hitInvincibilityTimer = Mathf.Max(player.hitInvincibilityTimer, 0.35f);
        player.canControl = true;
        rage.Busy = false;
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
        Color c = rage.FlameActive ? rage.flameColor : rage.auraColor;
        float k = rage.bladeIntensity * pulse * (rage.FlameActive ? 1.5f : 1f);
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
            float hk = rage.haloIntensity * pulse * (rage.FlameActive ? 1.6f : 1f);
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
