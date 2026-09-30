using UnityEngine;
using System.Collections;
using Unity.Cinemachine;

public class CombatImpactFeedback : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CinemachineImpulseSource impulseSource;

    [Header("Parry Impact")]
    [SerializeField] private float parryHitStopTimeScale = 0.03f;
    [SerializeField] private float parryHitStopDuration = 0.08f;
    [SerializeField] private float parryShakeForce = 0.35f;

    [Header("Block Impact")]
    [SerializeField] private float blockHitStopTimeScale = 0.05f;
    [SerializeField] private float blockHitStopDuration = 0.025f;
    [SerializeField] private float blockShakeForce = 0.15f;

    [Header("Balance Break Impact")]
    [SerializeField] private float balanceBreakHitStopTimeScale = 0.01f;
    [SerializeField] private float balanceBreakHitStopDuration = 0.12f;
    [SerializeField] private float balanceBreakShakeForce = 0.8f;

    private Coroutine hitStopRoutine;

    private void Awake()
    {
        if (impulseSource == null)
        {
            impulseSource =
                GetComponent<CinemachineImpulseSource>();
        }
    }

    // =========================================================
    // PARRY
    // =========================================================

    public void PlayParryImpact()
    {
        PlayHitStop(
            parryHitStopDuration,
            parryHitStopTimeScale
        );

        PlayScreenShake(
            parryShakeForce
        );
    }

    // =========================================================
    // BLOCK
    // =========================================================

    public void PlayBlockImpact()
    {
        PlayHitStop(
            blockHitStopDuration,
            blockHitStopTimeScale
        );

        PlayScreenShake(
            blockShakeForce
        );
    }

    // =========================================================
    // BALANCE BREAK
    // =========================================================

    public void PlayBalanceBreakImpact()
    {
        PlayHitStop(
            balanceBreakHitStopDuration,
            balanceBreakHitStopTimeScale
        );

        PlayScreenShake(
            balanceBreakShakeForce
        );
    }

    // =========================================================
    // HIT STOP
    // =========================================================

    private void PlayHitStop(
        float duration,
        float timeScale
    )
    {
        if (hitStopRoutine != null)
        {
            StopCoroutine(hitStopRoutine);

            Time.timeScale = 1f;
        }

        hitStopRoutine =
            StartCoroutine(
                HitStopCoroutine(
                    duration,
                    timeScale
                )
            );
    }

    private IEnumerator HitStopCoroutine(
        float duration,
        float timeScale
    )
    {
        Time.timeScale = timeScale;

        yield return new WaitForSecondsRealtime(
            duration
        );

        Time.timeScale = 1f;

        hitStopRoutine = null;
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

        impulseSource.GenerateImpulse(
            force
        );
    }
}