using UnityEngine;

public class WallSlideState : IPlayerState
{
    private PlayerController player;
    private PlayerStateMachine sm;

    public WallSlideState(
        PlayerController player,
        PlayerStateMachine sm
    )
    {
        this.player = player;
        this.sm = sm;
    }

    public void Enter()
    {
        player.isWallSliding = true;

        Vector2 velocity =
            player.rb.linearVelocity;

        if (
            velocity.y <
            -player.wallSlideSpeed
        )
        {
            velocity.y =
                -player.wallSlideSpeed;
        }

        player.SetVelocity(
            velocity
        );
    }

    public void Exit()
    {
        player.isWallSliding = false;
    }

    public void Update()
    {
        // =====================================================
        // GROUND
        // =====================================================

        if (player.IsGrounded())
        {
            player.wallJumpBlockedDirection = 0;

            sm.ChangeState(
                new GroundedState(
                    player,
                    sm
                )
            );

            return;
        }

        // =====================================================
        // WALL CHECK
        // =====================================================

        bool touchingWall =
            player.IsTouchingWall();

        if (!touchingWall)
        {
            sm.ChangeState(
                new AirState(
                    player,
                    sm
                )
            );

            return;
        }

        // =====================================================
        // WALL JUMP
        // =====================================================
        //
        // jumpBufferCounter kullanıyoruz.
        //
        // Böylece Space'e:
        //
        // "duvara gelirken"
        //
        // basılmış olsa bile wall jump
        // inputu kaybolmuyor.
        //
        // =====================================================

        if (player.jumpBufferCounter > 0f)
        {
            // Aynı duvardan arka arkaya
            // wall jump yapma.

            if (
                player.wallJumpBlockedDirection !=
                player.wallDirection
            )
            {
                player.jumpBufferCounter = 0f;

                sm.ChangeState(
                    new WallJumpState(
                        player,
                        sm
                    )
                );

                return;
            }
        }

        // =====================================================
        // DUVARA UZAKLAŞ
        // =====================================================

        bool pressingAwayFromWall =
            (
                player.wallDirection == 1 &&
                player.moveInput < -0.1f
            )
            ||
            (
                player.wallDirection == -1 &&
                player.moveInput > 0.1f
            );

        if (pressingAwayFromWall)
        {
            sm.ChangeState(
                new AirState(
                    player,
                    sm
                )
            );

            return;
        }

        // =====================================================
        // DASH
        // =====================================================

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
    }

    public void FixedUpdate()
    {
        Vector2 velocity =
            player.rb.linearVelocity;

        if (
            velocity.y <
            -player.wallSlideSpeed
        )
        {
            velocity.y =
                -player.wallSlideSpeed;
        }

        // X'e dokunmuyoruz.
        // Oyuncu duvardan ayrılabilsin.

        player.SetVelocity(
            velocity
        );
    }
}