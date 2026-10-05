using UnityEngine;

/// <summary>
/// UÇAN DALIŞÇI (Uçan tip). Yerçekimi kapalı; oyuncunun başının üstünde
/// süzülür. Saldırının UYARISI boyunca oyuncunun hizasına DALAR, vuruştan
/// sonra yükselir.
///
/// Cevaplar: dalışı parry'le (denge kırılınca yere düşer → infaz), yukarıdan
/// aşağı vuruşla POGO, havada yan vuruş, Şok Dalgası.
/// Sersemleyince / ölünce yerçekimi geri gelir (yere düşer).
///
/// Hareketin yatayını EnemyChaseState yapar; bu bileşen dikeyi yönetir.
/// </summary>
[DefaultExecutionOrder(200)]
[RequireComponent(typeof(EnemyController))]
public class EnemyFlyer : MonoBehaviour
{
    public float hoverHeight = 3.2f;
    public float bobAmplitude = 0.35f;
    public float bobSpeed = 2.2f;
    public float riseSpeed = 5f;
    public float diveSpeed = 16f;
    [Tooltip("Dalışta ayak hizası oyuncunun ayağının bu kadar üstünde durur.")]
    public float diveFeetOffset = 0.2f;

    private EnemyController enemy;
    private Rigidbody2D rb;
    private float originalGravity = 1f;
    private Transform player;
    private Collider2D playerCol;
    private float phase;

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        rb = GetComponent<Rigidbody2D>();

        if (rb != null)
            originalGravity = rb.gravityScale;

        phase = Random.value * 10f;
    }

    private void Start()
    {
        if (rb != null)
            rb.gravityScale = 0f;
    }

    private void FixedUpdate()
    {
        if (rb == null || enemy == null)
            return;

        bool grounded = enemy.IsDead || enemy.IsStaggered || enemy.CurrentState is EnemyExecuteState;

        // Sersem / ölü: düşer (infaz edilebilsin).
        if (grounded)
        {
            rb.gravityScale = originalGravity;
            return;
        }

        rb.gravityScale = 0f;

        if (player == null)
        {
            PlayerController p = FindFirstObjectByType<PlayerController>();

            if (p == null)
                return;

            player = p.transform;
            playerCol = p.col;
        }

        float playerFeet = playerCol != null ? playerCol.bounds.min.y : player.position.y;
        float myFeet = enemy.FeetY;

        float dt = Time.fixedDeltaTime * EnemyTime.Scale;

        Vector2 v = rb.linearVelocity;

        if (enemy.CurrentState is EnemyAttackState attack && attack.IsWindingUp)
        {
            // DALIŞ: oyuncunun hizasına in.
            float targetFeet = playerFeet + diveFeetOffset;
            float dy = targetFeet - myFeet;

            v.y = Mathf.Clamp(dy * 10f, -diveSpeed, diveSpeed) * EnemyTime.Scale;
        }
        else
        {
            // SÜZÜLME: başın üstünde, hafif sallanarak.
            phase += dt * bobSpeed;

            float targetFeet = playerFeet + hoverHeight + Mathf.Sin(phase) * bobAmplitude;
            float dy = targetFeet - myFeet;

            v.y = Mathf.Clamp(dy * 3f, -riseSpeed, riseSpeed) * EnemyTime.Scale;
        }

        rb.linearVelocity = v;
    }

    private void OnDisable()
    {
        if (rb != null)
            rb.gravityScale = originalGravity;
    }
}
