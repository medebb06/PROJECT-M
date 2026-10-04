using UnityEngine;

/// <summary>
/// Execute (E). Tuş TAMPONLUDUR: düşman sersemlerken / saldırın sürerken /
/// hasar kilidi biterken basılan E kaybolmaz, ilk fırsatta çalışır.
/// Saldırı sırasında basılırsa saldırı iptal edilir.
/// </summary>
public class PlayerFinisher : MonoBehaviour
{
    [Header("Finisher")]
    [SerializeField] private float finisherRange = 2.5f;
    [SerializeField] private float forwardPriority = 1.5f;

    [Header("Input")]
    [SerializeField] private KeyCode executeKey = KeyCode.E;

    [Tooltip("Execute tuşu bu kadar süre hatırlanır (sn).")]
    [Min(0f)]
    [SerializeField] private float inputBuffer = 0.25f;

    private PlayerController player;
    private PlayerCombatController combat;

    private float bufferTimer;

    private void Awake()
    {
        player =
            GetComponent<PlayerController>();

        combat =
            GetComponent<PlayerCombatController>();

        if (player == null)
        {
            Debug.LogError(
                "FINISHER: PlayerController bulunamadı!"
            );
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(executeKey))
            bufferTimer = inputBuffer > 0f ? inputBuffer : 0.0001f;

        if (bufferTimer <= 0f)
            return;

        bufferTimer -= Time.unscaledDeltaTime;

        // Hurt / dash / ölüm gibi kontrolsüz durumlarda bekle (tampon).
        if (
            player == null ||
            !player.canControl ||
            player.inputLocked ||
            player.isDashing
        )
        {
            return;
        }

        if (TryExecute())
            bufferTimer = 0f;
    }

    private bool TryExecute()
    {
        EnemyController target =
            FindBestTarget();

        // Hedef yoksa tampon beklemeye devam eder (denge tam o an
        // kırılabilir).
        if (target == null || !target.IsStaggered)
            return false;

        // Saldırı sürüyorsa iptal et: execute anında başlasın.
        if (combat != null)
            combat.CancelAttack();

        // =====================================================
        // EXECUTE
        // EnemyExecuteState zaten:
        // - Player'ı buluyor
        // - PlayerExecuteState'e geçiriyor
        // - Enemy'yi durduruyor
        // - Damage veriyor
        // =====================================================

        target.Execute();

        return true;
    }

    private EnemyController FindBestTarget()
    {
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

        // Sahnedeki aktif düşmanlar (FindObjectsByType'tan ucuz).
        for (int i = 0; i < EnemyController.All.Count; i++)
        {
            EnemyController enemy = EnemyController.All[i];

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