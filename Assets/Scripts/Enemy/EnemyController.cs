using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [Header("Attack")]
    public float attackRange = 1.5f;

    // Enemy attack state'e girdiğinde
    // bu süre boyunca oyuncu saldırıyı bekler.
    public float attackDuration = 1f;

    // Saldırıdan önce warning sesi.
    // Şimdilik 1 saniye olacak.
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

    [Header("Attack Knockback")]
    public float attackKnockbackForce = 6f;
    public float attackKnockbackVerticalForce = 1f;
    public float attackKnockbackDuration = 0.12f;

    public Transform target;
    public float chaseRange = 5f;

    private float attackRecoveryTimer;

    public bool CanAttack => attackRecoveryTimer <= 0f;

    IEnemyState currentState;

    void Update()
    {
        if (attackRecoveryTimer > 0f)
        {
            attackRecoveryTimer -= Time.deltaTime;

            if (attackRecoveryTimer < 0f)
                attackRecoveryTimer = 0f;
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

        ChangeState(new EnemyIdleState(this));
    }

    public void ChangeState(IEnemyState newState)
    {
        currentState?.Exit();

        currentState = newState;

        currentState.Enter();
    }

    public void StartAttackRecovery()
    {
        attackRecoveryTimer = attackRecoveryTime;
    }
}