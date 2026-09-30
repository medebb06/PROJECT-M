
using UnityEngine;
using System;
using System.Collections;

public class Health : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private int currentHealth;

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

    void Awake()
    {
        currentHealth = maxHealth;
        IsDead = false;

        sprites = GetComponentsInChildren<SpriteRenderer>();

        recoveryTimer = 0f;
        recoveryAccumulator = 0f;
    }

    void Update()
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

        recoveryAccumulator -= recoveryAmount;

        int previousHealth =
            currentHealth;

        currentHealth += recoveryAmount;

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
        currentHealth = maxHealth;
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

        // Hasar alınca recovery yeniden beklemeye başlar.
        recoveryTimer = recoveryDelay;
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

        currentHealth += amount;

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

        // SetHealth dışarıdan hasar gibi kullanılırsa
        // recovery timer'ı da sıfırlanır.
        recoveryTimer = recoveryDelay;
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

        if (fadeOnDeath)
        {
            StartCoroutine(FadeOut());
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

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;

            float progress =
                Mathf.Clamp01(
                    elapsed / fadeDuration
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
            GetComponentsInChildren<Collider2D>();

        foreach (Collider2D col in colliders)
        {
            col.enabled = false;
        }
    }

    private void SetAlpha(float alpha)
    {
        if (sprites == null)
            return;

        foreach (SpriteRenderer sprite in sprites)
        {
            if (sprite == null)
                continue;

            Color color =
                sprite.color;

            color.a = alpha;

            sprite.color = color;
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
            GetComponentsInChildren<Collider2D>();

        foreach (Collider2D col in colliders)
        {
            col.enabled = true;
        }
    }
}
