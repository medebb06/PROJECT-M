using UnityEngine;

public class EnemyChaseState : IEnemyState
{
    private EnemyController enemy;
    private Rigidbody2D rb;

    private float speed = 4f;

    public EnemyChaseState(EnemyController enemy)
    {
        this.enemy = enemy;
        rb = enemy.GetComponent<Rigidbody2D>();
    }

    public void Enter()
    {
    }

    public void Tick()
    {
        if (enemy.target == null)
            return;

        // =====================================================
        // MOVEMENT LOCK
        // =====================================================

        if (enemy.IsMovementLocked)
        {
            return;
        }

        float dist =
            Vector2.Distance(
                enemy.transform.position,
                enemy.target.position
            );

        // =====================================================
        // TOO FAR
        // =====================================================

        if (dist > enemy.chaseRange * 1.5f)
        {
            enemy.ChangeState(
                new EnemyIdleState(enemy)
            );

            return;
        }

        // =====================================================
        // STOP DISTANCE
        // =====================================================

        if (dist <= enemy.chaseStopDistance)
        {
            rb.linearVelocity =
                new Vector2(
                    0f,
                    rb.linearVelocity.y
                );

            // =================================================
            // ATTACK RANGE
            // =================================================

            if (dist <= enemy.attackRange)
            {
                // Attack recovery devam ediyorsa
                // kesinlikle yeni attack başlatma.
                if (!enemy.CanAttack)
                {
                    return;
                }

                enemy.ChangeState(
                    new EnemyAttackState(enemy)
                );

                return;
            }

            return;
        }

        // =====================================================
        // CHASE
        // =====================================================

        Vector2 dir =
            (
                enemy.target.position -
                enemy.transform.position
            ).normalized;

        rb.linearVelocity =
            new Vector2(
                dir.x * speed,
                rb.linearVelocity.y
            );
    }

    public void Exit()
    {
        rb.linearVelocity =
            Vector2.zero;
    }
}