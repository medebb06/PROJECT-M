using UnityEngine;

public class EnemyHitState : IEnemyState
{
    EnemyController enemy;
    Vector2 knockback;

    float timer;
    float duration = 0.12f;

    Rigidbody2D rb;
    SpriteRenderer sr;
    Color originalColor;

    public EnemyHitState(EnemyController enemy, Vector2 knockback)
    {
        this.enemy = enemy;
        this.knockback = knockback;
    }

    public void Enter()
    {
        rb = enemy.GetComponent<Rigidbody2D>();
        sr = enemy.GetComponent<SpriteRenderer>();

        originalColor = sr.color;

        timer = duration;

        // hit flash
        sr.color = Color.white;

        // temiz impuls hit
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(knockback, ForceMode2D.Impulse);
    }

    public void Tick()
    {
        timer -= Time.deltaTime;

        if (timer <= 0)
        {
            enemy.ChangeState(new EnemyChaseState(enemy));
        }
    }

    public void Exit()
    {
        rb.linearVelocity = Vector2.zero;
        sr.color = originalColor;
    }
}