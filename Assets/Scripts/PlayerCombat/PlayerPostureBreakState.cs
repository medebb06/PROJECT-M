using UnityEngine;

public class PlayerPostureBreakState : IPlayerState
{
    private PlayerController player;

    private float timer;
    private float knockbackDirection;

    public PlayerPostureBreakState(
        PlayerController player,
        Vector2 hitDirection
    )
    {
        this.player = player;

        knockbackDirection =
            Mathf.Sign(hitDirection.x);

        if (knockbackDirection == 0f)
            knockbackDirection = 1f;
    }

    public void Enter()
    {
        timer =
            player.postureBreakDuration;

        // =====================================================
        // TOTAL PLAYER CONTROL LOCK
        // =====================================================

        player.canControl = false;
        player.isAttackLocked = true;

        player.moveInput = 0f;
        player.jumpHeld = false;
        player.dashPressed = false;

        // Eski kısa input-lock timer'ı
        // stagger'ı etkilemesin.
        player.inputLocked = false;
        player.inputLockTimer = 0f;

        // =====================================================
        // KNOCKBACK
        // =====================================================

        if (player.rb != null)
        {
            player.rb.linearVelocity =
                new Vector2(
                    -knockbackDirection *
                    player.postureBreakKnockback,
                    player.rb.linearVelocity.y
                );
        }

        Debug.Log(
            "PLAYER POSTURE BREAK STATE!"
        );
    }

    public void Update()
    {
        // =====================================================
        // FORCE ALL PLAYER INPUT OFF
        // =====================================================

        player.moveInput = 0f;
        player.jumpHeld = false;
        player.dashPressed = false;

        timer -= Time.deltaTime;

        if (timer > 0f)
            return;

        ExitState();
    }

    public void FixedUpdate()
    {
        if (player.rb == null)
            return;

        // Stagger boyunca yatay hareket yok.
        player.rb.linearVelocity =
            new Vector2(
                0f,
                player.rb.linearVelocity.y
            );
    }

    public void Exit()
    {
        if (player.rb != null)
        {
            player.rb.linearVelocity =
                new Vector2(
                    0f,
                    player.rb.linearVelocity.y
                );
        }

        player.canControl = true;
        player.isAttackLocked = false;

        Debug.Log(
            "PLAYER POSTURE BREAK FINISHED!"
        );
    }

    private void ExitState()
    {
        if (player.IsGrounded())
        {
            player.stateMachine.ChangeState(
                new GroundedState(
                    player,
                    player.stateMachine
                )
            );
        }
        else
        {
            player.stateMachine.ChangeState(
                new AirState(
                    player,
                    player.stateMachine
                )
            );
        }
    }
}