
using UnityEngine;

public class PlayerFinisherTarget : MonoBehaviour
{
    [Header("Finisher Target")]
    [SerializeField] private float finisherRange = 2.5f;

    [SerializeField] private float forwardPriority = 1.5f;

    [Header("Input")]
    [SerializeField] private KeyCode executeKey = KeyCode.E;

    private PlayerController player;
    private EnemyController currentTarget;

    public EnemyController CurrentTarget => currentTarget;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
    }

    private void Update()
    {
        UpdateTarget();

        if (Input.GetKeyDown(executeKey))
        {
            TryExecute();
        }
    }

    private void UpdateTarget()
    {
        EnemyController bestTarget = FindBestTarget();

        if (bestTarget == currentTarget)
            return;

        if (currentTarget != null)
        {
            currentTarget.SetFinisherTarget(false);
        }

        currentTarget = bestTarget;

        if (currentTarget != null)
        {
            currentTarget.SetFinisherTarget(true);
        }
    }

    private EnemyController FindBestTarget()
    {
        EnemyController[] enemies =
            FindObjectsOfType<EnemyController>();

        EnemyController bestTarget = null;

        float bestScore = float.MinValue;

        float facing =
            player != null && player.facingDir < 0f
                ? -1f
                : 1f;

        Vector2 playerPosition =
            transform.position;

        foreach (EnemyController enemy in enemies)
        {
            if (enemy == null)
                continue;

            if (!enemy.IsStaggered)
                continue;

            Vector2 enemyPosition =
                enemy.transform.position;

            Vector2 offset =
                enemyPosition - playerPosition;

            float distance =
                offset.magnitude;

            if (distance > finisherRange)
                continue;

            float directionToEnemy =
                Mathf.Sign(offset.x);

            if (directionToEnemy == 0f)
                directionToEnemy = facing;

            bool isInFront =
                directionToEnemy == facing;

            float distanceScore =
                1f - Mathf.Clamp01(
                    distance / finisherRange
                );

            float directionScore =
                isInFront
                    ? forwardPriority
                    : 0f;

            float score =
                directionScore +
                distanceScore;

            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = enemy;
            }
        }

        return bestTarget;
    }

    private void TryExecute()
    {
        if (currentTarget == null)
        {
            Debug.Log("FINISHER: NO TARGET");
            return;
        }

        if (!currentTarget.IsStaggered)
        {
            currentTarget = null;
            return;
        }

        Debug.Log(
            "FINISHER TARGET: " +
            currentTarget.name
        );

        EnemyController target =
            currentTarget;

        currentTarget = null;

        target.SetFinisherTarget(false);

        target.Execute();
    }

    private void OnDisable()
    {
        if (currentTarget != null)
        {
            currentTarget.SetFinisherTarget(false);
            currentTarget = null;
        }
    }
}

