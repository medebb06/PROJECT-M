using UnityEngine;

public class PlayerDamageReceiver : MonoBehaviour, IDamageable
{
    [Header("References")]
    [SerializeField] private Health health;
    [SerializeField] private PlayerController player;

    [Header("Hit Reaction")]
    [SerializeField] private float knockbackForce = 8f;

    void Awake()
    {
        if (health == null)
            health = GetComponent<Health>();

        if (player == null)
            player = GetComponent<PlayerController>();
    }

    public void TakeDamage(int damage, Vector2 hitDirection)
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
                knockbackForce
            )
        );
    }
}