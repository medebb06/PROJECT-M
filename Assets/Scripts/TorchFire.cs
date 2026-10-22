using UnityEngine;
using UnityEngine.Rendering.Universal;

// Animated hand-painted torch flame: frame swap + wobble + warm Light2D that follows night/flicker.
// Isik 3 katmanli: ana isik (golgeli) + parlak sicak cekirdek + genis yumusak kor halesi.
// Titreme GERCEK ATES gibi kesik kesik: yumusak nefes + ani sicramalar + ara sira kisa sonmeler.
// Ayrica FireLights'a yazar: cevredeki agac/esya/karakter kenarlari ates tarafindan yanar.
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

    [Header("Kesik titreme (gercek ates)")]
    [Tooltip("Ani sicramalarin sikligi (saniyede ortalama).")]
    public float jumpRate = 11f;
    [Tooltip("Ani sicramalarin buyuklugu.")]
    public float jumpAmount = 0.28f;
    [Tooltip("Kisa sonme (alevin bir an cokmesi) olasiligi, sicrama basina.")]
    [Range(0f, 0.3f)] public float dipChance = 0.07f;
    [Tooltip("Hedefe gecis keskinligi (buyuk = daha kesik).")]
    public float snap = 28f;

    [Header("Kalite (cekirdek + hale)")]
    public bool coreAndGlow = true;
    public Color colorDeep = new Color(1f, 0.42f, 0.14f, 1f);     // turuncu-kizil
    public Color colorBright = new Color(1f, 0.74f, 0.38f, 1f);   // sari-sicak
    public float coreRadius = 9f;
    public float glowRadius = 46f;
    public float coreMul = 0.9f;
    public float glowMul = 0.32f;

    [Header("Kenar isigi (agac, esya, karakter)")]
    public float rimRadius = 22f;
    public float rimStrength = 1f;
    public float rimRadiusMul = 0.7f;

    public static readonly System.Collections.Generic.List<TorchFire> All = new System.Collections.Generic.List<TorchFire>();

    float seed;
    Light2D core, glow;
    float baseOuter = -1f;
    float jumpCur = 1f, jumpTarget = 1f, nextJump;
    readonly FireLights.Fire fire = new FireLights.Fire();

    void OnEnable()
    {
        seed = Random.value * 50f;
        if (!All.Contains(this)) All.Add(this);
        if (Application.isPlaying) FireLights.Register(fire);
    }

    void OnDisable()
    {
        All.Remove(this);
        FireLights.Unregister(fire);
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

    // Kesik titreme: rastgele aralikla yeni hedef secer, hizla oraya atlar (yumusak sinus degil).
    float Choppy(float dt)
    {
        if (Time.time >= nextJump)
        {
            nextJump = Time.time + Random.Range(0.4f, 1.6f) / Mathf.Max(0.1f, jumpRate);
            if (Random.value < dipChance)
                jumpTarget = 1f - jumpAmount * Random.Range(1.4f, 2.2f);   // bir an cokme
            else
                jumpTarget = 1f + Random.Range(-1f, 1f) * jumpAmount;
        }

        jumpCur = Mathf.Lerp(jumpCur, jumpTarget, 1f - Mathf.Exp(-snap * dt));
        return jumpCur;
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

            float f = 1f;
            float slowN = 0f, fastN = 0f, chop = 1f;
            if (Application.isPlaying)
            {
                slowN = (Mathf.PerlinNoise(t * flickerSpeed * 0.45f, seed + 7f) - 0.5f) * 2f;
                fastN = (Mathf.PerlinNoise(t * flickerSpeed * 2.6f, seed + 21f) - 0.5f) * 2f;
                chop = Choppy(Time.deltaTime);
                f = (1f + slowN * flicker * 0.6f + fastN * flicker * 0.3f) * chop;
            }

            float baseI = Mathf.Lerp(dayIntensity, nightIntensity, night);
            fireLight.intensity = baseI * f;

            // Renk alevle birlikte sari <-> kizil gezer (parlayinca sarariyor, cokunce kizariyor).
            float hot = Application.isPlaying ? Mathf.Clamp01(0.5f + slowN * 0.35f + (chop - 1f) * 1.6f) : 0.5f;
            Color col = Color.Lerp(colorDeep, colorBright, hot);
            fireLight.color = col;

            if (baseOuter < 0f) baseOuter = fireLight.pointLightOuterRadius;
            if (Application.isPlaying)
                fireLight.pointLightOuterRadius = baseOuter * (1f + slowN * 0.04f + (chop - 1f) * 0.12f);

            if (Application.isPlaying && coreAndGlow)
            {
                if (core == null) core = MakeLight("FireCore", coreRadius, 0.4f, 0.55f);
                if (glow == null) glow = MakeLight("FireGlow", glowRadius, 3f, 0.7f);

                core.intensity = baseI * coreMul * f;
                core.color = Color.Lerp(col, Color.white, 0.25f);

                glow.intensity = baseI * glowMul * (1f + slowN * flicker * 0.5f) * Mathf.Lerp(1f, chop, 0.5f);
                glow.color = Color.Lerp(colorDeep, colorBright, 0.25f);
            }

            if (Application.isPlaying)
            {
                Vector3 lp = fireLight.transform.position;
                fire.pos = new Vector2(lp.x, lp.y);
                fire.radius = rimRadius;
                fire.intensity = rimStrength * Mathf.Lerp(0.15f, 1f, night) * f;
                fire.color = col;
                FireLights.Publish();
            }
        }
    }
}
