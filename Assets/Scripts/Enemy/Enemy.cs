using UnityEngine;
using System.Collections;

public class Enemy : MonoBehaviour, IDamageable
{
    public Rigidbody2D rb;

    [Header("Knockback")]
    public float knockDuration = 0.12f;

    bool isKnocked;
    Coroutine knockRoutine;

    PhysicsMaterial2D mat;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        var col = GetComponent<Collider2D>();

        PhysicsMaterial2D mat = new PhysicsMaterial2D();
        mat.friction = 0f;
        mat.bounciness = 0f;

        col.sharedMaterial = mat;

        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    // =========================
    // DAMAGE ENTRY
    // =========================
    public void TakeDamage(int dmg, Vector2 kb)
    {
        Debug.Log($"🔥 DAMAGE RECEIVED | dmg:{dmg} kb:{kb}");

        if (knockRoutine != null)
            StopCoroutine(knockRoutine);

        knockRoutine = StartCoroutine(KnockbackRoutine(kb));
    }

    // =========================
    // CLEAN KNOCKBACK SYSTEM
    // =========================
    IEnumerator KnockbackRoutine(Vector2 kb)
    {
        isKnocked = true;

        rb.linearVelocity = Vector2.zero;

        Vector2 start = rb.position;
        Vector2 target = start + kb;

        float t = 0f;

        Debug.Log("💥 KNOCKBACK START");

        while (t < knockDuration)
        {
            t += Time.deltaTime;

            float curve = t / knockDuration;

            // 🔥 smooth + güçlü hissiyat
            rb.MovePosition(Vector2.Lerp(start, target, curve));

            yield return null;
        }

        rb.linearVelocity = Vector2.zero;

        isKnocked = false;
        knockRoutine = null;
    }

    // =========================
    // NO PHYSICS PUSH RULE
    // =========================
    void OnCollisionStay2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Player"))
        {
            // ❌ fiziksel push engeli
            rb.linearVelocity = Vector2.zero;
        }
    }

    void FixedUpdate()
    {
        if (isKnocked)
            return;

        // AI burada çalışır
    }

    public bool CanBeHitDuringDash()
    {
        return true;
    }
}