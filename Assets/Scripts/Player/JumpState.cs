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
        Vector2 velocity = player.rb.linearVelocity;

        velocity.y = 0f; // eski momentum temizle
        player.rb.linearVelocity = velocity;

        player.rb.AddForce(Vector2.up * player.jumpForce, ForceMode2D.Impulse);
    }

    public void Exit() { }

    public void Update()
    {
        player.ApplyMovement(); // air control burada devreye giriyor

        if (player.isGrounded && player.rb.linearVelocity.y <= 0.1f)
        {
            sm.ChangeState(new GroundedState(player, sm));
        }
    }

    public void FixedUpdate() { }
}