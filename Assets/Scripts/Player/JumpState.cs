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
        // 🔥 FX
        player.SpawnDust();

        // 🔥 vertical reset (smooth jump feel)
        Vector2 v = player.rb.linearVelocity;
        if (v.y < 0) v.y = 0;
        player.rb.linearVelocity = v;

        // 🔥 jump apply
        player.rb.AddForce(Vector2.up * player.jumpForce, ForceMode2D.Impulse);

        player.SpawnDust();
    }

    public void Exit() { }

    public void Update()
    {
        // movement allowed
        player.ApplyMovement(player.airControl);

        // ---------------- DASH ----------------
        if (player.dashPressed && player.dashCooldownTimer <= 0f && !player.isDashing)
        {
            sm.ChangeState(new DashState(player, sm));
            return;
        }

        // ---------------- FALL ----------------
        if (player.rb.linearVelocity.y < -0.1f)
        {
            sm.ChangeState(new AirState(player, sm));
            return;
        }

        // ---------------- LAND ----------------
        if (player.isGrounded && player.rb.linearVelocity.y <= 0.01f)
        {
            sm.ChangeState(new GroundedState(player, sm));
            return;
        }
    }

    public void FixedUpdate() { }
}