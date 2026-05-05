using UnityEngine;

public class DashState : IPlayerState
{
    private PlayerController player;
    private PlayerStateMachine sm;

    private float timer;
    private float dir;
    private float dashSpeed;

    public DashState(PlayerController player, PlayerStateMachine sm)
    {
        this.player = player;
        this.sm = sm;
    }

    public void Enter()
    {
        player.isDashing = true;

        timer = player.dashTime;
        dir = player.GetDashDirection();

        // 🔥 consistent dash speed (distance/time)
        dashSpeed = player.dashDistance / player.dashTime;
    }

    public void Exit()
    {
        player.isDashing = false;

        // cooldown
        player.dashCooldownTimer = player.dashCooldown;
    }

    public void Update()
    {
        timer -= Time.deltaTime;

        // 🔥 FULL OVERRIDE (no movement interference)
        player.rb.linearVelocity = new Vector2(dir * dashSpeed, 0f);

        // ---------------- END TRANSITION ----------------
        if (timer <= 0f)
        {
            // small smoothing frame (prevents "snap feel")
            Vector2 v = player.rb.linearVelocity;
            v.x *= 0.8f;
            player.rb.linearVelocity = v;

            if (player.isGrounded)
                sm.ChangeState(new GroundedState(player, sm));
            else
                sm.ChangeState(new AirState(player, sm));
        }
    }

    public void FixedUpdate() { }
}