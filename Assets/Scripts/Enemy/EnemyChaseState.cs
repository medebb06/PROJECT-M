using UnityEngine;

public class EnemyChaseState : IEnemyState
{
    EnemyController enemy;
    Rigidbody2D rb;

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

        // Oyuncu öldüyse kovalamayı bırak.
        if (enemy.IsTargetDead)
        {
            enemy.ChangeState(
                new EnemyIdleState(enemy)
            );

            return;
        }

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
        // FIX: Eskiden normalize edilmiş 2D vektörün x'i
        // kullanılıyordu; oyuncu yukarıdaysa/aşağıdaysa düşman
        // yavaşlıyordu. Artık sadece yatay yön kullanılıyor.
        // Hız da sabit 4 yerine enemy.chaseSpeed.

        float deltaX =
            enemy.target.position.x -
            enemy.transform.position.x;

        float dirX =
            Mathf.Abs(deltaX) > 0.05f
                ? Mathf.Sign(deltaX)
                : 0f;

        rb.linearVelocity = new Vector2(
            dirX * enemy.chaseSpeed,
            rb.linearVelocity.y
        );
    }

    public void Exit()
    {
        rb.linearVelocity = Vector2.zero;
    }
}