using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Refs")]
    public Rigidbody2D rb;
    public Transform groundCheck;
    public LayerMask groundLayer;
   

    [HideInInspector] public bool isDashing;

    [Header("Move")]
    public float moveSpeed = 7f;
    public float acceleration = 45f;
    public float deceleration = 60f;
    public float airControl = 0.6f;

    [Header("Jump")]
    public float jumpForce = 12f;

    [Header("Dash")]
    public float dashForce = 18f;
    public float dashDuration = 0.15f;
    [Header("Dash Feel")]
    public float dashDistance = 10f;
    public float dashTime = 0.15f;
    public float dashSpeedMultiplier = 0.7f;
    public float dashCooldown = 0.4f;
    public float dashCooldownTimer;

    [Header("Feel")]
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;

    [Header("Ground")]
    public float groundRadius = 0.15f;
    public LayerMask groundMask;

    [Header("Coyote / Buffer")]
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.15f;

    [Header("Runtime")]
    public float moveInput;
    public bool jumpPressed;
    public bool jumpHeld;
    public bool dashPressed;
    public bool isGrounded;
    public float facingDir = 1f;

    float coyoteCounter;
    float jumpBufferCounter;

    public PlayerStateMachine stateMachine;

    void Awake()
    {
        stateMachine = new PlayerStateMachine();
        stateMachine.Initialize(new GroundedState(this, stateMachine));
    }

    void Update()
    {
        if (dashCooldownTimer > 0)
            dashCooldownTimer -= Time.deltaTime;
        moveInput = Input.GetAxisRaw("Horizontal");

        if (moveInput != 0)
            facingDir = Mathf.Sign(moveInput);

        if (Input.GetKeyDown(KeyCode.Space))
            jumpBufferCounter = jumpBufferTime;

        jumpHeld = Input.GetKey(KeyCode.Space);

        if (Input.GetKeyDown(KeyCode.LeftShift))
            dashPressed = true;

        stateMachine.Update();

        jumpBufferCounter -= Time.deltaTime;
        dashPressed = false;
    }

    void FixedUpdate()
    {
        // GROUND CHECK
        isGrounded = Physics2D.OverlapCircle(
            groundCheck.position,
            groundRadius,
            groundMask
        );

        // COYOTE
        if (isGrounded)
            coyoteCounter = coyoteTime;
        else
            coyoteCounter -= Time.fixedDeltaTime;

        // 🔥 TEK MERKEZ GRAVITY SYSTEM (BUG FIX HERE)
        ApplyGravity();

        stateMachine.FixedUpdate();
    }

    // ---------------- MOVEMENT ----------------

    public void ApplyMovement(float control = 1f)
    {
        if (isDashing) return; // 🔥 KRİTİK FIX

        float targetSpeed = moveInput * moveSpeed;
        float speedDiff = targetSpeed - rb.linearVelocity.x;

        float accelRate = isGrounded
            ? (Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration)
            : acceleration * airControl;

        float movement = speedDiff * accelRate;

        rb.linearVelocity = new Vector2(
            rb.linearVelocity.x + movement * Time.fixedDeltaTime,
            rb.linearVelocity.y
        );
    }

    // ---------------- GRAVITY (FIXED CORE) ----------------

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

    // ---------------- JUMP SYSTEM ----------------

    public bool CanJump()
    {
        return jumpBufferCounter > 0f && coyoteCounter > 0f;
    }

    public void ConsumeJump()
    {
        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
    }

    // ---------------- DASH ----------------

    public float GetDashDirection()
    {
        return facingDir == 0 ? 1 : facingDir;
    }
}