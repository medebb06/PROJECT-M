using UnityEngine;
using Unity.Cinemachine;

public class PlayerController : MonoBehaviour
{
    [System.Serializable]
    public class ImpactSettings
    {
        public float minDistance = 2f;

        public float lightDistance = 4f;
        public float mediumDistance = 7f;
        public float heavyDistance = 10f;

        [Header("Shake Intensity")]
        public float lightIntensity = 0.25f;
        public float mediumIntensity = 0.6f;
        public float heavyIntensity = 1.2f;
    }

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

    // ---------------- STATE ----------------
    public PlayerStateMachine stateMachine;

    // ---------------- LANDING ----------------
    float highestY;
    bool inAir;
    bool wasGrounded;

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
        HandleInput();
        HandleTimers();
        HandleFacing();

        stateMachine.Update();
    }

    void FixedUpdate()
    {
        GroundCheck();

        ApplyBetterGravity();

        stateMachine.FixedUpdate();
    }

    // =========================================================
    // INPUT
    // =========================================================

    void HandleInput()
    {
        moveInput = Input.GetAxisRaw("Horizontal");

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
    }

    // =========================================================
    // GROUND CHECK
    // =========================================================

    void GroundCheck()
    {
        isGrounded = Physics2D.OverlapCircle(
            groundCheck.position,
            groundCheckRadius,
            groundMask
        );

        if (isGrounded)
        {
            coyoteCounter = coyoteTime;

            if (!wasGrounded)
            {
                SpawnDust();
                OnLand();

                inAir = false;
            }
        }
        else
        {
            coyoteCounter -= Time.fixedDeltaTime;

            if (!inAir)
            {
                inAir = true;
                highestY = transform.position.y;
            }

            if (transform.position.y > highestY)
            {
                highestY = transform.position.y;
            }
        }

        wasGrounded = isGrounded;
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
        // düşüş hızlandır
        if (rb.linearVelocity.y < 0f)
        {
            rb.linearVelocity += Vector2.up *
                Physics2D.gravity.y *
                (fallMultiplier - 1f) *
                Time.fixedDeltaTime;
        }

        // variable jump
        else if (rb.linearVelocity.y > 0f && !jumpHeld)
        {
            rb.linearVelocity += Vector2.up *
                Physics2D.gravity.y *
                (jumpCutMultiplier - 1f) *
                Time.fixedDeltaTime;
        }

        // max fall speed
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
        if (!impulseSource)
            return;

        float fallDistance = highestY - transform.position.y;

        if (fallDistance < impactSettings.minDistance)
            return;

        float intensity;

        if (fallDistance < impactSettings.lightDistance)
        {
            intensity = impactSettings.lightIntensity;
        }
        else if (fallDistance < impactSettings.mediumDistance)
        {
            intensity = impactSettings.mediumIntensity;
        }
        else
        {
            intensity = impactSettings.heavyIntensity;
        }

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