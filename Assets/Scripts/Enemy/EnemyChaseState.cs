using UnityEngine;

public class EnemyChaseState : IEnemyState
{
    EnemyController enemy;
    Rigidbody2D rb;

    float speed = 2.5f;

    public EnemyChaseState(EnemyController enemy)
    {
        this.enemy = enemy;
        rb = enemy.GetComponent<Rigidbody2D>();
    }

    public void Enter() { }

    public void Tick()
    {
        if (enemy.target == null) return;

        float dist = Vector2.Distance(enemy.transform.position, enemy.target.position);

        if (dist > enemy.chaseRange * 1.5f)
        {
            enemy.ChangeState(new EnemyIdleState(enemy));
            return;
        }

        Vector2 dir = (enemy.target.position - enemy.transform.position).normalized;

        rb.linearVelocity = new Vector2(dir.x * speed, rb.linearVelocity.y);
    }

    public void Exit()
    {
        rb.linearVelocity = Vector2.zero;
    }
}