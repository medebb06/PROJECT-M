using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [Header("Attack")]
    public float attackRange = 1.5f;

    [Header("Chase")]
    public float chaseStopDistance = 1.8f;
    public float chaseSpeed = 4f;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    [Header("Stagger")]
    public float staggerDuration = 1.2f;
    public float attackWarningTime = 1f;

    [Header("Attack Recovery")]
    public float attackRecoveryTime = 0.8f;

    [Header("Attack Commit (Super Armor)")]
    [Tooltip(
        "Uyarı süresinin bu oranından sonra saldırı hasar alınca " +
        "KESİLMEZ ve düşman savrulmaz. 0 = baştan itibaren, " +
        "1 = hiç (eski davranış). Ağır düşman: ~0.15, normal: ~0.4, " +
        "hafif: 1.")]
    [Range(0f, 1f)]
    public float attackCommitPoint = 0.4f;

    public Color committedHitFlashColor =
        new Color(1f, 0.6f, 0.1f);

    public float committedFlashDuration = 0.1f;

    [Tooltip("Kararlı saldırı vurulduğunda ek çalan ses (boşsa sessiz).")]
    public AudioClip committedHitClip;

    [Header("Hit Deceleration")]
    public float knockbackDeceleration = 45f;

    [Header("Hit Flash")]
    public Color hitFlashColor = Color.white;
    public float hitFlashDuration = 0.06f;

    [Header("Stagger Flash")]
    public Color staggerFlashColor = Color.yellow;
    public float staggerFlashDuration = 0.15f;

    [Header("Balance Damage Flash (beyaz flaş)")]
    [Tooltip(
        "Denge (posture) hasarı alınca sprite tamamen beyaz yanıp söner. " +
        "SpriteWhiteFlash.shader dosyası Assets/Resources içinde olmalı; " +
        "yoksa sadece renk tonu değişir (renkli sprite'ta görünmez).")]
    public bool solidWhiteFlash = true;

    public Color balanceHitFlashColor = Color.white;

    public float balanceHitFlashDuration = 0.09f;

    [Header("Enemy Attack Damage (Player'ın can birimine)")]
    [Tooltip(
        "Bu düşmanın normal vuruşunun oyuncunun Health'inden düştüğü miktar. " +
        "Health 'birim' mantığıyla çalışır: 1 = bir can birimi. " +
        "Block/parry'de can hasarı yoktur.")]
    [Min(1)]
    public int attackDamage = 1;

    [Header("Enemy Attack Knockback (Player'a uygulanır)")]
    public float attackKnockbackForce = 7f;
    public float attackKnockbackVerticalForce = 3f;
    public float attackKnockbackDuration = 0.12f;

    [Header("Balance Hit Knockback")]
    public float balanceHitKnockbackForce = 1.5f;
    public float balanceHitKnockbackVerticalForce = 0f;
    public float balanceHitKnockbackDuration = 0.10f;
    public float balanceHitKnockbackDeceleration = 15f;

    [Header("Posture Hit Knockback")]
    public float postureKnockbackForce = 6f;
    public float postureKnockbackVerticalForce = 1f;
    public float postureKnockbackDuration = 0.12f;

    [Header("Health Hit Knockback")]
    public float healthKnockbackForce = 8f;
    public float healthKnockbackVerticalForce = 1.5f;
    public float healthKnockbackDuration = 0.12f;

    [Header("Combo Knockback")]
    public float attack1KnockbackForce = 4f;
    public float attack2KnockbackForce = 5.5f;
    public float attack3KnockbackForce = 7f;
    public float attack4KnockbackForce = 9f;

    [Header("Block Knockback")]
    public float blockKnockbackForce = 2.5f;
    public float blockKnockbackVerticalForce = 0.2f;
    public float blockKnockbackDuration = 0.15f;
    public float blockKnockbackDeceleration = 12f;

    [Header("Defense Balance")]
    public int parryBalanceDamage = 2;

    [Header("Player Block Posture")]
    // Oyuncunun block'ladığı her vuruşta PlayerPosture'dan
    // düşen miktar. (PlayerPosture max 100.)
    public int blockPostureDamage = 25;

    [Header("Ground Slam")]
    // Oyuncunun ground slam'i bu düşmanın dengesine
    // ne kadar hasar verir.
    public int slamBalanceDamage = 3;

    [Header("Execute")]
    public int executeDamage = 10;
    public float executeDistance = 1.2f;
    public float executeDuration = 0.08f;

    [Header("Hit Sounds")]
    public AudioSource hitAudioSource;
    public AudioClip healthHitClip;
    public AudioClip postureHitClip;
    public AudioClip knockbackClip;

    public Transform target;
    public float chaseRange = 5f;
    [Header("Facing")]
    [SerializeField] private SpriteRenderer enemySprite;

    private float attackRecoveryTimer;
    private float movementLockTimer;

    private EnemyBalance enemyBalance;
    private IEnemyState currentState;

    private Health health;
    private Health targetHealth;
    private FinisherTargetHighlight finisherHighlight;
    private EnemyAttackTelegraph telegraph;

    private bool deathHandled;
    private float nextTargetSearchTime;

    // =========================================================
    // HIT FLASH
    // =========================================================

    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;
    private Material[] originalMaterials;

    private static Material sharedFlashMaterial;
    private static bool warnedMissingFlashShader;
    private Coroutine flashRoutine;

    // Stagger boyunca korunan renk tonu.
    // Hit flash bitince bu tona geri dönülür.
    private float staggerTintEndTime;

    public bool CanAttack =>
        attackRecoveryTimer <= 0f;

    public bool IsMovementLocked =>
        movementLockTimer > 0f;

    public bool IsStaggered =>
        currentState is EnemyStaggerState;

    public bool IsDead =>
        health != null &&
        health.IsDead;

    // Saldırının kararlı aşamasında mı?
    public bool IsAttackCommitted =>
        currentState is EnemyAttackState attack &&
        attack.IsCommitted;

    // Hedef (oyuncu) öldü mü?
    public bool IsTargetDead
    {
        get
        {
            if (target == null)
                return false;

            if (
                targetHealth == null ||
                targetHealth.gameObject != target.gameObject
            )
            {
                targetHealth =
                    target.GetComponent<Health>();
            }

            return targetHealth != null &&
                   targetHealth.IsDead;
        }
    }

    public IEnemyState CurrentState =>
        currentState;

    public void PlayAttackAnimation()
    {
        if (animator == null)
            return;

        animator.SetTrigger("Attack");
    }

    private void Awake()
    {
        if (hitAudioSource == null)
        {
            hitAudioSource =
                GetComponent<AudioSource>();

            if (hitAudioSource == null)
            {
                hitAudioSource =
                    gameObject.AddComponent<AudioSource>();
            }
        }

        hitAudioSource.playOnAwake = false;

        health =
            GetComponent<Health>();

        finisherHighlight =
            GetComponent<FinisherTargetHighlight>();

        telegraph =
            GetComponent<EnemyAttackTelegraph>();

        enemyBalance =
            GetComponent<EnemyBalance>();

        if (enemyBalance != null)
        {
            enemyBalance.OnBalanceBroken +=
                HandleBalanceBroken;
        }

        // -----------------------------------------------------
        // HIT FLASH
        // -----------------------------------------------------

        spriteRenderers =
            GetComponentsInChildren<SpriteRenderer>(true);

        originalColors =
            new Color[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null)
                continue;

            originalColors[i] =
                spriteRenderers[i].color;
        }

        originalMaterials =
            new Material[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null)
                continue;

            originalMaterials[i] =
                spriteRenderers[i].sharedMaterial;
        }
    }

    private void OnDestroy()
    {
        if (enemyBalance != null)
        {
            enemyBalance.OnBalanceBroken -=
                HandleBalanceBroken;
        }
    }

    // =========================================================
    // TARGET
    // =========================================================

    // Hedef yoksa Player tag'iyle bulmayı dener.
    // Her karede aramasın diye 0.5 sn'de bir dener.
    public bool TryFindTarget()
    {
        if (target != null)
            return true;

        if (Time.time < nextTargetSearchTime)
            return false;

        nextTargetSearchTime =
            Time.time + 0.5f;

        GameObject playerObj =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObj == null)
            return false;

        target =
            playerObj.transform;

        return true;
    }

    private void FaceTarget()
    {
        if (target == null)
            return;

        if (enemySprite == null)
            return;

        if (target.position.x > transform.position.x)
        {
            enemySprite.flipX = false;
        }
        else if (target.position.x < transform.position.x)
        {
            enemySprite.flipX = true;
        }
    }

    private void Update()
    {
        // =====================================================
        // ÖLÜ DÜŞMAN
        // Health fade-out sırasında düşman hâlâ Update
        // alıyordu ve ölürken bile oyuncuya vurabiliyordu.
        // =====================================================

        if (health != null && health.IsDead)
        {
            if (!deathHandled)
                HandleDeath();

            return;
        }

        if (attackRecoveryTimer > 0f)
        {
            attackRecoveryTimer -=
                Time.deltaTime;

            if (attackRecoveryTimer < 0f)
                attackRecoveryTimer = 0f;
        }

        if (movementLockTimer > 0f)
        {
            movementLockTimer -=
                Time.deltaTime;

            if (movementLockTimer < 0f)
                movementLockTimer = 0f;
        }

        UpdateStaggerTint();

        FaceTarget();

        currentState?.Tick();
    }

    private void HandleDeath()
    {
        deathHandled = true;

        // Telegraph kapatılmazsa her karede rengi ezip
        // fade-out'u bozar.
        EnemyAttackTelegraph telegraph =
            GetComponent<EnemyAttackTelegraph>();

        if (telegraph != null)
            telegraph.enabled = false;

        SetFinisherHighlight(false);
    }

    private void Start()
    {
        TryFindTarget();

        ChangeState(
            new EnemyIdleState(this)
        );
    }

    public void ChangeState(
        IEnemyState newState
    )
    {
        if (newState == null)
            return;

        if (currentState == newState)
            return;

        currentState?.Exit();

        currentState = newState;

        currentState.Enter();
    }

    public void ForceStagger()
    {
        if (IsStaggered)
            return;

        PlayFlash(
            staggerFlashColor,
            staggerFlashDuration
        );

        staggerTintEndTime =
            Time.time + staggerFlashDuration;

        ChangeState(
            new EnemyStaggerState(this)
        );
    }

    public void StartAttackRecovery()
    {
        attackRecoveryTimer =
            attackRecoveryTime;
    }

    // =========================================================
    // FINISHER HIGHLIGHT
    // =========================================================

    public void SetFinisherHighlight(bool highlighted)
    {
        if (finisherHighlight == null)
            return;

        finisherHighlight.SetHighlighted(
            highlighted
        );
    }

    // =========================================================
    // BALANCE HIT
    // =========================================================

    public void ApplyBalanceHit(
        Vector2 hitDirection
    )
    {
        // Kararlı saldırı: kesilmez, savrulmaz.
        if (TryAbsorbCommittedHit())
            return;

        // Denge hasarı: beyaz flaş.
        PlayBalanceDamageFlash();

        Rigidbody2D rb =
            GetComponent<Rigidbody2D>();

        if (rb == null)
            return;

        float direction =
            Mathf.Sign(hitDirection.x);

        if (direction == 0f)
            direction = 1f;

        Vector2 knockback =
            new Vector2(
                direction *
                balanceHitKnockbackForce,
                balanceHitKnockbackVerticalForce
            );

        movementLockTimer =
            balanceHitKnockbackDuration;

        if (!IsStaggered)
        {
            ChangeState(
                new EnemyHitState(
                    this,
                    balanceHitKnockbackDuration,
                    balanceHitKnockbackDeceleration
                )
            );
        }

        rb.linearVelocity =
            knockback;
    }

    // =========================================================
    // ATTACK HIT - OLD COMPATIBILITY METHOD
    // =========================================================

    public void ApplyAttackHit(
        Vector2 hitDirection,
        bool healthHit
    )
    {
        ApplyAttackHit(
            hitDirection,
            healthHit,
            1
        );
    }

    // =========================================================
    // ATTACK HIT - COMBO VERSION
    // =========================================================

    public void ApplyAttackHit(
        Vector2 hitDirection,
        bool healthHit,
        int attackStep
    )
    {
        // Kararlı saldırı: kesilmez, savrulmaz.
        if (TryAbsorbCommittedHit())
            return;

        // Can vuruşu: eski renk tonu flaşı.
        // Denge kıran vuruş (healthHit = false): beyaz flaş.
        if (healthHit)
        {
            PlayFlash(
                hitFlashColor,
                hitFlashDuration
            );
        }
        else
        {
            PlayBalanceDamageFlash();
        }

        Rigidbody2D rb =
            GetComponent<Rigidbody2D>();

        if (rb == null)
            return;

        float direction =
            Mathf.Sign(hitDirection.x);

        if (direction == 0f)
            direction = 1f;

        float knockbackForce;

        if (healthHit)
        {
            knockbackForce =
                GetComboKnockbackForce(
                    attackStep
                );
        }
        else
        {
            knockbackForce =
                postureKnockbackForce;
        }

        float knockbackVerticalForce =
            healthHit
                ? healthKnockbackVerticalForce
                : postureKnockbackVerticalForce;

        float knockbackDuration =
            healthHit
                ? healthKnockbackDuration
                : postureKnockbackDuration;

        Vector2 knockback =
            new Vector2(
                direction *
                knockbackForce,
                knockbackVerticalForce
            );

        movementLockTimer =
            knockbackDuration;

        if (!IsStaggered)
        {
            ChangeState(
                new EnemyHitState(
                    this,
                    knockbackDuration
                )
            );
        }

        rb.linearVelocity =
            knockback;

        PlayKnockbackSound();
    }

    // =========================================================
    // COMBO KNOCKBACK FORCE
    // =========================================================

    private float GetComboKnockbackForce(
        int attackStep
    )
    {
        switch (attackStep)
        {
            case 1:
                return attack1KnockbackForce;

            case 2:
                return attack2KnockbackForce;

            case 3:
                return attack3KnockbackForce;

            case 4:
                return attack4KnockbackForce;

            default:
                return attack1KnockbackForce;
        }
    }

    // =========================================================
    // HIT FLASH
    // =========================================================

    // Denge (posture) hasarı alınca çağrılır: beyaz flaş.
    // Shader bulunamazsa renk tonuna düşer (eski davranış).
    public void PlayBalanceDamageFlash()
    {
        PlayFlash(
            balanceHitFlashColor,
            balanceHitFlashDuration,
            solidWhiteFlash
        );
    }

    private void PlayFlash(
        Color flashColor,
        float duration,
        bool solid = false
    )
    {
        if (spriteRenderers == null ||
            spriteRenderers.Length == 0)
            return;

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);

            RestoreBaseColors();
        }

        flashRoutine =
            StartCoroutine(
                FlashCoroutine(
                    flashColor,
                    duration,
                    solid
                )
            );
    }

    private System.Collections.IEnumerator FlashCoroutine(
        Color flashColor,
        float duration,
        bool solid
    )
    {
        // Sprite renkliyse "beyaz tint" hiçbir şey değiştirmez.
        // Gerçek flaş için sprite'ı tam dolu silüete çeviren
        // materyale geçici olarak geçiyoruz.
        bool useSolid =
            solid &&
            GetFlashMaterial() != null;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null)
                continue;

            if (useSolid)
            {
                spriteRenderers[i].sharedMaterial =
                    sharedFlashMaterial;
            }

            Color color =
                spriteRenderers[i].color;

            color.r = flashColor.r;
            color.g = flashColor.g;
            color.b = flashColor.b;

            spriteRenderers[i].color =
                color;
        }

        yield return new WaitForSeconds(
            duration
        );

        RestoreBaseColors();

        flashRoutine = null;
    }

    private static Material GetFlashMaterial()
    {
        if (sharedFlashMaterial != null)
            return sharedFlashMaterial;

        Shader shader =
            Shader.Find("Custom/SpriteWhiteFlash");

        if (shader == null)
        {
            if (!warnedMissingFlashShader)
            {
                warnedMissingFlashShader = true;

                Debug.LogWarning(
                    "EnemyController: 'Custom/SpriteWhiteFlash' " +
                    "shader'ı bulunamadı. SpriteWhiteFlash.shader " +
                    "dosyasını Assets/Resources/ klasörüne koy. " +
                    "O zamana kadar flaş sadece renk tonu olarak çalışır."
                );
            }

            return null;
        }

        sharedFlashMaterial =
            new Material(shader);

        return sharedFlashMaterial;
    }

    // Flash bitince dönülecek renk:
    // Stagger tonu hâlâ aktifse o, değilse orijinal renk.
    // (Eskiden stagger sırasında alınan ilk vuruşun flash'ı
    // stagger rengini siliyordu.)
    private void RestoreBaseColors()
    {
        if (spriteRenderers == null)
            return;

        bool staggerTinted =
            IsStaggered &&
            Time.time < staggerTintEndTime;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null)
                continue;

            // Flaş için değiştirilen materyali geri ver.
            if (
                originalMaterials != null &&
                originalMaterials[i] != null &&
                spriteRenderers[i].sharedMaterial !=
                originalMaterials[i]
            )
            {
                spriteRenderers[i].sharedMaterial =
                    originalMaterials[i];
            }

            Color baseColor =
                staggerTinted
                    ? staggerFlashColor
                    : originalColors[i];

            Color color =
                spriteRenderers[i].color;

            // Alpha'ya dokunma (ölüm fade-out'u için).
            color.r = baseColor.r;
            color.g = baseColor.g;
            color.b = baseColor.b;

            spriteRenderers[i].color =
                color;
        }
    }

    private void UpdateStaggerTint()
    {
        if (staggerTintEndTime <= 0f)
            return;

        if (Time.time < staggerTintEndTime)
            return;

        staggerTintEndTime = 0f;

        if (flashRoutine == null)
            RestoreBaseColors();
    }

    // EnemyStaggerState.Exit tarafından çağrılır.
    public void ClearStaggerTint()
    {
        staggerTintEndTime = 0f;

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);

            flashRoutine = null;
        }

        RestoreBaseColors();
    }

    // =========================================================
    // AUDIO
    // =========================================================

    public void PlayHealthHitSound()
    {
        if (hitAudioSource == null)
            return;

        if (healthHitClip == null)
            return;

        hitAudioSource.PlayOneShot(
            healthHitClip
        );
    }

    public void PlayPostureHitSound()
    {
        if (hitAudioSource == null)
            return;

        if (postureHitClip == null)
            return;

        hitAudioSource.PlayOneShot(
            postureHitClip
        );
    }

    // =========================================================
    // COMMITTED HIT
    // Hasar ve denge hasarı zaten uygulandı (çağıran taraf).
    // Burada sadece state kesilmez, knockback yok, farklı geri bildirim.
    // Denge kırılırsa yine stagger olur ve saldırı iptal edilir.
    // =========================================================

    private bool TryAbsorbCommittedHit()
    {
        if (!IsAttackCommitted)
            return false;

        // Telegraph rengi her karede sprite'ı ezdiği için
        // vuruş flaşı görünmezdi; kısa süre sustur.
        if (telegraph != null)
            telegraph.Suppress(committedFlashDuration);

        PlayFlash(
            committedHitFlashColor,
            committedFlashDuration
        );

        PlayCommittedHitSound();

        return true;
    }

    public void PlayCommittedHitSound()
    {
        if (hitAudioSource == null)
            return;

        if (committedHitClip == null)
            return;

        hitAudioSource.PlayOneShot(
            committedHitClip
        );
    }

    public void PlayKnockbackSound()
    {
        if (hitAudioSource == null)
            return;

        if (knockbackClip == null)
            return;

        hitAudioSource.PlayOneShot(
            knockbackClip
        );
    }

    // =========================================================
    // BLOCK KNOCKBACK
    // =========================================================

    public void ApplyBlockKnockback(
        Vector2 hitDirection
    )
    {
        Rigidbody2D rb =
            GetComponent<Rigidbody2D>();

        if (rb == null)
            return;

        float direction =
            Mathf.Sign(
                hitDirection.x
            );

        if (direction == 0f)
            direction = 1f;

        float knockbackDirection =
            -direction;

        Vector2 knockback =
            new Vector2(
                knockbackDirection *
                blockKnockbackForce,
                blockKnockbackVerticalForce
            );

        movementLockTimer =
            blockKnockbackDuration;

        if (!IsStaggered)
        {
            ChangeState(
                new EnemyHitState(
                    this,
                    blockKnockbackDuration,
                    blockKnockbackDeceleration
                )
            );
        }

        rb.linearVelocity =
            knockback;
    }

    // =========================================================
    // EXECUTE
    // =========================================================

    public void Execute()
    {
        if (!IsStaggered)
            return;

        Debug.Log(
            "EXECUTE TARGET: " +
            gameObject.name
        );

        ChangeState(
            new EnemyExecuteState(this)
        );
    }

    // =========================================================
    // BALANCE BROKEN
    // =========================================================

    private void HandleBalanceBroken()
    {
        ForceStagger();
    }
}