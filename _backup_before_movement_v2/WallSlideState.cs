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
        PlayerMovement movement =
            player.Movement;

        movement.isWallSliding = true;

        Vector2 velocity =
            player.rb.linearVelocity;

        if (velocity.y < -movement.wallSlideSpeed)
        {
            velocity.y =
                -movement.wallSlideSpeed;
        }

        player.SetVelocity(velocity);
    }

    public void Exit()
    {
        player.Movement.isWallSliding = false;
    }

    public void Update()
    {
        PlayerMovement movement =
            player.Movement;

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

        if (movement.wallJumpBufferCounter > 0f)
        {
            if (
                movement.wallJumpBlockedDirection !=
                movement.wallDirection
            )
            {
                movement.wallJumpBufferCounter = 0f;

                sm.ChangeState(
                    new WallJumpState(
                        player,
                        sm
                    )
                );

                return;
            }
        }

        bool pressingAwayFromWall =
            (
                movement.wallDirection == 1 &&
                player.moveInput < -0.1f
            )
            ||
            (
                movement.wallDirection == -1 &&
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
        PlayerMovement movement =
            player.Movement;

        Vector2 velocity =
            player.rb.linearVelocity;

        if (velocity.y < -movement.wallSlideSpeed)
        {
            velocity.y =
                -movement.wallSlideSpeed;
        }

        player.SetVelocity(velocity);
    }
}