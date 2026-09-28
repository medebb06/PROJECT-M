using UnityEngine;
using System.Collections;

public class Enemy : MonoBehaviour, IDamageable
{
    [Header("Invulnerability")]
    [SerializeField] private float invulnerabilityDuration = 0.06f;

    private EnemyController controller;
    private Health health;

    private bool isInvulnerable;

    void Awake()
    {
        controller = GetComponent<EnemyController>();
        health = GetComponent<Health>();
    }

    void OnEnable()
    {
        if (health != null)
        {
            health.OnDeath += Die;
        }
    }

    void OnDisable()
    {
        if (health != null)
        {
            health.OnDeath -= Die;
        }
    }

    public void TakeDamage(int damage, Vector2 hitDirection)
    {
        if (isInvulnerable)
            return;

        if (health == null)
            return;

        if (health.IsDead)
            return;

        int healthBefore = health.CurrentHealth;

        health.TakeDamage(damage);

        // Hasar gerçekten uygulandı mı?
        if (health.CurrentHealth == healthBefore)
            return;

        // Ölüm darbesiyse HitState'e girme.
        if (health.IsDead)
            return;

        // Enemy saldırıyı bırakıp HitState'e girer.
        if (controller != null)
        {
            controller.ChangeState(
                new EnemyHitState(
                    controller,
                    hitDirection
                )
            );
        }

        StartCoroutine(IFrame());
    }

    private void Die()
    {
        Destroy(gameObject);
    }

    private IEnumerator IFrame()
    {
        isInvulnerable = true;

        yield return new WaitForSeconds(invulnerabilityDuration);

        isInvulnerable = false;
    }
}