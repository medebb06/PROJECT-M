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

        [Header("Jump Feel")]
        public float jumpBufferTime = 0.15f;
        public float coyoteTime = 0.12f;
        public float jumpCutMultiplier = 2f;
        public float jumpCutVelocityMultiplier = 0.75f;
        public float fallMultiplier = 2.5f;
        public float maxFallSpeed = 20f;

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
    bool wasAirborne;

    [HideInInspector] public bool slamGroundLock;
    [HideInInspector] public float slamLockTimer;
    public float slamLockDuration = 0.12f;

    [Header("Hit Freeze")]
    public float slamFreezeTime = 0.04f;

    public ImpactSettings impactSettings;

    [Header("Refs")]
    public Rigidbody2D rb;
    public Collider2D col;
    public Transform modelPivot;

    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Header("Defense")]
    [SerializeField] private PlayerDefenseController defenseController;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.15f;
    public LayerMask groundMask;

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

    [HideInInspector] public bool canControl = true;
    [HideInInspector] public bool isDashing;
    [HideInInspector] public bool isInvincible;
    [HideInInspector] public bool isAttackLocked;

    public bool canAttack =>
        !isDashing &&
        !isAttackLocked;

    [Header("Movement")]
    public float moveSpeed = 7f;
    public float acceleration = 45f;
    public float deceleration = 60f;
    public float airControl = 0.6f;

    [Header("Run Audio")]
    public float stepTimer;

    float nextStepTime;
    bool wasRunning;

    public bool jumpConsumed;

    float airTime;
    float maxAirHeight;
    bool wasGrounded;

    [Header("Jump")]
    public float jumpForce = 12f;

    [Header("Jump Feel")]
    public float jumpBufferTime = 0.15f;
    public float coyoteTime = 0.12f;
    public float jumpCutMultiplier = 2f;
    public float jumpCutVelocityMultiplier = 0.3f;
    public float fallMultiplier = 2.5f;
    public float maxFallSpeed = 20f;

    [HideInInspector] public float jumpBufferCounter;
    [HideInInspector] public float coyoteCounter;

    [Header("Dash")]
    public float dashDistance = 10f;
    public float dashTime = 0.15f;
    public float dashCooldown = 0.4f;

    [HideInInspector] public float dashCooldownTimer;
    [HideInInspector] public bool dashPressed;

    [Header("Dash FX")]
    public GameObject afterImagePrefab;
    public float afterImageSpacing = 0.05f;

    [HideInInspector] public float afterImageTimer;

    [Header("Runtime")]
    public float moveInput;
    public bool jumpHeld;
    public bool isGrounded;
    public float facingDir = 1f;

    [HideInInspector] public bool slamPressed;
    public float verticalInput;

    public PlayerStateMachine stateMachine;

    float highestY;
    bool inAir;

    [Header("Wall")]
    public Transform wallCheck;
    public float wallCheckDistance = 0.3f;
    public LayerMask wallMask;

    [Header("Wall Slide")]
    public float wallSlideSpeed = 2f;

    [Header("Wall Jump")]
    public float wallJumpForceX = 9f;
    public float wallJumpForceY = 12f;

    // İlk anda yatay momentumun tamamen korunacağı süre.
    public float wallJumpControlLock = 0.08f;

    // Space'e biraz erken basıldığında input'un tutulacağı süre.
    public float wallJumpBufferTime = 0.12f;

    // Lock bittikten sonra air control'ün yumuşak şekilde geri gelme süresi.
    public float wallJumpControlBlendTime = 0.18f;

    // Control geri gelirken hareketin ne kadar güçlü olacağı.
    public float wallJumpControlAccelerationMultiplier = 1f;

    [HideInInspector] public float wallJumpBufferCounter;

    [HideInInspector] public int wallDirection;
    [HideInInspector] public bool isWallSliding;

    // 0 = wall jump kilidi yok
    // 1 = sağdaki duvardan son wall jump yapıldı
    // -1 = soldaki duvardan son wall jump yapıldı
    [HideInInspector] public int wallJumpBlockedDirection;

    [HideInInspector] public bool inputLocked;
    [HideInInspector] public float inputLockTimer;
    public float inputLockDuration = 0.12f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        if (rb == null)
        {
            Debug.LogError(
                "PLAYER CONTROLLER: Rigidbody2D bulunamadı!"
            );
        }

        if (col == null)
        {
            Debug.LogError(
                "PLAYER CONTROLLER: Collider2D bulunamadı!"
            );
        }

        if (rb != null)
        {
            Debug.Log(
                "START GRAVITY: " +
                rb.gravityScale
            );

            rb.freezeRotation = true;
        }

        if (!audioPlayer)
            audioPlayer = GetComponent<PlayerAudio>();

        if (!impulseSource)
            impulseSource =
                GetComponent<CinemachineImpulseSource>();

        if (!defenseController)
            defenseController =
                GetComponent<PlayerDefenseController>();

        // Animator ModelPivot üzerinde.
        if (animator == null && modelPivot != null)
        {
            animator =
                modelPivot.GetComponent<Animator>();
        }

        if (animator == null)
        {
            Debug.LogWarning(
                "PLAYER CONTROLLER: ModelPivot üzerinde Animator bulunamadı!"
            );
        }

        stateMachine = new PlayerStateMachine();

        stateMachine.Initialize(
            new GroundedState(
                this,
                stateMachine
            )
        );
    }

    void Update()
    {
        if (rb != null &&
            rb.gravityScale != 3.5f)
        {
            Debug.LogWarning(
                "GRAVITY OVERRIDDEN: " +
                rb.gravityScale
            );
        }

        HandleRunAudio();
        HandleJump();
        HandleInput();
        HandleTimers();
        HandleFacing();
        HandleAnimator();

        stateMachine.Update();
    }

    void HandleAnimator()
    {
        if (animator == null)
            return;

        float speed =
            Mathf.Abs(moveInput);

        animator.SetFloat(
            "Speed",
            speed
        );

        animator.SetBool(
            "Grounded",
            isGrounded
        );

        animator.SetFloat(
            "VerticalVelocity",
            rb != null
                ? rb.linearVelocity.y
                : 0f
        );

        animator.SetBool(
            "IsDashing",
            isDashing
        );

        Debug.Log(
            "ANIMATOR → Dash: " +
            isDashing
        );
    }

    void HandleRunAudio()
    {
        bool isDefending =
            defenseController != null &&
            defenseController.IsDefending;

        bool isRunning =
            isGrounded &&
            !isDefending &&
            Mathf.Abs(moveInput) > 0.1f;

        if (!isRunning)
        {
            if (audioPlayer != null)
                audioPlayer.StopRun();

            stepTimer = 0f;
            return;
        }

        stepTimer -= Time.deltaTime;

        float speed =
            Mathf.Abs(rb.linearVelocity.x);

        float speedFactor =
            Mathf.InverseLerp(
                0f,
                moveSpeed,
                speed
            );

        if (stepTimer <= 0f)
        {
            if (audioPlayer != null)
                audioPlayer.StartRun(
                    speedFactor
                );

            stepTimer = Mathf.Lerp(
                0.45f,
                0.15f,
                speedFactor
            );
        }
    }

    void HandleJump()
    {
        if (!canControl)
            return;

        if (defenseController != null &&
            defenseController.IsDefending)
        {
            return;
        }

        if (isWallSliding)
            return;

        if (!isGrounded &&
            IsTouchingWall())
        {
            return;
        }

        if (jumpBufferCounter <= 0f)
            return;

        if (coyoteCounter <= 0f)
            return;

        if (jumpConsumed)
            return;

        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
        jumpConsumed = true;


        // Eğer Land trigger'ı bekliyorsa temizle.
        if (animator != null)
        {
            animator.ResetTrigger("Land");

            animator.CrossFadeInFixedTime(
                "Jump",
                0.03f,
                0,
                0f
            );
        }

        stateMachine.ChangeState(
            new JumpState(
                this,
                stateMachine
            )
        );
    }
    public void PlayDeathAnimation() { if (animator == null) return; animator.ResetTrigger("Land"); animator.CrossFadeInFixedTime("Death", 0.03f, 0, 0f); }
    void FixedUpdate()
    {
        GroundCheck();

        if (!isGrounded)
        {
            wasAirborne = true;

            airTime += Time.fixedDeltaTime;

            if (transform.position.y > maxAirHeight)
                maxAirHeight =
                    transform.position.y;
        }

        ApplyBetterGravity();

        stateMachine.FixedUpdate();
    }

    public void ApplyMovement(float control)
    {
        if (!canControl)
            return;

        if (inputLocked)
            return;

        float targetSpeed =
            moveInput * moveSpeed;

        float accelerationRate;

        if (Mathf.Abs(moveInput) > 0.01f)
            accelerationRate = acceleration;
        else
            accelerationRate = deceleration;

        accelerationRate *= control;

        float newVelocityX =
            Mathf.MoveTowards(
                rb.linearVelocity.x,
                targetSpeed,
                accelerationRate *
                Time.fixedDeltaTime
            );

        rb.linearVelocity =
            new Vector2(
                newVelocityX,
                rb.linearVelocity.y
            );
    }

    public void SetVelocity(
        Vector2 velocity
    )
    {
        if (rb == null)
            return;

        rb.linearVelocity = velocity;
    }

    void ApplyBetterGravity()
    {
        if (!canControl)
            return;

        if (slamGroundLock)
            return;

        if (rb.linearVelocity.y < 0f)
        {
            rb.linearVelocity +=
                Vector2.up *
                Physics2D.gravity.y *
                (fallMultiplier - 1f) *
                Time.fixedDeltaTime;
        }
        else if (
            rb.linearVelocity.y > 0f &&
            !jumpHeld
        )
        {
            rb.linearVelocity =
                new Vector2(
                    rb.linearVelocity.x,
                    rb.linearVelocity.y *
                    jumpCutVelocityMultiplier
                );
        }

        if (rb.linearVelocity.y < -maxFallSpeed)
        {
            rb.linearVelocity =
                new Vector2(
                    rb.linearVelocity.x,
                    -maxFallSpeed
                );
        }
    }

    void HandleInput()
    {
        if (inputLocked || !canControl)
        {
            moveInput = 0f;
            verticalInput = 0f;
            jumpHeld = false;
            dashPressed = false;
            return;
        }

        moveInput =
            Input.GetAxisRaw("Horizontal");

        verticalInput =
            Input.GetAxisRaw("Vertical");

        jumpHeld =
            Input.GetKey(KeyCode.Space);

        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpBufferCounter =
                jumpBufferTime;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            wallJumpBufferCounter =
                wallJumpBufferTime;
        }

        dashPressed =
            Input.GetKeyDown(
                KeyCode.LeftShift
            );

        if (dashCooldownTimer > 0f)
            dashCooldownTimer -=
                Time.deltaTime;
    }

    void HandleTimers()
    {
        jumpBufferCounter -=
            Time.deltaTime;

        jumpBufferCounter =
            Mathf.Max(
                0f,
                jumpBufferCounter
            );

        wallJumpBufferCounter -=
            Time.deltaTime;

        wallJumpBufferCounter =
            Mathf.Max(
                0f,
                wallJumpBufferCounter
            );

        coyoteCounter =
            Mathf.Max(
                0f,
                coyoteCounter
            );

        if (slamGroundLock)
        {
            slamLockTimer -=
                Time.deltaTime;

            if (slamLockTimer <= 0f)
                slamGroundLock = false;

            if (inputLocked)
            {
                inputLockTimer -=
                    Time.deltaTime;

                if (inputLockTimer <= 0f)
                    inputLocked = false;
            }
        }
    }

    void GroundCheck()
    {
        if (slamGroundLock)
        {
            isGrounded = true;
            return;
        }

        bool groundedNow =
            Physics2D.OverlapCircle(
                groundCheck.position,
                groundCheckRadius,
                groundMask
            );

        // Havadan yere gerçekten geçtik.
        if (groundedNow && !wasGrounded)
        {
            if (wasAirborne)
            {
                OnLand();
                wasAirborne = false;
            }

            airTime = 0f;

            maxAirHeight =
                transform.position.y;
        }

        // Yerden havaya çıktık.
        if (!groundedNow && wasGrounded)
        {
            coyoteCounter =
                coyoteTime;

            wasAirborne = true;
        }

        wasGrounded = groundedNow;
        isGrounded = groundedNow;
    }

    public bool IsTouchingWall()
    {
        if (wallCheck == null)
        {
            wallDirection = 0;
            return false;
        }

        Vector2 origin =
            wallCheck.position;

        RaycastHit2D rightHit =
            Physics2D.Raycast(
                origin,
                Vector2.right,
                wallCheckDistance,
                wallMask
            );

        RaycastHit2D leftHit =
            Physics2D.Raycast(
                origin,
                Vector2.left,
                wallCheckDistance,
                wallMask
            );

        if (rightHit.collider != null)
        {
            wallDirection = 1;
            return true;
        }

        if (leftHit.collider != null)
        {
            wallDirection = -1;
            return true;
        }

        wallDirection = 0;
        return false;
    }

    void OnLand()
    {
        

        jumpConsumed = false;
        wallJumpBlockedDirection = 0;

        if (animator != null)
        {
            animator.SetTrigger("Land");
        }

        float fallDistance =
            maxAirHeight -
            transform.position.y;

        if (fallDistance >=
            impactSettings.minDistance)
        {
            TriggerLandingShake(
                fallDistance
            );
        }

        void TriggerLandingShake(
            float distance
        )
        {
            if (impulseSource == null)
                return;

            float intensity = 0f;

            if (distance >=
                impactSettings.heavyThreshold)
            {
                intensity =
                    impactSettings.heavyIntensity;
            }
            else if (
                distance >=
                impactSettings.mediumThreshold
            )
            {
                intensity =
                    impactSettings.mediumIntensity;
            }
            else if (
                distance >=
                impactSettings.lightThreshold
            )
            {
                intensity =
                    impactSettings.lightIntensity;
            }
            else
            {
                return;
            }

            float normalizedDistance =
                Mathf.InverseLerp(
                    impactSettings.minDistance,
                    impactSettings.maxFallDistance,
                    distance
                );

            float curveValue =
                impactSettings.shakeCurve.Evaluate(
                    normalizedDistance
                );

            intensity *= curveValue;

            impulseSource.GenerateImpulse(
                intensity
            );
        }

        SpawnDust();

        maxAirHeight =
            transform.position.y;
    }

    public void SpawnDust()
    {
        if (!dustPrefab)
            return;

        Vector3 pos =
            footPoint
                ? footPoint.position
                : transform.position;

        Instantiate(
            dustPrefab,
            pos,
            Quaternion.identity
        );
    }

    void HandleFacing()
    {
        if (defenseController != null &&
            defenseController.IsDefending)
        {
            return;
        }

        if (moveInput == 0)
            return;

        facingDir =
            Mathf.Sign(moveInput);

        Vector3 s =
            modelPivot.localScale;

        s.x =
            Mathf.Abs(s.x) *
            facingDir;

        modelPivot.localScale = s;
    }

    public System.Collections.IEnumerator FreezeFrame(
        float duration
    )
    {
        Time.timeScale = 0f;

        yield return new WaitForSecondsRealtime(
            duration
        );

        Time.timeScale = 1f;
    }

    public bool IsDefending()
    {
        return defenseController != null &&
               defenseController.IsDefending;
    }

    public bool IsGrounded()
    {
        return isGrounded;
    }

    public float GetDashSpeedFactor()
    {
        return Mathf.Clamp01(
            Mathf.Abs(dashDistance) / 10f
        );
    }

    public float GetDashDirection()
    {
        return facingDir == 0
            ? 1f
            : facingDir;
    }

    public void PlayAttackAnimation(int attackStep)
    {
        if (animator == null)
            return;

        string stateName;

        switch (attackStep)
        {
            case 1:
                stateName = "Attack1";
                break;

            case 2:
                stateName = "Attack2";
                break;

            case 3:
                stateName = "Attack3";
                break;

            case 4:
                stateName = "Attack4";
                break;

            default:
                return;
        }

        animator.CrossFadeInFixedTime(
            stateName,
            0.035f,
            0,
            0f
        );
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck)
        {
            Gizmos.color = Color.green;

            Gizmos.DrawWireSphere(
                groundCheck.position,
                groundCheckRadius
            );
        }

        if (wallCheck)
        {
            Gizmos.color = Color.yellow;

            Gizmos.DrawWireSphere(
                wallCheck.position,
                0.05f
            );

            Gizmos.DrawLine(
                wallCheck.position,
                wallCheck.position +
                Vector3.right *
                wallCheckDistance
            );

            Gizmos.DrawLine(
                wallCheck.position,
                wallCheck.position +
                Vector3.left *
                wallCheckDistance
            );
        }
    }
}