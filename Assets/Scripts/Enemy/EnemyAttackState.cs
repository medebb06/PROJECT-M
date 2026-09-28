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
        // Enemy hedefi kaybettiyse Idle.
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

        // Player'ın controller'ını bul.
        PlayerController player =
            enemy.target.GetComponent<PlayerController>();

        if (player == null)
        {
            Debug.LogWarning("EnemyAttackState: PlayerController bulunamadı!");
            return;
        }

        // Player zaten HurtState / başka invincible durumdaysa
        // damage verme.
        if (player.isInvincible)
            return;

        Vector2 hitDirection =
            (player.transform.position - enemy.transform.position)
            .normalized;

        // --------------------------------
        // DAMAGE RECEIVER
        // --------------------------------

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

        damageReceiver.TakeDamage(
            1,
            hitDirection
        );
    }
}