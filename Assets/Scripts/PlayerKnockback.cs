using UnityEngine;

public class PlayerKnockback : MonoBehaviour
{
    [Header("Default Knockback")]
    [SerializeField] private float defaultForce = 8f;
    [SerializeField] private float defaultVerticalForce = 1f;
    [SerializeField] private float defaultDuration = 0.12f;

    private Rigidbody2D rb;

    private float timer;
    private bool isKnockedBack;

    public bool IsKnockedBack => isKnockedBack;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (!isKnockedBack)
            return;

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            EndKnockback();
        }
    }

    // --------------------------------------------------
    // DEFAULT KNOCKBACK
    // --------------------------------------------------

    public void ApplyKnockback(Vector2 direction)
    {
        ApplyKnockback(
            direction,
            defaultForce,
            defaultVerticalForce,
            defaultDuration
        );
    }

    // --------------------------------------------------
    // CUSTOM KNOCKBACK
    // --------------------------------------------------

    public void ApplyKnockback(
        Vector2 direction,
        float force,
        float verticalForce,
        float duration
    )
    {
        if (rb == null)
            return;

        if (direction == Vector2.zero)
            return;

        isKnockedBack = true;

        timer = duration;

        // Sadece yatay yönü kullanıyoruz.
        float horizontalDirection =
            Mathf.Sign(direction.x);

        // Mevcut hareketi temizle.
        rb.linearVelocity = Vector2.zero;

        // Yeni knockback.
        rb.linearVelocity = new Vector2(
            horizontalDirection * force,
            verticalForce
        );
    }

    private void EndKnockback()
    {
        isKnockedBack = false;

        if (rb != null)
        {
            // Knockback sonrası yatay momentum kalmasın.
            rb.linearVelocity = new Vector2(
                0f,
                rb.linearVelocity.y
            );
        }
    }
}