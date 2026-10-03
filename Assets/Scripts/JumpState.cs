using UnityEngine;

public class JumpState : IPlayerState
{
    private PlayerController player;
    private PlayerStateMachine sm;

    public JumpState(
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

        // =====================================================
        // HARD GUARD
        // =====================================================

        if (
            player.slamGroundLock ||
            player.inputLocked
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
        // JUMP FX  (animasyon + ses + toz: tek yerden)
        // =====================================================

        player.PlayJumpAnimation();

        if (player.audioPlayer != null)
        {
            float jumpStrength =
                Mathf.Clamp01(
                    Mathf.Abs(
                        player.moveInput
                    ) * 0.5f + 0.5f
                );

            player.audioPlayer.PlayJump(
                jumpStrength
            );
        }

        player.SpawnDust();

        // =====================================================
        // CLEAR DOWNWARD VELOCITY
        // =====================================================

        Vector2 velocity =
            player.rb.linearVelocity;

        if (velocity.y < 0f)
            velocity.y = 0f;

        player.SetVelocity(velocity);

        // =====================================================
        // JUMP IMPULSE
        // =====================================================

        player.rb.AddForce(
            Vector2.up *
            movement.jumpForce,
            ForceMode2D.Impulse
        );
    }

    public void Exit()
    {
    }

    public void Update()
    {
        PlayerMovement movement =
            player.Movement;

        // =====================================================
        // HARD GUARD
        // =====================================================

        if (
            player.slamGroundLock ||
            player.inputLocked
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
        // AIR MOVEMENT
        // =====================================================

        movement.ApplyMovement(
            movement.airControl
        );

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
        // FALL
        // =====================================================

        if (
            player.rb.linearVelocity.y <
            -0.1f
        )
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
        // LAND
        // =====================================================

        if (
            player.isGrounded &&
            player.rb.linearVelocity.y <=
            0.01f
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
    }

    public void FixedUpdate()
    {
    }
}