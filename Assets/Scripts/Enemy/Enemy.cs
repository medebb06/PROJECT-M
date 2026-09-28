using UnityEngine;
using System.Collections;

public class Enemy : MonoBehaviour, IDamageable
{
    [Header("Invulnerability")]
    [SerializeField] private float invulnerabilityDuration = 0.06f;

    private EnemyController controller;
    private Health health;
    private EnemyPosture posture;
    private EnemyHitFeedback hitFeedback;

    private bool isInvulnerable;

    void Awake()
    {
        controller = GetComponent<EnemyController>();
        health = GetComponent<Health>();
        posture = GetComponent<EnemyPosture>();
        hitFeedback = GetComponent<EnemyHitFeedback>();
    }

    void OnEnable()
    {
        if (health != null)
        {
            health.OnDeath += Die;
        }

        if (posture != null)
        {
            posture.OnPostureBroken += HandlePostureBroken;
        }
    }

    void OnDisable()
    {
        if (health != null)
        {
            health.OnDeath -= Die;
        }

        if (posture != null)
        {
            posture.OnPostureBroken -= HandlePostureBroken;
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

        // --------------------------------
        // POSTURE DAMAGE
        // --------------------------------

        if (posture != null && !posture.IsBroken)
        {
            bool postureDamaged =
                posture.TakeDamage(damage);

            if (!postureDamaged)
                return;

            if (hitFeedback != null)
            {
                hitFeedback.PlayPostureHit(hitDirection);
            }

            // --------------------------------
            // POSTURE BREAK
            // --------------------------------

            if (posture.IsBroken)
            {
                if (hitFeedback != null)
                {
                    hitFeedback.PlayPostureBreak(
                        hitDirection
                    );
                }
            }

            StartCoroutine(IFrame());

            return;
        }

        // --------------------------------
        // HEALTH DAMAGE
        // --------------------------------

        int healthBefore =
            health.CurrentHealth;

        health.TakeDamage(damage);

        // Hasar gerçekten uygulandı mı?
        if (health.CurrentHealth == healthBefore)
            return;

        if (hitFeedback != null)
        {
            hitFeedback.PlayHealthHit(
                hitDirection
            );
        }

        // --------------------------------
        // DEATH
        // --------------------------------

        if (health.IsDead)
            return;

        // --------------------------------
        // NORMAL HIT
        // --------------------------------

        EnterHitState(hitDirection);

        StartCoroutine(IFrame());
    }

    // =====================================================
    // HIT STATE
    // =====================================================

    private void EnterHitState(Vector2 hitDirection)
    {
        if (controller == null)
            return;

        controller.ChangeState(
            new EnemyHitState(
                controller,
                hitDirection
            )
        );
    }

    // =====================================================
    // POSTURE BROKEN
    // =====================================================

    private void HandlePostureBroken()
    {
        Debug.Log(
            gameObject.name + " POSTURE BROKEN!"
        );

        if (controller == null)
            return;

        controller.ChangeState(
            new EnemyStaggerState(
                controller
            )
        );
    }

    // =====================================================
    // DEATH
    // =====================================================

    private void Die()
    {
        Destroy(gameObject);
    }

    // =====================================================
    // I-FRAME
    // =====================================================

    private IEnumerator IFrame()
    {
        isInvulnerable = true;

        yield return new WaitForSeconds(
            invulnerabilityDuration
        );

        isInvulnerable = false;
    }
}