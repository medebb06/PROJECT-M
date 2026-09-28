using UnityEngine;

public class EnemyAttackState : IEnemyState
{
    private EnemyController enemy;

    private EnemyAttackAudio attackAudio;

    private float attackTimer;

    private bool attackDone;
    private bool warningPlayed;

    public EnemyAttackState(EnemyController enemy)
    {
        this.enemy = enemy;

        attackAudio =
            enemy.GetComponent<EnemyAttackAudio>();
    }

    public void Enter()
    {
        // =====================================================
        // ATTACK TIMER
        // =====================================================

        attackTimer = enemy.attackDuration;

        attackDone = false;
        warningPlayed = false;

        // =====================================================
        // ATTACK WARNING
        // =====================================================

        PlayWarning();

        warningPlayed = true;

        // =====================================================
        // STOP MOVEMENT
        // =====================================================

        Rigidbody2D rb =
            enemy.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.linearVelocity = new Vector2(
                0f,
                rb.linearVelocity.y
            );
        }
    }

    public void Tick()
    {
        if (enemy.target == null)
        {
            enemy.ChangeState(
                new EnemyIdleState(enemy)
            );

            return;
        }

        attackTimer -= Time.deltaTime;

        // =====================================================
        // ATTACK
        // =====================================================

        if (!attackDone &&
            attackTimer <= 0f)
        {
            DoAttack();

            attackDone = true;

            // Attack sonrası recovery.
            enemy.StartAttackRecovery();

            enemy.ChangeState(
                new EnemyChaseState(enemy)
            );
        }
    }

    public void Exit()
    {
    }

    // =====================================================
    // WARNING
    // =====================================================

    private void PlayWarning()
    {
        if (attackAudio == null)
            return;

        attackAudio.PlayWarning();
    }

    // =====================================================
    // ATTACK
    // =====================================================

    private void DoAttack()
    {
        if (enemy.target == null)
            return;

        float distance = Vector2.Distance(
            enemy.transform.position,
            enemy.target.position
        );

        if (distance > enemy.attackRange)
            return;

        PlayerController player =
            enemy.target.GetComponent<PlayerController>();

        if (player == null)
        {
            Debug.LogWarning(
                "EnemyAttackState: PlayerController bulunamadı!"
            );

            return;
        }

        if (player.isInvincible)
            return;

        // =====================================================
        // DEFENSE CHECK
        // =====================================================

        PlayerDefenseController defense =
            enemy.target.GetComponent<PlayerDefenseController>();

        if (defense != null)
        {
            // -------------------------------------------------
            // PARRY
            // -------------------------------------------------

            if (defense.CanParry())
            {
                Debug.Log("PLAYER PARRY!");

                return;
            }

            // -------------------------------------------------
            // BLOCK
            // -------------------------------------------------

            if (defense.CanBlock())
            {
                Debug.Log("PLAYER BLOCK!");

                return;
            }
        }

        // =====================================================
        // HIT DIRECTION
        // =====================================================

        Vector2 hitDirection =
            (player.transform.position -
             enemy.transform.position)
            .normalized;

        PlayerDamageReceiver damageReceiver =
            enemy.target.GetComponent<PlayerDamageReceiver>();

        if (damageReceiver == null)
        {
            Debug.LogError(
                "EnemyAttackState: PlayerDamageReceiver Player üzerinde bulunamadı!"
            );

            return;
        }

        Debug.Log("ENEMY HIT PLAYER");

        // =====================================================
        // DAMAGE
        // =====================================================

        damageReceiver.TakeDamage(
            1,
            hitDirection
        );

        // =====================================================
        // KNOCKBACK
        // =====================================================

        damageReceiver.ApplyKnockback(
            hitDirection,
            enemy.attackKnockbackForce,
            enemy.attackKnockbackVerticalForce,
            enemy.attackKnockbackDuration
        );
    }
}