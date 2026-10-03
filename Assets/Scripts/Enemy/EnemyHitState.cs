using UnityEngine;

public class EnemyHitState : IEnemyState
{
    private EnemyController enemy;
    private Rigidbody2D rb;

    private float timer;
    private float deceleration;

    public EnemyHitState(
        EnemyController enemy,
        float knockbackDuration
    )
    {
        this.enemy = enemy;
        this.timer = knockbackDuration;

        deceleration =
            enemy.knockbackDeceleration;
    }

    public EnemyHitState(
        EnemyController enemy,
        float knockbackDuration,
        float customDeceleration
    )
    {
        this.enemy = enemy;
        this.timer = knockbackDuration;
        this.deceleration = customDeceleration;
    }

    public void Enter()
    {
        rb =
            enemy.GetComponent<Rigidbody2D>();
    }

    public void Tick()
    {
        if (rb == null)
            return;

        timer -= EnemyTime.DeltaTime;

        // =====================================================
        // SMOOTH KNOCKBACK DECELERATION
        // =====================================================
        // Düşman bir anda durmak yerine,
        // aldığı darbeyle hızlıca geri gider ve
        // giderek yavaşlar.
        // =====================================================

        float currentX =
            rb.linearVelocity.x;

        float newX =
            Mathf.MoveTowards(
                currentX,
                0f,
                deceleration *
                EnemyTime.DeltaTime
            );

        rb.linearVelocity =
            new Vector2(
                newX,
                rb.linearVelocity.y
            );

        // =====================================================
        // END
        // =====================================================

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
            rb.linearVelocity =
                new Vector2(
                    0f,
                    rb.linearVelocity.y
                );
        }
    }
}