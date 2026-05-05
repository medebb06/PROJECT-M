using UnityEngine;

public class GroundedState : IPlayerState
{
    private PlayerController player;
    private PlayerStateMachine sm;

    public GroundedState(PlayerController player, PlayerStateMachine sm)
    {
        this.player = player;
        this.sm = sm;
    }

    public void Enter() { }

    public void Exit() { }

    public void Update()
    {
        if (player.IsInputLocked())
            return;

        player.ApplyMovement(1f);

        if (player.dashPressed && player.dashCooldownTimer <= 0f)
        {
            sm.ChangeState(new DashState(player, sm));
            return;
        }

        // ---------------- JUMP (FINAL FIX) ----------------
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