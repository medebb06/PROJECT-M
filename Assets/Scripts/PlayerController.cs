using Unity.Cinemachine;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [System.Serializable]
    public class ImpactSettings
    {


        [Header("Ground Slam")]
        public float slamSpeed = 35f;
        public float slamDamageRadius = 2f;
        public int slamDamage = 25;
        public LayerMask enemyLayer;

        [Header("Height Thresholds")]
        public float lightThreshold = 4f;
        public float mediumThreshold = 7f;
        public float heavyThreshold = 10f;

        [Header("Shake Intensity")]
        public float lightIntensity = 0.25f;
        public float mediumIntensity = 0.6f;
        public float heavyIntensity = 1.2f;

        [Header("Shake Curve")]
        public AnimationCurve shakeCurve =
            AnimationCurve.EaseInOut(0, 0, 1, 1);

        public float maxFallDistance = 10f;
        public float minDistance = 2f;
    }

    [Header("Core References")]
    public Rigidbody2D rb;
    public Collider2D col;
    public Transform modelPivot;

    [Header("Attack")]
    public Transform attackPoint;

    [Header("Audio")]
    public PlayerAudio audioPlayer;

    [Header("VFX")]
    public GameObject dustPrefab;
    public Transform footPoint;

    [Header("Visual")]
    public SpriteRenderer playerSprite;

    [Header("Camera Shake")]
    public CinemachineImpulseSource impulseSource;

    [Header("Defense")]
    [SerializeField]
    private PlayerDefenseController defenseController;

    [Header("Impact")]
    public ImpactSettings impactSettings;

    [Header("Dash")]
    public float dashDistance = 10f;
    public float dashTime = 0.15f;
    public float dashCooldown = 0.4f;

    [Tooltip(
        "Dash tuşu bu kadar süre hatırlanır (sn): saldırı, hasar ya da " +
        "bekleme süresi bitmeden basılan dash kaybolmaz.")]
    [Min(0f)]
    public float dashBufferTime = 0.15f;

    [Header("Akış v2 (46. adım)")]
    [Tooltip("Dash / parry / saldırı tamponu en az bu kadar (vurulunca 0.22 sn sersemleme tuşu yutmasın).")]
    public float minInputBuffer = 0.3f;
    [Tooltip("Hasar sersemlemesinin bu oranından sonra dash ile çıkılabilir (0.5 = ikinci yarı).")]
    [Range(0f, 1f)] public float hurtDashCancelAfter = 0.5f;
    [Tooltip("Ground slam düşmana değmeden inince kilit (sn). Eski: Input Lock Duration.")]
    public float slamLandLock = 0.05f;
    [Tooltip("Koşarken ilk vuruş: hızın bu kadar saniyelik mesafesi vuruş ilerlemesine eklenir.")]
    public float attackMomentumCarry = 0.12f;
    [Tooltip("Momentumla eklenen en fazla mesafe (birim).")]
    public float attackMomentumMax = 1f;

    [Header("Dash FX")]
    public GameObject afterImagePrefab;
    public float afterImageSpacing = 0.05f;

    [Header("Run Audio")]
    public float stepTimer;

    [Header("Input Lock")]
    public float inputLockDuration = 0.12f;

    [Header("Posture Break")]
    public float postureBreakDuration = 0.45f;
    public float postureBreakKnockback = 2.5f;

    [Header("Ground Slam")]
    public float slamLockDuration = 0.12f;

    [Header("Hit Freeze")]
    public float slamFreezeTime = 0.04f;

    [Header("Ground Slam v2 (itme / sıçrama)")]
    [Tooltip("Etki yarıçapı en az bu (Impact Settings yarıçapı daha büyükse o).")]
    public float slamMinRadius = 3f;
    [Tooltip("Bu yükseklikten (birim) düşünce slam tam güç (×2).")]
    public float slamFullPowerDrop = 6f;
    [Tooltip("Düşmanın MAX dengesinin bu oranı kadar denge hasarı (× güç).")]
    [Range(0f, 1f)] public float slamBalancePercent = 0.18f;
    public float slamPushForce = 10f;
    public float slamLiftForce = 5f;
    [Tooltip("Slam bir düşmana değerse oyuncu bu hızla yukarı sıçrar (0 = kapalı).")]
    public float slamBounceVelocity = 10f;

    [HideInInspector] public bool canControl = true;
    [HideInInspector] public bool isDashing;
    // Dokunulmazlık iki kaynaktan beslenir:
    //  - State kaynaklı (Dash, Death...): player.isInvincible = true/false
    //  - Hasar sonrası korumalı dönem: hitInvincibilityTimer
    // Ayrı tutuldu ki Dash çıkışta "false" yapınca hasar korumasını silmesin.
    private bool stateInvincible;

    [HideInInspector] public float hitInvincibilityTimer;

    public bool isInvincible
    {
        get { return stateInvincible || hitInvincibilityTimer > 0f; }
        set { stateInvincible = value; }
    }
    [HideInInspector] public bool isAttackLocked;
    [HideInInspector] public bool slamGroundLock;
    [HideInInspector] public float slamLockTimer;

    [Header("Ground Slam bekleme")]
    [Tooltip("Slam başladıktan sonra tekrar kullanılabilmesi için gereken süre (sn).")]
    public float slamCooldown = 1.0f;

    [HideInInspector] public float slamReadyTime;

    public bool CanGroundSlam => Time.time >= slamReadyTime;
    [HideInInspector] public float dashCooldownTimer;
    [HideInInspector] public bool dashPressed;
    [HideInInspector] public float dashBufferTimer;
    [HideInInspector] public bool jumpConsumed;
    [HideInInspector] public bool inputLocked;
    [HideInInspector] public float inputLockTimer;
    [HideInInspector] public bool attackFacingLocked;

    public float moveInput;
    public bool jumpHeld;
    public bool isGrounded;
    public float facingDir = 1f;

    [HideInInspector] public bool slamPressed;
    public float verticalInput;

    public bool canAttack =>
        canControl &&
        !isDashing &&
        !isAttackLocked;

    public bool IsFullyLocked =>
        stateMachine != null &&
        stateMachine.CurrentState is PlayerPostureBreakState;

    public PlayerStateMachine stateMachine;

    private PlayerMovement movement;
    private PlayerInputHandler inputHandler;
    private PlayerAnimationController animationController;
    private PlayerFeedback feedback;
    private PlayerCombatController combat;

    public PlayerMovement Movement => movement;

    public float MoveSpeed => movement != null
        ? movement.moveSpeed
        : 7f;

    public float Acceleration => movement != null
        ? movement.acceleration
        : 45f;

    public float Deceleration => movement != null
        ? movement.deceleration
        : 60f;

    public float AirControl => movement != null
        ? movement.airControl
        : 0.6f;
    public float moveSpeed => movement != null
    ? movement.moveSpeed
    : 7f;

    public float acceleration => movement != null
        ? movement.acceleration
        : 45f;

    public float deceleration => movement != null
        ? movement.deceleration
        : 60f;

    public float wallJumpControlAccelerationMultiplier =>
        movement != null
            ? movement.wallJumpControlAccelerationMultiplier
            : 1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        if (rb == null)
            Debug.LogError(
                "PLAYER CONTROLLER: Rigidbody2D bulunamadı!"
            );

        if (col == null)
            Debug.LogError(
                "PLAYER CONTROLLER: Collider2D bulunamadı!"
            );

        if (rb != null)
        {
            Debug.Log("START GRAVITY: " + rb.gravityScale);
            rb.freezeRotation = true;
        }

        if (audioPlayer == null)
            audioPlayer = GetComponent<PlayerAudio>();

        if (impulseSource == null)
            impulseSource =
                GetComponent<CinemachineImpulseSource>();

        if (defenseController == null)
            defenseController =
                GetComponent<PlayerDefenseController>();

        combat = GetComponent<PlayerCombatController>();

        SetupComponents();

        stateMachine = new PlayerStateMachine();

        stateMachine.Initialize(
            new GroundedState(this, stateMachine)
        );
    }

    private void SetupComponents()
    {
        movement = GetComponent<PlayerMovement>();

        if (movement == null)
            movement = gameObject.AddComponent<PlayerMovement>();

        movement.Initialize(this);

        inputHandler = GetComponent<PlayerInputHandler>();

        if (inputHandler == null)
            inputHandler =
                gameObject.AddComponent<PlayerInputHandler>();

        inputHandler.Initialize(this);

        animationController =
            GetComponent<PlayerAnimationController>();

        if (animationController == null)
            animationController =
                gameObject.AddComponent<PlayerAnimationController>();

        animationController.Initialize(this);

        feedback = GetComponent<PlayerFeedback>();

        if (feedback == null)
            feedback = gameObject.AddComponent<PlayerFeedback>();

        feedback.Initialize(this);
    }

    void Update()
    {
        inputHandler.ReadInput();

        HandleJump();
        HandleTimers();
        HandleFacing();
        HandleRunAudio();

        animationController.UpdateAnimation();

        stateMachine.Update();
    }

    void FixedUpdate()
    {
        movement.FixedUpdateMovement();
        stateMachine.FixedUpdate();
    }

    private void HandleTimers()
    {
        movement.UpdateTimers();

        if (hitInvincibilityTimer > 0f)
        {
            hitInvincibilityTimer -= Time.deltaTime;

            if (hitInvincibilityTimer < 0f)
                hitInvincibilityTimer = 0f;
        }

        if (inputLocked)
        {
            inputLockTimer -= Time.deltaTime;

            if (inputLockTimer <= 0f)
            {
                inputLockTimer = 0f;
                inputLocked = false;
            }
        }

        if (slamGroundLock)
        {
            slamLockTimer -= Time.deltaTime;

            if (slamLockTimer <= 0f)
            {
                slamLockTimer = 0f;
                slamGroundLock = false;
            }
        }
    }

    // ZIPLAMA. Tampon (jumpBufferCounter) kilitliyken de dolar; burada
    // ilk uygun anda tüketilir. Block/parry'den zıplayarak çıkılabilir;
    // saldırının vuruş karesinden SONRA zıplama saldırıyı iptal eder
    // (öncesinde tampon bekler).
    private void HandleJump()
    {
        if (!canControl)
            return;

        // Kilitliyken tamponu harcama (JumpState açılıp kapanırdı).
        if (inputLocked)
            return;

        if (movement.isWallSliding)
            return;

        if (!isGrounded &&
            movement.IsTouchingWall())
            return;

        if (movement.jumpBufferCounter <= 0f)
            return;

        if (movement.coyoteCounter <= 0f)
            return;

        if (jumpConsumed)
            return;

        // (46. adım) Zıplama saldırıyı HER AN keser (eskiden vuruş karesine
        // kadar bekliyordu; süpürmeye zıplamak gecikiyordu).

        // Savunmadan zıplayarak çık (ör. süpürmeye karşı).
        if (defenseController != null &&
            defenseController.IsDefending)
        {
            defenseController.CancelDefense();
        }

        if (combat != null)
            combat.CancelAttack();

        movement.jumpBufferCounter = 0f;
        movement.coyoteCounter = 0f;

        jumpConsumed = true;

        // Zıplama animasyonu ve sesi artık JumpState.Enter içinde.
        // (Eskiden hem burada hem GroundedState'te kopya vardı.)
        stateMachine.ChangeState(
            new JumpState(this, stateMachine)
        );
    }

    public void ApplyMovement(float control)
    {
        movement.ApplyMovement(control);
    }

    public void SetVelocity(Vector2 velocity)
    {
        movement.SetVelocity(velocity);
    }

    public bool IsTouchingWall()
    {
        return movement.IsTouchingWall();
    }

    public bool IsGrounded()
    {
        return isGrounded;
    }



    private void HandleFacing()
    {
        if (defenseController != null &&
            defenseController.IsDefending)
            return;

        if (attackFacingLocked)
            return;

        if (moveInput == 0f)
            return;

        facingDir = Mathf.Sign(moveInput);

        if (playerSprite == null)
            return;

        playerSprite.flipX =
            facingDir < 0f;
    }





    private void HandleRunAudio()
    {
        if (audioPlayer == null)
            return;

        if (!isGrounded ||
            !canControl ||
            isDashing ||
            inputLocked ||
            moveInput == 0f)
        {
            audioPlayer.StopRun();
            return;
        }

        float speedFactor = 0f;

        if (MoveSpeed > 0f && rb != null)
        {
            speedFactor =
                Mathf.Clamp01(
                    Mathf.Abs(rb.linearVelocity.x) /
                    MoveSpeed
                );
        }

        audioPlayer.StartRun(speedFactor);
    }


    public void OnPlayerLand()
    {
        jumpConsumed = false;

        movement.wallJumpBlockedDirection = 0;

        animationController.PlayLandAnimation();

        float fallDistance =
            movement.GetMaxAirHeight() -
            transform.position.y;

        if (impactSettings != null &&
            fallDistance >= impactSettings.minDistance)
        {
            feedback.PlayLandingFeedback(fallDistance);
        }
        else
        {
            SpawnDust();
        }

        movement.ResetAirData();
    }

    public void SpawnDust()
    {
        Collider2D col = GetComponent<Collider2D>();

        Vector3 feet =
            col != null
                ? new Vector3(col.bounds.center.x, col.bounds.min.y, 0f)
                : transform.position;

        // Çizilmiş "dust" efekti tanımlıysa onu oynat; yoksa eski toz.
        if (SpriteFxLibrary.Play("dust", feet, new Vector2(transform.localScale.x, 0f)))
            return;

        feedback.SpawnDust();
    }


    public float GetDashSpeedFactor()
    {
        return Mathf.Clamp01(
            Mathf.Abs(dashDistance) / 10f
        );
    }

    // Yön tuşu basılıysa o yöne (block'tan / saldırıdan çıkarken de),
    // değilse baktığın yöne. Yön değişirse sprite da döner.
    public float GetDashDirection()
    {
        if (Mathf.Abs(moveInput) > 0.1f)
        {
            facingDir = Mathf.Sign(moveInput);

            if (playerSprite != null)
                playerSprite.flipX = facingDir < 0f;
        }

        return facingDir == 0f
            ? 1f
            : facingDir;
    }

    public void ApplyPostureBreak(Vector2 hitDirection)
    {
        if (rb == null)
            return;

        moveInput = 0f;
        jumpHeld = false;
        dashPressed = false;
        dashBufferTimer = 0f;

        // Posture kırıldığı anda mevcut
        // block / parry state'ini kapat.
        if (defenseController != null)
        {
            defenseController.ChangeState(null);
        }

        stateMachine.ChangeState(
            new PlayerPostureBreakState(
                this,
                hitDirection
            )
        );

        Debug.Log(
            "PLAYER POSTURE BREAK → STAGGER STATE"
        );
    }

    public void PlayAttackAnimation(int attackStep)
    {
        animationController.PlayAttackAnimation(attackStep);
    }

    // Silah hızı için saldırı animasyonu hızı (1 = normal).
    public void SetAttackAnimationSpeed(float speed)
    {
        if (animationController != null)
            animationController.SetSpeed(speed);
    }

    public void PlayDeathAnimation()
    {
        animationController.PlayDeathAnimation();
    }

    public void PlayParryAnimation()
    {
        animationController.PlayParryAnimation();
    }

    public void PlayBlockAnimation()
    {
        animationController.PlayBlockAnimation();
    }

    public void SetBlockingAnimation(bool blocking)
    {
        animationController.SetBlockingAnimation(blocking);
    }

    public bool IsDefending()
    {
        return defenseController != null &&
               defenseController.IsDefending;
    }

    // JumpState tarafından çağrılır.
    public void PlayJumpAnimation()
    {
        animationController.PlayJump();
    }

    // Respawn gibi durumlarda animator'ü temiz bir
    // hareket animasyonuna döndürür (Death'te takılı kalmasın).
    public void ResetAnimation()
    {
        animationController.ResetToLocomotion();
    }

    void OnDrawGizmosSelected()
    {
        if (movement == null)
            return;

        if (movement.groundCheck != null)
        {
            Gizmos.color = Color.green;

            Gizmos.DrawWireSphere(
                movement.groundCheck.position,
                movement.groundCheckRadius
            );
        }

        if (movement.wallCheck != null)
        {
            Gizmos.color = Color.yellow;

            Gizmos.DrawWireSphere(
                movement.wallCheck.position,
                0.05f
            );

            Gizmos.DrawLine(
                movement.wallCheck.position,
                movement.wallCheck.position +
                Vector3.right *
                movement.wallCheckDistance
            );

            Gizmos.DrawLine(
                movement.wallCheck.position,
                movement.wallCheck.position +
                Vector3.left *
                movement.wallCheckDistance
            );
        }
    }
}