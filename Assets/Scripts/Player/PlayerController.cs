using UnityEngine;
using Unity.Cinemachine;

public class PlayerController : MonoBehaviour
{
    [Header("Refs")]
    public Rigidbody2D rb;
    public Transform groundCheck;
    public LayerMask groundMask;

    [Header("Visual")]
    public Transform modelPivot;

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

    [HideInInspector] public float dashCooldownTimer;
    [HideInInspector] public bool dashPressed;
    [HideInInspector] public bool isDashing;

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

    // ---------------- STATE ----------------
    public PlayerStateMachine stateMachine;

    bool wasGrounded;
    bool inAir;
    float highestY;

    // ---------------- IMPACT ----------------
    [System.Serializable]
    public class ImpactSettings
    {
        public float minDistance = 2f;
        public float lightDistance = 4f;
        public float mediumDistance = 7f;
        public float heavyDistance = 10f;

        public float lightIntensity = 0.25f;
        public float mediumIntensity = 0.6f;
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
        // cooldown
        if (dashCooldownTimer > 0)
            dashCooldownTimer -= Time.deltaTime;

        // INPUT
        moveInput = Input.GetAxisRaw("Horizontal");
        jumpHeld = Input.GetKey(KeyCode.Space);

        // dash single frame
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

        // ---------------- FALL TRACK ----------------
        if (!isGrounded)
        {
            if (!inAir)
            {
                inAir = true;
                highestY = transform.position.y;
            }

            if (transform.position.y > highestY)
                highestY = transform.position.y;
        }
        else
        {
            if (inAir && !wasGrounded)
            {
                HandleLanding(highestY - transform.position.y);
                inAir = false;
            }
        }

        wasGrounded = isGrounded;

        ApplyGravity();
        stateMachine.FixedUpdate();
    }

    // ---------------- LANDING ----------------
    void HandleLanding(float fallDistance)
    {
        if (impulseSource == null) return;

        if (fallDistance < impactSettings.minDistance)
            return;

        float intensity;

        if (fallDistance < impactSettings.lightDistance)
            intensity = impactSettings.lightIntensity;
        else if (fallDistance < impactSettings.mediumDistance)
            intensity = impactSettings.mediumIntensity;
        else
            intensity = impactSettings.heavyIntensity;

        impulseSource.GenerateImpulse(intensity);
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

    // ---------------- STATE HELPERS ----------------
    public float GetDashDirection()
    {
        return facingDir == 0 ? 1 : facingDir;
    }

    public bool IsInputLocked()
    {
        return isDashing;
    }
}