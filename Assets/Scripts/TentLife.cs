using UnityEngine;
using UnityEngine.Rendering.Universal;

// Tent sway in the wind (only in Play mode) + warm lantern Light2D that follows night/flicker.
[ExecuteAlways]
public class TentLife : MonoBehaviour
{
    [Header("Sway (sadece Play modunda)")]
    public float swayDegrees = 0.9f;
    public float windBias = 1.4f;       // extra lean from wind direction
    public float swaySpeed = 0.9f;
    public float gustSpeed = 0.37f;

    [Header("Lantern light")]
    public Light2D lantern;
    public float dayIntensity = 0.25f;
    public float nightIntensity = 1.6f;
    public float flicker = 0.12f;
    public float flickerSpeed = 2.3f;

    float seed;

    void OnEnable() { seed = Random.value * 50f; }
    void OnDisable() { transform.localRotation = Quaternion.identity; }

    void LateUpdate()
    {
        float t = Application.isPlaying ? Time.time : 0f;

        if (Application.isPlaying)
        {
            float wind = Shader.GetGlobalFloat("_ProjectM_WindDir");
            float s = Mathf.Sin(t * swaySpeed + seed) * 0.6f + Mathf.Sin(t * gustSpeed * 3.1f + seed * 2f) * 0.4f;
            float gust = Mathf.PerlinNoise(t * gustSpeed, seed) - 0.5f;
            float ang = (s * swayDegrees + gust * swayDegrees * 0.8f) - wind * windBias;
            transform.localRotation = Quaternion.Euler(0, 0, ang);
        }
        else
        {
            transform.localRotation = Quaternion.identity;
        }

        if (lantern != null)
        {
            float night = Shader.GetGlobalFloat("_ProjectM_NightRim");
            float f = Application.isPlaying ? 1f + (Mathf.PerlinNoise(t * flickerSpeed, seed + 7f) - 0.5f) * 2f * flicker : 1f;
            lantern.intensity = Mathf.Lerp(dayIntensity, nightIntensity, night) * f;
        }
    }
}
