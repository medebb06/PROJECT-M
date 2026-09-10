using UnityEngine;

public class EnemyAttackState : IEnemyState
{
    private EnemyController enemy;

    private float attackTimer;
    private bool attackDone;

    public EnemyAttackState(EnemyController enemy)
    {
        this.enemy = enemy;
    }

    public void Enter()
    {
        attackTimer = enemy.attackDuration;
        attackDone = false;

        // Attack başladığında hareket durabilir.
        Rigidbody2D rb = enemy.GetComponent<Rigidbody2D>();

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
        // Enemy HitState'e geçtiyse bu state zaten
        // çalışmayacaktır. Bu kontrol ekstra güvenliktir.
        if (enemy.target == null)
        {
            enemy.ChangeState(
                new EnemyIdleState(enemy)
            );

            return;
        }

        attackTimer -= Time.deltaTime;

        // --------------------------------
        // ATTACK MOMENT
        // --------------------------------

        if (!attackDone &&
            attackTimer <= enemy.attackDuration * 0.5f)
        {
            DoAttack();

            attackDone = true;
        }

        // --------------------------------
        // ATTACK END
        // --------------------------------

        if (attackTimer <= 0f)
        {
            enemy.ChangeState(
                new EnemyChaseState(enemy)
            );
        }
    }

    public void Exit()
    {
        // Eğer ileride hitbox kullanırsak
        // burada kapatacağız.
    }

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
            return;

        // Player dash vb. sırasında dokunulmazsa hasar verme.
        if (player.isInvincible)
            return;

        Vector2 hitDirection =
            (player.transform.position - enemy.transform.position)
            .normalized;

        player.stateMachine.ChangeState(
            new PlayerHurtState(
                player,
                player.stateMachine,
                hitDirection,
                8f
            )
        );
    }
}