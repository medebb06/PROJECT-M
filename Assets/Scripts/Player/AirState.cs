using UnityEngine;

public class AirState : IPlayerState
{
    private PlayerController player;
    private PlayerStateMachine sm;

    public AirState(
        PlayerController player,
        PlayerStateMachine sm
    )
    {
        this.player = player;
        this.sm = sm;
    }

    public void Enter()
    {
    }

    public void Exit()
    {
    }

    public void Update()
    {
        // =====================================================
        // AIR MOVEMENT
        // =====================================================

        player.ApplyMovement(
            player.airControl
        );

        // =====================================================
        // GROUND SLAM
        // =====================================================

        if (
            player.verticalInput < -0.5f &&
            Input.GetKeyDown(KeyCode.Space)
        )
        {
            sm.ChangeState(
                new GroundSlamState(
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

        // =====================================================
        // GROUND
        // =====================================================

        if (
            player.isGrounded &&
            player.rb.linearVelocity.y <= 0.1f
        )
        {
            sm.ChangeState(
                new GroundedState(
                    player,
                    sm
                )
            );

            return;
        }

        // =====================================================
        // WALL SLIDE
        // =====================================================

        bool touchingWall =
            player.IsTouchingWall();

        if (
            !player.isGrounded &&
            player.rb.linearVelocity.y < 0f &&
            touchingWall
        )
        {
            sm.ChangeState(
                new WallSlideState(
                    player,
                    sm
                )
            );

            return;
        }
    }

    public void FixedUpdate()
    {
    }
}