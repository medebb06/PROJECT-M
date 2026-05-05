using UnityEngine;
using Unity.Cinemachine;

public class PlayerController : MonoBehaviour
{
    [Header("Refs")]
    public Rigidbody2D rb;
    public Transform groundCheck;
    public LayerMask groundMask;

    public SquashStretch squashStretch;

    [Header("Visual")]
    public Transform modelPivot;

    [HideInInspector] public bool isDashing;

    [Header("Landing Shake")]
    public CinemachineImpulseSource impulseSource;

    // ---------------- MOVE ----------------
    [Header("Move")]
    public float moveSpeed = 7f;
    public float acceleration = 45f;
    public float deceleration = 60f;
    public float airControl = 0.6f;

    // ---------------- JUMP ----------------
    [Header("Jump")]
    public float jumpForce = 12f;

    // ---------------- DASH ----------------
    [Header("Dash")]
    public float dashDistance = 10f;
    public float dashTime = 0.15f;
    public float dashCooldown = 0.4f;
    public float dashCooldownTimer;
    public bool dashPressed;

    // ---------------- FEEL ----------------
    [Header("Physics Feel")]
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;

    // ---------------- GROUND ----------------
    [Header("Ground")]
    public float groundRadius = 0.15f;

    // ---------------- INPUT ----------------
    [Header("Runtime")]
    public float moveInput;
    public bool jumpHeld;

    public bool isGrounded;
    public float facingDir = 1f;

    float coyoteCounter;
    float jumpBufferCounter;

    public PlayerStateMachine stateMachine;

    // ---------------- FALL TRACK ----------------
    float fallStartY;
    bool trackingFall;
    bool wasGrounded;

    // ---------------- IMPACT SETTINGS ----------------
    [System.Serializable]
    public class ImpactSettings
    {
        public float minDistance = 1.5f;

        public float lightDistance = 3f;
        public float mediumDistance = 6f;
        public float heavyDistance = 9f;

        [Header("Intensity")]
        public float lightIntensity = 0.3f;
        public float mediumIntensity = 0.7f;
        public float heavyIntensity = 1.2f;
    }

    [Header("Impact Settings")]
    public ImpactSettings impactSettings;

    void Awake()
    {
        rb.freezeRotation = true;

        stateMachine = new PlayerStateMachine();
        stateMachine.Initialize(new GroundedState(this, stateMachine));

        if (impulseSource == null)
            impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    void Update()
    {
        if (dashCooldownTimer > 0)
            dashCooldownTimer -= Time.deltaTime;

        moveInput = Input.GetAxisRaw("Horizontal");

        if (Input.GetKeyDown(KeyCode.Space))
            jumpBufferCounter = 0.15f;

        jumpHeld = Input.GetKey(KeyCode.Space);
        dashPressed = Input.GetKeyDown(KeyCode.LeftShift);

        // flip
        if (moveInput != 0)
        {
            facingDir = Mathf.Sign(moveInput);

            if (modelPivot != null)
            {
                Vector3 scale = modelPivot.localScale;
                scale.x = Mathf.Abs(scale.x) * facingDir;
                modelPivot.localScale = scale;
            }
        }

        stateMachine.Update();
    }

    void FixedUpdate()
    {
        isGrounded = Physics2D.OverlapCircle(
            groundCheck.position,
            groundRadius,
            groundMask
        );

        // ---------------- FALL START ----------------
        if (!isGrounded)
        {
            if (!trackingFall)
            {
                trackingFall = true;
                fallStartY = transform.position.y;
            }
        }

        // coyote
        if (isGrounded)
            coyoteCounter = 0.12f;
        else
            coyoteCounter -= Time.fixedDeltaTime;

        ApplyGravity();
        stateMachine.FixedUpdate();

        // ---------------- LAND DETECT ----------------
        if (!wasGrounded && isGrounded)
        {
            OnLand();
            trackingFall = false;
        }

        wasGrounded = isGrounded;
    }

    // ---------------- LANDING ----------------
    void OnLand()
    {
        if (impulseSource == null) return;

        float fallDistance = fallStartY - transform.position.y;

        if (fallDistance < impactSettings.minDistance)
            return;

        float intensity;

        if (fallDistance < impactSettings.lightDistance)
        {
            intensity = impactSettings.lightIntensity;
            Debug.Log("LIGHT LAND");
        }
        else if (fallDistance < impactSettings.mediumDistance)
        {
            intensity = impactSettings.mediumIntensity;
            Debug.Log("MEDIUM LAND");
        }
        else
        {
            intensity = impactSettings.heavyIntensity;
            Debug.Log("HEAVY LAND");
        }

        impulseSource.GenerateImpulse(intensity);

        Debug.Log("FALL DIST: " + fallDistance);
    }

    // ---------------- MOVEMENT ----------------
    public void ApplyMovement(float control)
    {
        if (isDashing) return;

        float targetSpeed = moveInput * moveSpeed;
        float speedDiff = targetSpeed - rb.linearVelocity.x;

        float accelRate = isGrounded
            ? (Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration)
            : acceleration * airControl;

        rb.linearVelocity = new Vector2(
            rb.linearVelocity.x + speedDiff * accelRate * Time.fixedDeltaTime * control,
            rb.linearVelocity.y
        );
    }

    // ---------------- GRAVITY ----------------
    void ApplyGravity()
    {
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0 && !jumpHeld)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }
    }

    // ---------------- HELPERS ----------------
    public bool CanJump()
    {
        return jumpBufferCounter > 0f && coyoteCounter > 0f;
    }

    public void ConsumeJump()
    {
        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
    }

    public float GetDashDirection()
    {
        return facingDir == 0 ? 1 : facingDir;
    }

    public bool IsInputLocked()
    {
        return isDashing;
    }
}