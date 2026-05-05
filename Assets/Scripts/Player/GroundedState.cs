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
        // ---------------- INPUT LOCK ----------------
        if (player.IsInputLocked())
            return;

        // ---------------- MOVEMENT ----------------
        player.ApplyMovement(1f);

        // ---------------- DASH ----------------
        if (player.dashPressed && player.dashCooldownTimer <= 0f)
        {
            sm.ChangeState(new DashState(player, sm));
            return;
        }

        // ---------------- JUMP BUFFER SUPPORT ----------------
        // 🔥 FIX: input kaçırmaz
        if (player.jumpBufferCounter > 0f && player.isGrounded)
        {
            player.jumpBufferCounter = 0f;
            sm.ChangeState(new JumpState(player, sm));
            return;
        }
    }

    public void FixedUpdate() { }
}