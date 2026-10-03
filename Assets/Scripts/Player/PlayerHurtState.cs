using UnityEngine;

public class PlayerHurtState : IPlayerState
{
    private PlayerController player;
    private PlayerStateMachine sm;

    // Hurt state'in toplam süresi (bu sürede dokunulmaz + kontrolsüz)
    private float timer;
    private readonly float duration = 0.35f;

    // Knockback hızının uygulandığı süre.
    // Süre bitince yatay hız sıfırlanır.
    private float knockbackTimer;
    private bool knockbackEnded;

    private Vector2 knockbackDir;
    private float knockbackForce;
    private float knockbackVerticalForce;
    private float knockbackDuration;

    public PlayerHurtState(
        PlayerController player,
        PlayerStateMachine sm,
        Vector2 hitDirection,
        float force = 8f,
        float verticalForce = -1f,
        float knockbackDuration = 0.1f)
    {
        this.player = player;
        this.sm = sm;

        knockbackDir = hitDirection;
        knockbackForce = force;

        // verticalForce verilmediyse (negatif) eski davranış:
        // yatay kuvvetin %60'ı kadar yukarı.
        knockbackVerticalForce =
            verticalForce >= 0f
                ? verticalForce
                : force * 0.6f;

        this.knockbackDuration = knockbackDuration;
    }

    public void Enter()
    {
        timer = duration;
        knockbackTimer = knockbackDuration;
        knockbackEnded = false;

        player.isInvincible = true;
        player.canControl = false;

        // Hurt sırasında saldırı yapılamasın.
        player.isAttackLocked = true;

        // Devam eden saldırı varsa iptal et.
        // (AttackState.Exit yatay hızı sıfırladığı için
        // knockback'ten ÖNCE çağrılmalı.)
        PlayerCombatController combat =
            player.GetComponent<PlayerCombatController>();

        if (combat != null)
            combat.CancelAttack();

        ApplyKnockback();
    }

    public void Exit()
    {
        player.isInvincible = false;
        player.canControl = true;
        player.isAttackLocked = false;
    }

    public void Update()
    {
        float dt = Time.deltaTime;

        timer -= dt;

        // Knockback süresi bitince yatay momentum kalmasın.
        if (!knockbackEnded)
        {
            knockbackTimer -= dt;

            if (knockbackTimer <= 0f)
            {
                knockbackEnded = true;

                player.SetVelocity(
                    new Vector2(
                        0f,
                        player.rb.linearVelocity.y
                    )
                );
            }
        }

        if (timer <= 0f)
        {
            // FIX: Eskiden her zaman GroundedState'e dönüyordu.
            // Havadaysan GroundedState.Enter coyote time'ı yeniliyor
            // ve havada bedava zıplama hakkı veriyordu.
            if (player.IsGrounded())
            {
                sm.ChangeState(
                    new GroundedState(player, sm)
                );
            }
            else
            {
                sm.ChangeState(
                    new AirState(player, sm)
                );
            }
        }
    }

    public void FixedUpdate()
    {
        // ekstra physics yok
    }

    private void ApplyKnockback()
    {
        Rigidbody2D rb = player.rb;

        if (rb == null)
            return;

        // Sadece yatay yönü kullan.
        float directionX;

        if (Mathf.Abs(knockbackDir.x) > 0.01f)
        {
            directionX = Mathf.Sign(knockbackDir.x);
        }
        else
        {
            // Yön belli değilse baktığın yönün tersine.
            directionX = player.facingDir >= 0f ? -1f : 1f;
        }

        rb.linearVelocity =
            new Vector2(
                directionX * knockbackForce,
                knockbackVerticalForce
            );
    }
}