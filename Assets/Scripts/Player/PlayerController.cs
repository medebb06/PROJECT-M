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

    [Header("Refs")]
    public Rigidbody2D rb;
    public Transform groundCheck;
    public LayerMask groundMask;
    public Transform modelPivot;

    [Header("VFX")]
    public GameObject dustPrefab;
    public Transform footPoint;

    [Header("Camera Shake")]
    public CinemachineImpulseSource impulseSource;

    // ---------------- CONTROL ----------------
    [HideInInspector] public bool isDashing;
    [HideInInspector] public bool canControl = true;

    // ---------------- MOVE ----------------
    [Header("Move")]
    public float moveSpeed = 7f;
    public float acceleration = 45f;
    public float deceleration = 60f;
    public float airControl = 0.6f;

    // ---------------- JUMP ----------------
    [Header("Jump")]
    public float jumpForce = 12f;
    public float jumpBufferTime = 0.15f;
    public float coyoteTime = 0.12f;

    [HideInInspector] public float jumpBufferCounter;
    [HideInInspector] public float coyoteCounter;

    // ---------------- DASH ----------------
    [Header("Dash")]
    public float dashDistance = 10f;
    public float dashTime = 0.15f;
    public float dashCooldown = 0.4f;

    [Header("Dash FX")]
    public GameObject afterImagePrefab;
    public float afterImageSpacing = 0.05f;

    [HideInInspector] public float afterImageTimer;
    public SpriteRenderer playerSprite;

    [HideInInspector] public float dashCooldownTimer;
    [HideInInspector] public bool dashPressed;

    // ---------------- INPUT ----------------
    [Header("Runtime")]
    public float moveInput;
    public bool jumpHeld;
    public bool isGrounded;
    public float facingDir = 1f;

    public Transform attackPoint;

    // ---------------- STATE ----------------
    public PlayerStateMachine stateMachine;

    float highestY;
    bool inAir;
    bool wasGrounded;

    void Awake()
    {
        rb.freezeRotation = true;

        stateMachine = new PlayerStateMachine();
        stateMachine.Initialize(new GroundedState(this, stateMachine));

        if (!impulseSource)
            impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    void Update()
    {
        moveInput = Input.GetAxisRaw("Horizontal");
        jumpHeld = Input.GetKey(KeyCode.Space);

        // ✅ JUMP BUFFER (FIX)
        if (Input.GetKeyDown(KeyCode.Space))
            jumpBufferCounter = jumpBufferTime;

        jumpBufferCounter = Mathf.Max(0, jumpBufferCounter - Time.deltaTime);

        // DASH
        dashPressed = Input.GetKeyDown(KeyCode.LeftShift);
        if (dashCooldownTimer > 0)
            dashCooldownTimer -= Time.deltaTime;

        // FACE
        if (moveInput != 0)
        {
            facingDir = Mathf.Sign(moveInput);

            Vector3 s = modelPivot.localScale;
            s.x = Mathf.Abs(s.x) * facingDir;
            modelPivot.localScale = s;
        }

        stateMachine.Update();
    }

    void FixedUpdate()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, 0.15f, groundMask);

        // ---------------- COYOTE FIX ----------------
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
            coyoteCounter = Mathf.Max(0, coyoteCounter - Time.fixedDeltaTime);

            if (!inAir)
            {
                inAir = true;
                highestY = transform.position.y;
            }

            if (transform.position.y > highestY)
                highestY = transform.position.y;
        }

        wasGrounded = isGrounded;

        ApplyGravity();
        stateMachine.FixedUpdate();
    }

    void OnLand()
    {
        if (!impulseSource) return;

        float fallDistance = highestY - transform.position.y;

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

    public void ApplyMovement(float control)
    {
        if (!canControl) return;

        float targetSpeed = moveInput * moveSpeed;
        float speedDiff = targetSpeed - rb.linearVelocity.x;

        float accel = isGrounded
            ? (Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration)
            : acceleration * airControl;

        rb.linearVelocity = new Vector2(
            rb.linearVelocity.x + speedDiff * accel * Time.fixedDeltaTime * control,
            rb.linearVelocity.y
        );
    }

    void ApplyGravity()
    {
        if (rb.linearVelocity.y < 0)
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * 1.5f * Time.fixedDeltaTime;

        else if (rb.linearVelocity.y > 0 && !jumpHeld)
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * 1.2f * Time.fixedDeltaTime;
    }

    public void SpawnDust()
    {
        if (!dustPrefab) return;

        Vector3 pos = footPoint ? footPoint.position : transform.position;
        Instantiate(dustPrefab, pos, Quaternion.identity);
    }

    public float GetDashDirection() => facingDir == 0 ? 1 : facingDir;
}