using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [Header("Attack")]
    public float attackRange = 1.5f;

    [Header("Chase")]
    public float chaseStopDistance = 1.8f;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    [Header("Stagger")]
    public float staggerDuration = 1.2f;
    public float attackDuration = 1f;
    public float attackWarningTime = 1f;



    [Header("Attack Recovery")]
    public float attackRecoveryTime = 0.8f;

    [Header("Hit / Knockback")]
    public float knockbackForceX = 7f;
    public float knockbackForceY = 3f;
    public float hitDuration = 0.12f;

    [Header("Hit Deceleration")]
    public float knockbackDeceleration = 45f;

    [Header("Hit Flash")]
    public Color hitFlashColor = Color.white;
    public float hitFlashDuration = 0.06f;

    [Header("Stagger Flash")]
    public Color staggerFlashColor = Color.yellow;
    public float staggerFlashDuration = 0.15f;

    [Header("Enemy Attack Knockback")]
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
    public float blockRecoveryTime = 0.25f;

    [Header("Attack Damage")]
    public int attackDamage = 1;


    [Header("Parry")]
    public int parryBalanceDamage = 50;
  
    [Header("Defense Balance")]
    public int blockBalanceDamage = 1;

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

    // =========================================================
    // PUBLIC STATE INFO
    // =========================================================

    public bool CanAttack =>
        attackRecoveryTimer <= 0f;

    public bool IsMovementLocked =>
        movementLockTimer > 0f;

    public bool IsStaggered =>
        currentState is EnemyStaggerState;

    public bool IsAttacking =>
        currentState is EnemyAttackState;

    public bool IsInAttackRecovery =>
        attackRecoveryTimer > 0f;

    public IEnemyState CurrentState =>
        currentState;

    // =========================================================
    // HIT FLASH
    // =========================================================

    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;
    private Coroutine flashRoutine;

    // =========================================================
    // ATTACK ANIMATION
    // =========================================================

    public void PlayAttackAnimation()
    {
        if (animator == null)
            return;

        animator.SetTrigger("Attack");
    }

    // =========================================================
    // AWAKE
    // =========================================================

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
    }

    // =========================================================
    // DESTROY
    // =========================================================

    private void OnDestroy()
    {
        if (enemyBalance != null)
        {
            enemyBalance.OnBalanceBroken -=
                HandleBalanceBroken;
        }
    }

    // =========================================================
    // FACING
    // =========================================================

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

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        // -----------------------------------------------------
        // ATTACK RECOVERY TIMER
        // -----------------------------------------------------

        if (attackRecoveryTimer > 0f)
        {
            attackRecoveryTimer -=
                Time.deltaTime;

            if (attackRecoveryTimer < 0f)
                attackRecoveryTimer = 0f;
        }

        // -----------------------------------------------------
        // MOVEMENT LOCK
        // -----------------------------------------------------

        if (movementLockTimer > 0f)
        {
            movementLockTimer -=
                Time.deltaTime;

            if (movementLockTimer < 0f)
                movementLockTimer = 0f;
        }

        // -----------------------------------------------------
        // FACING
        // -----------------------------------------------------
        // Enemy attack sırasında oyuncuyu takip ederek
        // dönmeyecek.
        //
        // Attack state'ten çıktığında tekrar FaceTarget
        // çalışmaya başlayacak.
        // -----------------------------------------------------

        if (!(currentState is EnemyAttackState))
        {
            FaceTarget();
        }

        // -----------------------------------------------------
        // STATE
        // -----------------------------------------------------

        currentState?.Tick();
    }

    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        if (target == null)
        {
            GameObject playerObj =
                GameObject.FindGameObjectWithTag("Player");

            if (playerObj != null)
                target = playerObj.transform;
        }

        ChangeState(
            new EnemyIdleState(this)
        );
    }

    // =========================================================
    // CHANGE STATE
    // =========================================================

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

    // =========================================================
    // FORCE STAGGER
    // =========================================================

    public void ForceStagger()
    {
        if (IsStaggered)
            return;

        PlayFlash(
            staggerFlashColor,
            staggerFlashDuration
        );

        ChangeState(
            new EnemyStaggerState(this)
        );
    }

    // =========================================================
    // ATTACK RECOVERY
    // =========================================================

    public void StartAttackRecovery()
    {
        attackRecoveryTimer =
            attackRecoveryTime;
    }

    // =========================================================
    // BALANCE HIT
    // =========================================================

    public void ApplyBalanceHit(
        Vector2 hitDirection
    )
    {
        PlayFlash(
            hitFlashColor,
            hitFlashDuration
        );

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
    // ATTACK HIT - OLD COMPATIBILITY
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
    // ATTACK HIT - COMBO
    // =========================================================

    public void ApplyAttackHit(
        Vector2 hitDirection,
        bool healthHit,
        int attackStep
    )
    {
        PlayFlash(
            hitFlashColor,
            hitFlashDuration
        );

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
    // COMBO KNOCKBACK
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

    private void PlayFlash(
        Color flashColor,
        float duration
    )
    {
        if (spriteRenderers == null ||
            spriteRenderers.Length == 0)
            return;

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);

            RestoreOriginalColors();
        }

        flashRoutine =
            StartCoroutine(
                FlashCoroutine(
                    flashColor,
                    duration
                )
            );
    }

    private System.Collections.IEnumerator FlashCoroutine(
        Color flashColor,
        float duration
    )
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null)
                continue;

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

        RestoreOriginalColors();

        flashRoutine = null;
    }

    private void RestoreOriginalColors()
    {
        if (spriteRenderers == null)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null)
                continue;

            Color color =
                spriteRenderers[i].color;

            color.r =
                originalColors[i].r;

            color.g =
                originalColors[i].g;

            color.b =
                originalColors[i].b;

            spriteRenderers[i].color =
                color;
        }
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