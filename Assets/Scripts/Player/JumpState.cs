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
        // 🔥 Dash sonrası fake jump tamamen engel
        if (player.isDashing)
        {
            sm.ChangeState(new AirState(player, sm));
            return;
        }

        // clean vertical reset (double jump bug fix)
        Vector2 v = player.rb.linearVelocity;
        v.y = Mathf.Max(0f, v.y); // negatifse sıfırla ama pozitif momentum kalabilir
        player.rb.linearVelocity = v;

        // jump impulse
        player.rb.AddForce(Vector2.up * player.jumpForce, ForceMode2D.Impulse);
    }

    public void Exit() { }

    public void Update()
    {
        // movement (air control)
        player.ApplyMovement(player.airControl);

        // ---------------- DASH CANCEL ----------------
        if (player.dashPressed && player.dashCooldownTimer <= 0f)
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

        // ---------------- GROUNDED SAFE CHECK ----------------
        if (player.isGrounded && player.rb.linearVelocity.y <= 0.05f)
        {
            sm.ChangeState(new GroundedState(player, sm));
            return;
        }
    }

    public void FixedUpdate() { }
}