using UnityEngine;
using System.Collections;

public class PlayerCombatController : MonoBehaviour
{
    public PlayerController player;
    public LayerMask enemyLayer;

    [SerializeField] private PlayerDefenseController defenseController;

    public float inputBufferTime = 0.2f;

    [Header("İnfaz hızı")]
    [Tooltip("Sersemlemiş (infaz edilebilir) düşmana yönelen vuruş bu kat daha HIZLI oynar (süre / bu değer). 1 = kapalı.")]
    [Min(1f)] public float executionSpeedMult = 1.8f;
    private bool lastLungeStaggered;
    public float comboResetTime = 0.45f;

    [Header("Tepki (iptal penceresi)")]
    [Tooltip(
        "Saldırının bu oranından sonra (vuruş karesi geçmişse) tamponlu " +
        "sonraki saldırı, animasyonun bitmesini beklemeden başlar. " +
        "1 = eski davranış (sonuna kadar bekle).")]
    [Range(0f, 1f)]
    public float comboCancelPoint = 0.6f;

    [Header("Havada saldırı")]
    [Tooltip("İnişe kadar yan vuruş hakkı (aşağı vuruş sınırsız, pogo hakları yeniler).")]
    [Min(0)] public int maxAirAttacks = 2;
    public float airAttackDuration = 0.28f;
    [Range(0f, 1f)] public float airAttackHitTime = 0.35f;
    [Tooltip("Yan vuruş sırasında en yüksek düşüş hızı (asılı kalma hissi).")]
    public float airAttackMaxFallSpeed = 3f;

    [Header("Aşağı vuruş (↓ + saldırı, havada) / pogo")]
    public float downAttackDuration = 0.3f;
    [Range(0f, 1f)] public float downAttackHitTime = 0.25f;
    public Vector2 downAttackBoxSize = new Vector2(1.4f, 1.7f);
    public float downAttackBalanceMultiplier = 1.2f;
    [Tooltip("Aşağı vuruş isabet edince yukarı sıçrama hızı.")]
    public float pogoBounceVelocity = 15f;

    private float bufferTimer;
    private float comboTimer;
    private int comboStep;
    private int airAttacksLeft;
    private int airStep;
    private PlayerAnimationController animationController;

    // Vuruş kutusu (silah çarpanı kaldırıldı).
    public Vector2 EffectiveHitBox
    {
        get { return new Vector2(Mathf.Max(0.2f, swordTipEnd - swordTipStart), swordTipHeight); }
    }

    // Hasar sadece kilicin UCUNDA: oyuncunun ayagindan (x) swordTipStart..swordTipEnd mesafesi.
    // Sap / govde bolgesindeki (daha yakin) dusman hasar almaz; arkaya da vurmaz.
    public Vector2 SwordTipCenter(float dir)
    {
        float x = player.transform.position.x + (dir >= 0f ? 1f : -1f) * (swordTipStart + swordTipEnd) * 0.5f;
        float y = player.attackPoint != null ? player.attackPoint.position.y : player.transform.position.y + 1.6f;
        return new Vector2(x, y);
    }

    private float SwordTipMid => (swordTipStart + swordTipEnd) * 0.5f;

    // Aktif saldırının zamanlaması (iptal / zıplama kuralları için).
    private float attackStartTime;
    private float attackDurationNow;
    private float attackHitTimeNow;

    [Header("Kılıç ucu hasar bölgesi")]
    [Tooltip("Hasar bölgesi oyuncunun ayağından bu kadar uzakta başlar (sap/gövde bölgesi hasar vermez).")]
    public float swordTipStart = 3.6f;
    [Tooltip("Kılıç ucunun oyuncudan en uzak noktası.")]
    public float swordTipEnd = 5.8f;
    [Tooltip("Hasar bölgesinin yüksekliği.")]
    public float swordTipHeight = 3.6f;

    [Header("Attack Range")]
    public Vector2 hitBoxSize = new Vector2(1.5f, 1.2f);

    [Header("Hit VFX Position")]
    [SerializeField]
    private float hitVFXInsideAmount = 0.65f;

    [SerializeField]
    private float hitVFXForwardOffset = 0.20f;

    public float HitVFXInsideAmount =>
        hitVFXInsideAmount;

    public float HitVFXForwardOffset =>
        hitVFXForwardOffset;

    [Header("Balance Damage (düşmanın dengesine)")]
    [Tooltip(
        "Her kombo vuruşunun düşman dengesine verdiği hasar. " +
        "Düşmanın EnemyBalance.maxBalance değeriyle birlikte düşün " +
        "(prefab'da 7). Parry: EnemyBase.parryBalanceDamage, " +
        "Slam: EnemyBase.slamBalanceDamage.")]
    [Min(0)] public int attack1BalanceDamage = 1;
    [Min(0)] public int attack2BalanceDamage = 1;
    [Min(0)] public int attack3BalanceDamage = 1;
    [Min(0)] public int attack4BalanceDamage = 1;

    public int GetBalanceDamage(int comboStep)
    {
        switch (comboStep)
        {
            case 1:
                return attack1BalanceDamage;

            case 2:
                return attack2BalanceDamage;

            case 3:
                return attack3BalanceDamage;

            default:
                return attack4BalanceDamage;
        }
    }

    [Header("Health Damage (sersemlemiş düşmana)")]
    [Tooltip(
        "Kombo vuruşunun, DENGESİ KIRIK düşmanın canına verdiği taban hasar. " +
        "Düşman canı büyük ölçekteyse (ör. 100) bunu artır; charm'lar bunu " +
        "çarpar.")]
    [Min(1)] public int attackHealthDamage = 1;

    [Header("Akış: mıknatıs / riposte / hareketle iptal")]
    [Tooltip("Saldırı başlarken önündeki düşman bu mesafe içindeyse ona doğru kayar (havaya vurma bitsin).")]
    public float magnetRange = 3.2f;
    [Tooltip("Normal saldırıda mıknatısın en fazla kaydıracağı mesafe.")]
    public float magnetMaxDistance = 2.6f;
    [Tooltip("Düşmanın önünde durulacak mesafe (vuruş kutusunun içinde kalsın).")]
    public float magnetStopGap = 1.1f;
    [Tooltip("Parry sonrası ilk saldırı (riposte): bu mesafedeki düşmana atılır; sersemlemiş düşman öncelikli.")]
    public float riposteRange = 9f;
    public float riposteMaxDistance = 8f;
    [Tooltip("Vuruştan sonra, saldırının bu oranından itibaren yön tuşu saldırının toparlanmasını iptal eder (yapışkanlık azalır).")]
    [Range(0.3f, 1f)] public float moveCancelPoint = 0.72f;

    [Header("Attack Move (Feel)")]
    public float attackMoveDistance = 0.25f;
    public float attackMoveSpeed = 6f;
    public AnimationCurve attackMoveCurve =
        AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Attack Timing")]
    public float attack1Duration = 0.33f;
    public float attack2Duration = 0.33f;
    public float attack3Duration = 0.33f;
    public float attack4Duration = 0.33f;

    [Header("Attack 1")]
    [Range(0f, 1f)]
    public float attack1MoveStart = 0.15f;

    [Range(0f, 1f)]
    public float attack1MoveEnd = 0.70f;

    [Range(0f, 1f)]
    public float attack1HitTime = 0.35f;

    [Header("Attack 2")]
    [Range(0f, 1f)]
    public float attack2MoveStart = 0.15f;

    [Range(0f, 1f)]
    public float attack2MoveEnd = 0.70f;

    [Range(0f, 1f)]
    public float attack2HitTime = 0.35f;

    [Header("Attack 3")]
    [Range(0f, 1f)]
    public float attack3MoveStart = 0.15f;

    [Range(0f, 1f)]
    public float attack3MoveEnd = 0.70f;

    [Range(0f, 1f)]
    public float attack3HitTime = 0.35f;

    [Header("Attack 4")]
    [Range(0f, 1f)]
    public float attack4MoveStart = 0.10f;

    [Range(0f, 1f)]
    public float attack4MoveEnd = 0.75f;

    [Range(0f, 1f)]
    public float attack4HitTime = 0.50f;

    private ICombatState currentState;

    // =========================================================
    // DIŞARIYA AÇIK DURUM
    // =========================================================

    public bool IsAttacking => currentState != null;

    // Son saldiri tusuna basilma zamani (kosarken iptal kurali icin).
    public float LastAttackPressTime { get; private set; } = -10f;

    // Saldırının ilerleme oranı (0..1). Saldırı yoksa 1.
    public float AttackProgress =>
        currentState != null && attackDurationNow > 0f
            ? Mathf.Clamp01((Time.time - attackStartTime) / attackDurationNow)
            : 1f;

    // Vuruş karesi henüz gelmedi mi? (Bu aralıkta zıplama tampon bekler;
    // dash ve parry yine de iptal eder.)
    public bool IsAttackCommitted =>
        currentState != null &&
        AttackProgress < attackHitTimeNow;

    void Awake()
    {
        if (player == null)
            player = GetComponent<PlayerController>();

        if (defenseController == null)
            defenseController =
                GetComponent<PlayerDefenseController>();

        animationController = GetComponent<PlayerAnimationController>();

        airAttacksLeft = maxAirAttacks;
    }

    void OnDisable()
    {
        // Ölümde combat controller kapatılıyor.
        // Yarım kalan saldırı geri açılınca devam etmesin.
        CancelAttack();
    }

    void Update()
    {
        bufferTimer -= Time.deltaTime;
        comboTimer -= Time.deltaTime;

        // =====================================================
        // DEFENSE LOCK
        // =====================================================

        // Saldırı tuşu savunmadayken de tampona yazılır (eskiden siliniyordu:
        // parry'den hemen sonra basılan saldırı kayboluyordu).
        if (MobileInput.GetMouseButtonDown(0))
            LastAttackPressTime = Time.time;

        if (MobileInput.GetMouseButtonDown(0))
            bufferTimer = Mathf.Max(
                Mathf.Max(inputBufferTime, player.minInputBuffer),
                defenseController != null ? defenseController.postParryRecovery + 0.1f : 0f);

        if (
            defenseController != null &&
            defenseController.IsDefending
        )
        {
            // PARRY → SALDIRI: başarılı parry'den hemen sonra saldırı
            // savunmayı keser ve ANINDA başlar (riposte akışı).
            if (
                bufferTimer > 0f &&
                defenseController.RecentlyParried &&
                !defenseController.InParryRecovery
            )
            {
                defenseController.CancelDefense();
            }
            else
            {
                currentState?.Tick();
                return;
            }
        }

        // =====================================================
        // ATTACK INPUT (tampon)
        // Havadayken basılan saldırı da tampona yazılır: yere
        // inince (süre dolmadıysa) başlar.
        // =====================================================

        if (comboTimer <= 0f && currentState == null)
            comboStep = 0;

        // Yerdeyken havada saldırı hakları dolar.
        if (player.IsGrounded())
        {
            airAttacksLeft = maxAirAttacks;
            airStep = 0;
        }

        // =====================================================
        // HAVADA SALDIRI: basınca hemen (inişi beklemez).
        // =====================================================

        if (
            bufferTimer > 0f &&
            currentState == null &&
            CanStartAirAttack(out bool downAttack)
        )
        {
            bufferTimer = 0f;
            StartAirAttack(downAttack);
        }

        // =====================================================
        // KOMBO İPTAL PENCERESİ
        // Vuruş karesi geçtiyse ve saldırı yeterince ilerlediyse,
        // sıradaki saldırı animasyonun sonunu beklemeden başlar.
        // =====================================================

        if (
            bufferTimer > 0f &&
            currentState != null &&
            !(currentState is AirAttackState) &&
            AttackProgress >= Mathf.Max(comboCancelPoint, attackHitTimeNow) &&
            CanStartAttack()
        )
        {
            bufferTimer = 0f;

            ICombatState old = currentState;
            currentState = null;
            old.Exit();

            StartAttack();
        }

        if (
            bufferTimer > 0f &&
            currentState == null &&
            CanStartAttack()
        )
        {
            // Hurt / dash / slam / posture break sırasında
            // tampon beklemeye devam eder, süresi dolarsa düşer.
            bufferTimer = 0f;
            StartAttack();
        }

        currentState?.Tick();
    }

    // Parry sonrası ilk vuruşta ekstra posture hasarı.
    [HideInInspector] public bool counterHit;

    public int GetHitBalance(int comboStep)
    {
        return GetBalanceDamage(comboStep) +
            (counterHit && defenseController != null ? defenseController.counterBonusBalance : 0);
    }

    private bool ParryRecovering =>
        defenseController != null && defenseController.InParryRecovery;

    private float nextAttackAllowedTime;

    private bool CanStartAttack()
    {
        return
            !ParryRecovering &&
            (Time.time >= nextAttackAllowedTime ||
             (defenseController != null && defenseController.RecentlyParried)) &&
            player.IsGrounded() &&
            player.canControl &&
            player.canAttack &&
            !player.inputLocked;
    }

    private bool CanStartAirAttack(out bool down)
    {
        down = false;

        if (player.IsGrounded())
            return false;

        if (!player.canControl || !player.canAttack || player.inputLocked)
            return false;

        if (ParryRecovering)
            return false;

        if (defenseController != null && defenseController.IsDefending)
            return false;

        down = player.verticalInput < -0.5f;

        // İnmek üzereyken yan vuruş başlatma: tampon bekler, yere değince
        // YER KOMBOSU başlar (havada başlayıp hemen kesilmesin).
        if (!down && AboutToLand())
            return false;

        // Aşağı vuruş sınırsız; yan vuruş hakla.
        return down || airAttacksLeft > 0;
    }

    private bool AboutToLand()
    {
        if (player.rb == null || player.col == null || player.Movement == null)
            return false;

        if (player.rb.linearVelocity.y > -0.5f)
            return false;

        Bounds b = player.col.bounds;

        RaycastHit2D hit =
            Physics2D.BoxCast(
                new Vector2(b.center.x, b.min.y + 0.05f),
                new Vector2(b.size.x * 0.8f, 0.05f),
                0f,
                Vector2.down,
                0.7f,
                player.Movement.groundMask
            );

        return hit.collider != null;
    }

    // Yeni vuruş başlarken basılı yön tuşuna dön (kombo ortasında arkaya
    // dönebilmek için; saldırı sırasında yön kilitli olduğundan).
    private void FaceInputDirection()
    {
        if (Mathf.Abs(player.moveInput) < 0.1f)
        {
            return;
        }

        player.facingDir = Mathf.Sign(player.moveInput);

        if (player.playerSprite != null)
            player.playerSprite.flipX = player.facingDir < 0f;
    }

    private void StartAirAttack(bool down)
    {
        if (!down)
            airAttacksLeft--;

        FaceInputDirection();

        airStep = airStep % 2 + 1;

        float duration = down ? downAttackDuration : airAttackDuration;
        float hit = down ? downAttackHitTime : airAttackHitTime;

        attackStartTime = Time.time;
        attackDurationNow = duration;
        attackHitTimeNow = hit;

        currentState =
            new AirAttackState(
                player,
                this,
                enemyLayer,
                down,
                airStep,
                duration,
                hit,
                OnAirAttackEnd
            );

        currentState.Enter();
    }

    private void OnAirAttackEnd()
    {
        currentState = null;
    }

    /// <summary>Pogo: havada yan vuruş hakları yenilenir.</summary>
    public void RefillAirAttacks()
    {
        airAttacksLeft = maxAirAttacks;
    }

    public void PlayAirAttackAnimation(bool down, int step)
    {
        if (animationController != null)
            animationController.PlayAirAttackAnimation(down, step);
        else
            player.PlayAttackAnimation(down ? 3 : step);
    }

    // =========================================================
    // CANCEL
    // =========================================================

    public void CancelAttack()
    {
        bufferTimer = 0f;

        if (currentState == null)
            return;

        ICombatState state = currentState;

        currentState = null;

        // Sahne kapanırken rb yok edilmiş olabilir.
        if (
            player == null ||
            player.rb == null
        )
        {
            return;
        }

        // AttackState.Exit -> OnAttackEnd çağırır.
        state.Exit();
    }

    void StartAttack()
    {
        if (
            defenseController != null &&
            defenseController.IsDefending
        )
        {
            return;
        }

        if (!player.IsGrounded())
            return;

        if (!player.canAttack)
            return;

        FaceInputDirection();

        counterHit = defenseController != null && defenseController.ConsumeCounter();

        // =====================================================
        // COMBO STEP
        // =====================================================

        comboStep =
            Mathf.Clamp(
                comboStep + 1,
                1,
                4
            );

        comboTimer = comboResetTime;

        // =====================================================
        // ATTACK DURATION / TIMING
        // =====================================================

        float attackDuration;
        float moveStart;
        float moveEnd;
        float hitTime;

        switch (comboStep)
        {
            case 1:
                attackDuration = attack1Duration;
                moveStart = attack1MoveStart;
                moveEnd = attack1MoveEnd;
                hitTime = attack1HitTime;
                break;

            case 2:
                attackDuration = attack2Duration;
                moveStart = attack2MoveStart;
                moveEnd = attack2MoveEnd;
                hitTime = attack2HitTime;
                break;

            case 3:
                attackDuration = attack3Duration;
                moveStart = attack3MoveStart;
                moveEnd = attack3MoveEnd;
                hitTime = attack3HitTime;
                break;

            default:
                attackDuration = attack4Duration;
                moveStart = attack4MoveStart;
                moveEnd = attack4MoveEnd;
                hitTime = attack4HitTime;
                break;
        }

        float lunge = ComputeMagnetLunge();

        attackDuration /= Charms.StormMult;   // FIRTINA charm'ı: saldırı hızı

        // İNFAZ: sersemlemiş düşmana giden vuruş çok daha çabuk.
        bool execAttack = lastLungeStaggered && executionSpeedMult > 1f;
        if (execAttack) attackDuration /= executionSpeedMult;

        // (lunge, süre hesabından önce yukarıda hesaplanır)

        attackStartTime = Time.time;
        attackDurationNow = attackDuration;
        attackHitTimeNow = hitTime;

        // Sabit saldırı ritmi: saldırı nasıl biterse bitsin (yön iptali, dash, zıplama),
        // sonraki yer vuruşu en erken normal kombo iptal anında başlar.
        nextAttackAllowedTime = Time.time + attackDuration * Mathf.Max(comboCancelPoint, hitTime);

        // =====================================================
        // CREATE ATTACK STATE
        // =====================================================

        currentState = new AttackState(
            player,
            enemyLayer,
            comboStep,
            OnAttackEnd,
            attackMoveDistance * lunge,
            attackMoveSpeed,
            attackMoveCurve,
            attackDuration,
            moveStart,
            moveEnd,
            hitTime
        );

        if (execAttack)
            player.SetAttackAnimationSpeed(executionSpeedMult);

        if (player.Movement != null) player.Movement.DisarmParryBurst();   // saldırı, parry atılma hakkını yakar
        PlayerGrunt.Play();   // saldırı sesi: her vuruşta "hah!"
        currentState.Enter();
    }

    // Önündeki düşmana doğru saldırı kayması. Riposte'ta (parry sonrası) uzun atılma, sersemlemiş düşman öncelikli.
    private float ComputeMagnetLunge()
    {
        lastLungeStaggered = false;
        if (attackMoveDistance <= 0.001f) return 1f;
        float dir = player.facingDir >= 0f ? 1f : -1f;
        float range = counterHit ? riposteRange : magnetRange;
        float maxDist = counterHit ? riposteMaxDistance : magnetMaxDistance;
        EnemyBase best = null;
        float bestScore = float.MaxValue;
        float bestDx = 0f;
        Vector3 pp = player.transform.position;
        for (int i = 0; i < EnemyBase.All.Count; i++)
        {
            EnemyBase e = EnemyBase.All[i];
            if (e == null || e.IsDead) continue;
            float dx = (e.transform.position.x - pp.x) * dir;
            if (dx < 0.2f || dx > range + swordTipEnd) continue;
            if (Mathf.Abs(e.transform.position.y - pp.y) > 3f) continue;
            float score = dx - (counterHit && e.IsStaggered ? 100f : 0f);
            if (score < bestScore) { bestScore = score; best = e; bestDx = dx; }
        }
        if (best == null) return 1f;
        lastLungeStaggered = best.IsStaggered;
        // Kilic dovusu: dusmanin govdesi kilic ucu mesafesine (swordTipEnd) gelene kadar yaklas, daha fazla degil.
        float dist = Mathf.Clamp(bestDx - swordTipEnd, 0f, maxDist);
        return dist / attackMoveDistance;
    }

    void OnAttackEnd()
    {
        currentState = null;

        // Attack4 tamamlandıysa combo kapanır.
        // Bir sonraki saldırı yeniden Attack1 olur.
        if (comboStep >= 4)
        {
            comboStep = 0;
            comboTimer = 0f;
        }
    }
}