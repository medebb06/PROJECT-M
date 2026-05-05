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
        // ---------------- DASH SAFETY ----------------
        if (player.isDashing)
        {
            sm.ChangeState(new AirState(player, sm));
            return;
        }

        // ---------------- CLEAN JUMP ----------------
        Vector2 v = player.rb.linearVelocity;
        if (v.y < 0) v.y = 0; // downward momentum cancel
        player.rb.linearVelocity = v;

        player.rb.AddForce(Vector2.up * player.jumpForce, ForceMode2D.Impulse);
    }

    public void Exit() { }

    public void Update()
    {
        // ---------------- AIR MOVEMENT ----------------
        player.ApplyMovement(player.airControl);

        // ---------------- DASH ----------------
        if (player.dashPressed && player.dashCooldownTimer <= 0f && !player.isDashing)
        {
            sm.ChangeState(new DashState(player, sm));
            return;
        }

        // ---------------- FALL TRANSITION ----------------
        if (player.rb.linearVelocity.y < -0.1f)
        {
            sm.ChangeState(new AirState(player, sm));
            return;
        }

        // ---------------- GROUNDED RETURN (SAFE) ----------------
        // 🔥 FIX: instant flicker bug önler
        if (player.isGrounded && player.rb.linearVelocity.y <= 0.01f)
        {
            sm.ChangeState(new GroundedState(player, sm));
            return;
        }
    }

    public void FixedUpdate() { }
}