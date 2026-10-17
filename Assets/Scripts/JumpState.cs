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

        bool airJump = player.doingAirJump;
        player.doingAirJump = false;

        if (!airJump)
            player.SpawnDust();

        // =====================================================
        // CLEAR DOWNWARD VELOCITY
        // =====================================================

        // =====================================================
        // JUMP v2: dikey hizi DOGRUDAN ayarla (eskiden ustune ekleniyordu;
        // bu yuzden yukseklik zamanlamaya gore degisiyordu). Hep ayni yukseklik.
        // =====================================================

        Vector2 velocity =
            player.rb.linearVelocity;

        velocity.y =
            movement.jumpForce *
            (airJump ? player.airJumpForceMul : 1f);

        player.SetVelocity(velocity);

        movement.jumpRising = true;
        movement.jumpCutLockTimer = movement.minJumpHoldTime;
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
        // GROUND SLAM (yükselirken de: ↓ + Space)
        // =====================================================

        if (
            player.verticalInput < -0.5f &&
            MobileInput.GetKeyDown(KeyCode.Space) &&
            !player.isGrounded &&
            player.CanGroundSlam
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
        // LEDGE CLIMB (zıplamanın sonlarına doğru, kenara basarken)
        // =====================================================

        if (
            player.rb.linearVelocity.y < movement.jumpForce * 0.6f &&
            LedgeClimbState.TryStart(player, sm, 0)
        )
            return;

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
        // Hava hareketi sabit fizik adiminda uygulanir (kare hizindan bagimsiz).
        // ApplyMovement Time.fixedDeltaTime kullandigi icin Update'te cagrilmamali.
        player.Movement.ApplyMovement(
            player.Movement.airControl
        );
    }
}