using UnityEngine;

public class EnemyAttackState : IEnemyState
{
    EnemyController enemy;

    float attackTimer;
    float attackDuration = 0.6f;

    bool attackDone;

    public EnemyAttackState(EnemyController enemy)
    {
        this.enemy = enemy;
    }

    public void Enter()
    {
        attackTimer = attackDuration;
        attackDone = false;

        // burada hitbox açabilirsin
        // enemy.EnableHitbox(true);
    }

    public void Tick()
    {
        if (enemy.target == null)
        {
            enemy.ChangeState(new EnemyIdleState(enemy));
            return;
        }

        attackTimer -= Time.deltaTime;

        // 🔥 saldırı anı (örnek: ortasında vur)
        if (!attackDone && attackTimer <= attackDuration * 0.5f)
        {
            DoAttack();
            attackDone = true;
        }

        if (attackTimer <= 0f)
        {
            enemy.ChangeState(new EnemyChaseState(enemy));
        }
    }

    public void Exit()
    {
        // enemy.DisableHitbox();
    }

    private void DoAttack()
    {
        float dist = Vector2.Distance(enemy.transform.position, enemy.target.position);

        if (dist <= enemy.attackRange + 0.3f)
        {
            // buraya player damage sistemi gelecek
            var player = enemy.target.GetComponent<PlayerController>();

            if (player != null && !player.isInvincible)
            {
                Vector2 dir = (player.transform.position - enemy.transform.position).normalized;

                player.stateMachine.ChangeState(
                    new PlayerHurtState(player, player.stateMachine, dir, 8f)
                );
            }
        }
    }
}