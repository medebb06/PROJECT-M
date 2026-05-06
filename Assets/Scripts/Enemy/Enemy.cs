using UnityEngine;

public class Enemy : MonoBehaviour, IDamageable
{
    public int hp = 3;

    private Rigidbody2D rb;

    public float knockbackMultiplier = 1f;
    public float knockbackControlLock = 0.15f;

    bool isStunned;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>(); // 🔥 otomatik bağla
    }

    public void TakeDamage(int damage, Vector2 knockback)
    {
        hp -= damage;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(knockback * knockbackMultiplier, ForceMode2D.Impulse);
        }

        if (!isStunned)
            StartCoroutine(StunRoutine());

        if (hp <= 0)
            Destroy(gameObject);
    }

    System.Collections.IEnumerator StunRoutine()
    {
        isStunned = true;
        yield return new WaitForSeconds(knockbackControlLock);
        isStunned = false;
    }
}