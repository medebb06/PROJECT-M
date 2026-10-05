using UnityEngine;

public class PlayerHurtState : IPlayerState
{
    private PlayerController player;
    private PlayerStateMachine sm;

    // SERSEMLEME: Bu süre boyunca tam kontrol kaybı.
    // Sonrasında kontrol geri gelir ama korumalı dönem (i-frame)
    // PlayerController.hitInvincibilityTimer ile devam eder.
    private float timer;
    private readonly float lockDuration;
    private readonly float invincibleDuration;

    // Knockback hızının uygulandığı süre.
    // Süre bitince yatay hız sıfırlanır.
    private float knockbackTimer;
    private bool knockbackEnded;

    private Vector2 knockbackDir;
    private float knockbackForce;
    private float knockbackVerticalForce;
    private float knockbackDuration;
    private float knockbackDeceleration;

    public PlayerHurtState(
        PlayerController player,
        PlayerStateMachine sm,
        Vector2 hitDirection,
        float force = 8f,
        float verticalForce = -1f,
        float knockbackDuration = 0.1f,
        float lockDuration = 0.22f,
        float invincibleDuration = 0.7f,
        float knockbackDeceleration = 40f)
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
        this.knockbackDeceleration = knockbackDeceleration;

        this.lockDuration =
            Mathf.Max(0.01f, lockDuration);

        // Korumalı dönem sersemlemeden kısa olamaz.
        this.invincibleDuration =
            Mathf.Max(this.lockDuration, invincibleDuration);
    }

    public void Enter()
    {
        timer = lockDuration;
        knockbackTimer = knockbackDuration;
        knockbackEnded = false;

        // Korumalı dönem: sersemleme + sonrası.
        // (Ayrı sayaç; Dash gibi state'ler isInvincible'ı kapatınca
        // bu koruma silinmez.)
        player.hitInvincibilityTimer =
            Mathf.Max(
                player.hitInvincibilityTimer,
                invincibleDuration
            );

        player.canControl = false;

        // Hurt sırasında saldırı yapılamasın.
        player.isAttackLocked = true;

        // Savunma (sağ tık) da başlatılamasın:
        // PlayerDefenseController.StartDefense inputLocked'a bakıyor.
        player.inputLocked = true;
        player.inputLockTimer = lockDuration + 0.1f;

        // Devam eden saldırı varsa iptal et.
        // (AttackState.Exit yatay hızı sıfırladığı için
        // knockback'ten ÖNCE çağrılmalı.)
        PlayerCombatController combat =
            player.GetComponent<PlayerCombatController>();

        if (combat != null)
            combat.CancelAttack();

        // Vurulunca guard düşer. (Engellenemez vuruş, block/parry
        // sırasında da vurabilir; savunma state'i açık kalmasın.)
        PlayerDefenseController defense =
            player.GetComponent<PlayerDefenseController>();

        if (defense != null)
            defense.ChangeState(null);

        ApplyKnockback();
    }

    public void Exit()
    {
        player.canControl = true;
        player.isAttackLocked = false;

        player.inputLocked = false;
        player.inputLockTimer = 0f;
    }

    public void Update()
    {
        float dt = Time.deltaTime;

        timer -= dt;

        if (!knockbackEnded)
        {
            // 1) Sabit hızla savrulma fazı (knockbackDuration kadar).
            knockbackTimer -= dt;

            if (knockbackTimer <= 0f)
            {
                knockbackEnded = true;

                // Yavaşlama kapalıysa (<= 0) eski davranış: ani dur.
                if (knockbackDeceleration <= 0f)
                {
                    player.SetVelocity(
                        new Vector2(
                            0f,
                            player.rb.linearVelocity.y
                        )
                    );
                }
            }
        }
        else if (knockbackDeceleration > 0f)
        {
            // 2) Yavaşlayarak durma fazı: "duvara çarpma" hissi yok.
            float newX =
                Mathf.MoveTowards(
                    player.rb.linearVelocity.x,
                    0f,
                    knockbackDeceleration * dt
                );

            player.SetVelocity(
                new Vector2(
                    newX,
                    player.rb.linearVelocity.y
                )
            );
        }

        // (46. adım) Sersemlemenin ikinci yarısında dash ile çık
        // (korumalı dönem devam eder).
        if (
            timer > 0f &&
            timer <= lockDuration * (1f - player.hurtDashCancelAfter) &&
            player.dashBufferTimer > 0f &&
            player.dashCooldownTimer <= 0f &&
            (player.GetComponent<Health>() == null || !player.GetComponent<Health>().IsDead)
        )
        {
            // Kilitliyken yön girişi sıfırlanıyor: dash yönü için tuşu oku.
            player.moveInput = Input.GetAxisRaw("Horizontal");

            sm.ChangeState(new DashState(player, sm));
            return;
        }

        if (timer <= 0f)
        {
            // Havadaysan GroundedState'e dönmek bedava coyote jump
            // verirdi; bu yüzden zemine göre seç.
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