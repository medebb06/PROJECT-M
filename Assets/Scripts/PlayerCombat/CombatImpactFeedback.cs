using UnityEngine;
using System.Collections;
using Unity.Cinemachine;

public class CombatImpactFeedback : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CinemachineImpulseSource impulseSource;
    [SerializeField] private Transform punchTarget;

    [Header("Normal Attack Impact")]
    [SerializeField] private float attackHitStopTimeScale = 0.05f;
    [SerializeField] private float attackHitStopDuration = 0.04f;

    [Header("Attack Punch")]
    [SerializeField] private float attackPunchAmount = 0.08f;
    [SerializeField] private float attackPunchDuration = 0.06f;

    [Header("Parry Impact")]
    [SerializeField] private float parryHitStopTimeScale = 0.03f;
    [SerializeField] private float parryHitStopDuration = 0.08f;
    [SerializeField] private float parryShakeForce = 0.35f;

    [Header("Parry Punch")]
    [SerializeField] private float parryPunchAmount = 0.12f;
    [SerializeField] private float parryPunchDuration = 0.08f;

    [Header("Block Impact")]
    [SerializeField] private float blockHitStopTimeScale = 0.035f;
    [SerializeField] private float blockHitStopDuration = 0.035f;
    [SerializeField] private float blockShakeForce = 0.25f;

    [Header("Block Punch")]
    [SerializeField] private float blockPunchAmount = 0.06f;
    [SerializeField] private float blockPunchDuration = 0.05f;

    [Header("Balance Break Impact")]
    [SerializeField] private float balanceBreakHitStopTimeScale = 0.01f;
    [SerializeField] private float balanceBreakHitStopDuration = 0.12f;
    [SerializeField] private float balanceBreakShakeForce = 0.8f;

    [Header("Balance Break Punch")]
    [SerializeField] private float balanceBreakPunchAmount = 0.18f;
    [SerializeField] private float balanceBreakPunchDuration = 0.1f;

    private Coroutine hitStopRoutine;
    private Coroutine punchRoutine;

    private Vector3 originalPunchScale;

    // Balance break sırasında normal attack hitstop'un
    // sistemi ezmesini engeller.
    private bool balanceBreakActive;

    private void Awake()
    {
        if (impulseSource == null)
            impulseSource =
                GetComponent<CinemachineImpulseSource>();

        if (punchTarget != null)
            originalPunchScale =
                punchTarget.localScale;
    }

    // =========================================================
    // NORMAL ATTACK
    // =========================================================

    public void PlayAttackImpact()
    {
        if (balanceBreakActive)
        {
            Debug.Log(
                "ATTACK HITSTOP IGNORED -> BALANCE BREAK ACTIVE"
            );

            // Punch yine oynayabilir.
            PlayPunch(
                attackPunchAmount,
                attackPunchDuration
            );

            return;
        }

        PlayHitStop(
            attackHitStopDuration,
            attackHitStopTimeScale,
            false
        );

        PlayPunch(
            attackPunchAmount,
            attackPunchDuration
        );
    }

    // =========================================================
    // PARRY
    // =========================================================

    public void PlayParryImpact()
    {
        PlayHitStop(
            parryHitStopDuration,
            parryHitStopTimeScale,
            false
        );

        PlayScreenShake(parryShakeForce);

        PlayPunch(
            parryPunchAmount,
            parryPunchDuration
        );
    }

    // =========================================================
    // BLOCK
    // =========================================================

    public void PlayBlockImpact()
    {
        PlayHitStop(
            blockHitStopDuration,
            blockHitStopTimeScale,
            false
        );

        PlayScreenShake(blockShakeForce);

        PlayPunch(
            blockPunchAmount,
            blockPunchDuration
        );
    }

    // =========================================================
    // BALANCE BREAK
    // =========================================================

    public void PlayBalanceBreakImpact()
    {
        Debug.Log(
            "BALANCE BREAK IMPACT CALLED"
        );

        balanceBreakActive = true;

        PlayHitStop(
            balanceBreakHitStopDuration,
            balanceBreakHitStopTimeScale,
            true
        );

        PlayScreenShake(
            balanceBreakShakeForce
        );

        PlayPunch(
            balanceBreakPunchAmount,
            balanceBreakPunchDuration
        );
    }

    // =========================================================
    // HITSTOP
    // =========================================================

    private void PlayHitStop(
        float duration,
        float timeScale,
        bool isBalanceBreak
    )
    {
        // Balance break zaten aktifse,
        // normal hitstop hiçbir şekilde üzerine yazamaz.
        if (
            balanceBreakActive &&
            !isBalanceBreak
        )
        {
            Debug.Log(
                "HITSTOP IGNORED -> BALANCE BREAK ACTIVE"
            );

            return;
        }

        if (hitStopRoutine != null)
        {
            StopCoroutine(hitStopRoutine);
            hitStopRoutine = null;

            Time.timeScale = 1f;
        }

        hitStopRoutine =
            StartCoroutine(
                HitStopCoroutine(
                    duration,
                    timeScale,
                    isBalanceBreak
                )
            );
    }

    private IEnumerator HitStopCoroutine(
        float duration,
        float timeScale,
        bool isBalanceBreak
    )
    {
        Debug.Log(
            "HITSTOP START -> Scale: " +
            timeScale +
            " Duration: " +
            duration
        );

        Time.timeScale = timeScale;

        Debug.Log(
            "TIME SCALE AFTER SET -> " +
            Time.timeScale
        );

        yield return new WaitForSecondsRealtime(
            duration
        );

        Debug.Log(
            "HITSTOP END -> Before Reset: " +
            Time.timeScale
        );

        Time.timeScale = 1f;

        hitStopRoutine = null;

        if (isBalanceBreak)
        {
            balanceBreakActive = false;

            Debug.Log(
                "BALANCE BREAK HITSTOP FINISHED"
            );
        }

        Debug.Log(
            "TIME SCALE AFTER RESET -> " +
            Time.timeScale
        );
    }

    // =========================================================
    // SCREEN SHAKE
    // =========================================================

    private void PlayScreenShake(
        float force
    )
    {
        if (impulseSource == null)
            return;

        impulseSource.GenerateImpulse(force);
    }

    // =========================================================
    // PUNCH
    // =========================================================

    private void PlayPunch(
        float amount,
        float duration
    )
    {
        if (punchTarget == null)
            return;

        if (punchRoutine != null)
        {
            StopCoroutine(punchRoutine);

            punchTarget.localScale =
                originalPunchScale;
        }

        punchRoutine =
            StartCoroutine(
                PunchCoroutine(
                    amount,
                    duration
                )
            );
    }

    private IEnumerator PunchCoroutine(
        float amount,
        float duration
    )
    {
        Vector3 punchScale =
            originalPunchScale *
            (1f + amount);

        float halfDuration =
            duration * 0.5f;

        float timer = 0f;

        // SCALE UP
        while (timer < halfDuration)
        {
            timer +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    timer / halfDuration
                );

            t =
                Mathf.Sin(
                    t * Mathf.PI * 0.5f
                );

            punchTarget.localScale =
                Vector3.Lerp(
                    originalPunchScale,
                    punchScale,
                    t
                );

            yield return null;
        }

        // SCALE DOWN
        timer = 0f;

        while (timer < halfDuration)
        {
            timer +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    timer / halfDuration
                );

            t =
                1f -
                Mathf.Cos(
                    t * Mathf.PI * 0.5f
                );

            punchTarget.localScale =
                Vector3.Lerp(
                    punchScale,
                    originalPunchScale,
                    t
                );

            yield return null;
        }

        punchTarget.localScale =
            originalPunchScale;

        punchRoutine = null;
    }
}