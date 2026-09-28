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
        staggerTimer = enemy.staggerDuration;

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
        // =====================================================
        // EXECUTE
        // =====================================================

        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("EXECUTE INPUT!");

            enemy.ChangeState(
                new EnemyExecuteState(enemy)
            );

            return;
        }

        // =====================================================
        // STAGGER TIMER
        // =====================================================

        staggerTimer -= Time.deltaTime;

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