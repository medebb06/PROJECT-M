using UnityEngine;

public class EnemyController : MonoBehaviour
{
    public float attackRange = 1.2f;
    
    public Transform target;
    public float chaseRange = 5f;

    IEnemyState currentState;

    void Update()
    {
        currentState?.Tick();
    }
    void Start()
    {
        if (target == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                target = playerObj.transform;
        }

        ChangeState(new EnemyIdleState(this));
    }
    public void ChangeState(IEnemyState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState.Enter();
    }
}