using UnityEngine;

public class EnemyStaggerState : IEnemyState
{
    private EnemyController enemy;
    private float staggerTimer;

    public EnemyStaggerState(EnemyController enemy)
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
        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("EXECUTE INPUT!");

            enemy.ChangeState(
                new EnemyExecuteState(enemy)
            );

            return;
        }

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
    }

    private void RecoverBalance()
    {
        EnemyBalance balance =
            enemy.GetComponent<EnemyBalance>();

        if (balance == null)
            return;

        balance.RecoverBalance();

        Debug.Log("ENEMY BALANCE RECOVERED!");
    }
}