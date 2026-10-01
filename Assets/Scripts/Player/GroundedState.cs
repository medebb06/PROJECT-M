using UnityEngine;

public class GroundedState : IPlayerState
{
    private PlayerController player;
    private PlayerStateMachine sm;

    public GroundedState(
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

        // Yere basınca coyote yenilenir.
        movement.coyoteCounter =
            movement.coyoteTime;

        // Jump lock reset.
        player.jumpConsumed = false;

        Vector2 vel =
            player.rb.linearVelocity;

        // Küçük zemin snap.
        if (Mathf.Abs(vel.y) < 0.01f)
        {
            vel.y = 0f;

            player.SetVelocity(vel);
        }
    }

    public void Exit()
    {
    }

    public void Update()
    {
        if (!player.canControl)
            return;

        PlayerMovement movement =
            player.Movement;

        // =====================================================
        // DEFENSE
        // =====================================================

        if (player.IsDefending())
        {
            player.SetVelocity(
                new Vector2(
                    0f,
                    player.rb.linearVelocity.y
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
        // FALL
        // =====================================================

        if (!player.IsGrounded())
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
        // JUMP
        // =====================================================

        if (
            !player.jumpConsumed &&
            movement.jumpBufferCounter > 0f &&
            movement.coyoteCounter > 0f
        )
        {
            movement.jumpBufferCounter = 0f;
            movement.coyoteCounter = 0f;

            player.jumpConsumed = true;

            float jumpStrength =
                Mathf.Clamp01(
                    Mathf.Abs(
                        player.moveInput
                    ) * 0.5f + 0.5f
                );

            if (player.audioPlayer != null)
            {
                player.audioPlayer.PlayJump(
                    jumpStrength
                );
            }

            sm.ChangeState(
                new JumpState(
                    player,
                    sm
                )
            );

            return;
        }
    }

    public void FixedUpdate()
    {
        // =====================================================
        // DEFENSE
        // =====================================================

        if (player.IsDefending())
        {
            player.SetVelocity(
                new Vector2(
                    0f,
                    player.rb.linearVelocity.y
                )
            );

            return;
        }

        // =====================================================
        // NORMAL GROUND MOVEMENT
        // =====================================================

        player.Movement.ApplyMovement(1f);
    }
}