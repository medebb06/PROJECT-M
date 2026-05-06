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

        // ---------------- IMPACT (priority override) ----------------
        if (impactTimer > 0f)
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