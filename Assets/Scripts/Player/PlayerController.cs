using UnityEngine;
using Unity.Cinemachine;


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
        public AnimationCurve shakeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        public float maxFallDistance = 10f;

        public float minDistance = 2f;
    
    }

    [HideInInspector] public bool slamGroundLock;
    [HideInInspector] public float slamLockTimer;
    public float slamLockDuration = 0.12f;

    [Header("Hit Freeze")]
    public float slamFreezeTime = 0.04f;

    public ImpactSettings impactSettings;

    // ---------------- REFERENCES ----------------
    [Header("Refs")]
    public Rigidbody2D rb;
    public Collider2D col;

    public Transform modelPivot;

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

    // ---------------- CONTROL ----------------
    [HideInInspector] public bool canControl = true;
    [HideInInspector] public bool isDashing;
    [HideInInspector] public bool isInvincible;
    [HideInInspector] public bool isAttackLocked;

    public bool canAttack => !isDashing && !isAttackLocked;


    // ---------------- MOVEMENT ----------------
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

    // ---------------- JUMP ----------------
    [Header("Jump")]
    public float jumpForce = 12f;

    [Header("Jump Feel")]
    public float jumpBufferTime = 0.15f;
    public float coyoteTime = 0.12f;

    [Tooltip("Jump bırakılınca ne kadar hızlı kesilsin")]
    public float jumpCutMultiplier = 2f;

    [Tooltip("Düşüş ne kadar ağırlaşsın")]
    public float fallMultiplier = 2.5f;

    [Tooltip("Maksimum düşüş hızı")]
    public float maxFallSpeed = 20f;

    [HideInInspector] public float jumpBufferCounter;
    [HideInInspector] public float coyoteCounter;

    // ---------------- DASH ----------------
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

    // ---------------- INPUT ----------------
    [Header("Runtime")]
    public float moveInput;
    public bool jumpHeld;
    public bool isGrounded;
    public float facingDir = 1f;
    [HideInInspector] public bool slamPressed;
    public float verticalInput;


    // ---------------- STATE ----------------
    public PlayerStateMachine stateMachine;

    // ---------------- LANDING ----------------
    float highestY;
    bool inAir;

    [HideInInspector] public bool inputLocked;
    [HideInInspector] public float inputLockTimer;
    public float inputLockDuration = 0.12f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (!audioPlayer)
            audioPlayer = GetComponent<PlayerAudio>();

        if (!impulseSource)
            impulseSource = GetComponent<CinemachineImpulseSource>();

        rb.freezeRotation = true;

        stateMachine = new PlayerStateMachine();
        stateMachine.Initialize(new GroundedState(this, stateMachine));
    }

    void Update()
    {

        HandleRunAudio();
        HandleJump();
        HandleInput();
        HandleTimers();
        HandleFacing();

        stateMachine.Update();
       
    }
   

    void HandleRunAudio()
    {
        bool isRunning = isGrounded && Mathf.Abs(moveInput) > 0.1f;

        if (!isRunning)
        {
            audioPlayer.StopRun();
            stepTimer = 0f;
            return;
        }

        stepTimer -= Time.deltaTime;

        float speed = Mathf.Abs(rb.linearVelocity.x);
        float speedFactor = Mathf.InverseLerp(0f, moveSpeed, speed);

        if (stepTimer <= 0f)
        {
            audioPlayer.StartRun(speedFactor);
            stepTimer = Mathf.Lerp(0.45f, 0.15f, speedFactor);
        }
    }
    void HandleJump()
    {

        if (!canControl) return;

        if (jumpBufferCounter <= 0f) return;
        if (coyoteCounter <= 0f) return;
        if (jumpConsumed) return;

        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
        jumpConsumed = true;

        stateMachine.ChangeState(new JumpState(this, stateMachine));
    }

    void FixedUpdate()
    {
        GroundCheck();

        if (!isGrounded)
        {
            airTime += Time.fixedDeltaTime;

            if (transform.position.y > maxAirHeight)
                maxAirHeight = transform.position.y;
        }

        ApplyBetterGravity();
        stateMachine.FixedUpdate();
    }

    // =========================================================
    // INPUT
    // =========================================================

    void HandleInput()
    {
        if (inputLocked)
        {
            moveInput = 0f;
            verticalInput = 0f;
            jumpHeld = false;
            dashPressed = false;
            return;
        }
        moveInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        jumpHeld = Input.GetKey(KeyCode.Space);

        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpBufferCounter = jumpBufferTime;
        }

        dashPressed = Input.GetKeyDown(KeyCode.LeftShift);

        if (dashCooldownTimer > 0f)
        {
            dashCooldownTimer -= Time.deltaTime;
        }
    }

    // =========================================================
    // TIMERS
    // =========================================================

    void HandleTimers()
    {
        jumpBufferCounter -= Time.deltaTime;
        jumpBufferCounter = Mathf.Max(0f, jumpBufferCounter);

        coyoteCounter = Mathf.Max(0f, coyoteCounter);

        // ---------------- SLAM LOCK ----------------
        if (slamGroundLock)
        {
            slamLockTimer -= Time.deltaTime;

            if (slamLockTimer <= 0f)
                slamGroundLock = false;
            if (inputLocked)
            {
                inputLockTimer -= Time.deltaTime;

                if (inputLockTimer <= 0f)
                    inputLocked = false;
            }
        }
    }


    // =========================================================
    // GROUND CHECK
    // =========================================================

    void GroundCheck()
    {
        // 🔥 slam sırasında physics flicker IGNORE
        if (slamGroundLock)
        {
            isGrounded = true;
            return;
        }

        bool groundedNow = Physics2D.OverlapCircle(
            groundCheck.position,
            groundCheckRadius,
            groundMask
        );

        if (groundedNow && !wasGrounded)
        {
            OnLand();
            airTime = 0f;
            maxAirHeight = transform.position.y;
        }

        if (!groundedNow && wasGrounded)
        {
            coyoteCounter = coyoteTime;
        }

        wasGrounded = groundedNow;
        isGrounded = groundedNow;
    }

    // =========================================================
    // MOVEMENT
    // =========================================================

    public void ApplyMovement(float control)
    {
        if (!canControl)
            return;

        float targetSpeed = moveInput * moveSpeed;

        float speedDiff = targetSpeed - rb.linearVelocity.x;

        float accelRate;

        if (isGrounded)
        {
            accelRate = Mathf.Abs(targetSpeed) > 0.01f
                ? acceleration
                : deceleration;
        }
        else
        {
            accelRate = acceleration * airControl;
        }

        float movement = speedDiff * accelRate * Time.fixedDeltaTime * control;

        rb.linearVelocity = new Vector2(
            rb.linearVelocity.x + movement,
            rb.linearVelocity.y
        );
    }

    public void SetVelocity(Vector2 vel)
    {
        rb.linearVelocity = vel;
    }

    // =========================================================
    // BETTER GRAVITY
    // =========================================================

    void ApplyBetterGravity()
    {
        // 🔥 slam sırasında gravity kapalı
        if (slamGroundLock)
            return;

        if (rb.linearVelocity.y < 0f)
        {
            rb.linearVelocity += Vector2.up *
                Physics2D.gravity.y *
                (fallMultiplier - 1f) *
                Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0f && !jumpHeld)
        {
            rb.linearVelocity += Vector2.up *
                Physics2D.gravity.y *
                (jumpCutMultiplier - 1f) *
                Time.fixedDeltaTime;
        }

        if (rb.linearVelocity.y < -maxFallSpeed)
        {
            rb.linearVelocity = new Vector2(
                rb.linearVelocity.x,
                -maxFallSpeed
            );
        }
    }

    // =========================================================
    // LANDING
    // =========================================================

    void OnLand()
    {
        if (slamGroundLock)
            return;

        if (!impulseSource)
            return;

        float fallDistance = maxAirHeight - transform.position.y;

        if (airTime < 0.15f)
            return;

        if (fallDistance < impactSettings.minDistance)
            return;

        float intensity = impactSettings.shakeCurve.Evaluate(
            Mathf.Clamp01(fallDistance / impactSettings.maxFallDistance)
        );

        impulseSource.GenerateImpulse(intensity);
    }

    // =========================================================
    // VFX
    // =========================================================

    public void SpawnDust()
    {
        if (!dustPrefab)
            return;

        Vector3 pos = footPoint
            ? footPoint.position
            : transform.position;

        Instantiate(dustPrefab, pos, Quaternion.identity);
    }

    // =========================================================
    // FACE DIRECTION
    // =========================================================

    void HandleFacing()
    {
        if (moveInput == 0)
            return;

        facingDir = Mathf.Sign(moveInput);

        Vector3 s = modelPivot.localScale;

        s.x = Mathf.Abs(s.x) * facingDir;

        modelPivot.localScale = s;
    }
    public System.Collections.IEnumerator FreezeFrame(float duration)
    {
        Time.timeScale = 0f;

        yield return new WaitForSecondsRealtime(duration);

        Time.timeScale = 1f;
    }
    // =========================================================
    // HELPERS
    // =========================================================

    public bool IsGrounded()
    {
        return isGrounded;
    }

    public float GetDashSpeedFactor()
    {
        // dash ne kadar “sert” hissedilsin
        return Mathf.Clamp01(Mathf.Abs(dashDistance) / 10f);
    }
    public float GetDashDirection()
    {
        return facingDir == 0 ? 1 : facingDir;
    }

    // =========================================================
    // DEBUG
    // =========================================================

    void OnDrawGizmosSelected()
    {
        if (!groundCheck)
            return;

        Gizmos.color = Color.green;

        Gizmos.DrawWireSphere(
            groundCheck.position,
            groundCheckRadius
        );
    }
}