using UnityEngine;

public class DashState : IPlayerState
{
    private PlayerController player;
    private PlayerStateMachine sm;

    private float timer;
    private float dir;
    private float dashSpeed;

    private float originalGravity;

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

        // 🔥 speed (Inspector controlled)
        dashSpeed = (player.dashDistance / player.dashTime) * player.dashSpeedMultiplier;

        // gravity off → smooth dash feel
        originalGravity = player.rb.gravityScale;
        player.rb.gravityScale = 0f;
    }

    public void Exit()
    {
        player.isDashing = false;

        player.rb.gravityScale = originalGravity;

        // cooldown
        player.dashCooldownTimer = player.dashCooldown;
    }

    public void Update()
    {
        timer -= Time.deltaTime;

        // 🔥 FULL OVERRIDE (movement disabled via flag)
        player.rb.linearVelocity = new Vector2(dir * dashSpeed, 0f);

        if (timer <= 0f)
        {
            // ❌ NEVER go to JumpState directly (bug fix)
            if (player.isGrounded)
            {
                sm.ChangeState(new GroundedState(player, sm));
            }
            else
            {
                sm.ChangeState(new AirState(player, sm));
            }
        }
    }

    public void FixedUpdate() { }
}