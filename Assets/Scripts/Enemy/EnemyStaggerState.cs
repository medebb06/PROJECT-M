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
        staggerTimer =
            enemy.staggerDuration;

        Rigidbody2D rb =
            enemy.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        Debug.Log(
            "ENEMY STAGGER STARTED"
        );
    }

    public void Tick()
    {
        // --------------------------------
        // EXECUTE
        // --------------------------------

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

        // --------------------------------
        // STAGGER TIMER
        // --------------------------------

        staggerTimer -=
            Time.deltaTime;

        if (staggerTimer > 0f)
            return;

        // --------------------------------
        // STAGGER END
        // --------------------------------

        EnemyBalance balance =
            enemy.GetComponent<EnemyBalance>();

        if (balance != null)
        {
            balance.RecoverBalance();

            Debug.Log(
                "ENEMY BALANCE RECOVERED!"
            );
        }

        enemy.ChangeState(
            new EnemyChaseState(enemy)
        );
    }

    public void Exit()
    {
        Debug.Log(
            "ENEMY STAGGER END"
        );
    }
}