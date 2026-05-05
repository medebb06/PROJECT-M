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
        // ---------------- INPUT LOCK SAFETY ----------------
        if (player.IsInputLocked())
            return;

        // ---------------- MOVEMENT ----------------
        player.ApplyMovement(1f);

        // ---------------- DASH (PRIORITY) ----------------
        if (player.dashPressed && player.dashCooldownTimer <= 0f)
        {
            sm.ChangeState(new DashState(player, sm));
            return;
        }

        // ---------------- JUMP (DIRECT SYSTEM FIX) ----------------
        // CanJump sistemi kaldırıldı → artık grounded + input
        if (player.isGrounded && Input.GetKeyDown(KeyCode.Space))
        {
            sm.ChangeState(new JumpState(player, sm));
            return;
        }
    }

    public void FixedUpdate() { }
}