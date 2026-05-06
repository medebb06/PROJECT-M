using UnityEngine;
using System.Collections;

public class Enemy : MonoBehaviour, IDamageable
{
    public Rigidbody2D rb;

    bool isKnocked;
    Coroutine knockRoutine;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    public void TakeDamage(int dmg, Vector2 kb)
    {
        Debug.Log($"🔥 DAMAGE RECEIVED | dmg:{dmg} kb:{kb}");

        if (knockRoutine != null)
            StopCoroutine(knockRoutine);

        knockRoutine = StartCoroutine(KnockbackRoutine(kb));
    }

    IEnumerator KnockbackRoutine(Vector2 kb)
    {
        isKnocked = true;

        // ❗ HARD STOP
        rb.linearVelocity = Vector2.zero;

        // 🔥 instant impulse (stable)
        rb.linearVelocity = kb;

        Debug.Log("💥 KNOCKBACK START");

        float t = 0f;
        float duration = 0.12f;

        while (t < duration)
        {
            t += Time.deltaTime;

            // ❗ stabilize Y so sliding olmaz
            rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.9f, rb.linearVelocity.y);

            yield return null;
        }

        rb.linearVelocity = Vector2.zero;

        isKnocked = false;
        knockRoutine = null;
    }
    public bool CanBeHitDuringDash()
    {
        return true; // şimdilik hep true
    }
    void FixedUpdate()
    {
        if (isKnocked)
            return;

        // AI HERE (important)
    }
}