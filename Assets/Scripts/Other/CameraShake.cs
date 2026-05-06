using UnityEngine;
using Unity.Cinemachine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance;

    public CinemachineImpulseSource impulse;

    void Awake()
    {
        Instance = this;
    }

    public void Shake(float intensity)
    {
        if (impulse == null) return;

        impulse.GenerateImpulse(intensity);
    }
}