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
        // ==========================================
        // STAGGER TIMER
        // ==========================================

        staggerTimer =
            enemy.staggerDuration;

        flashTimer =
            enemy.staggerFlashDuration;

        // ==========================================
        // STOP ENEMY MOVEMENT
        // ==========================================

        StopMovement();

        // ==========================================
        // STAGGER FLASH
        // ==========================================

        spriteRenderer =
            enemy.GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            originalColor =
                spriteRenderer.color;

            spriteRenderer.color =
                enemy.staggerFlashColor;
        }

        Debug.Log(
            "ENEMY STAGGER!"
        );
    }

    public void Tick()
    {
        // ==========================================
        // STAGGER MOVEMENT LOCK
        // ==========================================

        StopMovement();

        // ==========================================
        // STAGGER FLASH
        // ==========================================

        if (flashTimer > 0f)
        {
            flashTimer -=
                Time.deltaTime;

            if (flashTimer <= 0f)
            {
                RestoreColor();
            }
        }

        // ==========================================
        // STAGGER TIMER
        // ==========================================

        staggerTimer -=
            Time.deltaTime;

        if (staggerTimer > 0f)
            return;

        // ==========================================
        // STAGGER FINISHED
        // ==========================================

        RecoverBalance();

        enemy.ChangeState(
            new EnemyChaseState(enemy)
        );
    }

    public void Exit()
    {
        StopMovement();

        RestoreColor();
    }

    private void StopMovement()
    {
        Rigidbody2D rb =
            enemy.GetComponent<Rigidbody2D>();

        if (rb == null)
            return;

        rb.linearVelocity =
            new Vector2(
                0f,
                rb.linearVelocity.y
            );
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

        if (!balance.IsBroken)
            return;

        balance.RecoverBalance();

        Debug.Log(
            "ENEMY BALANCE RECOVERED!"
        );
    }
}