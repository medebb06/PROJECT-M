using UnityEngine;

public class DashState : IPlayerState
{
    private PlayerController player;
    private PlayerStateMachine sm;

    private float timer;
    private float dir;

    public DashState(PlayerController player, PlayerStateMachine sm)
    {
        this.player = player;
        this.sm = sm;
    }

    public void Enter()
    {
        timer = 0.15f;

        // 🔥 FIX: INPUT DEĞİL MEMORY
        dir = player.facingDir;

        // ekstra güvenlik
        if (dir == 0)
            dir = 1;
    }

    public void Exit() { }

    public void Update()
    {
        timer -= Time.deltaTime;

        player.rb.linearVelocity = new Vector2(
            dir * player.dashForce,
            0
        );

        if (timer <= 0)
        {
            if (player.isGrounded)
                sm.ChangeState(new GroundedState(player, sm));
            else
                sm.ChangeState(new JumpState(player, sm));
        }
    }

    public void FixedUpdate() { }
}