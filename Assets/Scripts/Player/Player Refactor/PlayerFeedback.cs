using Unity.Cinemachine;
using UnityEngine;

public class PlayerFeedback : MonoBehaviour
{
    private PlayerController player;

    [Header("VFX")]
    [SerializeField] private GameObject dustPrefab;
    [SerializeField] private Transform footPoint;

    [Header("Camera Shake")]
    [SerializeField] private CinemachineImpulseSource impulseSource;

    public void Initialize(PlayerController controller)
    {
        player = controller;

        if (impulseSource == null)
        {
            impulseSource =
                player.GetComponent<
                    CinemachineImpulseSource
                >();
        }

        if (dustPrefab == null)
            dustPrefab = player.dustPrefab;

        if (footPoint == null)
            footPoint = player.footPoint;
    }

    public void PlayLandingFeedback(
        float fallDistance
    )
    {
        PlayLandingShake(fallDistance);
        SpawnDust();
    }

    private void PlayLandingShake(
        float distance
    )
    {
        if (impulseSource == null)
            return;

        if (player.impactSettings == null)
            return;

        var settings =
            player.impactSettings;

        float intensity = 0f;

        if (distance >= settings.heavyThreshold)
        {
            intensity =
                settings.heavyIntensity;
        }
        else if (
            distance >= settings.mediumThreshold)
        {
            intensity =
                settings.mediumIntensity;
        }
        else if (
            distance >= settings.lightThreshold)
        {
            intensity =
                settings.lightIntensity;
        }
        else
        {
            return;
        }

        float normalizedDistance =
            Mathf.InverseLerp(
                settings.minDistance,
                settings.maxFallDistance,
                distance
            );

        float curveValue =
            settings.shakeCurve.Evaluate(
                normalizedDistance
            );

        intensity *= curveValue;

        impulseSource.GenerateImpulse(
            intensity
        );
    }

    public void SpawnDust()
    {
        if (dustPrefab == null)
            return;

        Vector3 position =
            footPoint != null
                ? footPoint.position
                : player.transform.position;

        Instantiate(
            dustPrefab,
            position,
            Quaternion.identity
        );
    }
}