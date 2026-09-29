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

    private Coroutine hitStopRoutine;

    private void Awake()
    {
        if (impulseSource == null)
        {
            impulseSource =
                GetComponent<CinemachineImpulseSource>();
        }
    }

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

    private void PlayHitStop(
        float duration,
        float timeScale
    )
    {
        if (hitStopRoutine != null)
        {
            StopCoroutine(hitStopRoutine);
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