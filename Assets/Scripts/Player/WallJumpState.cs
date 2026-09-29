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

        player.SetVelocity(
            new Vector2(
                jumpDirection *
                player.wallJumpForceX,

                player.wallJumpForceY
            )
        );

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

        if (
            timer >=
            player.wallJumpControlLock
        )
        {
            sm.ChangeState(
                new AirState(player, sm)
            );

            return;
        }
    }

    public void FixedUpdate()
    {
        if (
            timer <
            player.wallJumpControlLock
        )
        {
            Vector2 velocity =
                player.rb.linearVelocity;

            velocity.x =
                jumpDirection *
                player.wallJumpForceX;

            player.SetVelocity(velocity);
        }
    }
}