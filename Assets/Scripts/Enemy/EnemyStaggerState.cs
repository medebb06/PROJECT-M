using UnityEngine;

public class EnemyStaggerState : IEnemyState
{
    private EnemyController enemy;

    private float staggerTimer;

    public EnemyStaggerState(
        EnemyController enemy
    )
    {
        this.enemy = enemy;
    }

    public void Enter()
    {
        // =====================================================
        // STAGGER DURATION
        // =====================================================

        staggerTimer = enemy.staggerDuration;

        // =====================================================
        // STOP MOVEMENT
        // =====================================================

        Rigidbody2D rb =
            enemy.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.linearVelocity = new Vector2(
                0f,
                rb.linearVelocity.y
            );
        }

        Debug.Log("ENEMY STAGGER!");
    }

    public void Tick()
    {
        staggerTimer -= Time.deltaTime;

        // =====================================================
        // STAGGER END
        // =====================================================

        if (staggerTimer <= 0f)
        {
            enemy.ChangeState(
                new EnemyChaseState(enemy)
            );
        }
    }

    public void Exit()
    {
    }
}