using UnityEngine;

public class EnemyHitState : IEnemyState
{
    private EnemyController enemy;
    private Vector2 hitDirection;

    private Rigidbody2D rb;
    private SpriteRenderer sr;

    private Color originalColor;

    private float timer;
    private float flashTimer;

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
        sr = enemy.GetComponent<SpriteRenderer>();

        timer = enemy.hitDuration;
        flashTimer = enemy.hitFlashDuration;

        // --------------------------------
        // HIT FLASH
        // --------------------------------

        if (sr != null)
        {
            originalColor = sr.color;
            sr.color = enemy.hitFlashColor;
        }

        // --------------------------------
        // KNOCKBACK
        // --------------------------------

        float direction = Mathf.Sign(hitDirection.x);

        if (direction == 0f)
            direction = 1f;

        Vector2 velocity = rb.linearVelocity;

        velocity.x = direction * enemy.knockbackForceX;
        velocity.y = enemy.knockbackForceY;

        rb.linearVelocity = velocity;
    }

    public void Tick()
    {
        timer -= Time.deltaTime;
        flashTimer -= Time.deltaTime;

        // --------------------------------
        // FLASH
        // --------------------------------

        if (flashTimer <= 0f && sr != null)
        {
            sr.color = originalColor;
        }

        // --------------------------------
        // KNOCKBACK DECELERATION
        // --------------------------------

        float newX = Mathf.MoveTowards(
            rb.linearVelocity.x,
            0f,
            enemy.knockbackDeceleration * Time.deltaTime
        );

        rb.linearVelocity = new Vector2(
            newX,
            rb.linearVelocity.y
        );

        // --------------------------------
        // HIT END
        // --------------------------------

        if (timer <= 0f)
        {
            enemy.ChangeState(
                new EnemyChaseState(enemy)
            );
        }
    }

    public void Exit()
    {
        if (sr != null)
        {
            sr.color = originalColor;
        }

        // Hit state bittiğinde yatay savrulmayı temizle.
        rb.linearVelocity = new Vector2(
            0f,
            rb.linearVelocity.y
        );
    }
}