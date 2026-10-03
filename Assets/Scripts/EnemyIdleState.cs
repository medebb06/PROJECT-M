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
        // FIX: Sahnede "Player" tag'li obje yoksa (veya henüz
        // yoksa) eskiden her karede NullReferenceException atıyordu.
        if (!enemy.TryFindTarget())
            return;

        // Oyuncu öldüyse boşta bekle.
        if (enemy.IsTargetDead)
            return;

        float dist = Vector2.Distance(enemy.transform.position, enemy.target.position);

        if (enemy.alwaysHunt || dist < enemy.chaseRange)
        {
            enemy.ChangeState(new EnemyChaseState(enemy));
        }
    }

    public void Exit() { }
}