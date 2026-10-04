using UnityEngine;
using System.Collections;

public class PlayerCombatController : MonoBehaviour
{
    public PlayerController player;
    public LayerMask enemyLayer;

    [SerializeField] private PlayerDefenseController defenseController;

    public float inputBufferTime = 0.2f;
    public float comboResetTime = 0.45f;

    [Header("Tepki (iptal penceresi)")]
    [Tooltip(
        "Saldırının bu oranından sonra (vuruş karesi geçmişse) tamponlu " +
        "sonraki saldırı, animasyonun bitmesini beklemeden başlar. " +
        "1 = eski davranış (sonuna kadar bekle).")]
    [Range(0f, 1f)]
    public float comboCancelPoint = 0.6f;

    private float bufferTimer;
    private float comboTimer;
    private int comboStep;

    // Aktif saldırının zamanlaması (iptal / zıplama kuralları için).
    private float attackStartTime;
    private float attackDurationNow;
    private float attackHitTimeNow;

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
        "(prefab'da 7). Parry: EnemyController.parryBalanceDamage, " +
        "Slam: EnemyController.slamBalanceDamage.")]
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

        if (
            defenseController != null &&
            defenseController.IsDefending
        )
        {
            bufferTimer = 0f;

            currentState?.Tick();

            return;
        }

        // =====================================================
        // ATTACK INPUT (tampon)
        // Havadayken basılan saldırı da tampona yazılır: yere
        // inince (süre dolmadıysa) başlar.
        // =====================================================

        if (Input.GetMouseButtonDown(0))
            bufferTimer = inputBufferTime;

        if (comboTimer <= 0f && currentState == null)
            comboStep = 0;

        // =====================================================
        // KOMBO İPTAL PENCERESİ
        // Vuruş karesi geçtiyse ve saldırı yeterince ilerlediyse,
        // sıradaki saldırı animasyonun sonunu beklemeden başlar.
        // =====================================================

        if (
            bufferTimer > 0f &&
            currentState != null &&
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

    private bool CanStartAttack()
    {
        return
            player.IsGrounded() &&
            player.canControl &&
            player.canAttack &&
            !player.inputLocked;
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

        attackStartTime = Time.time;
        attackDurationNow = attackDuration;
        attackHitTimeNow = hitTime;

        // =====================================================
        // CREATE ATTACK STATE
        // =====================================================

        currentState = new AttackState(
            player,
            enemyLayer,
            comboStep,
            OnAttackEnd,
            attackMoveDistance,
            attackMoveSpeed,
            attackMoveCurve,
            attackDuration,
            moveStart,
            moveEnd,
            hitTime
        );

        currentState.Enter();
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