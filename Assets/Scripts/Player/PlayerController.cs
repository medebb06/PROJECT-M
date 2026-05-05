using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Refs")]
    public Rigidbody2D rb;

    [Header("Movement")]
    public float moveSpeed = 7f;
    public float jumpForce = 12f;
    public float dashForce = 18f;
    [Header("Movement Feel")]
    public float acceleration = 40f;
    public float deceleration = 50f;
    public float airControl = 0.6f;

    [Header("Ground")]
    public Transform groundCheck;
    public float groundRadius = 0.15f;
    public LayerMask groundLayer;

    [Header("Runtime")]
    public float moveInput;
    public bool jumpPressed;
    public bool dashPressed;
    public bool isGrounded;

    public float facingDir = 1f;

    public PlayerStateMachine stateMachine;

    void Awake()
    {
        stateMachine = new PlayerStateMachine();
        stateMachine.Initialize(new GroundedState(this, stateMachine));
    }

    void Update()
    {
        moveInput = Input.GetAxisRaw("Horizontal");

        // 👇 FACING MEMORY (KRİTİK FIX)
        if (moveInput != 0)
            facingDir = Mathf.Sign(moveInput);

        if (Input.GetKeyDown(KeyCode.Space))
            jumpPressed = true;

        if (Input.GetKeyDown(KeyCode.LeftShift))
            dashPressed = true;

        stateMachine.Update();

        jumpPressed = false;
        dashPressed = false;
    }

    void FixedUpdate()
    {
        isGrounded = Physics2D.OverlapCircle(
            groundCheck.position,
            groundRadius,
            groundLayer
        );

        stateMachine.FixedUpdate();
    }

    public void ApplyMovement()
    {
        float targetSpeed = moveInput * moveSpeed;

        float speedDiff = targetSpeed - rb.linearVelocity.x;

        float accelRate;

        // 👇 ground vs air ayrımı
        if (isGrounded)
        {
            accelRate = Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration;
        }
        else
        {
            accelRate = acceleration * airControl;
        }

        float movement = speedDiff * accelRate;

        rb.linearVelocity = new Vector2(
            rb.linearVelocity.x + movement * Time.fixedDeltaTime,
            rb.linearVelocity.y
        );
    }
}