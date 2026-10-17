using UnityEngine;

public class SquashStretch : MonoBehaviour
{
    public Rigidbody2D rb;
    public Transform visual;

    public System.Func<bool> IsGrounded;

    [Header("Curves")]
    public AnimationCurve stretchCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve squashCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Intensity")]
    public float maxStretch = 0.25f;
    public float maxSquash = 0.35f;

    [Header("Recovery")]
    public float recoverySpeed = 14f;

    [Header("Impact")]
    public float impactDuration = 0.12f;

    Vector3 targetScale = Vector3.one;
    Vector3 vel;

    float lastYVel;
    bool wasGrounded;

    float impactTimer;
    float impactValue;

    // Disaridan tetiklenen kisa squash/stretch (dash basi / sonu gibi).
    Vector2 punchScale = Vector2.one;
    float punchTime;
    float punchTimer;

    /// <summary>Verilen olcekle baslar, 'duration' icinde 1'e doner (yumusak). x = yatay, y = dikey.</summary>
    public void Punch(Vector2 scale, float duration)
    {
        punchScale = scale;
        punchTime = Mathf.Max(0.02f, duration);
        punchTimer = punchTime;
    }

    void LateUpdate()
    {
        if (rb == null || visual == null) return;

        float yVel = rb.linearVelocity.y;
        bool grounded = IsGrounded != null && IsGrounded();

        // ---------------- LANDING TRIGGER ----------------
        if (!wasGrounded && grounded)
        {
            float impact01 = Mathf.InverseLerp(0f, -20f, lastYVel);
            float curveValue = squashCurve.Evaluate(impact01);

            impactValue = curveValue * maxSquash;
            impactTimer = impactDuration;
        }

        // ---------------- PUNCH (dash vb.: en yuksek oncelik) ----------------
        if (punchTimer > 0f)
        {
            punchTimer -= Time.deltaTime;
            float k = Mathf.Clamp01(punchTimer / punchTime);   // 1 -> 0
            k = k * k * (3f - 2f * k);
            targetScale = new Vector3(
                Mathf.Lerp(1f, punchScale.x, k),
                Mathf.Lerp(1f, punchScale.y, k),
                1f
            );
        }
        // ---------------- IMPACT (priority override) ----------------
        else if (impactTimer > 0f)
        {
            impactTimer -= Time.deltaTime;

            targetScale = new Vector3(
                1f + impactValue,
                1f - impactValue,
                1f
            );
        }
        else
        {
            // ---------------- STRETCH ----------------
            if (!grounded && yVel < 0)
            {
                float fallSpeed01 = Mathf.InverseLerp(0f, -20f, yVel);
                float curveValue = stretchCurve.Evaluate(fallSpeed01);

                float stretch = curveValue * maxStretch;

                targetScale = new Vector3(
                    1f - stretch,
                    1f + stretch,
                    1f
                );
            }
            else
            {
                // ---------------- RECOVERY ----------------
                targetScale = Vector3.Lerp(targetScale, Vector3.one, Time.deltaTime * recoverySpeed);
            }
        }

        // ---------------- FLIP SAFE ----------------
        float xSign = Mathf.Sign(visual.localScale.x);

        Vector3 finalScale = new Vector3(
            xSign * targetScale.x,
            targetScale.y,
            1f
        );

        visual.localScale = Vector3.SmoothDamp(
            visual.localScale,
            finalScale,
            ref vel,
            0.05f
        );

        lastYVel = yVel;
        wasGrounded = grounded;
    }
}