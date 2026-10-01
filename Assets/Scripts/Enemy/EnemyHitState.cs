using UnityEngine;

public class EnemyHitState : IEnemyState
{
    private EnemyController enemy;
    private Rigidbody2D rb;

    private float timer;
    private float deceleration;

    public EnemyHitState(
        EnemyController enemy,
        float knockbackDuration
    )
    {
        this.enemy = enemy;
        this.timer = knockbackDuration;

        deceleration =
            enemy.knockbackDeceleration;
    }

    public EnemyHitState(
        EnemyController enemy,
        float knockbackDuration,
        float customDeceleration
    )
    {
        this.enemy = enemy;
        this.timer = knockbackDuration;
        this.deceleration = customDeceleration;
    }

    public void Enter()
    {
        rb =
            enemy.GetComponent<Rigidbody2D>();
    }

    public void Tick()
    {
        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            enemy.ChangeState(
                new EnemyChaseState(enemy)
            );
        }
    }

    public void Exit()
    {
        if (rb != null)
        {
            rb.linearVelocity =
                new Vector2(
                    0f,
                    rb.linearVelocity.y
                );
        }
    }
}