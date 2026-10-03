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
        // Coyote time artık PlayerMovement.UpdateTimers içinde
        // yerdeyken sürekli yenileniyor.

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

        // Zıplama PlayerController.HandleJump'ta başlıyor ve
        // JumpState.Enter'da uygulanıyor (burada tekrar yok).
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