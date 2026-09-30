using UnityEngine;

public class EnemyStaggerState : IEnemyState
{
    private EnemyController enemy;

    private float staggerTimer;

    private SpriteRenderer spriteRenderer;
    private Color originalColor;

    private float flashTimer;

    public EnemyStaggerState(
        EnemyController enemy
    )
    {
        this.enemy = enemy;
    }

    public void Enter()
    {
        staggerTimer =
            enemy.staggerDuration;

        flashTimer =
     enemy.staggerFlashDuration;

        spriteRenderer =
            enemy.GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            originalColor =
                spriteRenderer.color;

            spriteRenderer.color =
    enemy.staggerFlashColor;
        }

        Rigidbody2D rb =
            enemy.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.linearVelocity =
                new Vector2(
                    0f,
                    rb.linearVelocity.y
                );
        }

        Debug.Log(
            "ENEMY STAGGER!"
        );
    }

    public void Tick()
    {
        // ==========================================
        // STAGGER FLASH
        // ==========================================

        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;

            if (flashTimer <= 0f)
            {
                RestoreColor();
            }
        }

        // ==========================================
        // EXECUTE
        // ==========================================

        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log(
                "EXECUTE INPUT!"
            );

            enemy.ChangeState(
                new EnemyExecuteState(enemy)
            );

            return;
        }

        // ==========================================
        // STAGGER TIMER
        // ==========================================

        staggerTimer -= Time.deltaTime;

        if (staggerTimer <= 0f)
        {
            RecoverBalance();

            enemy.ChangeState(
                new EnemyChaseState(enemy)
            );
        }
    }

    public void Exit()
    {
        RestoreColor();
    }

    private void RestoreColor()
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.color =
            originalColor;
    }

    private void RecoverBalance()
    {
        EnemyBalance balance =
            enemy.GetComponent<EnemyBalance>();

        if (balance == null)
            return;

        balance.RecoverBalance();

        Debug.Log(
            "ENEMY BALANCE RECOVERED!"
        );
    }
}