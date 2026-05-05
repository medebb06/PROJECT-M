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
        if (player.isDashing) return; // 🔥 dash sonrası fake jump engel

        Vector2 v = player.rb.linearVelocity;
        v.y = 0f;
        player.rb.linearVelocity = v;

        player.rb.AddForce(Vector2.up * player.jumpForce, ForceMode2D.Impulse);
    }

    public void Exit() { }

    public void Update()
    {
        player.ApplyMovement(player.airControl);

        // dash mid-air de çalışsın
        if (player.dashPressed && player.dashCooldownTimer <= 0f)
        {
            sm.ChangeState(new DashState(player, sm));
            return;
        }

        if (player.isGrounded && player.rb.linearVelocity.y <= 0.1f)
        {
            sm.ChangeState(new GroundedState(player, sm));
        }
    }

    public void FixedUpdate() { }
}