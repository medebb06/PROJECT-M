using UnityEngine;

public class EnemyChaseState : IEnemyState
{
    EnemyController enemy;
    Rigidbody2D rb;

    float speed = 4f;

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

        // -----------------------------------------
        // BLOCK KNOCKBACK / MOVEMENT LOCK
        // -----------------------------------------

        if (enemy.IsMovementLocked)
        {
            return;
        }

        float dist = Vector2.Distance(
            enemy.transform.position,
            enemy.target.position
        );

        // -----------------------------------------
        // TOO FAR
        // -----------------------------------------

        if (dist > enemy.chaseRange * 1.5f)
        {
            enemy.ChangeState(
                new EnemyIdleState(enemy)
            );

            return;
        }

        // -----------------------------------------
        // STOP DISTANCE
        // -----------------------------------------

        if (dist <= enemy.chaseStopDistance)
        {
            // Enemy oyuncuya yeterince yaklaştı.
            // Artık chase hareketi yapma.
            rb.linearVelocity = new Vector2(
                0f,
                rb.linearVelocity.y
            );

            // -----------------------------------------
            // ATTACK RANGE
            // -----------------------------------------

            if (dist <= enemy.attackRange)
            {
                // Saldırı cooldown'daysa bekle.
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

        // -----------------------------------------
        // CHASE
        // -----------------------------------------

        Vector2 dir =
            (enemy.target.position -
             enemy.transform.position)
            .normalized;

        rb.linearVelocity = new Vector2(
            dir.x * speed,
            rb.linearVelocity.y
        );
    }

    public void Exit()
    {
        rb.linearVelocity = Vector2.zero;
    }
}