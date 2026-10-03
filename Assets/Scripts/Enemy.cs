using UnityEngine;
using System.Collections;

public class Enemy : MonoBehaviour, IDamageable
{
    [Header("Invulnerability")]
    [SerializeField] private float invulnerabilityDuration = 0.06f;

    private EnemyController controller;
    private Health health;
    private EnemyBalance balance;
    private EnemyHitFeedback hitFeedback;

    private bool isInvulnerable;

    void Awake()
    {
        controller =
            GetComponent<EnemyController>();

        health =
            GetComponent<Health>();

        balance =
            GetComponent<EnemyBalance>();

        hitFeedback =
            GetComponent<EnemyHitFeedback>();
    }

    void OnEnable()
    {
        if (health != null)
            health.OnDeath += Die;

        if (balance != null)
            balance.OnBalanceBroken +=
                HandleBalanceBroken;
    }

    void OnDisable()
    {
        if (health != null)
            health.OnDeath -= Die;

        if (balance != null)
            balance.OnBalanceBroken -=
                HandleBalanceBroken;
    }

    public void TakeDamage(
        int damage,
        Vector2 hitDirection
    )
    {
        if (isInvulnerable)
            return;

        if (health == null)
            return;

        if (health.IsDead)
            return;

        if (damage <= 0)
            return;

        // ==================================================
        // STAGGER
        // ==================================================

        if (controller != null &&
            controller.IsStaggered)
        {
            DealHealthDamage(
                damage,
                hitDirection
            );

            return;
        }

        // ==================================================
        // NORMAL
        // BALANCE DAMAGE ONLY
        // ==================================================

        if (balance == null)
            return;

        bool balanceChanged =
            balance.AddBalanceDamage(damage);

        if (!balanceChanged)
            return;

        Debug.Log(
            "ENEMY BALANCE: " +
            balance.CurrentBalance +
            "/" +
            balance.MaxBalance
        );

        // ==================================================
        // BALANCE BREAK
        // ==================================================

        if (balance.IsBroken)
        {
            if (controller != null)
            {
                controller.ApplyAttackHit(
                    hitDirection,
                    false
                );
            }

            return;
        }

        // ==================================================
        // BALANCE HIT FEEDBACK
        // ==================================================

        if (hitFeedback != null)
        {
            hitFeedback.PlayBalanceHit(
                hitDirection
            );
        }

        // ==================================================
        // SMALL BALANCE HIT KNOCKBACK
        // ==================================================

        if (controller != null)
        {
            controller.ApplyBalanceHit(
                hitDirection
            );
        }

        StartCoroutine(IFrame());
    }

    private void DealHealthDamage(
        int damage,
        Vector2 hitDirection
    )
    {
        int healthBefore =
            health.CurrentHealth;

        health.TakeDamage(damage);

        if (health.CurrentHealth == healthBefore)
            return;

        Debug.Log(
            "STAGGER → ENEMY HEALTH DAMAGE: " +
            damage
        );

        if (hitFeedback != null)
        {
            hitFeedback.PlayHealthHit(
                hitDirection
            );
        }

        if (health.IsDead)
            return;

        // ==================================================
        // HEALTH KNOCKBACK
        // ==================================================

        if (controller != null)
        {
            controller.ApplyAttackHit(
                hitDirection,
                true
            );
        }

        StartCoroutine(IFrame());
    }

    private void HandleBalanceBroken()
    {
        Debug.Log(
            gameObject.name +
            " BALANCE BROKEN!"
        );

        // ==================================================
        // BALANCE BREAK IMPACT
        // ==================================================

        if (controller != null &&
            controller.target != null)
        {
            CombatImpactFeedback combatFeedback =
                controller.target.GetComponent<
                    CombatImpactFeedback
                >();

            if (combatFeedback != null)
            {
                combatFeedback.PlayBalanceBreakImpact();

                Debug.Log(
                    "BALANCE BREAK IMPACT PLAYED!"
                );
            }
            else
            {
                Debug.LogWarning(
                    "CombatImpactFeedback Player üzerinde bulunamadı!"
                );
            }
        }

        // ==================================================
        // ENEMY BALANCE BREAK FEEDBACK
        // ==================================================

        if (hitFeedback != null)
        {
            hitFeedback.PlayBalanceBreak(
                Vector2.zero
            );
        }

        // ==================================================
        // STAGGER
        // ==================================================

        if (controller == null)
            return;

        controller.ForceStagger();
    }

    private void Die()
    {
        Destroy(gameObject);
    }

    private IEnumerator IFrame()
    {
        isInvulnerable = true;

        yield return new WaitForSeconds(
            invulnerabilityDuration
        );

        isInvulnerable = false;
    }
}