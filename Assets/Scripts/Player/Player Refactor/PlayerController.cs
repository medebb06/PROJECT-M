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

    [Header("Dash FX")]
    public GameObject afterImagePrefab;
    public float afterImageSpacing = 0.05f;

    [Header("Run Audio")]
    public float stepTimer;

    [Header("Input Lock")]
    public float inputLockDuration = 0.12f;

    [Header("Ground Slam")]
    public float slamLockDuration = 0.12f;

    [Header("Hit Freeze")]
    public float slamFreezeTime = 0.04f;

    [HideInInspector] public bool canControl = true;
    [HideInInspector] public bool isDashing;
    [HideInInspector] public bool isInvincible;
    [HideInInspector] public bool isAttackLocked;
    [HideInInspector] public bool slamGroundLock;
    [HideInInspector] public float slamLockTimer;
    [HideInInspector] public float dashCooldownTimer;
    [HideInInspector] public bool dashPressed;
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
        !isDashing &&
        !isAttackLocked;

    public PlayerStateMachine stateMachine;

    private PlayerMovement movement;
    private PlayerInputHandler inputHandler;
    private PlayerAnimationController animationController;
    private PlayerFeedback feedback;

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

    private void HandleJump()
    {
        if (!canControl)
            return;

        if (defenseController != null &&
            defenseController.IsDefending)
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

        movement.jumpBufferCounter = 0f;
        movement.coyoteCounter = 0f;

        jumpConsumed = true;

        animationController.PlayJump();

        if (audioPlayer != null)
        {
            float jumpStrength =
                Mathf.Clamp01(
                    movement.jumpForce / 12f
                );

            audioPlayer.PlayJump(jumpStrength);
        }

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
            feedback.SpawnDust();
        }

        movement.ResetAirData();
    }

    public void SpawnDust()
    {
        feedback.SpawnDust();
    }


    public float GetDashSpeedFactor()
    {
        return Mathf.Clamp01(
            Mathf.Abs(dashDistance) / 10f
        );
    }

    public float GetDashDirection()
    {
        return facingDir == 0f
            ? 1f
            : facingDir;
    }

    public void ApplyPostureBreak(Vector2 hitDirection)
    {
        if (rb == null)
            return;

        float direction =
            Mathf.Sign(hitDirection.x);

        if (direction == 0f)
            direction = 1f;

        rb.linearVelocity =
            new Vector2(
                -direction * 2.5f,
                rb.linearVelocity.y
            );

        inputLocked = true;
        inputLockTimer = 0.10f;

        moveInput = 0f;
        jumpHeld = false;
        dashPressed = false;

        Debug.Log(
            "PLAYER POSTURE BREAK → INPUT LOCK + KNOCKBACK"
        );
    }

    public void PlayAttackAnimation(int attackStep)
    {
        animationController.PlayAttackAnimation(attackStep);
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

    public System.Collections.IEnumerator FreezeFrame(
        float duration)
    {
        Time.timeScale = 0f;

        yield return new WaitForSecondsRealtime(duration);

        Time.timeScale = 1f;
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