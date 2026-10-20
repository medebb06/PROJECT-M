using UnityEngine;
using UnityEngine.Rendering.Universal;

// Animated hand-painted torch flame: frame swap + wobble + warm Light2D that follows night/flicker.
[ExecuteAlways]
public class TorchFire : MonoBehaviour
{
    [Header("Flame")]
    public SpriteRenderer flame;
    public Sprite[] frames;
    public float flameScale = 3f;       // flame local scale (wobble is applied around this)
    public float fps = 9f;
    public float scaleWobble = 0.06f;
    public float alphaBase = 0.95f;

    [Header("Light")]
    public Light2D fireLight;
    public float dayIntensity = 0.2f;
    public float nightIntensity = 1.6f;
    public float flicker = 0.18f;
    public float flickerSpeed = 7f;

    float seed;

    void OnEnable() { seed = Random.value * 50f; }

    void LateUpdate()
    {
        float t = Application.isPlaying ? Time.time : 0f;

        if (flame != null && frames != null && frames.Length > 0)
        {
            int i = Mathf.FloorToInt(t * fps + seed) % frames.Length;
            if (i < 0) i += frames.Length;
            flame.sprite = frames[i];
            float w = 1f + (Mathf.PerlinNoise(t * 3.1f, seed) - 0.5f) * 2f * scaleWobble;
            float h = 1f + (Mathf.PerlinNoise(seed, t * 2.3f) - 0.5f) * 2f * scaleWobble * 1.6f;
            flame.transform.localScale = new Vector3(flameScale * w, flameScale * h, 1f);
            var c = flame.color; c.a = alphaBase; flame.color = c;
        }

        if (fireLight != null)
        {
            float night = Shader.GetGlobalFloat("_ProjectM_NightRim");
            float f = Application.isPlaying ? 1f + (Mathf.PerlinNoise(t * flickerSpeed, seed + 7f) - 0.5f) * 2f * flicker : 1f;
            fireLight.intensity = Mathf.Lerp(dayIntensity, nightIntensity, night) * f;
        }
    }
}
