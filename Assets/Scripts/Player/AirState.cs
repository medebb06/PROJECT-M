using UnityEngine;

public class AirState : IPlayerState
{
    private PlayerController player;
    private PlayerStateMachine sm;

    public AirState(PlayerController player, PlayerStateMachine sm)
    {
        this.player = player;
        this.sm = sm;
    }

    public void Enter() { }
    public void Exit() { }

    public void Update()
    {
        player.ApplyMovement(player.airControl);

        // Ground check
        if (player.isGrounded && player.rb.linearVelocity.y <= 0.1f)
        {
            sm.ChangeState(new GroundedState(player, sm));
        }

        // dash mid-air tekrar yapýlabilsin
        if (player.dashPressed && player.dashCooldownTimer <= 0f)
        {
            sm.ChangeState(new DashState(player, sm));
        }
    }

    public void FixedUpdate() { }
}