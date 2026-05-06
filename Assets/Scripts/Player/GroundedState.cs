using UnityEngine;

public class GroundedState : IPlayerState
{
    PlayerController player;
    PlayerStateMachine sm;

    public GroundedState(PlayerController player, PlayerStateMachine sm)
    {
        this.player = player;
        this.sm = sm;
    }

    public void Enter()
    {
        // ground’a basınca coyote reset
        player.coyoteCounter = player.coyoteTime;
    }

    public void Exit() { }

    public void Update()
    {
        if (!player.canControl) return;

        player.ApplyMovement(1f);

        // ---------------- DASH ----------------
        if (player.dashPressed && player.dashCooldownTimer <= 0f)
        {
            sm.ChangeState(new DashState(player, sm));
            return;
        }

        // ---------------- JUMP (TEK YER) ----------------
        if (player.jumpBufferCounter > 0f && player.coyoteCounter > 0f)
        {
            player.jumpBufferCounter = 0f;
            player.coyoteCounter = 0f;

            sm.ChangeState(new JumpState(player, sm));
            return;
        }
    }

    public void FixedUpdate() { }
}