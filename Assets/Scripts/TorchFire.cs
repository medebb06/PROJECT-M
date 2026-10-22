using UnityEngine;
using UnityEngine.Rendering.Universal;

// Animated hand-painted torch flame: frame swap + wobble + warm Light2D that follows night/flicker.
// Isik 3 katmanli: ana isik (golgeli) + parlak sicak cekirdek + genis yumusak kor halesi.
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
    public float nightIntensity = 2.2f;
    public float flicker = 0.2f;
    public float flickerSpeed = 7f;

    [Header("Kalite (cekirdek + hale)")]
    public bool coreAndGlow = true;
    public Color colorDeep = new Color(1f, 0.42f, 0.14f, 1f);     // turuncu-kizil
    public Color colorBright = new Color(1f, 0.74f, 0.38f, 1f);   // sari-sicak
    public float coreRadius = 9f;
    public float glowRadius = 46f;
    public float coreMul = 0.9f;
    public float glowMul = 0.32f;

    float seed;
    Light2D core, glow;
    float baseOuter = -1f;

    void OnEnable() { seed = Random.value * 50f; }

    void OnDisable()
    {
        if (core != null) DestroyImmediate(core.gameObject);
        if (glow != null) DestroyImmediate(glow.gameObject);
        core = glow = null;
    }

    Light2D MakeLight(string name, float radius, float inner, float falloff)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(fireLight != null ? fireLight.transform : transform, false);
        Light2D l = go.AddComponent<Light2D>();
        l.lightType = Light2D.LightType.Point;
        l.pointLightOuterRadius = radius;
        l.pointLightInnerRadius = inner;
        l.pointLightInnerAngle = 360f;
        l.pointLightOuterAngle = 360f;
        l.falloffIntensity = falloff;
        l.shadowsEnabled = false;
        return l;
    }

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

            // Iki katmanli titreme: yavas nefes + hizli kivilcim; ara sira ruzgar nefesi.
            float f = 1f;
            float slowN = 0f, fastN = 0f, gust = 0f;
            if (Application.isPlaying)
            {
                slowN = (Mathf.PerlinNoise(t * flickerSpeed * 0.45f, seed + 7f) - 0.5f) * 2f;
                fastN = (Mathf.PerlinNoise(t * flickerSpeed * 2.6f, seed + 21f) - 0.5f) * 2f;
                gust = Mathf.Max(0f, Mathf.PerlinNoise(t * 0.35f, seed + 40f) - 0.62f) * 2.4f;   // nadir kisa sonme
                f = 1f + slowN * flicker + fastN * flicker * 0.45f - gust * flicker * 1.4f;
            }

            float baseI = Mathf.Lerp(dayIntensity, nightIntensity, night);
            fireLight.intensity = baseI * f;

            // Renk alevle birlikte sari <-> kizil gezer.
            float hot = Application.isPlaying ? Mathf.Clamp01(0.5f + slowN * 0.5f + fastN * 0.2f) : 0.5f;
            Color col = Color.Lerp(colorDeep, colorBright, hot);
            fireLight.color = col;

            if (baseOuter < 0f) baseOuter = fireLight.pointLightOuterRadius;
            if (Application.isPlaying)
                fireLight.pointLightOuterRadius = baseOuter * (1f + slowN * 0.05f);

            if (Application.isPlaying && coreAndGlow)
            {
                if (core == null) core = MakeLight("FireCore", coreRadius, 0.4f, 0.55f);
                if (glow == null) glow = MakeLight("FireGlow", glowRadius, 3f, 0.7f);

                core.intensity = baseI * coreMul * (1f + fastN * flicker * 0.7f - gust * flicker);
                core.color = Color.Lerp(colorBright, Color.white, 0.25f);

                glow.intensity = baseI * glowMul * (1f + slowN * flicker * 0.5f);
                glow.color = Color.Lerp(colorDeep, colorBright, 0.25f);
            }
        }
    }
}
