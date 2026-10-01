using UnityEngine;

public class WallJumpState : IPlayerState
{
    private PlayerController player;
    private PlayerStateMachine sm;

    private float timer;

    private int jumpDirection;
    private int jumpedFromWallDirection;

    public WallJumpState(
        PlayerController player,
        PlayerStateMachine sm
    )
    {
        this.player = player;
        this.sm = sm;
    }

    public void Enter()
    {
        timer = 0f;

        PlayerMovement movement =
            player.Movement;

        jumpedFromWallDirection =
            movement.wallDirection;

        movement.wallJumpBlockedDirection =
            jumpedFromWallDirection;

        jumpDirection =
            -jumpedFromWallDirection;

        // --------------------------------------------------
        // INITIAL WALL JUMP LAUNCH
        // --------------------------------------------------

        player.SetVelocity(
            new Vector2(
                jumpDirection *
                movement.wallJumpForceX,

                movement.wallJumpForceY
            )
        );

        // Oyuncunun yönünü launch yönüne çevir.
        player.facingDir =
            jumpDirection;

        Vector3 scale =
            player.modelPivot.localScale;

        scale.x =
            Mathf.Abs(scale.x) *
            player.facingDir;

        player.modelPivot.localScale =
            scale;

        movement.jumpBufferCounter = 0f;
        movement.wallJumpBufferCounter = 0f;
        movement.coyoteCounter = 0f;

        player.jumpConsumed = true;
    }

    public void Exit()
    {
    }

    public void Update()
    {
        timer += Time.deltaTime;

        PlayerMovement movement =
            player.Movement;

        // --------------------------------------------------
        // GROUND
        // --------------------------------------------------

        if (player.IsGrounded())
        {
            movement.wallJumpBlockedDirection = 0;

            sm.ChangeState(
                new GroundedState(
                    player,
                    sm
                )
            );

            return;
        }

        // --------------------------------------------------
        // DASH
        // --------------------------------------------------

        if (
            player.dashPressed &&
            player.dashCooldownTimer <= 0f
        )
        {
            sm.ChangeState(
                new DashState(
                    player,
                    sm
                )
            );

            return;
        }

        // --------------------------------------------------
        // WALL JUMP CONTROL TIME
        // --------------------------------------------------

        float totalWallJumpTime =
            movement.wallJumpControlLock +
            movement.wallJumpControlBlendTime;

        if (timer >= totalWallJumpTime)
        {
            sm.ChangeState(
                new AirState(
                    player,
                    sm
                )
            );

            return;
        }
    }

    public void FixedUpdate()
    {
        PlayerMovement movement =
            player.Movement;

        Vector2 velocity =
            player.rb.linearVelocity;

        // --------------------------------------------------
        // 1. PHASE
        // INITIAL LAUNCH LOCK
        // --------------------------------------------------

        if (
            timer <
            movement.wallJumpControlLock
        )
        {
            velocity.x =
                jumpDirection *
                movement.wallJumpForceX;

            player.SetVelocity(
                velocity
            );

            return;
        }

        // --------------------------------------------------
        // 2. PHASE
        // CONTROL BLEND
        // --------------------------------------------------

        float blendTimer =
            timer -
            movement.wallJumpControlLock;

        float blendDuration =
            Mathf.Max(
                0.001f,
                movement.wallJumpControlBlendTime
            );

        float blend =
            Mathf.Clamp01(
                blendTimer /
                blendDuration
            );

        // SmoothStep:
        // başlangıçta yumuşak,
        // sonunda daha doğal control.
        float smoothBlend =
            blend *
            blend *
            (3f - 2f * blend);

        float control =
            Mathf.Lerp(
                0f,
                movement.airControl,
                smoothBlend
            );

        // --------------------------------------------------
        // TARGET SPEED
        // --------------------------------------------------

        float targetSpeed =
            player.moveInput *
            movement.moveSpeed;

        float accelerationRate;

        if (
            Mathf.Abs(
                player.moveInput
            ) > 0.01f
        )
        {
            accelerationRate =
                movement.acceleration;
        }
        else
        {
            accelerationRate =
                movement.deceleration;
        }

        accelerationRate *=
            control *
            movement.wallJumpControlAccelerationMultiplier;

        // --------------------------------------------------
        // APPLY HORIZONTAL CONTROL
        // --------------------------------------------------

        float newVelocityX =
            Mathf.MoveTowards(
                velocity.x,
                targetSpeed,
                accelerationRate *
                Time.fixedDeltaTime
            );

        velocity.x =
            newVelocityX;

        player.SetVelocity(
            velocity
        );
    }
}