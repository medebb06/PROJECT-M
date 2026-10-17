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

        // WALL JUMP v2: yatay hiz sabit + yukseklik = jumpHeight x oran.
        // (v0 = jumpForce x sqrt(oran) -> yukseklik oranla orantili)
        player.SetVelocity(
            new Vector2(
                jumpDirection *
                movement.wallJumpSpeedX,

                movement.jumpForce *
                Mathf.Sqrt(
                    Mathf.Max(0.1f, movement.wallJumpHeightRatio)
                )
            )
        );

        // Space hizli birakilsa da duvar zıplaması tam yukselsin.
        movement.jumpRising = true;
        movement.jumpCutLockTimer =
            movement.timeToApex * 0.9f;

        // --------------------------------------------------
        // FACING
        // --------------------------------------------------
        // FIX: Oyunun geri kalanı (PlayerController.HandleFacing)
        // yönü SpriteRenderer.flipX ile çeviriyor.
        // Burada ayrıca modelPivot.localScale.x'i çevirmek
        // çift ters çevirmeye (sprite yanlış yöne bakmasına)
        // ve DashState hayaletlerinin yanlış bakmasına yol açıyordu.

        player.facingDir =
            jumpDirection;

        // Kilit suresi: kilit + blend suresi (en az 0.25 sn) boyunca yon ziplama yonunde kalir.
        player.facingLockTimer =
            Mathf.Max(0.25f, movement.wallJumpLockTime + movement.wallJumpBlendTime);

        if (player.playerSprite != null)
        {
            player.playerSprite.flipX =
                player.facingDir < 0f;
        }

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
        // LEDGE CLIMB: karsi duvarin kenarina yetisince tirman
        // --------------------------------------------------

        if (
            timer > 0.06f &&
            LedgeClimbState.TryStart(player, sm, jumpDirection)
        )
            return;

        // --------------------------------------------------
        // WALL JUMP CONTROL TIME
        // --------------------------------------------------

        float totalWallJumpTime =
            movement.wallJumpLockTime +
            movement.wallJumpBlendTime;

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
            movement.wallJumpLockTime
        )
        {
            velocity.x =
                jumpDirection *
                movement.wallJumpSpeedX;

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
            movement.wallJumpLockTime;

        float blendDuration =
            Mathf.Max(
                0.001f,
                movement.wallJumpBlendTime
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

        float control = smoothBlend;

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
                movement.airAcceleration;
        }
        else
        {
            accelerationRate =
                movement.airDeceleration;
        }

        accelerationRate *= control;

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