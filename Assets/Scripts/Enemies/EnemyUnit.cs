using UnityEngine;

// Yeni düşman çekirdeği. Davranış tamamen EnemyDef / AttackDef'ten gelir.
// Durumlar: Idle, Chase (sıra/standby dahil), Windup, Active, Recover, Hurt, Stagger, Dead.
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyUnit : EnemyBase
{
    public enum UnitState { Idle, Chase, Windup, Active, Recover, Hurt, Stagger, Dead }

    [Header("Veri")]
    public EnemyDef def;

    [Header("Görsel")]
    [Tooltip("Boşsa gövde SpriteRenderer.flipX kullanılır; doluysa scale.x = bakış yönü.")]
    public Transform visualRoot;
    [SerializeField] private SpriteRenderer bodySprite;

    [Header("Okunabilirlik")]
    [Range(0.1f, 0.9f)] public float commitFraction = 0.55f;
    [Tooltip("Vuruştan bu kadar önce son uyarı (cue) çakar.")]
    public float cueLead = 0.14f;
    [Tooltip("Hurt/Stagger/Recover içinde oyuncuya doğru dönmeyi bırak.")]
    public bool lockFacingWhileAttacking = true;

    [Header("Parry yavaşlatma")]
    public float parrySlowDuration = 0.25f;
    public float parrySlowScale = 0.3f;
    public float breakSlowDuration = 0.45f;
    public float breakSlowScale = 0.15f;

    // ---- runtime ----
    private Rigidbody2D rb;
    private Health health;
    private EnemyBalance balance;
    private EnemyHitFeedback hitFeedback;
    private EnemyAttackTelegraph telegraph;
    private Animator animator;

    private UnitState state = UnitState.Idle;
    private AttackDef current;
    private float stateTime;
    private float facing = 1f;
    private float attackCooldownTimer;
    private float decisionTimer;
    private float[] attackCd;
    private AttackDef planned;
    private bool cueFired, committedFired, hitDone;
    private float lungeApplied;
    private float hurtDuration;
    private bool dead;

    private float flashTimer;
    private Color flashColor = Color.white;
    private Color baseColor = Color.white;

    public UnitState State { get { return state; } }
    public AttackDef CurrentAttack { get { return current; } }
    public float StateTime { get { return stateTime; } }
    public float HorizontalSpeed { get { return rb != null ? Mathf.Abs(rb.linearVelocity.x) : 0f; } }
    public string DebugState { get { return state + (current != null && (state == UnitState.Windup || state == UnitState.Active || state == UnitState.Recover) ? ":" + current.name : ""); } }

    // Animasyon köprüsü bu olaya abone olur.
    public event System.Action<UnitState, AttackDef> StateChanged;

    // ---- EnemyBase sözleşmesi ----
    public override bool IsDead { get { return dead || (health != null && health.IsDead); } }
    public override bool IsStaggered { get { return state == UnitState.Stagger; } }
    public override bool IsAttackCommitted
    {
        get
        {
            if (state == UnitState.Active) return true;
            if (state == UnitState.Windup && current != null && current.windup > 0f)
                return stateTime / current.windup >= commitFraction;
            return false;
        }
    }
    public override bool IsOpen { get { return state == UnitState.Stagger || (state == UnitState.Recover && !IsDead); } }
    public override float FacingDirection { get { return facing; } }
    public override int SlamBalanceDamage { get { return 3; } }
    public override float StaggerHitHealthFraction { get { return def != null ? def.staggerHitHealthFraction : 0f; } }

    // =========================================================
    // YAŞAM DÖNGÜSÜ
    // =========================================================

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        balance = GetComponent<EnemyBalance>();
        hitFeedback = GetComponent<EnemyHitFeedback>();
        telegraph = GetComponent<EnemyAttackTelegraph>();
        animator = GetComponentInChildren<Animator>();
        if (bodySprite == null) bodySprite = GetComponentInChildren<SpriteRenderer>();
        if (bodySprite != null) baseColor = bodySprite.color;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (health != null) health.OnDeath += HandleDeath;
    }

    protected override void OnDisable()
    {
        if (health != null) health.OnDeath -= HandleDeath;
        EnemyAttackCoordinator.ReleaseAttack(this);
        base.OnDisable();
    }

    private void Start()
    {
        ApplyDef();
        FindTarget();
        SetState(UnitState.Idle);
    }

    // Def değiştiğinde (arena) yeniden uygula.
    public void ApplyDef()
    {
        if (def == null) return;
        if (health != null) health.SetMaxHealth(def.maxHealth, true);
        if (balance != null)
        {
            balance.SetMaxBalance(def.maxBalance);
            balance.chipRatioOverride = def.hitChipHealthRatio;
        }
        attackCd = new float[def.attacks.Count];
    }

    public void ResetVitals()
    {
        if (health != null) health.SetHealth(health.MaxHealth);
        if (balance != null) { balance.ResetBalance(); }
    }

    private bool FindTarget()
    {
        if (target != null) return true;
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null)
        {
            target = p.transform;
            if (def != null && def.passThroughPlayer) IgnorePlayerCollision();
        }
        return target != null;
    }

    // Oyuncu ile gövde çarpışmasını kapat: birbirini itmez, içinden geçer.
    private void IgnorePlayerCollision()
    {
        Collider2D[] mine = GetComponentsInChildren<Collider2D>();
        Collider2D[] theirs = target.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < mine.Length; i++)
            for (int j = 0; j < theirs.Length; j++)
                Physics2D.IgnoreCollision(mine[i], theirs[j], true);
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (def == null || dead) return;
        float dt = EnemyTime.DeltaTime;
        stateTime += dt;

        if (attackCooldownTimer > 0f) attackCooldownTimer -= dt;
        if (attackCd != null)
            for (int i = 0; i < attackCd.Length; i++)
                if (attackCd[i] > 0f) attackCd[i] -= dt;

        if (animator != null) animator.speed = EnemyTime.Scale;

        if (!FindTarget()) { Stop(); return; }

        switch (state)
        {
            case UnitState.Idle:
            case UnitState.Chase: TickLocomotion(dt); break;
            case UnitState.Windup: TickWindup(dt); break;
            case UnitState.Active: TickActive(dt); break;
            case UnitState.Recover: Decelerate(dt); if (stateTime >= current.recovery) { SetState(UnitState.Chase); } break;
            case UnitState.Hurt: Decelerate(dt); if (stateTime >= hurtDuration) SetState(UnitState.Chase); break;
            case UnitState.Stagger: Decelerate(dt); if (stateTime >= def.staggerDuration) EndStagger(); break;
        }
    }

    private void LateUpdate()
    {
        UpdateMarker();
        if (bodySprite == null) return;
        if (flashTimer > 0f)
        {
            flashTimer -= EnemyTime.DeltaTime;
            bodySprite.color = flashColor;
            if (flashTimer <= 0f && telegraph == null) bodySprite.color = baseColor;
        }
        else if (state == UnitState.Stagger)
        {
            bodySprite.color = new Color(1f, 0.85f, 0.35f, baseColor.a);
        }
    }

    // =========================================================
    // HAREKET + KARAR
    // =========================================================

    private float DxToTarget { get { return target.position.x - transform.position.x; } }

    private void TickLocomotion(float dt)
    {
        float dx = DxToTarget;
        float dist = Mathf.Abs(dx);

        if (dist > def.sightRange) { SetState(UnitState.Idle); Stop(); return; }
        if (state == UnitState.Idle) SetState(UnitState.Chase);

        FaceTarget();

        bool ready = attackCooldownTimer <= 0f;

        if (!ready)
        {
            // Saldırı arası: sırada ise geride, değilse yakın dur.
            planned = null;
            Hold(dist);
            return;
        }

        EnemyAttackCoordinator.JoinQueue(this);
        int rank = EnemyAttackCoordinator.GetStandbyRank(this);
        if (rank > 0)
        {
            MoveToStandby(rank);
            planned = null;
            decisionTimer = 0f;
            return;
        }

        // Seçimi koru: koordinatör uyarı süresine göre izin verir; her karede yeniden
        // seçersek uzun uyarılı saldırı hep kazanır.
        int pi = planned == null ? -1 : def.attacks.IndexOf(planned);
        if (pi < 0 || !AttackUsable(pi, dist)) planned = PickAttack(dist);
        AttackDef pick = planned;
        if (pick == null)
        {
            // Menzile gir.
            float want = ApproachRange();
            if (dist > want) Move(Mathf.Sign(dx) * def.chaseSpeed * (dist > def.sprintDistance ? def.sprintMultiplier : 1f));
            else Stop();
            decisionTimer = 0f;
            return;
        }

        Stop();
        decisionTimer += dt;
        if (decisionTimer < def.attackDecisionDelay) return;

        if (EnemyAttackCoordinator.TryRequestAttack(this, pick.windup))
        {
            planned = null;
            StartAttack(pick);
        }
    }

    private void Hold(float dist)
    {
        float dx = DxToTarget;
        float comfy = Mathf.Max(def.standbyDistance * 0.6f, 1.2f);
        if (dist < comfy) Move(-Mathf.Sign(dx) * def.chaseSpeed * def.standbySpeedMultiplier);
        else Stop();
    }

    private void MoveToStandby(int rank)
    {
        float side = Mathf.Sign(transform.position.x - target.position.x);
        if (side == 0f) side = -facing;
        float want = target.position.x + side * (def.standbyDistance + (rank - 1) * def.standbySpacing);
        float delta = want - transform.position.x;
        if (Mathf.Abs(delta) <= def.arrivalTolerance) { Stop(); return; }
        Move(Mathf.Sign(delta) * def.chaseSpeed * def.standbySpeedMultiplier);
    }

    private AttackDef PickAttack(float dist)
    {
        float total = 0f;
        for (int i = 0; i < def.attacks.Count; i++)
            if (AttackUsable(i, dist)) total += Mathf.Max(0.01f, def.attacks[i].weight);
        if (total <= 0f) return null;
        float r = Random.value * total;
        for (int i = 0; i < def.attacks.Count; i++)
        {
            if (!AttackUsable(i, dist)) continue;
            r -= Mathf.Max(0.01f, def.attacks[i].weight);
            if (r <= 0f) return def.attacks[i];
        }
        return null;
    }

    private bool AttackUsable(int i, float dist)
    {
        AttackDef a = def.attacks[i];
        if (attackCd[i] > 0f || dist < a.minRange || dist > a.maxRange) return false;
        // Yakın dövüş saldırıları (minRange 0) önce ortak yakın menzile girmeyi bekler;
        // böylece tüm saldırılar seçilebilir (en uzun menzilli olan hep kazanmaz).
        if (a.minRange <= 0f && dist > CloseRange()) return false;
        return true;
    }

    // Hazır yakın dövüş saldırılarının en kısa menzili (x0.9): bu mesafede hepsi seçilebilir.
    private float CloseRange()
    {
        float best = float.MaxValue;
        for (int i = 0; i < def.attacks.Count; i++)
            if (attackCd[i] <= 0f && def.attacks[i].minRange <= 0f) best = Mathf.Min(best, def.attacks[i].maxRange * 0.9f);
        return best == float.MaxValue ? def.standbyDistance : best;
    }

    private float ApproachRange()
    {
        return CloseRange();
    }

    private void FaceTarget()
    {
        float dx = DxToTarget;
        if (Mathf.Abs(dx) < 0.05f) return;
        SetFacing(Mathf.Sign(dx));
    }

    private void SetFacing(float f)
    {
        facing = f;
        if (visualRoot != null)
        {
            Vector3 s = visualRoot.localScale;
            s.x = Mathf.Abs(s.x) * facing;
            visualRoot.localScale = s;
        }
        else if (bodySprite != null) bodySprite.flipX = facing < 0f;
    }

    private void Move(float vx)
    {
        // İvmeli hareket: ani kalkış/duruş yok, hareket okunur ama çevik.
        float nx = Mathf.MoveTowards(rb.linearVelocity.x, vx * EnemyTime.Scale, def.acceleration * EnemyTime.DeltaTime);
        rb.linearVelocity = new Vector2(nx, rb.linearVelocity.y);
    }

    private void Stop()
    {
        rb.linearVelocity = new Vector2(Mathf.MoveTowards(rb.linearVelocity.x, 0f, def.acceleration * 1.5f * EnemyTime.DeltaTime), rb.linearVelocity.y);
    }

    private void Decelerate(float dt)
    {
        rb.linearVelocity = new Vector2(Mathf.MoveTowards(rb.linearVelocity.x, 0f, def.knockbackDeceleration * dt), rb.linearVelocity.y);
    }

    // =========================================================
    // SALDIRI
    // =========================================================

    private void StartAttack(AttackDef a)
    {
        current = a;
        cueFired = committedFired = hitDone = false;
        activeInit = false;
        lungeApplied = 0f;
        decisionTimer = 0f;
        if (telegraph != null) telegraph.StartWarning(a.unblockable, a.windup);
        if (a.unblockable) PlayTintFlash(Color.white, 0.15f);
        SetState(UnitState.Windup);
    }

    private void TickWindup(float dt)
    {
        // Engellenemez saldırıda yön BAŞTAN kilitli: arkasına geçersen dönüp vurmaz.
        bool facingLocked = current.unblockable || (lockFacingWhileAttacking && committedFired);
        if (!facingLocked) FaceTarget();

        // Kararlı aşamaya kadar, oyuncu ÖNÜNDEyse menzili koru.
        if (!committedFired && !current.unblockable && current.lungeDistance <= 0f && current.hitReach > 0f)
        {
            float dxw = DxToTarget;
            if (dxw * facing > 0f && Mathf.Abs(dxw) > current.hitReach * 0.75f) Move(facing * def.chaseSpeed * 0.6f);
            else Stop();
        }
        else Stop();

        float p = current.windup <= 0f ? 1f : Mathf.Clamp01(stateTime / current.windup);

        if (telegraph != null) telegraph.SetProgress(p);

        if (!cueFired && stateTime >= current.windup - cueLead)
        {
            cueFired = true;
            if (telegraph != null) telegraph.TriggerCue();
        }
        if (!committedFired && p >= commitFraction)
        {
            committedFired = true;
            if (telegraph != null) telegraph.SetCommitted();
        }

        if (stateTime >= current.windup)
            SetState(UnitState.Active);
    }

    private float lungeTravel;
    private bool lungeCancelled;

    private void TickActive(float dt)
    {
        if (!activeInit)
        {
            activeInit = true;

            // Atılma mesafesi: oyuncuya olan GERÇEK mesafeyle sınırlı (içinden/arkasına geçmez).
            float dxs = DxToTarget;
            lungeTravel = (dxs * facing > 0f) ? Mathf.Max(0f, Mathf.Abs(dxs) - current.lungeStopGap) : 0f;
            lungeCancelled = false;
            lungeStrike = current.lungeDistance > 0f && lungeTravel > 0.5f;

            // Atılmasız saldırı: vuruş hemen. Atılmalı saldırı: önce kayar, VARINCA vurur.
            if (!lungeStrike)
            {
                hitDone = true;
                ResolveHit();
                if (state != UnitState.Active) return;
            }
        }

        // Lunge
        if (!lungeCancelled && lungeStrike && current.active > 0f && !hitDone)
        {
            float tt = Mathf.Clamp01(stateTime / current.active);
            float want = Mathf.Min(current.lungeDistance, lungeTravel) * current.lungeCurve.Evaluate(tt);
            float step = Mathf.Max(0f, want - lungeApplied);
            lungeApplied += step;
            rb.linearVelocity = new Vector2(facing * step / SafeDt(), rb.linearVelocity.y);
        }
        else Decelerate(dt);

        if (stateTime >= current.active)
        {
            // Atılmalı saldırının vuruşu kayma BİTİNCE gelir.
            if (!hitDone)
            {
                hitDone = true;
                ResolveHit();
                if (state != UnitState.Active) return;
            }
            SetState(UnitState.Recover);
        }
    }

    private bool activeInit;
    private bool lungeStrike;

    private float SafeDt() { return Mathf.Max(0.0001f, EnemyTime.DeltaTime); }

    private void ResolveHit()
    {
        EnemyAttackCoordinator.ReleaseAttack(this);
        if (telegraph != null) telegraph.StopWarning();

        PlayerController player = target.GetComponent<PlayerController>();
        if (player == null) return;

        bool unblockable = current.unblockable;

        // Kaçış
        if (player.isInvincible)
        {
            CombatEvents.RaiseDodge(this, unblockable);
            return;
        }

        // Menzil + ön alan
        float dx = target.position.x - transform.position.x;
        float dy = Mathf.Abs(target.position.y - transform.position.y);
        bool inFront = dx * facing >= -0.25f;
        bool reachable = inFront && Mathf.Abs(dx) <= current.hitReach && dy <= current.hitHeight;
        if (!reachable)
        {
            CombatEvents.RaiseAttackMissed(this, unblockable);
            return;
        }

        Vector2 dir = new Vector2(Mathf.Sign(dx == 0f ? facing : dx), 0f);

        if (unblockable) { DealHit(dir, true); return; }

        PlayerDefenseController defense = target.GetComponent<PlayerDefenseController>();

        if (defense != null && defense.CanParry())
        {
            defense.PlayParryFeedback();
            if (hitFeedback != null) hitFeedback.PlayParry(dir);
            // Parry posture barının sabit bir oranını doldurur (bar büyüse de parry hızlı kırar);
            // can götürmez. Düz vuruş ise bar büyüdükçe yavaşlar.
            if (balance != null)
            {
                int dmg = Mathf.Max(1, Mathf.RoundToInt(balance.MaxBalance * def.parryPostureFraction * Mathf.Max(0f, current.parryBalanceMultiplier)));
                balance.AddBalanceDamage(dmg, false);
            }
            PlayBalanceDamageFlash();
            lungeCancelled = true;
            bool broke = balance != null && balance.IsBroken;
            PlayParrySlowMotion(broke);
            CombatEvents.RaiseParry(this, broke);
            return;
        }

        if (defense != null && defense.CanBlock())
        {
            if (hitFeedback != null) hitFeedback.PlayBlock(dir);
            CombatImpactFeedback fx = target.GetComponent<CombatImpactFeedback>();
            if (fx != null) fx.PlayBlockImpact();
            lungeCancelled = true;
            ApplyBlockKnockback(dir);
            defense.HandleBlockHit(dir, Mathf.Max(1, def.blockPostureDamage));
            PlayerPosture posture = target.GetComponent<PlayerPosture>();
            CombatEvents.RaisePlayerBlocked(this, posture != null && posture.IsBroken);
            return;
        }

        DealHit(dir, false);
    }

    private void DealHit(Vector2 dir, bool unblockable)
    {
        PlayerDamageReceiver rec = target.GetComponent<PlayerDamageReceiver>();
        if (rec == null) { Debug.LogError("EnemyUnit: Player üzerinde PlayerDamageReceiver yok!"); return; }

        rec.TakeDamage(
            current.damage,
            dir,
            current.knockbackForce,
            current.knockbackVertical,
            current.knockbackDuration,
            -1f,
            this,
            unblockable ? PlayerHitKind.Unblockable : PlayerHitKind.Normal,
            false);
        EnemyAttackCoordinator.NotifyPlayerHit();
    }

    // =========================================================
    // STATE GEÇİŞİ
    // =========================================================

    private void SetState(UnitState s)
    {
        UnitState prev = state;

        if (prev == UnitState.Recover && s != UnitState.Recover) { /* toparlanma bitti */ }

        // Saldırı state'inden çıkış temizliği
        if ((prev == UnitState.Windup || prev == UnitState.Active) && s != UnitState.Active && s != UnitState.Windup)
        {
            EnemyAttackCoordinator.ReleaseAttack(this);
            if (telegraph != null) telegraph.StopWarning();
        }

        state = s;
        stateTime = 0f;

        if (s == UnitState.Recover)
        {
            StartAttackRecovery(); // global bekleme
            if (current != null)
            {
                int idx = def.attacks.IndexOf(current);
                if (idx >= 0) attackCd[idx] = current.cooldown + current.recovery;
            }
        }

        if (StateChanged != null) StateChanged(s, current);
    }

    private void CancelAttack()
    {
        if (state == UnitState.Windup || state == UnitState.Active)
        {
            EnemyAttackCoordinator.ReleaseAttack(this);
            if (telegraph != null) telegraph.StopWarning();
            if (current != null)
            {
                int idx = def.attacks.IndexOf(current);
                if (idx >= 0) attackCd[idx] = Mathf.Max(attackCd[idx], current.cooldown);
            }
        }
    }

    private void EnterHurt(float duration)
    {
        CancelAttack();
        hurtDuration = duration;
        SetState(UnitState.Hurt);
    }

    private void EndStagger()
    {
        if (balance != null) balance.RecoverBalance();
        SetState(UnitState.Chase);
    }

    private void HandleDeath()
    {
        if (dead) return;
        dead = true;
        CancelAttack();
        EnemyAttackCoordinator.ReleaseAttack(this);
        if (telegraph != null) telegraph.enabled = false;
        rb.linearVelocity = Vector2.zero;
        state = UnitState.Dead;
        if (StateChanged != null) StateChanged(UnitState.Dead, current);
        CombatEvents.RaiseEnemyKilled(this);
    }

    // =========================================================
    // HASAR ALMA (EnemyBase)
    // =========================================================

    private bool AbsorbsHit()
    {
        return def != null && def.superArmor && IsAttackCommitted;
    }

    private void Knock(Vector2 dir, float force)
    {
        float d = Mathf.Sign(dir.x);
        if (d == 0f) d = 1f;
        rb.linearVelocity = new Vector2(d * force * def.knockbackScale, rb.linearVelocity.y);
    }

    public override void ApplyBalanceHit(Vector2 hitDirection)
    {
        if (IsDead) return;
        if (AbsorbsHit()) { PlayBalanceDamageFlash(); return; }
        PlayBalanceDamageFlash();
        if (state != UnitState.Stagger)
        {
            Knock(hitDirection, def.balanceHitKnockback);
            EnterHurt(def.balanceHitStun);
        }
    }

    public override void ApplyAttackHit(Vector2 hitDirection, bool healthHit)
    {
        ApplyAttackHit(hitDirection, healthHit, 1);
    }

    public override void ApplyAttackHit(Vector2 hitDirection, bool healthHit, int attackStep)
    {
        if (IsDead) return;
        if (state == UnitState.Stagger)
        {
            Knock(hitDirection, def.healthHitKnockback);
            PlayTintFlash(new Color(1f, 0.35f, 0.3f, 1f), 0.08f);
            return;
        }
        if (AbsorbsHit()) { PlayTintFlash(Color.white, 0.06f); return; }

        Knock(hitDirection, healthHit ? def.healthHitKnockback : def.balanceHitKnockback);
        if (!healthHit) PlayBalanceDamageFlash();
        else PlayTintFlash(new Color(1f, 0.35f, 0.3f, 1f), 0.08f);
        // Denge kırıldıysa ForceStagger OnBalanceBroken yoluyla gelir; burada sadece sersemlet.
        if (state != UnitState.Stagger && state != UnitState.Hurt) EnterHurt(def.balanceHitStun);
    }

    public override void ApplyBlockKnockback(Vector2 hitDirection)
    {
        float d = Mathf.Sign(hitDirection.x);
        if (d == 0f) d = facing;
        rb.linearVelocity = new Vector2(-d * def.blockKnockback * def.knockbackScale, rb.linearVelocity.y);
    }

    public override void ForceStagger()
    {
        if (IsDead || state == UnitState.Stagger) return;
        CancelAttack();
        PlayTintFlash(new Color(1f, 0.85f, 0.35f, 1f), 0.15f);
        SetState(UnitState.Stagger);
    }

    public override void InterruptWithHitStun(float duration)
    {
        if (IsDead || state == UnitState.Stagger) return;
        EnterHurt(duration);
    }

    public override void StartAttackRecovery()
    {
        attackCooldownTimer = def != null ? def.attackCooldown : 0.5f;
    }

    public override void StartAttackRecovery(float duration)
    {
        attackCooldownTimer = Mathf.Max(0f, duration);
    }

    // =========================================================
    // GERİ BİLDİRİM
    // =========================================================

    public override void PlayTintFlash(Color color, float duration)
    {
        flashColor = color;
        flashTimer = duration;
        if (telegraph != null) telegraph.Suppress(duration);
    }

    public override void PlayBalanceDamageFlash()
    {
        PlayTintFlash(Color.white, 0.08f);
    }

    public override void PlayParrySlowMotion(bool brokeBalance)
    {
        if (brokeBalance) EnemyTime.RequestRamp(breakSlowDuration, breakSlowScale, 2f);
        else EnemyTime.RequestRamp(parrySlowDuration, parrySlowScale, 2f);
    }

    public override void PlayHealthHitSound()
    {
        PlayRandom(hitFeedback != null ? hitFeedback.HealthHitSounds : null);
    }

    public override void PlayPostureHitSound()
    {
        PlayRandom(hitFeedback != null ? hitFeedback.BalanceHitSounds : null);
    }

    private void PlayRandom(AudioClip[] clips)
    {
        if (hitFeedback == null || clips == null || clips.Length == 0) return;
        AudioClip c = clips[Random.Range(0, clips.Length)];
        if (c != null && hitFeedback.Source != null)
            EnemyHitFeedback.PlayLoud(hitFeedback.Source, c, 1f);
    }

    // =========================================================
    // ENGELLENEMEZ UYARISI: yerde vuruş alanı + "!" simgesi
    // =========================================================

    private GameObject markRoot;
    private SpriteRenderer markZone;
    private TextMesh markIcon;

    private void EnsureMarker()
    {
        if (markRoot != null) return;
        markRoot = new GameObject(name + "_UnblockableMarker");

        GameObject z = new GameObject("Zone");
        z.transform.SetParent(markRoot.transform, false);
        markZone = z.AddComponent<SpriteRenderer>();
        markZone.sprite = bodySprite != null ? bodySprite.sprite : null;
        if (bodySprite != null) markZone.sortingLayerID = bodySprite.sortingLayerID;
        markZone.sortingOrder = bodySprite != null ? bodySprite.sortingOrder - 1 : 0;

        GameObject i = new GameObject("Icon");
        i.transform.SetParent(markRoot.transform, false);
        markIcon = i.AddComponent<TextMesh>();
        markIcon.text = "!";
        markIcon.anchor = TextAnchor.MiddleCenter;
        markIcon.alignment = TextAlignment.Center;
        markIcon.characterSize = 0.6f;
        markIcon.fontSize = 100;
        markIcon.fontStyle = FontStyle.Bold;
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f != null)
        {
            markIcon.font = f;
            MeshRenderer mr = i.GetComponent<MeshRenderer>();
            mr.sharedMaterial = f.material;
            mr.sortingOrder = 600;
            if (bodySprite != null) mr.sortingLayerID = bodySprite.sortingLayerID;
        }
    }

    private void UpdateMarker()
    {
        bool show = !dead && state == UnitState.Windup && current != null && current.unblockable;
        if (!show)
        {
            if (markRoot != null && markRoot.activeSelf) markRoot.SetActive(false);
            return;
        }

        EnsureMarker();
        if (!markRoot.activeSelf) markRoot.SetActive(true);

        float p = current.windup <= 0f ? 1f : Mathf.Clamp01(stateTime / current.windup);
        bool cue = stateTime >= current.windup - cueLead;
        Vector3 pos = transform.position;

        markZone.transform.position = new Vector3(pos.x + facing * current.hitReach * 0.5f, pos.y, pos.z);
        markZone.transform.localScale = new Vector3(current.hitReach, current.hitHeight, 1f);
        markZone.color = cue ? new Color(1f, 1f, 1f, 0.55f) : new Color(1f, 0.15f, 0.1f, Mathf.Lerp(0.12f, 0.4f, p));

        Collider2D c = GetComponent<Collider2D>();
        float top = c != null ? c.bounds.max.y : pos.y + 4f;
        markIcon.transform.position = new Vector3(pos.x, top + 1.6f, pos.z);
        markIcon.color = cue ? Color.white : new Color(1f, 0.85f, 0.1f, 1f);
    }

    private void OnDestroy()
    {
        if (markRoot != null) Destroy(markRoot);
    }

    // =========================================================
    // DEBUG
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        if (def == null) return;
        Vector3 p = transform.position;
        float f = Application.isPlaying ? facing : 1f;
        foreach (AttackDef a in def.attacks)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.9f);
            Gizmos.DrawLine(p, p + Vector3.right * f * a.hitReach);
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Gizmos.DrawLine(p + Vector3.up * 0.3f, p + Vector3.up * 0.3f + Vector3.right * f * a.maxRange);
        }
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireSphere(p, def.standbyDistance);
    }
}
