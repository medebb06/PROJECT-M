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

    public void Enter()
    {
        // yere basınca coyote yenilenir
        player.coyoteCounter = player.coyoteTime;

        // jump lock reset (çok önemli)
        player.jumpConsumed = false;

        Vector2 vel = player.rb.linearVelocity;

        // küçük zemin snap
        if (Mathf.Abs(vel.y) < 0.01f)
        {
            vel.y = 0f;
            player.SetVelocity(vel);
        }
    }

    public void Exit() { }

    public void Update()
    {
        if (!player.canControl)
            return;

        // ---------------- DASH ----------------
        if (player.dashPressed && player.dashCooldownTimer <= 0f)
        {
            sm.ChangeState(new DashState(player, sm));
            return;
        }

        // ---------------- FALL ----------------
        if (!player.IsGrounded())
        {
            sm.ChangeState(new AirState(player, sm));
            return;
        }

        // ---------------- JUMP ----------------
        if (!player.jumpConsumed &&
            player.jumpBufferCounter > 0f &&
            player.coyoteCounter > 0f)
        {
            player.jumpBufferCounter = 0f;
            player.coyoteCounter = 0f;

            player.jumpConsumed = true;

            float jumpStrength = Mathf.Clamp01(
                Mathf.Abs(player.moveInput) * 0.5f + 0.5f
            );

            if (player.audioPlayer != null)
                player.audioPlayer.PlayJump(jumpStrength);

            sm.ChangeState(new JumpState(player, sm));
            return;
        }
    }

    public void FixedUpdate()
    {
        Vector2 vel = player.rb.linearVelocity;

        float targetSpeed = player.moveInput * player.moveSpeed;
        float speedDif = targetSpeed - vel.x;

        float accel = (Mathf.Abs(targetSpeed) > 0.01f)
            ? player.acceleration
            : player.deceleration;

        vel.x += speedDif * accel * Time.fixedDeltaTime;

        player.SetVelocity(vel);
    }
}