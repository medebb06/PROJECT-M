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

            // Posture kırıldıysa burada sadece
            // posture feedback oynar.
            //
            // Aynı vuruş HP'ye geçmez.
            if (posture.IsBroken)
            {
                if (hitFeedback != null)
                {
                    hitFeedback.PlayPostureBreak(
                        hitDirection
                    );
                }

                EnterHitState(hitDirection);
            }

            StartCoroutine(IFrame());

            return;
        }

        // --------------------------------
        // HEALTH DAMAGE
        // --------------------------------

        int healthBefore = health.CurrentHealth;

        health.TakeDamage(damage);

        // Hasar gerçekten uygulandı mı?
        if (health.CurrentHealth == healthBefore)
            return;

        if (hitFeedback != null)
        {
            hitFeedback.PlayHealthHit(hitDirection);
        }

        // Ölüm darbesiyse HitState'e girme.
        if (health.IsDead)
            return;

        EnterHitState(hitDirection);

        StartCoroutine(IFrame());
    }

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

    private void HandlePostureBroken()
    {
        Debug.Log(
            gameObject.name + " POSTURE BROKEN!"
        );

        // Şimdilik sadece log.
        //
        // Bir sonraki aşamada burada:
        // - StaggerState
        // - posture break animasyonu
        // - finisher window
        // - hit stop
        // gibi sistemleri bağlayacağız.
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