using UnityEngine;

public class EnemyHitState : IEnemyState
{
    private EnemyController enemy;
    private Vector2 hitDirection;

    private Rigidbody2D rb;

    private float timer;

    public EnemyHitState(
        EnemyController enemy,
        Vector2 hitDirection
    )
    {
        this.enemy = enemy;
        this.hitDirection = hitDirection;
    }

    public void Enter()
    {
        rb = enemy.GetComponent<Rigidbody2D>();

        timer = enemy.hitDuration;

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
            rb.linearVelocity = new Vector2(
                0f,
                rb.linearVelocity.y
            );
        }
    }
}