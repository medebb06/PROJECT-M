using UnityEngine;

public class JumpState : IPlayerState
{
    private PlayerController player;
    private PlayerStateMachine sm;

    public JumpState(PlayerController player, PlayerStateMachine sm)
    {
        this.player = player;
        this.sm = sm;
    }

    public void Enter()
    {
        // 🔥 HARD GUARD
        if (player.slamGroundLock || player.inputLocked)
        {
            sm.ChangeState(new GroundedState(player, sm));
            return;
        }

        player.SpawnDust();

        Vector2 v = player.rb.linearVelocity;
        if (v.y < 0) v.y = 0;
        player.rb.linearVelocity = v;

        player.rb.AddForce(Vector2.up * player.jumpForce, ForceMode2D.Impulse);
    }

    public void Exit() { }

    public void Update()
    {
        if (player.slamGroundLock || player.inputLocked)
        {
            sm.ChangeState(new GroundedState(player, sm));
            return;
        }

        player.ApplyMovement(player.airControl);

        if (player.dashPressed && player.dashCooldownTimer <= 0f)
        {
            sm.ChangeState(new DashState(player, sm));
            return;
        }

        if (player.rb.linearVelocity.y < -0.1f)
        {
            sm.ChangeState(new AirState(player, sm));
            return;
        }

        if (player.isGrounded && player.rb.linearVelocity.y <= 0.01f)
        {
            sm.ChangeState(new GroundedState(player, sm));
            return;
        }
    }

    public void FixedUpdate() { }
}