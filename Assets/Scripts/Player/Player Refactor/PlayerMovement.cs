using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    private PlayerController player;

    // =========================================================
    // MOVEMENT
    // =========================================================

    [Header("Movement")]
    public float moveSpeed = 7f;
    public float acceleration = 45f;
    public float deceleration = 60f;
    public float airControl = 0.6f;

    // =========================================================
    // JUMP
    // =========================================================

    [Header("Jump")]
    public float jumpForce = 12f;

    [Header("Jump Feel")]
    public float jumpBufferTime = 0.15f;
    public float coyoteTime = 0.12f;
    public float jumpCutMultiplier = 2f;
    public float jumpCutVelocityMultiplier = 0.3f;
    public float fallMultiplier = 2.5f;
    public float maxFallSpeed = 20f;

    // =========================================================
    // WALL
    // =========================================================

    [Header("Wall")]
    public Transform wallCheck;
    public float wallCheckDistance = 0.3f;
    public LayerMask wallMask;

    [Header("Wall Slide")]
    public float wallSlideSpeed = 2f;

    [Header("Wall Jump")]
    public float wallJumpForceX = 9f;
    public float wallJumpForceY = 12f;
    public float wallJumpControlLock = 0.08f;
    public float wallJumpBufferTime = 0.12f;
    public float wallJumpControlBlendTime = 0.18f;
    public float wallJumpControlAccelerationMultiplier = 1f;

    // =========================================================
    // GROUND CHECK
    // =========================================================

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.15f;
    public LayerMask groundMask;

    // =========================================================
    // RUNTIME STATE
    // =========================================================

    [HideInInspector] public float jumpBufferCounter;
    [HideInInspector] public float coyoteCounter;

    [HideInInspector] public float wallJumpBufferCounter;
    [HideInInspector] public int wallDirection;
    [HideInInspector] public bool isWallSliding;
    [HideInInspector] public int wallJumpBlockedDirection;

    private bool wasGrounded;
    private bool wasAirborne;

    private float airTime;
    private float maxAirHeight;

    // =========================================================
    // INITIALIZE
    // =========================================================

    public void Initialize(PlayerController controller)
    {
        player = controller;
    }

    // =========================================================
    // UPDATE TIMERS
    // =========================================================

    public void UpdateTimers()
    {
        jumpBufferCounter =
            Mathf.Max(
                0f,
                jumpBufferCounter - Time.deltaTime
            );

        wallJumpBufferCounter =
            Mathf.Max(
                0f,
                wallJumpBufferCounter - Time.deltaTime
            );

        // FIX: Eskiden coyoteCounter hiç azalmıyordu.
        // Bu yüzden platformdan yürüyerek düşünce havada,
        // istediğin zaman bir kez zıplayabiliyordun.
        //
        // Artık: yerdeyken sürekli dolu, havadayken geri sayar.
        if (
            player != null &&
            player.isGrounded
        )
        {
            coyoteCounter = coyoteTime;
        }
        else
        {
            coyoteCounter =
                Mathf.Max(
                    0f,
                    coyoteCounter - Time.deltaTime
                );
        }
    }

    // =========================================================
    // FIXED UPDATE
    // =========================================================

    public void FixedUpdateMovement()
    {
        GroundCheck();

        if (!player.isGrounded)
        {
            wasAirborne = true;

            airTime +=
                Time.fixedDeltaTime;

            if (
                player.transform.position.y >
                maxAirHeight
            )
            {
                maxAirHeight =
                    player.transform.position.y;
            }
        }

        ApplyBetterGravity();
    }

    // =========================================================
    // HORIZONTAL MOVEMENT
    // =========================================================

    public void ApplyMovement(float control)
    {
        if (!player.canControl)
            return;

        if (player.inputLocked)
            return;

        float targetSpeed =
            player.moveInput *
            moveSpeed;

        float accelerationRate =
            Mathf.Abs(
                player.moveInput
            ) > 0.01f
                ? acceleration
                : deceleration;

        accelerationRate *= control;

        float newVelocityX =
            Mathf.MoveTowards(
                player.rb.linearVelocity.x,
                targetSpeed,
                accelerationRate *
                Time.fixedDeltaTime
            );

        player.rb.linearVelocity =
            new Vector2(
                newVelocityX,
                player.rb.linearVelocity.y
            );
    }

    // =========================================================
    // VELOCITY
    // =========================================================

    public void SetVelocity(Vector2 velocity)
    {
        if (player == null)
            return;

        if (player.rb == null)
            return;

        player.rb.linearVelocity =
            velocity;
    }

    // =========================================================
    // GRAVITY / JUMP CUT
    // =========================================================

    private void ApplyBetterGravity()
    {
        if (!player.canControl)
            return;

        if (player.slamGroundLock)
            return;

        if (player.rb.linearVelocity.y < 0f)
        {
            player.rb.linearVelocity +=
                Vector2.up *
                Physics2D.gravity.y *
                (fallMultiplier - 1f) *
                Time.fixedDeltaTime;
        }
        else if (
            player.rb.linearVelocity.y > 0f &&
            !player.jumpHeld
        )
        {
            player.rb.linearVelocity =
                new Vector2(
                    player.rb.linearVelocity.x,
                    player.rb.linearVelocity.y *
                    jumpCutVelocityMultiplier
                );
        }

        if (
            player.rb.linearVelocity.y <
            -maxFallSpeed
        )
        {
            player.rb.linearVelocity =
                new Vector2(
                    player.rb.linearVelocity.x,
                    -maxFallSpeed
                );
        }
    }

    // =========================================================
    // GROUND CHECK
    // =========================================================

    private void GroundCheck()
    {
        if (player.slamGroundLock)
        {
            player.isGrounded = true;
            return;
        }

        if (groundCheck == null)
        {
            player.isGrounded = false;
            return;
        }

        bool groundedNow =
            Physics2D.OverlapCircle(
                groundCheck.position,
                groundCheckRadius,
                groundMask
            );

        // =====================================================
        // LAND
        // =====================================================

        if (
            groundedNow &&
            !wasGrounded
        )
        {
            if (wasAirborne)
            {
                player.OnPlayerLand();

                wasAirborne = false;
            }

            airTime = 0f;

            maxAirHeight =
                player.transform.position.y;
        }

        // =====================================================
        // COYOTE TIME
        // =====================================================

        if (
            !groundedNow &&
            wasGrounded
        )
        {
            coyoteCounter =
                coyoteTime;

            wasAirborne = true;
        }

        wasGrounded =
            groundedNow;

        player.isGrounded =
            groundedNow;
    }

    // =========================================================
    // WALL CHECK
    // =========================================================

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

    // =========================================================
    // JUMP RESET
    // =========================================================

    public void ResetJump()
    {
        jumpConsumedReset();
    }

    private void jumpConsumedReset()
    {
        // JumpState / PlayerController yönetir.
        // Compatibility amacıyla tutuluyor.
    }

    // =========================================================
    // EXTERNAL SETTERS
    // =========================================================

    public void SetGroundCheck(
        Transform check,
        float radius,
        LayerMask mask
    )
    {
        groundCheck =
            check;

        groundCheckRadius =
            radius;

        groundMask =
            mask;
    }

    public void SetWallCheck(
        Transform check,
        float distance,
        LayerMask mask
    )
    {
        wallCheck =
            check;

        wallCheckDistance =
            distance;

        wallMask =
            mask;
    }

    // =========================================================
    // AIR DATA
    // =========================================================

    public float GetMaxAirHeight()
    {
        return maxAirHeight;
    }

    public float GetAirTime()
    {
        return airTime;
    }

    public void ResetAirData()
    {
        airTime = 0f;

        if (player != null)
        {
            maxAirHeight =
                player.transform.position.y;
        }
    }
}