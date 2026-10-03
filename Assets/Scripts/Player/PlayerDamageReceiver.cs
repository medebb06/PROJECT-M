using UnityEngine;

public class PlayerDamageReceiver : MonoBehaviour, IDamageable
{
    [Header("References")]
    [SerializeField] private Health health;
    [SerializeField] private PlayerController player;

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
    // (Eskiden hem HurtState hem PlayerKnockback aynı anda
    // hızı eziyordu ve birbirini bozuyordu.)
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

        // Player zaten dokunulmazsa yeni damage alma.
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
                knockbackDuration
            )
        );
    }
}