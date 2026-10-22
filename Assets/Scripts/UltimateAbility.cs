using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ULTI: canin altindaki yetenek cubugu savasla dolar (vurus, parry, oldurme).
/// Dolunca [Q]: 1 sn odaklanir (hasar almaz), sonra cok buyuk bir kesik patlar ve alana agir hasar verir.
/// Kendi kendini kurar (sahneye bir sey koymak gerekmez).
/// </summary>
public class UltimateAbility : MonoBehaviour
{
    public static UltimateAbility Instance { get; private set; }

    /// <summary>true iken Q tusu bu yetenege aittir (eski Kin yetenegi Q'yu dinlemez).</summary>
    public static bool OwnsQ => Instance != null;

    public KeyCode key = KeyCode.Q;
    public float maxCharge = 100f;
    public float hitCharge = 3f;
    public float parryCharge = 15f;
    public float killCharge = 12f;

    [Header("Patlama")]
    public float focusTime = 1f;
    public float radiusX = 20f;
    public float radiusY = 9f;
    [Range(0f, 1f)] public float balancePercent = 1f;
    [Range(0f, 1f)] public float healthPercent = 0.65f;
    public float vfxScale = 10f;
    public float afterBlastInvulnerable = 0.35f;

    [Header("Cubuk (1080p piksel)")]
    public float barGap = 8f;
    public float barHeight = 4f;
    public Color fillColor = new Color(0.55f, 0.6f, 1f, 0.95f);
    public Color readyColor = new Color(0.85f, 0.9f, 1f, 1f);
    public Color backColor = new Color(0.55f, 0.6f, 1f, 0.18f);

    public float Charge { get; private set; }
    public bool Ready => Charge >= maxCharge;
    public bool Casting { get; private set; }

    private PlayerController player;
    private Health health;
    private GameObject vfxPrefab;
    private float shown;
    private float flash;
    private bool wasInvincible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (FindFirstObjectByType<UltimateAbility>() != null) return;
        GameObject go = new GameObject("UltimateAbility");
        DontDestroyOnLoad(go);
        go.AddComponent<UltimateAbility>();
    }

    private void Awake()
    {
        Instance = this;
        vfxPrefab = Resources.Load<GameObject>("Ultimate/Slash_Aoe_Ultimate");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        CombatEvents.EnemyHit += OnEnemyHit;
        CombatEvents.ParrySucceeded += OnParry;
        CombatEvents.EnemyKilled += OnKilled;
    }

    private void OnDisable()
    {
        CombatEvents.EnemyHit -= OnEnemyHit;
        CombatEvents.ParrySucceeded -= OnParry;
        CombatEvents.EnemyKilled -= OnKilled;
    }

    private void Add(float v)
    {
        if (Casting || v <= 0f) return;
        bool was = Ready;
        Charge = Mathf.Min(maxCharge, Charge + v);
        if (!was && Ready) flash = 1f;
    }

    private void OnEnemyHit(EnemyBase enemy, DamageInfo info, HitResult result)
    {
        if (info.source == DamageSource.Attack && result.hit) Add(hitCharge);
    }

    private void OnParry(EnemyBase enemy, bool brokeBalance) { Add(parryCharge); }

    private void OnKilled(EnemyBase enemy) { Add(killCharge); }

    /// <summary>Test icin cubugu doldur.</summary>
    public void Fill() { Charge = maxCharge; flash = 1f; }

    private bool Bind()
    {
        PlayerController p = player != null ? player : FindFirstObjectByType<PlayerController>();
        if (p == null) return false;

        if (p != player)
        {
            player = p;
            health = p.GetComponent<Health>();
            Charge = 0f;
            shown = 0f;
            Casting = false;
        }

        return true;
    }

    private bool CanCast()
    {
        if (Casting || !Ready) return false;
        if (player == null || !player.canControl || player.inputLocked) return false;
        if (Time.timeScale < 0.01f) return false;
        if (health != null && health.IsDead) return false;
        if (player.isDashing || ExecutionSlide.Active) return false;

        IPlayerState s = player.stateMachine != null ? player.stateMachine.CurrentState : null;
        if (s is PlayerHurtState || s is PlayerDeathState || s is PlayerPostureBreakState) return false;
        return true;
    }

    private void Update()
    {
        if (!Bind()) return;

        shown = Mathf.MoveTowards(shown, Charge / maxCharge, Time.unscaledDeltaTime * 1.5f);
        flash = Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime * 2f);

        if (MobileInput.GetKeyDown(key) && CanCast())
            StartCoroutine(Cast());
    }

    private IEnumerator Cast()
    {
        Casting = true;

        PlayerCombatController combat = player.GetComponent<PlayerCombatController>();
        if (combat != null && combat.IsAttacking) combat.CancelAttack();

        wasInvincible = player.isInvincible;
        player.isInvincible = true;
        player.inputLocked = true;

        float t = 0f;
        float ghost = 0f;

        while (t < focusTime)
        {
            float dt = Time.deltaTime;
            t += dt;
            ghost -= dt;

            if (player.rb != null) player.rb.linearVelocity = new Vector2(0f, player.rb.linearVelocity.y);
            player.isInvincible = true;

            // cubuk bosalarak odaklanmayi gosterir
            shown = Mathf.Lerp(1f, 0f, t / focusTime);

            if (ghost <= 0f)
            {
                ghost = 0.09f;
                DashFx.Ghost(player);
            }

            // yukselen titresim
            RunCameraZoom.AddShake(0.12f + 0.5f * (t / focusTime));

            yield return null;
        }

        Blast();
        Charge = 0f;
        shown = 0f;

        player.inputLocked = false;

        // patlamadan sonra kisa sure daha dokunulmaz
        float hold = afterBlastInvulnerable;
        while (hold > 0f) { hold -= Time.deltaTime; player.isInvincible = true; yield return null; }

        player.isInvincible = wasInvincible;
        Casting = false;
    }

    private void Blast()
    {
        Vector3 pos = player.transform.position;

        SpawnVfx(pos);

        RunCameraZoom.AddShake(3f);
        HitStop.Request(0.12f, 0.05f);

        List<EnemyBase> all = new List<EnemyBase>(EnemyBase.All);
        for (int i = 0; i < all.Count; i++)
        {
            EnemyBase e = all[i];
            if (e == null || e.IsDead || !e.gameObject.activeInHierarchy) continue;

            Vector3 d = e.transform.position - pos;
            if (Mathf.Abs(d.x) > radiusX || Mathf.Abs(d.y) > radiusY) continue;

            float dir = d.x >= 0f ? 1f : -1f;

            EnemyBalance bal = e.GetComponent<EnemyBalance>();
            Health h = e.GetComponent<Health>();

            DamageInfo info = new DamageInfo
            {
                source = DamageSource.Ability,
                comboStep = 0,
                balanceDamage = bal != null ? Mathf.Max(1, Mathf.RoundToInt(bal.MaxBalance * balancePercent)) : 0,
                healthDamage = h != null ? Mathf.Max(1, Mathf.RoundToInt(h.MaxHealth * healthPercent)) : 0,
                direction = new Vector2(dir, 0f),
                hitPosition = e.transform.position + Vector3.up
            };

            PlayerDamage.HitEnemy(e, info);
        }
    }

    private void SpawnVfx(Vector3 pos)
    {
        if (vfxPrefab == null) return;

        GameObject go = Instantiate(vfxPrefab, new Vector3(pos.x, pos.y + 1f, pos.z), Quaternion.identity);
        go.transform.localScale = Vector3.one * vfxScale;

        ParticleSystem[] ps = go.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < ps.Length; i++)
        {
            ParticleSystem.MainModule m = ps[i].main;
            m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            ps[i].Play();
        }

        Destroy(go, 5f);
    }

    private void LateUpdate()
    {
        if (player == null || MainMenu.Active || !MinimalHealthBar.HasRect) return;

        float s = HudCanvas.Scale();
        Rect hb = MinimalHealthBar.LastRect;
        float h = Mathf.Max(2f, barHeight * s);
        Rect r = new Rect(hb.x, hb.yMax + barGap * s, hb.width, h);

        float pulse = Ready && !Casting ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 8f) : 1f;
        Color c = Ready && !Casting ? Color.Lerp(fillColor, readyColor, 0.5f + 0.5f * pulse) : fillColor;
        if (flash > 0f) c = Color.Lerp(c, Color.white, flash);

        HudCanvas.Fill(r, backColor);
        HudCanvas.Fill(new Rect(r.x, r.y, r.width * shown, r.height), c);
    }
}
