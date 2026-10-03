using UnityEngine;
using System;
using System.Collections;

public class Health : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private int currentHealth;

    [Header("Death")]
    [SerializeField] private bool destroyOnDeath = true;

    [Header("Health Recovery")]
    [SerializeField] private bool enableRecovery = false;
    [SerializeField] private float recoveryDelay = 2f;
    [SerializeField] private float recoverySpeed = 0.25f;

    [Header("Death Fade")]
    [SerializeField] private bool fadeOnDeath = true;
    [SerializeField] private float fadeDuration = 0.35f;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead { get; private set; }

    public event Action<int, int> OnHealthChanged;
    public event Action OnDeath;

    private SpriteRenderer[] sprites;
    private bool fading;

    private float recoveryTimer;
    private float recoveryAccumulator;

    private bool isPlayer;

    private void Awake()
    {
        currentHealth = maxHealth;
        IsDead = false;

        sprites =
            GetComponentsInChildren<
                SpriteRenderer
            >();

        recoveryTimer = 0f;
        recoveryAccumulator = 0f;

        // Player hiçbir zaman Health tarafından Destroy edilmez.
        isPlayer = GetComponent<PlayerController>() != null;
    }

    private void Update()
    {
        HandleRecovery();
    }

    private void HandleRecovery()
    {
        if (!enableRecovery)
            return;

        if (IsDead)
            return;

        if (currentHealth >= maxHealth)
            return;

        recoveryTimer -= Time.deltaTime;

        if (recoveryTimer > 0f)
            return;

        recoveryAccumulator +=
            recoverySpeed * Time.deltaTime;

        int recoveryAmount =
            Mathf.FloorToInt(
                recoveryAccumulator
            );

        if (recoveryAmount <= 0)
            return;

        recoveryAccumulator -=
            recoveryAmount;

        int previousHealth =
            currentHealth;

        currentHealth +=
            recoveryAmount;

        currentHealth =
            Mathf.Clamp(
                currentHealth,
                0,
                maxHealth
            );

        if (currentHealth != previousHealth)
        {
            OnHealthChanged?.Invoke(
                currentHealth,
                maxHealth
            );
        }
    }

    public void Revive()
    {
        StopAllCoroutines();

        currentHealth =
            maxHealth;

        IsDead = false;
        fading = false;

        recoveryTimer = 0f;
        recoveryAccumulator = 0f;

        RestoreVisuals();

        OnHealthChanged?.Invoke(
            currentHealth,
            maxHealth
        );
    }

    public void TakeDamage(int damage)
    {
        if (IsDead)
            return;

        if (damage <= 0)
            return;

        currentHealth -= damage;

        currentHealth =
            Mathf.Max(
                currentHealth,
                0
            );

        recoveryTimer =
            recoveryDelay;

        recoveryAccumulator = 0f;

        OnHealthChanged?.Invoke(
            currentHealth,
            maxHealth
        );

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (IsDead)
            return;

        if (amount <= 0)
            return;

        currentHealth +=
            amount;

        currentHealth =
            Mathf.Min(
                currentHealth,
                maxHealth
            );

        OnHealthChanged?.Invoke(
            currentHealth,
            maxHealth
        );
    }

    // Kendiliğinden can yenilenmesini çalışma anında aç/kapat.
    // (Koşuda kapatılır: hasar yiyip yenilenerek vurmak parry'den
    // daha kârlı olmasın.)
    public void SetRecoveryEnabled(bool enabled)
    {
        enableRecovery = enabled;

        recoveryTimer = 0f;
        recoveryAccumulator = 0f;
    }

    // Çalışma anında max canı ayarlar (ör. bölümle ölçeklenen düşmanlar).
    // refill: true ise can yeni maksimuma doldurulur.
    public void SetMaxHealth(int newMax, bool refill = true)
    {
        maxHealth = Mathf.Max(1, newMax);

        currentHealth =
            refill
                ? maxHealth
                : Mathf.Min(currentHealth, maxHealth);

        OnHealthChanged?.Invoke(
            currentHealth,
            maxHealth
        );
    }

    public void SetHealth(int amount)
    {
        if (IsDead)
            return;

        currentHealth =
            Mathf.Clamp(
                amount,
                0,
                maxHealth
            );

        recoveryTimer =
            recoveryDelay;

        recoveryAccumulator = 0f;

        OnHealthChanged?.Invoke(
            currentHealth,
            maxHealth
        );

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (IsDead)
            return;

        IsDead = true;

        DisablePhysics();

        // =====================================================
        // PLAYER
        // =====================================================

        // Player hiçbir koşulda burada Destroy edilmez.
        if (isPlayer)
        {
            OnDeath?.Invoke();
            return;
        }

        // =====================================================
        // ENEMY / NORMAL OBJECT
        // =====================================================

        if (!destroyOnDeath)
        {
            OnDeath?.Invoke();
            return;
        }

        if (fadeOnDeath)
        {
            StartCoroutine(
                FadeOut()
            );
        }
        else
        {
            OnDeath?.Invoke();

            Destroy(gameObject);
        }
    }

    private IEnumerator FadeOut()
    {
        fading = true;

        float elapsed = 0f;

        while (
            elapsed <
            fadeDuration
        )
        {
            elapsed +=
                Time.deltaTime;

            float progress =
                Mathf.Clamp01(
                    elapsed /
                    fadeDuration
                );

            float alpha =
                Mathf.Lerp(
                    1f,
                    0f,
                    progress
                );

            SetAlpha(alpha);

            yield return null;
        }

        SetAlpha(0f);

        OnDeath?.Invoke();

        Destroy(gameObject);
    }

    private void DisablePhysics()
    {
        Rigidbody2D rb =
            GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.linearVelocity =
                Vector2.zero;

            rb.simulated = false;
        }

        Collider2D[] colliders =
            GetComponentsInChildren<
                Collider2D
            >();

        foreach (
            Collider2D col in
            colliders
        )
        {
            col.enabled = false;
        }
    }

    private void SetAlpha(float alpha)
    {
        if (sprites == null)
            return;

        foreach (
            SpriteRenderer sprite in
            sprites
        )
        {
            if (sprite == null)
                continue;

            Color color =
                sprite.color;

            color.a = alpha;

            sprite.color =
                color;
        }
    }

    private void RestoreVisuals()
    {
        SetAlpha(1f);

        Rigidbody2D rb =
            GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.simulated = true;
        }

        Collider2D[] colliders =
            GetComponentsInChildren<
                Collider2D
            >();

        foreach (
            Collider2D col in
            colliders
        )
        {
            col.enabled = true;
        }
    }
}