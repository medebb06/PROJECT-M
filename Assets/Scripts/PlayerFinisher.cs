using UnityEngine;

public class PlayerFinisher : MonoBehaviour
{
    [Header("Finisher")]
    [SerializeField] private float finisherRange = 2.5f;
    [SerializeField] private float forwardPriority = 1.5f;

    [Header("Input")]
    [SerializeField] private KeyCode executeKey = KeyCode.E;

    private PlayerController player;

    private void Awake()
    {
        player =
            GetComponent<PlayerController>();

        if (player == null)
        {
            Debug.LogError(
                "FINISHER: PlayerController bulunamadı!"
            );
        }
    }

    private void Update()
    {
        if (!Input.GetKeyDown(executeKey))
            return;

        // FIX: Hurt / dash / ölüm gibi kontrolsüz
        // durumlarda finisher tetiklenmesin.
        if (
            player == null ||
            !player.canControl
        )
        {
            return;
        }

        TryExecute();
    }

    private void TryExecute()
    {
        EnemyController target =
            FindBestTarget();

        if (target == null)
        {
            Debug.Log(
                "FINISHER: NO TARGET"
            );

            return;
        }

        if (!target.IsStaggered)
        {
            Debug.Log(
                "FINISHER: TARGET NO LONGER STAGGERED"
            );

            return;
        }

        Debug.Log(
            "FINISHER TARGET: " +
            target.name
        );

        // =====================================================
        // EXECUTE
        // EnemyExecuteState zaten:
        // - Player'ı buluyor
        // - PlayerExecuteState'e geçiriyor
        // - Enemy'yi durduruyor
        // - Damage veriyor
        // =====================================================

        target.Execute();
    }

    private EnemyController FindBestTarget()
    {
        // FIX: FindObjectsOfType Unity 6'da obsolete.
        EnemyController[] enemies =
            FindObjectsByType<EnemyController>(
                FindObjectsSortMode.None
            );

        EnemyController bestTarget = null;

        float bestScore =
            float.MinValue;

        float facing =
            player != null &&
            player.facingDir < 0f
                ? -1f
                : 1f;

        Vector2 playerPosition =
            transform.position;

        foreach (EnemyController enemy in enemies)
        {
            if (enemy == null)
                continue;

            // Sadece staggered enemy
            if (!enemy.IsStaggered)
                continue;

            Vector2 enemyPosition =
                enemy.transform.position;

            Vector2 offset =
                enemyPosition -
                playerPosition;

            float distance =
                offset.magnitude;

            // Finisher menzili dışında
            if (distance > finisherRange)
                continue;

            float directionToEnemy =
                Mathf.Sign(offset.x);

            if (directionToEnemy == 0f)
                directionToEnemy = facing;

            bool isInFront =
                directionToEnemy == facing;

            // Yakın enemy daha yüksek puan
            float distanceScore =
                1f -
                Mathf.Clamp01(
                    distance /
                    finisherRange
                );

            // Ön taraftaki enemy'ye öncelik
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
}