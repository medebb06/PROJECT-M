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
        // ---------------- MOVEMENT ----------------
        player.ApplyMovement(1f);

        // ---------------- INPUT LOCK SAFETY ----------------
        if (player.IsInputLocked())
            return;

        // ---------------- JUMP ----------------
        if (player.CanJump())
        {
            player.ConsumeJump();
            sm.ChangeState(new JumpState(player, sm));
            return;
        }

        // ---------------- DASH ----------------
        if (player.dashPressed && player.dashCooldownTimer <= 0f)
        {
            sm.ChangeState(new DashState(player, sm));
            return;
        }
    }

    public void FixedUpdate() { }
}