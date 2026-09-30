using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [Header("Attack")]
    public float attackRange = 1.5f;

    [Header("Chase")]
    public float chaseStopDistance = 1.8f;

    [Header("Stagger")]
    public float staggerDuration = 1.2f;



    public float attackDuration = 1f;
    public float attackWarningTime = 1f;

    [Header("Attack Recovery")]
    public float attackRecoveryTime = 0.8f;

    [Header("Hit / Knockback")]
    public float knockbackForceX = 7f;
    public float knockbackForceY = 3f;
    public float hitDuration = 0.12f;

    [Header("Hit Deceleration")]
    public float knockbackDeceleration = 45f;

    [Header("Hit Flash")]
    public Color hitFlashColor = Color.white;
    public float hitFlashDuration = 0.06f;

    [Header("Stagger Flash")]
    public Color staggerFlashColor = Color.yellow;
    public float staggerFlashDuration = 0.15f;

    [Header("Attack Knockback")]
    public float attackKnockbackForce = 6f;
    public float attackKnockbackVerticalForce = 1f;
    public float attackKnockbackDuration = 0.12f;

    [Header("Block Knockback")]
    public float blockKnockbackForce = 2.5f;
    public float blockKnockbackVerticalForce = 0.2f;
    public float blockKnockbackDuration = 0.15f;
    public float blockRecoveryTime = 0.25f;

    [Header("Defense Balance")]
    public int parryBalanceDamage = 2;

    [Header("Execute")]
    public int executeDamage = 10;
    public float executeDistance = 1.2f;
    public float executeDuration = 0.08f;

    public Transform target;
    public float chaseRange = 5f;

    private float attackRecoveryTimer;
    private float movementLockTimer;

    public bool CanAttack => attackRecoveryTimer <= 0f;

    public bool IsMovementLocked =>
        movementLockTimer > 0f;

    private IEnemyState currentState;

    public bool IsStaggered =>
        currentState is EnemyStaggerState;

    public IEnemyState CurrentState =>
        currentState;

    void Update()
    {
        if (attackRecoveryTimer > 0f)
        {
            attackRecoveryTimer -= Time.deltaTime;

            if (attackRecoveryTimer < 0f)
                attackRecoveryTimer = 0f;
        }

        if (movementLockTimer > 0f)
        {
            movementLockTimer -= Time.deltaTime;

            if (movementLockTimer < 0f)
                movementLockTimer = 0f;
        }

        currentState?.Tick();
    }

    void Start()
    {
        if (target == null)
        {
            GameObject playerObj =
                GameObject.FindGameObjectWithTag("Player");

            if (playerObj != null)
                target = playerObj.transform;
        }

        ChangeState(
            new EnemyIdleState(this)
        );
    }

    public void ChangeState(IEnemyState newState)
    {
        if (newState == null)
            return;

        currentState?.Exit();

        currentState = newState;

        currentState.Enter();
    }

    public void ForceStagger()
    {
        if (IsStaggered)
            return;

        ChangeState(
            new EnemyStaggerState(this)
        );
    }

    public void StartAttackRecovery()
    {
        attackRecoveryTimer = attackRecoveryTime;
    }

    public void ApplyBlockKnockback(Vector2 hitDirection)
    {
        Rigidbody2D rb =
            GetComponent<Rigidbody2D>();

        if (rb == null)
            return;

        float direction =
            Mathf.Sign(hitDirection.x);

        if (direction == 0f)
            direction = 1f;

        float knockbackDirection =
            -direction;

        rb.linearVelocity = new Vector2(
            knockbackDirection * blockKnockbackForce,
            blockKnockbackVerticalForce
        );

        movementLockTimer =
            blockKnockbackDuration;
    }
}