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
        // STOP HORIZONTAL MOVEMENT
        // --------------------------------

        if (rb != null)
        {
            rb.linearVelocity = new Vector2(
                0f,
                rb.linearVelocity.y
            );
        }

        // --------------------------------
        // HIT FLASH
        // --------------------------------

        if (sr != null)
        {
            originalColor = sr.color;
            sr.color = enemy.hitFlashColor;
        }
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

        if (rb != null)
        {
            rb.linearVelocity = new Vector2(
                0f,
                rb.linearVelocity.y
            );
        }
    }
}