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

        jumpedFromWallDirection =
            player.wallDirection;

        player.wallJumpBlockedDirection =
            jumpedFromWallDirection;

        jumpDirection =
            -jumpedFromWallDirection;

        // İlk launch.
        // X ve Y doğrudan güçlü şekilde veriliyor.
        player.SetVelocity(
            new Vector2(
                jumpDirection *
                player.wallJumpForceX,

                player.wallJumpForceY
            )
        );

        // Oyuncunun yönünü launch yönüne çevir.
        player.facingDir = jumpDirection;

        Vector3 scale =
            player.modelPivot.localScale;

        scale.x =
            Mathf.Abs(scale.x) *
            player.facingDir;

        player.modelPivot.localScale = scale;

        player.jumpBufferCounter = 0f;
        player.wallJumpBufferCounter = 0f;
        player.coyoteCounter = 0f;
        player.jumpConsumed = true;
    }

    public void Exit()
    {
    }

    public void Update()
    {
        timer += Time.deltaTime;

        if (player.IsGrounded())
        {
            player.wallJumpBlockedDirection = 0;

            sm.ChangeState(
                new GroundedState(player, sm)
            );

            return;
        }

        if (
            player.dashPressed &&
            player.dashCooldownTimer <= 0f
        )
        {
            sm.ChangeState(
                new DashState(player, sm)
            );

            return;
        }

        // Launch lock + control blend bittikten sonra
        // normal AirState'e geçiyoruz.
        float totalWallJumpTime =
            player.wallJumpControlLock +
            player.wallJumpControlBlendTime;

        if (timer >= totalWallJumpTime)
        {
            sm.ChangeState(
                new AirState(player, sm)
            );

            return;
        }
    }

    public void FixedUpdate()
    {
        Vector2 velocity =
            player.rb.linearVelocity;

        // --------------------------------------------------
        // 1. FAZ
        // İlk birkaç frame tamamen güçlü launch.
        // --------------------------------------------------

        if (
            timer <
            player.wallJumpControlLock
        )
        {
            velocity.x =
                jumpDirection *
                player.wallJumpForceX;

            player.SetVelocity(velocity);

            return;
        }

        // --------------------------------------------------
        // 2. FAZ
        // Air control yavaşça geri geliyor.
        // --------------------------------------------------

        float blendTimer =
            timer -
            player.wallJumpControlLock;

        float blendDuration =
            Mathf.Max(
                0.001f,
                player.wallJumpControlBlendTime
            );

        float blend =
            Mathf.Clamp01(
                blendTimer /
                blendDuration
            );

        // SmoothStep sayesinde control
        // başlangıçta yumuşak,
        // sonunda daha doğal şekilde geliyor.
        float smoothBlend =
            blend * blend *
            (3f - 2f * blend);

        float control =
            Mathf.Lerp(
                0f,
                player.airControl,
                smoothBlend
            );

        // Oyuncunun input'una göre hedef hız.
        float targetSpeed =
            player.moveInput *
            player.moveSpeed;

        float accelerationRate;

        if (Mathf.Abs(player.moveInput) > 0.01f)
        {
            accelerationRate =
                player.acceleration;
        }
        else
        {
            accelerationRate =
                player.deceleration;
        }

        accelerationRate *=
            control *
            player.wallJumpControlAccelerationMultiplier;

        float newVelocityX =
            Mathf.MoveTowards(
                velocity.x,
                targetSpeed,
                accelerationRate *
                Time.fixedDeltaTime
            );

        velocity.x =
            newVelocityX;

        player.SetVelocity(velocity);
    }
}