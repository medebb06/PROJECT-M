
using UnityEngine;

public class EnemyAttackState : IEnemyState
{
    private EnemyController enemy;

    private EnemyAttackAudio attackAudio;

    private float attackTimer;

    private bool attackDone;
    private bool warningPlayed;

    private int parryDamage = 1;

    public EnemyAttackState(EnemyController enemy)
    {
        this.enemy = enemy;

        attackAudio =
            enemy.GetComponent<EnemyAttackAudio>();
    }

    public void Enter()
    {
        attackTimer = enemy.attackDuration;

        attackDone = false;
        warningPlayed = false;

        PlayWarning();

        warningPlayed = true;

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

        if (!attackDone &&
            attackTimer <= 0f)
        {
            bool enemyStaggered = DoAttack();

            attackDone = true;

            if (enemyStaggered)
            {
                // Posture kırıldı.
                // Enemy zaten StaggerState'e geçti.
                return;
            }

            enemy.StartAttackRecovery();

            enemy.ChangeState(
                new EnemyChaseState(enemy)
            );
        }
    }

    public void Exit()
    {
    }

    private void PlayWarning()
    {
        if (attackAudio == null)
            return;

        attackAudio.PlayWarning();
    }

    private bool DoAttack()
    {
        if (enemy.target == null)
            return false;

        float distance = Vector2.Distance(
            enemy.transform.position,
            enemy.target.position
        );

        if (distance > enemy.attackRange)
            return false;

        PlayerController player =
            enemy.target.GetComponent<PlayerController>();

        if (player == null)
        {
            Debug.LogWarning(
                "EnemyAttackState: PlayerController bulunamadı!"
            );

            return false;
        }

        if (player.isInvincible)
            return false;

        Vector2 hitDirection =
            (player.transform.position -
             enemy.transform.position)
            .normalized;

        PlayerDefenseController defense =
            enemy.target.GetComponent<PlayerDefenseController>();

        if (defense != null)
        {
            // =========================================
            // PARRY
            // =========================================

            if (defense.CanParry())
            {
                Debug.Log("PLAYER PARRY!");

                // PARRY'DE ENEMY KNOCKBACK YOK.
                return HandleParry(hitDirection);
            }

            // =========================================
            // BLOCK
            // =========================================

            if (defense.CanBlock())
            {
                Debug.Log("PLAYER BLOCK!");

                // Block hasar almaz ama enemy geri itilir.
                enemy.ApplyBlockKnockback(hitDirection);

                return false;
            }
        }

        // =============================================
        // NORMAL HIT
        // =============================================

        PlayerDamageReceiver damageReceiver =
            enemy.target.GetComponent<PlayerDamageReceiver>();

        if (damageReceiver == null)
        {
            Debug.LogError(
                "EnemyAttackState: PlayerDamageReceiver Player üzerinde bulunamadı!"
            );

            return false;
        }

        Debug.Log("ENEMY HIT PLAYER");

        damageReceiver.TakeDamage(
            1,
            hitDirection
        );

        damageReceiver.ApplyKnockback(
            hitDirection,
            enemy.attackKnockbackForce,
            enemy.attackKnockbackVerticalForce,
            enemy.attackKnockbackDuration
        );

        return false;
    }

    private bool HandleParry(Vector2 hitDirection)
    {
        Enemy enemyDamageReceiver =
            enemy.GetComponent<Enemy>();

        if (enemyDamageReceiver == null)
        {
            Debug.LogError(
                "EnemyAttackState: Enemy component bulunamadı!"
            );

            return false;
        }

        EnemyPosture posture =
            enemy.GetComponent<EnemyPosture>();

        if (posture == null)
        {
            Debug.LogWarning(
                "EnemyAttackState: EnemyPosture bulunamadı!"
            );

            return false;
        }

        bool wasBrokenBefore =
            posture.IsBroken;

        Debug.Log("PARRY → ENEMY POSTURE DAMAGE");

        enemyDamageReceiver.TakeDamage(
            parryDamage,
            hitDirection
        );

        // PARRY'DE ENEMY KNOCKBACK YOK.

        if (!wasBrokenBefore &&
            posture.IsBroken)
        {
            Debug.Log(
                "PARRY → ENEMY POSTURE BROKEN → STAGGER"
            );

            return true;
        }

        return false;
    }
}

