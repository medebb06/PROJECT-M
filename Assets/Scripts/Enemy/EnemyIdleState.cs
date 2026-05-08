using UnityEngine;

public class EnemyIdleState : IEnemyState
{
    EnemyController enemy;

    public EnemyIdleState(EnemyController enemy)
    {
        this.enemy = enemy;
    }

    public void Enter() { }

    public void Tick()
    {
        float dist = Vector2.Distance(enemy.transform.position, enemy.target.position);

        if (dist < enemy.chaseRange)
        {
            enemy.ChangeState(new EnemyChaseState(enemy));
        }
    }

    public void Exit() { }
}