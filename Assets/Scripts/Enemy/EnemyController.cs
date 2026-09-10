using UnityEngine;

public class EnemyController : MonoBehaviour
{

    [Header("Hit / Knockback")]
    public float knockbackForceX = 7f;
    public float knockbackForceY = 3f;
    public float hitDuration = 0.12f;

    [Header("Hit Deceleration")]
    public float knockbackDeceleration = 45f;

    [Header("Hit Flash")]
    public Color hitFlashColor = Color.white;
    public float hitFlashDuration = 0.06f;

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