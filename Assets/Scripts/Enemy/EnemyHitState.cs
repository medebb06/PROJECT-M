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

        // 🔥 kontrolü temizle + fizik çakışmasını önle
        rb.linearVelocity = Vector2.zero;

        // AddForce yerine direkt velocity (senin sistemle uyumlu)
        rb.linearVelocity = knockback;
    }

    public void Tick()
    {
        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            enemy.ChangeState(new EnemyChaseState(enemy));
        }
    }

    public void Exit()
    {
        // güvenlik: state değişirken “white flash stuck” olmasın
        sr.color = originalColor;

        // küçük stabilizasyon
        rb.linearVelocity = Vector2.zero;
    }
}