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
        if (player.isDashing)
        {
            sm.ChangeState(new AirState(player, sm));
            return;
        }

        Vector2 v = player.rb.linearVelocity;
        v.y = Mathf.Max(0f, v.y);
        player.rb.linearVelocity = v;

        player.rb.AddForce(Vector2.up * player.jumpForce, ForceMode2D.Impulse);
    }

    public void Exit() { }

    public void Update()
    {
        player.ApplyMovement(player.airControl);

        // DASH (FIXED)
        if (player.dashPressed && player.dashCooldownTimer <= 0f && !player.isDashing)
        {
            sm.ChangeState(new DashState(player, sm));
            return;
        }

        if (player.rb.linearVelocity.y < -0.1f)
        {
            sm.ChangeState(new AirState(player, sm));
            return;
        }

        if (player.isGrounded && player.rb.linearVelocity.y <= 0.05f)
        {
            sm.ChangeState(new GroundedState(player, sm));
            return;
        }
    }

    public void FixedUpdate() { }
}