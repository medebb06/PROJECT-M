using UnityEngine;
using System.Collections;

public class PlayerCombatController : MonoBehaviour
{
    public PlayerController player;
    public LayerMask enemyLayer;

    [SerializeField] private PlayerDefenseController defenseController;

    public float inputBufferTime = 0.2f;
    public float comboResetTime = 0.45f;

    private float bufferTimer;
    private float comboTimer;
    private int comboStep;

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
        // ATTACK INPUT
        // =====================================================
        // FIX: Havadayken tıklayınca eskiden Update'ten
        // return ediliyordu ve o karede currentState.Tick()
        // atlanıyordu. Artık sadece buffer'a yazılmıyor.

        if (
            Input.GetMouseButtonDown(0) &&
            player.IsGrounded()
        )
        {
            bufferTimer = inputBufferTime;
        }

        if (comboTimer <= 0f)
            comboStep = 0;

        if (
            bufferTimer > 0f &&
            currentState == null
        )
        {
            if (!player.IsGrounded())
            {
                bufferTimer = 0f;
            }
            else if (
                player.canControl &&
                player.canAttack &&
                !player.inputLocked
            )
            {
                // Hurt / dash / slam / posture break sırasında
                // buffer beklemeye devam eder, süresi dolarsa düşer.
                // Dash biterken tıklarsan saldırı yine başlar.
                bufferTimer = 0f;
                StartAttack();
            }
        }

        currentState?.Tick();
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

        Debug.Log("ATTACK START");

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
        // ATTACK DURATION
        // =====================================================

        float attackDuration;

        switch (comboStep)
        {
            case 1:
                attackDuration = attack1Duration;
                break;

            case 2:
                attackDuration = attack2Duration;
                break;

            case 3:
                attackDuration = attack3Duration;
                break;

            default:
                attackDuration = attack4Duration;
                break;
        }

        // =====================================================
        // ATTACK MOVEMENT TIMING
        // =====================================================

        float moveStart;
        float moveEnd;
        float hitTime;

        switch (comboStep)
        {
            case 1:
                moveStart = attack1MoveStart;
                moveEnd = attack1MoveEnd;
                hitTime = attack1HitTime;
                break;

            case 2:
                moveStart = attack2MoveStart;
                moveEnd = attack2MoveEnd;
                hitTime = attack2HitTime;
                break;

            case 3:
                moveStart = attack3MoveStart;
                moveEnd = attack3MoveEnd;
                hitTime = attack3HitTime;
                break;

            default:
                moveStart = attack4MoveStart;
                moveEnd = attack4MoveEnd;
                hitTime = attack4HitTime;
                break;
        }

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