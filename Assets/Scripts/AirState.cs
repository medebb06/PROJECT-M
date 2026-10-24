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
        PlayerMovement movement =
            player.Movement;

        // =====================================================
        // GROUND SLAM
        // =====================================================

        if (
            player.verticalInput < -0.5f &&
            MobileInput.GetKeyDown(KeyCode.Space) &&
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
            player.dashCooldownTimer <= 0f &&
            (player.isGrounded || !DashState.AirDashUsed)
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
        // EĞİM: eğimli zemine değince kaymaya geç
        // =====================================================

        if (SlopeSlide.TryEnter(player, sm))
            return;

        // =====================================================
        // LAND
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
        // LEDGE CLIMB (kenara dogru basarken)
        // =====================================================

        if (LedgeClimbState.TryStart(player, sm, 0))
            return;

        // =====================================================
        // WALL SLIDE
        // =====================================================

        bool touchingWall =
            player.IsTouchingWall();

        // WALL SLIDE v2: duvara yaklasinca (dusmeye basladigi / tepe noktasi) yapisir.
        // Duvardan uzaga basiliyorsa yapismaz (tekrar tekrar yakalamayi onler).
        bool pressingAway =
            (
                movement.wallDirection == 1 &&
                player.moveInput < -0.1f
            )
            ||
            (
                movement.wallDirection == -1 &&
                player.moveInput > 0.1f
            );

        if (
            !player.isGrounded &&
            player.rb.linearVelocity.y < 1f &&
            touchingWall &&
            !pressingAway
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
        // Hava hareketi sabit fizik adiminda uygulanir (kare hizindan bagimsiz).
        // ApplyMovement Time.fixedDeltaTime kullandigi icin Update'te cagrilmamali.
        player.Movement.ApplyMovement(
            player.Movement.airControl
        );
    }
}