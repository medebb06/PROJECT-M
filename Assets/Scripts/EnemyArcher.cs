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
///   ZIPLA  → ok DÜMDÜZ yatay uçar; üstünden atlanabilir
///
/// Oyuncu çok yaklaşınca geri çekilir; ara sıra (bekleme süreli) GERİYE
/// DASH atıp mesafe açar ve hemen ok atmaya hazır olur.
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
    public float arrowSpeed = 24f;

    [Tooltip("Ok bu kadar sn sonra kaybolur.")]
    [Min(0.2f)]
    public float arrowLifetime = 2f;

    [Tooltip("Okun çıktığı yükseklik: gövde yüksekliğinin oranı (ayaktan).")]
    [Range(0f, 1f)]
    public float muzzleHeight = 0.6f;

    [Tooltip(
        "Açıksa ok, ATEŞ ANINDA hedefin gövde yüksekliğinde çıkar (büyük boss'ların " +
        "oku oyuncunun başının üstünden geçmesin). Zıplayarak yine kaçılır.")]
    public bool matchTargetHeight = false;

    [Tooltip("Okun gövdeden ne kadar önde çıktığı (birim).")]
    public float muzzleForward = 0.4f;

    [Tooltip(
        "Oyuncuya nişan alırken en fazla dikey açı (derece). 0 = DÜMDÜZ yatay " +
        "(önerilen; zıplayarak kaçılabilir), büyük = havadaki oyuncuyu da vurur.")]
    [Range(0f, 45f)]
    public float maxAimAngle = 0f;

    [Header("Görünüm")]
    [Tooltip("Boşsa koddan üretilen pixel ok (sağa bakar).")]
    public Sprite arrowSprite;

    public Color arrowColor = new Color(1f, 0.9f, 0.7f);

    public Color reflectedColor = new Color(0.6f, 0.95f, 1f);

    [Tooltip("Ok boyutu. Karakterlerin büyükse (ölçek 2) 2 civarı.")]
    public float arrowScale = 2f;

    [Header("İsabet")]
    [Tooltip("Okun oyuncu gövdesine 'değme' payı (birim).")]
    [Min(0f)]
    public float hitRadius = 0.25f;

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
    public float retreatDistance = 6f;

    [Tooltip("Geri çekilme hızı = Chase Speed × bu.")]
    [Range(0.1f, 1.5f)]
    public float retreatSpeedMultiplier = 0.8f;

    [Tooltip("Arkasında zemin yoksa (uçurum) geri çekilmez / dash atmaz.")]
    public bool checkLedge = true;

    [Header("Geri Dash (kaçış)")]
    public bool enableBackDash = true;

    [Tooltip("Oyuncu bundan yakınsa geri dash düşünülür.")]
    [Min(0f)]
    public float backDashTriggerDistance = 4.5f;

    [Tooltip("Her kontrolde dash atma ihtimali (0.35 = %35). Kontrol aralığı aşağıda.")]
    [Range(0f, 1f)]
    public float backDashChance = 0.35f;

    [Tooltip("İki kontrol arası (sn).")]
    [Min(0.05f)]
    public float backDashCheckInterval = 0.4f;

    [Tooltip("İki dash arası en az süre (sn).")]
    [Min(0f)]
    public float backDashCooldown = 5f;

    [Tooltip("Dash hızı (birim/sn) ve süresi (sn): mesafe ≈ hız × süre.")]
    public float backDashSpeed = 22f;

    public float backDashDuration = 0.25f;

    [Tooltip("Dash'te hafif sıçrama (dikey hız).")]
    public float backDashHop = 5f;

    [Tooltip("Dash bitince saldırı beklemesi bu kadar kısalır (hemen ok atabilsin).")]
    [Min(0f)]
    public float attackReadyAfterDash = 0.15f;

    [Tooltip("Dash sırasında arkada bırakılan iz (yarı saydam kopya) sayısı.")]
    [Range(0, 6)]
    public int backDashGhosts = 3;

    private EnemyController enemy;
    private Rigidbody2D rb;
    private SpriteRenderer mainSprite;
    private int groundMask;

    private float nextDashCheck;
    private float lastDashTime = -999f;
    private float dashUntil;
    private float dashDirection;
    private float nextGhostTime;

    public bool IsBackDashing => Time.time < dashUntil;

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        rb = GetComponent<Rigidbody2D>();
    }

    // ChaseState hızı ayarladıktan SONRA (LateUpdate): geri dash ya da
    // geri çekilme.
    private void LateUpdate()
    {
        if (enemy == null || rb == null)
            return;

        if (enemy.IsDead || enemy.target == null || enemy.IsTargetDead)
        {
            dashUntil = 0f;
            return;
        }

        bool chasing = enemy.CurrentState is EnemyChaseState;

        // ---------------- GERİ DASH SÜRÜYOR ----------------

        if (IsBackDashing)
        {
            // Vuruldu / sersemledi / saldırıya geçti: dash kesilir.
            if (!chasing || enemy.IsMovementLocked)
            {
                dashUntil = 0f;
                return;
            }

            rb.linearVelocity =
                new Vector2(
                    dashDirection * backDashSpeed * EnemyTime.Scale,
                    rb.linearVelocity.y
                );

            SpawnGhostIfDue();

            if (Time.time + Time.deltaTime >= dashUntil)
                enemy.StartAttackRecovery(attackReadyAfterDash);

            return;
        }

        if (!chasing || enemy.IsMovementLocked)
            return;

        float dx = transform.position.x - enemy.target.position.x;
        float away = dx >= 0f ? 1f : -1f;

        // ---------------- GERİ DASH KARARI ----------------

        if (TryStartBackDash(Mathf.Abs(dx), away))
            return;

        // ---------------- GERİ ÇEKİLME ----------------

        if (retreatDistance <= 0f || Mathf.Abs(dx) >= retreatDistance)
            return;

        if (checkLedge && !GroundBehind(away))
            return;

        rb.linearVelocity =
            new Vector2(
                away * enemy.ScaledChaseSpeed * retreatSpeedMultiplier,
                rb.linearVelocity.y
            );
    }

    private bool TryStartBackDash(float distance, float away)
    {
        if (!enableBackDash || distance > backDashTriggerDistance)
            return false;

        if (Time.time < nextDashCheck)
            return false;

        nextDashCheck = Time.time + backDashCheckInterval;

        if (Time.time - lastDashTime < backDashCooldown)
            return false;

        if (Random.value > backDashChance)
            return false;

        if (checkLedge && !GroundBehind(away))
            return false;

        lastDashTime = Time.time;
        dashUntil = Time.time + backDashDuration;
        dashDirection = away;
        nextGhostTime = 0f;

        rb.linearVelocity =
            new Vector2(
                away * backDashSpeed * EnemyTime.Scale,
                Mathf.Max(rb.linearVelocity.y, backDashHop)
            );

        SpawnGhostIfDue();

        return true;
    }

    // Dash izi: sprite'ın kısa süre sönen yarı saydam kopyası.
    private void SpawnGhostIfDue()
    {
        if (backDashGhosts <= 0 || Time.time < nextGhostTime)
            return;

        nextGhostTime = Time.time + backDashDuration / backDashGhosts;

        if (mainSprite == null)
            mainSprite = GetComponentInChildren<SpriteRenderer>();

        if (mainSprite == null || mainSprite.sprite == null)
            return;

        GameObject ghost = new GameObject("ArcherDashGhost");

        ghost.transform.position = mainSprite.transform.position;
        ghost.transform.rotation = mainSprite.transform.rotation;
        ghost.transform.localScale = mainSprite.transform.lossyScale;

        SpriteRenderer sr = ghost.AddComponent<SpriteRenderer>();

        sr.sprite = mainSprite.sprite;
        sr.flipX = mainSprite.flipX;
        sr.sortingLayerID = mainSprite.sortingLayerID;
        sr.sortingOrder = mainSprite.sortingOrder - 1;
        sr.color = new Color(0.6f, 0.85f, 1f, 0.5f);

        ghost.AddComponent<FadeAndDestroy>().duration = 0.25f;
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

        if (matchTargetHeight && enemy.target != null)
        {
            Collider2D tc = enemy.target.GetComponent<Collider2D>();

            if (tc == null)
                tc = enemy.target.GetComponentInChildren<Collider2D>();

            if (tc != null)
                origin.y = Mathf.Clamp(tc.bounds.center.y, b.min.y + 0.2f, b.max.y);
        }

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

        Fired?.Invoke(this, damage);
    }

    /// <summary>Her ok bırakıldığında (boss yankısı vb. dinler).</summary>
    public static event System.Action<EnemyArcher, int> Fired;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetFired()
    {
        Fired = null;
    }
}

// Geri dash izi için: sprite'ı söndürüp yok eder (gerçek zaman).
public class FadeAndDestroy : MonoBehaviour
{
    public float duration = 0.25f;

    private SpriteRenderer sr;
    private float startAlpha;
    private float age;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        startAlpha = sr != null ? sr.color.a : 1f;
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;

        float t = Mathf.Clamp01(age / Mathf.Max(0.01f, duration));

        if (sr != null)
        {
            Color c = sr.color;
            c.a = startAlpha * (1f - t);
            sr.color = c;
        }

        if (t >= 1f)
            Destroy(gameObject);
    }
}
