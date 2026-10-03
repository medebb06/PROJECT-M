using UnityEngine;

public class PlayerDamageReceiver : MonoBehaviour, IDamageable
{
    [Header("References")]
    [SerializeField] private Health health;
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerInvincibilityBlink blink;

    [Header("Hit Reaction")]
    [Tooltip(
        "Hasar alınca TAM kontrol kaybı (sersemleme) süresi. " +
        "Hareket, zıplama, dash, saldırı ve savunma kapalı.")]
    [SerializeField] private float hurtLockDuration = 0.22f;

    [Tooltip(
        "Hasar alınınca TOPLAM dokunulmazlık süresi (sersemleme dahil). " +
        "Sersemleme bitince kontrol geri gelir ama oyuncu bu süre " +
        "dolana kadar hâlâ korumalıdır ve sprite yanıp söner.")]
    [SerializeField] private float invincibilityDuration = 0.7f;

    [Header("Default Knockback")]
    [SerializeField] private float defaultKnockbackForce = 8f;
    [SerializeField] private float defaultKnockbackVerticalForce = 4.8f;
    [SerializeField] private float defaultKnockbackDuration = 0.1f;

    void Awake()
    {
        if (health == null)
            health = GetComponent<Health>();

        if (player == null)
            player = GetComponent<PlayerController>();

        // Yanıp sönme bileşeni yoksa kendiliğinden eklenir.
        if (blink == null)
            blink = GetComponent<PlayerInvincibilityBlink>();

        if (blink == null)
            blink = gameObject.AddComponent<PlayerInvincibilityBlink>();
    }

    private void OnValidate()
    {
        hurtLockDuration =
            Mathf.Max(0.01f, hurtLockDuration);

        invincibilityDuration =
            Mathf.Max(hurtLockDuration, invincibilityDuration);
    }

    // --------------------------------------------------
    // IDamageable
    // Varsayılan knockback değerleriyle hasar al.
    // --------------------------------------------------

    public void TakeDamage(int damage, Vector2 hitDirection)
    {
        TakeDamage(
            damage,
            hitDirection,
            defaultKnockbackForce,
            defaultKnockbackVerticalForce,
            defaultKnockbackDuration
        );
    }

    // --------------------------------------------------
    // Hasar + knockback TEK yerden.
    // Knockback'i PlayerHurtState uygular.
    // --------------------------------------------------

    public void TakeDamage(
        int damage,
        Vector2 hitDirection,
        float knockbackForce,
        float knockbackVerticalForce,
        float knockbackDuration
    )
    {
        if (health == null)
            return;

        if (player == null)
            return;

        // Dash i-frame'i VEYA hasar sonrası korumalı dönem.
        if (player.isInvincible)
            return;

        if (health.IsDead)
            return;

        // --------------------------------
        // DAMAGE
        // --------------------------------

        health.TakeDamage(damage);

        // --------------------------------
        // ÖLDÜ
        // --------------------------------

        if (health.IsDead)
        {
            // Yanıp sönme alfa'yı düşük bırakmasın:
            // ölüm fade'i bu değerden başlar.
            player.hitInvincibilityTimer = 0f;

            if (blink != null)
                blink.ResetVisuals();

            player.stateMachine.ChangeState(
                new PlayerDeathState(
                    player,
                    player.stateMachine
                )
            );

            return;
        }

        // --------------------------------
        // NORMAL DAMAGE
        // --------------------------------

        player.stateMachine.ChangeState(
            new PlayerHurtState(
                player,
                player.stateMachine,
                hitDirection,
                knockbackForce,
                knockbackVerticalForce,
                knockbackDuration,
                hurtLockDuration,
                invincibilityDuration
            )
        );
    }
}