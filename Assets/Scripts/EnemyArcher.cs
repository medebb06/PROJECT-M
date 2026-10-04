using UnityEngine;

/// <summary>
/// OKÇU AYARLARI + ok bırakma. EnemyAttackState, hamledeki 'Shot' vuruşunun
/// anında Fire() çağırır; ok bir EnemyProjectile olarak uçar.
///
/// Oka karşı:
///   PARRY  → ok OKÇUYA GERİ YANSIR (parry denge hasarı hemen; yansıyan ok
///            okçuya çarparsa ek denge hasarı + "YANSITILDI!")
///   BLOCK  → posture yer
///   DASH   → okun içinden geçersin
///   ZIPLA  → ok yataya yakın uçar; üstünden atlanabilir
///
/// KURULUM YOK: EnemyArchetype (Okçu) ya da EnemyAttackState gerekince
/// ekler. Kendi ok sprite'ın varsa prefab'a ekleyip 'Arrow Sprite' ata
/// (sprite SAĞA baksın).
/// </summary>
public class EnemyArcher : MonoBehaviour
{
    [Header("Ok")]
    [Tooltip("Ok hızı (birim/sn, düşman zamanı: parry slow-mo'sunda yavaşlar).")]
    [Min(1f)]
    public float arrowSpeed = 12f;

    [Tooltip("Ok bu kadar sn sonra kaybolur.")]
    [Min(0.2f)]
    public float arrowLifetime = 2.5f;

    [Tooltip("Okun çıktığı yükseklik: gövde yüksekliğinin oranı (ayaktan).")]
    [Range(0f, 1f)]
    public float muzzleHeight = 0.6f;

    [Tooltip("Okun gövdeden ne kadar önde çıktığı (birim).")]
    public float muzzleForward = 0.4f;

    [Tooltip(
        "Oyuncuya nişan alırken en fazla dikey açı (derece). 0 = hep yatay " +
        "(zıplayarak kaçmak kolay), büyük = havadaki oyuncuyu da vurur.")]
    [Range(0f, 45f)]
    public float maxAimAngle = 15f;

    [Header("Görünüm")]
    [Tooltip("Boşsa koddan üretilen pixel ok (sağa bakar).")]
    public Sprite arrowSprite;

    public Color arrowColor = new Color(1f, 0.9f, 0.7f);

    public Color reflectedColor = new Color(0.6f, 0.95f, 1f);

    public float arrowScale = 1f;

    [Header("İsabet")]
    [Tooltip("Okun oyuncu gövdesine 'değme' payı (birim).")]
    [Min(0f)]
    public float hitRadius = 0.15f;

    [Tooltip("İsabette oyuncu savrulması = Attack Knockback Force × bu.")]
    [Min(0f)]
    public float knockbackMultiplier = 0.6f;

    [Header("Parry ile yansıtma")]
    [Tooltip("Yansıyan okun hız çarpanı.")]
    [Min(0.5f)]
    public float reflectSpeedMultiplier = 1.5f;

    [Tooltip(
        "Yansıyan ok okçuya çarparsa ek denge hasarı = Parry Balance Damage × bu.")]
    [Min(0f)]
    public float reflectBalanceBonus = 0.6f;

    public string reflectText = "YANSITILDI!";

    [Header("Mesafe koruma")]
    [Tooltip("Oyuncu bundan yakınsa (ve saldırmıyorsa) geri çekilir. 0 = kapalı.")]
    [Min(0f)]
    public float retreatDistance = 3.5f;

    [Tooltip("Geri çekilme hızı = Chase Speed × bu.")]
    [Range(0.1f, 1.5f)]
    public float retreatSpeedMultiplier = 0.8f;

    [Tooltip("Arkasında zemin yoksa (uçurum) geri çekilmez.")]
    public bool checkLedge = true;

    private EnemyController enemy;
    private Rigidbody2D rb;
    private int groundMask;

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        rb = GetComponent<Rigidbody2D>();
    }

    // ChaseState hızı ayarladıktan SONRA (LateUpdate) geri çekilmeyi uygula.
    private void LateUpdate()
    {
        if (retreatDistance <= 0f || enemy == null || rb == null)
            return;

        if (enemy.IsDead || enemy.target == null || enemy.IsTargetDead)
            return;

        if (!(enemy.CurrentState is EnemyChaseState) || enemy.IsMovementLocked)
            return;

        float dx = transform.position.x - enemy.target.position.x;

        if (Mathf.Abs(dx) >= retreatDistance)
            return;

        float away = dx >= 0f ? 1f : -1f;

        if (checkLedge && !GroundBehind(away))
            return;

        rb.linearVelocity =
            new Vector2(
                away * enemy.ScaledChaseSpeed * retreatSpeedMultiplier,
                rb.linearVelocity.y
            );
    }

    private bool GroundBehind(float away)
    {
        if (groundMask == 0)
        {
            PlayerController player =
                enemy.target != null ? enemy.target.GetComponent<PlayerController>() : null;

            if (player == null || player.Movement == null)
                return true;   // bilinmiyorsa engelleme

            groundMask = player.Movement.groundMask;
        }

        Collider2D body = GetComponent<Collider2D>();

        Vector2 origin =
            body != null
                ? new Vector2(
                    body.bounds.center.x + away * (body.bounds.extents.x + 0.3f),
                    body.bounds.min.y + 0.2f
                )
                : (Vector2)transform.position + new Vector2(away * 0.8f, 0f);

        return Physics2D.Raycast(origin, Vector2.down, 1.5f, groundMask).collider != null;
    }

    public void Fire(int damage)
    {
        if (enemy == null)
            enemy = GetComponent<EnemyController>();

        if (enemy == null)
            return;

        float facing = enemy.FacingDirection;

        Collider2D body = GetComponent<Collider2D>();

        Bounds b =
            body != null
                ? body.bounds
                : new Bounds(transform.position, Vector3.one);

        Vector2 origin =
            new Vector2(
                b.center.x + facing * (b.extents.x + muzzleForward),
                b.min.y + b.size.y * muzzleHeight
            );

        Vector2 forward = new Vector2(facing, 0f);
        Vector2 dir = forward;

        // Nişan: oyuncu ÖNÜNDEYSE gövdesine, sınırlı açıyla.
        if (enemy.target != null)
        {
            PlayerController player = enemy.target.GetComponent<PlayerController>();

            Vector2 aim =
                player != null && player.col != null
                    ? (Vector2)player.col.bounds.center
                    : (Vector2)enemy.target.position;

            Vector2 to = aim - origin;

            if (Mathf.Sign(to.x) == facing && to.sqrMagnitude > 0.01f)
            {
                float angle = Vector2.SignedAngle(forward, to);

                angle = Mathf.Clamp(angle, -maxAimAngle, maxAimAngle);

                dir = Quaternion.Euler(0f, 0f, angle) * forward;
            }
        }

        EnemyProjectile.Spawn(this, enemy, origin, dir.normalized, damage);
    }
}
