using UnityEngine;

public class PlayerHurtState : IPlayerState
{
    private PlayerController player;
    private PlayerStateMachine sm;

    private float timer;
    private float duration = 0.35f;

    private Vector2 knockbackDir;
    private float knockbackForce;

    private bool applied;

    public PlayerHurtState(
        PlayerController player,
        PlayerStateMachine sm,
        Vector2 hitDirection,
        float force = 8f)
    {
        this.player = player;
        this.sm = sm;

        knockbackDir = hitDirection.normalized;
        knockbackForce = force;
    }

    public void Enter()
    {
        timer = duration;
        applied = false;

        player.isInvincible = true;
        player.canControl = false;   // kontrolü kesiyoruz (çok önemli)
    }

    public void Exit()
    {
        player.isInvincible = false;
        player.canControl = true;
    }

    public void Update()
    {
        if (!applied)
        {
            ApplyKnockback();
            applied = true;
        }

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            sm.ChangeState(new GroundedState(player, sm));
        }
    }

    public void FixedUpdate()
    {
        // ekstra physics yok, kasmıyoruz
    }

    private void ApplyKnockback()
    {
        Rigidbody2D rb = player.rb;

        // mevcut hız sıfırla → kontrol hissi daha temiz olur
        rb.linearVelocity = Vector2.zero;

        // yatay + hafif yukarı knockback
        Vector2 force = new Vector2(
            knockbackDir.x * knockbackForce,
            knockbackForce * 0.6f
        );

        rb.linearVelocity = force;
    }
}