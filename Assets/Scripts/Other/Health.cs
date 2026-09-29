
using UnityEngine;
using System;
using System.Collections;

public class Health : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private int currentHealth;

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

    void Awake()
    {
        currentHealth = maxHealth;
        IsDead = false;

        sprites = GetComponentsInChildren<SpriteRenderer>();
    }

    public void Revive()
    {
        currentHealth = maxHealth;
        IsDead = false;
        fading = false;

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
        currentHealth = Mathf.Max(currentHealth, 0);

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
        currentHealth = Mathf.Min(
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

        currentHealth = Mathf.Clamp(
            amount,
            0,
            maxHealth
        );

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

        // Fizik ve collider hemen devre dışı.
        DisablePhysics();

        // Fade başlasın.
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
                Mathf.Clamp01(elapsed / fadeDuration);

            float alpha =
                Mathf.Lerp(1f, 0f, progress);

            SetAlpha(alpha);

            yield return null;
        }

        SetAlpha(0f);

        // Fade bittikten sonra ölüm event'i.
        OnDeath?.Invoke();

        Destroy(gameObject);
    }

    private void DisablePhysics()
    {
        Rigidbody2D rb =
            GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
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

            Color color = sprite.color;
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

