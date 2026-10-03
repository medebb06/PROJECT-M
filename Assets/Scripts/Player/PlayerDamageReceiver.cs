using UnityEngine;

// Vurulunca uygulanan slow-mo profili.
// Ağır başlar, zamanla normal hıza döner:
//   timeScale(t) = Lerp(startTimeScale, 1, t ^ rampPower)
[System.Serializable]
public class HitSlowMotion
{
    public bool enabled = true;

    [Tooltip("Slow-mo'nun toplam süresi (GERÇEK zaman, saniye).")]
    [Min(0f)]
    public float duration = 0.35f;

    [Tooltip("Başlangıç zaman hızı. Küçük = daha ağır başlar (0.1 = %10 hız).")]
    [Range(0f, 1f)]
    public float startTimeScale = 0.12f;

    [Tooltip(
        "1 = düz hızlanma. 2 = bir süre ağır kalır sonra hızlanır. " +
        "3 = daha da geç hızlanır.")]
    [Min(1f)]
    public float rampPower = 2f;

    public HitSlowMotion() { }

    public HitSlowMotion(
        float duration,
        float startTimeScale,
        float rampPower
    )
    {
        this.duration = duration;
        this.startTimeScale = startTimeScale;
        this.rampPower = rampPower;
    }
}

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

    [Header("Hit Slow-Mo")]
    [Tooltip(
        "Vurulduktan sonra kalan can bu değer veya altındaysa " +
        "'Low Health' profili kullanılır (1 = son can birimi).")]
    [SerializeField] private int lowHealthThreshold = 1;

    [Tooltip("Normal hasar.")]
    [SerializeField]
    private HitSlowMotion normalHitSlowMo =
        new HitSlowMotion(0.35f, 0.12f, 2f);

    [Tooltip("Hasar sonrası son can (veya eşik) kaldığında: daha uzun ve ağır.")]
    [SerializeField]
    private HitSlowMotion lowHealthSlowMo =
        new HitSlowMotion(0.6f, 0.08f, 2f);

    [Tooltip("Ölümcül vuruş: en uzun ve en ağır.")]
    [SerializeField]
    private HitSlowMotion lethalSlowMo =
        new HitSlowMotion(1.2f, 0.05f, 2.5f);

    [Header("Default Knockback")]
    [SerializeField] private float defaultKnockbackForce = 8f;
    [SerializeField] private float defaultKnockbackVerticalForce = 4.8f;
    [SerializeField] private float defaultKnockbackDuration = 0.1f;

    [Tooltip(
        "Savrulma sonunda yatay hızın ne kadar hızlı sıfırlandığı " +
        "(birim/sn²). Küçük = uzun kayar, büyük = çabuk durur, " +
        "0 = ani dur (eski davranış). Ek kayma mesafesi ≈ hız² / (2 × bu değer).")]
    [SerializeField] private float defaultKnockbackDeceleration = 40f;

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
    // SLOW-MO
    // --------------------------------------------------

    private void PlayHitSlowMotion()
    {
        HitSlowMotion profile;
        int priority = 0;

        if (health.IsDead)
        {
            profile = lethalSlowMo;

            // Denge kırılma hit-stop'uyla (10) birlikte çalışsın.
            priority = 10;
        }
        else if (health.CurrentHealth <= lowHealthThreshold)
        {
            profile = lowHealthSlowMo;
        }
        else
        {
            profile = normalHitSlowMo;
        }

        if (profile == null || !profile.enabled)
            return;

        HitStop.RequestRamp(
            profile.duration,
            profile.startTimeScale,
            1f,
            profile.rampPower,
            priority
        );
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
            defaultKnockbackDuration,
            defaultKnockbackDeceleration
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
        float knockbackDuration,
        float knockbackDeceleration = -1f
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

        // Vurulma flaşı (ölümcül vuruşta da oynar).
        if (blink != null)
            blink.PlayHitFlash();

        // Vurulma slow-mo'su: normal / son can / ölümcül.
        PlayHitSlowMotion();

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
                invincibilityDuration,
                knockbackDeceleration >= 0f
                    ? knockbackDeceleration
                    : defaultKnockbackDeceleration
            )
        );
    }
}