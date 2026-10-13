using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// SAAT DİLİMİ (gece / şafak / gündüz / gün batımı). [ATMOSPHERE] objesinde durur.
///   Hour (0-24) kaydırıcısını oynat: gökyüzü, güneş/ay, yıldızlar, sis, ışık tozu, hüzmeler,
///   ağaç/bulut kenar ışığı, su, karakter kenar ışığı, global ışık ve post-process birlikte değişir.
///   Hazır anahtar saatler (Keys): Night, Dawn, Day, Sunset. Aralarında yumuşak geçiş yapılır.
/// Play'de: Auto Advance ile zaman akar. Tuşlar (Hotkeys açıksa): , ve . = önceki/sonraki hazır saat,
///   [ ve ] = saati geri/ileri al.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(10040)]
public class TimeOfDay : MonoBehaviour
{
    public static TimeOfDay Instance { get; private set; }

    [System.Serializable]
    public class TodKey
    {
        public string name = "Key";
        [Range(0f, 24f)] public float hour;

        [Header("Gökyüzü")]
        public Color skyTop = new Color(0.10f, 0.14f, 0.24f);
        public Color skyMid = new Color(0.20f, 0.26f, 0.38f);
        public Color skyHorizon = new Color(0.42f, 0.45f, 0.52f);
        [Tooltip("Ufuk parıltısı (rgb) ve şiddeti (a).")]
        public Color glow = new Color(1f, 0.74f, 0.48f, 0.5f);
        [Range(0f, 1f)] public float stars;
        public Color sunColor = new Color(1f, 0.9f, 0.7f);
        [Range(0f, 1f)] public float sun;
        public Color moonColor = new Color(0.9f, 0.95f, 1f);
        [Range(0f, 1f)] public float moon;

        [Header("Sis")]
        public Color fogFar = new Color(0.86f, 0.80f, 0.72f, 0.4f);
        public Color fogNear = new Color(0.55f, 0.50f, 0.46f, 0.3f);
        public Color mist = new Color(0.6f, 0.65f, 0.75f);
        [Range(0f, 2f)] public float mistAmount = 0.6f;

        [Header("Işık (güneş/ay)")]
        [Tooltip("Kenar ışığı, su pırıltısı, sis parıltısı bu renkte.")]
        public Color light = new Color(0.7f, 0.82f, 1.1f);
        [Tooltip("Ağaç/bulut kenar ışığı gücü.")]
        [Range(0f, 2f)] public float rimGain = 1f;
        public Color ambient = Color.white;
        [Range(0f, 2f)] public float ambientIntensity = 0.9f;
        public Color groundLight = new Color(1f, 0.78f, 0.55f, 0.2f);

        [Header("Sahne boyası")]
        public Color bodyTint = Color.white;
        public Color bodyLift = Color.black;
        [Tooltip("Pus renklerini çarpar (uzak çalılar, bulutlar).")]
        public Color hazeMul = Color.white;
        public Color water = Color.white;
        [Range(0f, 3f)] public float glint = 1f;

        [Header("Hüzme ve toz")]
        [Range(0f, 3f)] public float rays = 1f;
        public Color rayColor = new Color(1f, 0.87f, 0.68f);
        [Range(0f, 2f)] public float dust = 1f;
        public Color dustColor = new Color(1f, 0.9f, 0.75f);

        [Header("Post-process")]
        public Color grade = Color.white;
        public float exposure = 0f;
        [Range(-100f, 100f)] public float saturation = -10f;
        [Range(-100f, 100f)] public float contrast = 10f;
        [Range(0f, 3f)] public float bloom = 0.8f;
        [Range(0f, 1f)] public float vignette = 0.4f;

        public TodKey Clone() { return (TodKey)MemberwiseClone(); }
    }

    [Range(0f, 24f)] public float hour = 22f;

    [Header("Akış (Play)")]
    [Tooltip("Açıksa gün kendiliğinden döner (gece-şafak-gündüz-gün batımı).")]
    public bool autoCycle = true;
    [Tooltip("24 saatlik bir döngü kaç dakika sürer.")]
    public float dayLengthMinutes = 6f;
    [Tooltip("Tuş kısayolu ile saat değişirken hız (saat/sn).")]
    public float transitionSpeed = 8f;
    public bool hotkeys = true;

    [Header("Güneş / ay yörüngesi")]
    public float sunrise = 6f;
    public float sunset = 18f;
    public float moonRise = 18.5f;
    public float moonSet = 5.5f;
    [Tooltip("Gökyüzündeki en yüksek nokta (ufuktan birim).")]
    public float peak = 8.5f;
    [Tooltip("Yörüngenin yarı genişliği (kamera merkezinden birim).")]
    public float arcHalfWidth = 16f;
    [Tooltip("Gökyüzünün kamera dikey hareketini takibi.")]
    [Range(0f, 1f)] public float skyFollow = 0.85f;
    public float camRestY = 2.65f;
    public float skyHeight = 22f;

    public List<TodKey> keys = new List<TodKey>();

    private TodKey cur = new TodKey();
    private float goalHour = -1f;

    private readonly Dictionary<string, SpriteRenderer> srs = new Dictionary<string, SpriteRenderer>();
    private float nextRefresh;
    private AtmosphereLight al;
    private GodRays godRays;
    private MistLayers mistLayers;
    private AtmosphereDust dustFx;
    private Volume volume;
    private Light2D globalLight;
    private CharacterRim[] rims;
    private MaterialPropertyBlock mpb;
    private bool hotkeyBroken;

    private static readonly int TodOnId = Shader.PropertyToID("_ProjectM_TodOn");
    private static readonly int BodyTintId = Shader.PropertyToID("_ProjectM_BodyTint");
    private static readonly int BodyLiftId = Shader.PropertyToID("_ProjectM_BodyLift");
    private static readonly int HazeMulId = Shader.PropertyToID("_ProjectM_HazeMul");
    private static readonly int LightColorId = Shader.PropertyToID("_ProjectM_LightColor");
    private static readonly int WaterMulId = Shader.PropertyToID("_ProjectM_WaterMul");
    private static readonly int GlintId = Shader.PropertyToID("_ProjectM_Glint");
    private static readonly int MoonPosId = Shader.PropertyToID("_ProjectM_MoonPos");
    private static readonly int WaterMoonXId = Shader.PropertyToID("_WaterMoonX");
    private static readonly int TopId = Shader.PropertyToID("_Top");
    private static readonly int MidId = Shader.PropertyToID("_Mid");
    private static readonly int HorizonId = Shader.PropertyToID("_Horizon");
    private static readonly int HorizonYId = Shader.PropertyToID("_HorizonY");
    private static readonly int HeightId = Shader.PropertyToID("_Height");

    // =====================================================================

    private void OnEnable()
    {
        Instance = this;

        if (keys == null || keys.Count == 0)
            keys = DefaultKeys();

        mpb = new MaterialPropertyBlock();
        nextRefresh = 0f;
        LateUpdate();
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;

        Shader.SetGlobalFloat(TodOnId, 0f);
    }

    [ContextMenu("Hazır saatleri sıfırla")]
    public void ResetKeys()
    {
        keys = DefaultKeys();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && isActiveAndEnabled)
                LateUpdate();
        };
    }
#endif

    /// <summary>Saati ayarla (instant = anında, değilse yumuşak geçiş).</summary>
    public void SetHour(float h, bool instant = false)
    {
        h = Mathf.Repeat(h, 24f);

        if (instant || !Application.isPlaying)
        {
            hour = h;
            goalHour = -1f;
        }
        else
        {
            goalHour = h;
        }
    }

    /// <summary>Sonraki (dir=+1) / önceki (dir=-1) hazır saate geç.</summary>
    public void StepPreset(int dir)
    {
        // Anahtarlardan "tepe" saatler: gece, şafak, gündüz, gün batımı.
        float[] hs = { 23f, 6f, 12f, 18f };
        float best = hs[0];
        int idx = 0;
        float bd = 99f;

        for (int i = 0; i < hs.Length; i++)
        {
            float d = Mathf.Abs(Mathf.DeltaAngle(hour * 15f, hs[i] * 15f)) / 15f;
            if (d < bd) { bd = d; idx = i; best = hs[i]; }
        }

        // Zaten bir hazır saatin üzerindeyse sonrakine, değilse en yakına git.
        if (bd < 0.6f)
            idx = (idx + dir + hs.Length) % hs.Length;

        SetHour(hs[idx]);
    }

    // =====================================================================

    private void LateUpdate()
    {
        if (keys == null || keys.Count == 0)
            keys = DefaultKeys();

        if (Application.isPlaying)
        {
            if (hotkeys && !hotkeyBroken)
                HandleHotkeys();

            if (goalHour >= 0f)
            {
                float d = Mathf.DeltaAngle(hour * 15f, goalHour * 15f) / 15f;   // en kısa yön (saat)
                float step = transitionSpeed * Time.unscaledDeltaTime;

                if (Mathf.Abs(d) <= step) { hour = goalHour; goalHour = -1f; }
                else hour = Mathf.Repeat(hour + Mathf.Sign(d) * step, 24f);
            }
            else if (autoCycle)
            {
                hour = Mathf.Repeat(hour + Time.deltaTime * 24f / Mathf.Max(0.1f, dayLengthMinutes * 60f), 24f);
            }
        }

        if (Time.realtimeSinceStartup >= nextRefresh)
        {
            nextRefresh = Time.realtimeSinceStartup + 1.5f;
            Refresh();
        }

        Evaluate(hour, cur);
        Apply(cur);
    }

    private void HandleHotkeys()
    {
        try
        {
            if (Input.GetKeyDown(KeyCode.Period)) StepPreset(+1);
            if (Input.GetKeyDown(KeyCode.Comma)) StepPreset(-1);
            if (Input.GetKey(KeyCode.RightBracket)) SetHour(hour + Time.unscaledDeltaTime * 3f, true);
            if (Input.GetKey(KeyCode.LeftBracket)) SetHour(hour - Time.unscaledDeltaTime * 3f, true);
        }
        catch (System.InvalidOperationException)
        {
            hotkeyBroken = true;   // yeni Input System: eski tuş API'si kapalı
        }
    }

    private void Refresh()
    {
        string[] names = { "Sky Gradient", "Moon", "Moon Halo", "Sun", "Sun Halo", "Horizon Glow", "Fog Far", "Fog Near", "Ground Light", "Stars" };
        var all = GetComponentsInChildren<SpriteRenderer>(true);

        srs.Clear();

        foreach (SpriteRenderer r in all)
            foreach (string n in names)
                if (r.name == n && !srs.ContainsKey(n))
                    srs[n] = r;

        al = GetComponent<AtmosphereLight>();
        godRays = GetComponentInChildren<GodRays>(true);
        mistLayers = GetComponentInChildren<MistLayers>(true);
        dustFx = GetComponentInChildren<AtmosphereDust>(true);
        volume = GetComponentInChildren<Volume>(true);

        globalLight = null;

        foreach (Light2D l in FindObjectsByType<Light2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (l.lightType == Light2D.LightType.Global) { globalLight = l; break; }

        rims = FindObjectsByType<CharacterRim>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    // =====================================================================
    // Anahtarlar arası geçiş
    // =====================================================================

    private void Evaluate(float h, TodKey o)
    {
        int n = keys.Count;

        if (n == 1) { Copy(keys[0], o); return; }

        TodKey a = keys[0], b = keys[n - 1];
        float t = 0f;

        if (h <= keys[0].hour) { Copy(keys[0], o); return; }
        if (h >= keys[n - 1].hour) { Copy(keys[n - 1], o); return; }

        for (int i = 0; i < n - 1; i++)
        {
            if (h >= keys[i].hour && h <= keys[i + 1].hour)
            {
                a = keys[i];
                b = keys[i + 1];
                float span = Mathf.Max(0.0001f, b.hour - a.hour);
                t = (h - a.hour) / span;
                t = t * t * (3f - 2f * t);
                break;
            }
        }

        Blend(a, b, t, o);
    }

    private static void Copy(TodKey a, TodKey o)
    {
        Blend(a, a, 0f, o);
    }

    private static void Blend(TodKey a, TodKey b, float t, TodKey o)
    {
        o.name = t < 0.5f ? a.name : b.name;
        o.hour = Mathf.Lerp(a.hour, b.hour, t);

        o.skyTop = Color.Lerp(a.skyTop, b.skyTop, t);
        o.skyMid = Color.Lerp(a.skyMid, b.skyMid, t);
        o.skyHorizon = Color.Lerp(a.skyHorizon, b.skyHorizon, t);
        o.glow = Color.Lerp(a.glow, b.glow, t);
        o.stars = Mathf.Lerp(a.stars, b.stars, t);
        o.sunColor = Color.Lerp(a.sunColor, b.sunColor, t);
        o.sun = Mathf.Lerp(a.sun, b.sun, t);
        o.moonColor = Color.Lerp(a.moonColor, b.moonColor, t);
        o.moon = Mathf.Lerp(a.moon, b.moon, t);

        o.fogFar = Color.Lerp(a.fogFar, b.fogFar, t);
        o.fogNear = Color.Lerp(a.fogNear, b.fogNear, t);
        o.mist = Color.Lerp(a.mist, b.mist, t);
        o.mistAmount = Mathf.Lerp(a.mistAmount, b.mistAmount, t);

        o.light = Color.Lerp(a.light, b.light, t);
        o.rimGain = Mathf.Lerp(a.rimGain, b.rimGain, t);
        o.ambient = Color.Lerp(a.ambient, b.ambient, t);
        o.ambientIntensity = Mathf.Lerp(a.ambientIntensity, b.ambientIntensity, t);
        o.groundLight = Color.Lerp(a.groundLight, b.groundLight, t);

        o.bodyTint = Color.Lerp(a.bodyTint, b.bodyTint, t);
        o.bodyLift = Color.Lerp(a.bodyLift, b.bodyLift, t);
        o.hazeMul = Color.Lerp(a.hazeMul, b.hazeMul, t);
        o.water = Color.Lerp(a.water, b.water, t);
        o.glint = Mathf.Lerp(a.glint, b.glint, t);

        o.rays = Mathf.Lerp(a.rays, b.rays, t);
        o.rayColor = Color.Lerp(a.rayColor, b.rayColor, t);
        o.dust = Mathf.Lerp(a.dust, b.dust, t);
        o.dustColor = Color.Lerp(a.dustColor, b.dustColor, t);

        o.grade = Color.Lerp(a.grade, b.grade, t);
        o.exposure = Mathf.Lerp(a.exposure, b.exposure, t);
        o.saturation = Mathf.Lerp(a.saturation, b.saturation, t);
        o.contrast = Mathf.Lerp(a.contrast, b.contrast, t);
        o.bloom = Mathf.Lerp(a.bloom, b.bloom, t);
        o.vignette = Mathf.Lerp(a.vignette, b.vignette, t);
    }

    // =====================================================================
    // Uygula
    // =====================================================================

    private static void Col(SpriteRenderer r, Color c)
    {
        if (r != null && r.color != c)
            r.color = c;
    }

    private SpriteRenderer Get(string n)
    {
        SpriteRenderer r;
        return srs.TryGetValue(n, out r) ? r : null;
    }

    private void Apply(TodKey k)
    {
        Camera cam = Camera.main;
        float horizonY = al != null ? al.horizonY : 0f;

        // ---- genel shader değerleri ----
        Shader.SetGlobalFloat(TodOnId, 1f);
        Shader.SetGlobalVector(BodyTintId, k.bodyTint);
        Shader.SetGlobalVector(BodyLiftId, k.bodyLift);
        Shader.SetGlobalVector(HazeMulId, k.hazeMul);
        Shader.SetGlobalVector(LightColorId, new Vector4(k.light.r, k.light.g, k.light.b, k.rimGain));
        Shader.SetGlobalVector(WaterMulId, k.water);
        Shader.SetGlobalVector(GlintId, new Vector4(k.glint, 0f, 0f, 0f));

        // ---- gökyüzü ----
        SpriteRenderer sky = Get("Sky Gradient");

        if (sky != null)
        {
            sky.GetPropertyBlock(mpb);
            mpb.SetColor(TopId, k.skyTop);
            mpb.SetColor(MidId, k.skyMid);
            mpb.SetColor(HorizonId, k.skyHorizon);
            mpb.SetFloat(HorizonYId, horizonY);
            mpb.SetFloat(HeightId, skyHeight);
            sky.SetPropertyBlock(mpb);
        }

        Col(Get("Horizon Glow"), k.glow);
        Col(Get("Fog Far"), k.fogFar);
        Col(Get("Fog Near"), k.fogNear);
        Col(Get("Ground Light"), k.groundLight);
        Col(Get("Stars"), new Color(1f, 1f, 1f, k.stars));

        // ---- güneş ve ay ----
        Vector2 sunP = new Vector2(0f, -20f), moonP = new Vector2(0f, -20f);

        if (cam != null)
        {
            float cx = cam.transform.position.x;
            float yShift = (cam.transform.position.y - camRestY) * skyFollow;

            float st = (hour - sunrise) / Mathf.Max(0.01f, sunset - sunrise);
            sunP = new Vector2(
                cx + Mathf.LerpUnclamped(-arcHalfWidth, arcHalfWidth, st),
                horizonY + Mathf.Sin(st * Mathf.PI) * peak + yShift);

            float mh = hour >= 12f ? hour : hour + 24f;
            float mSpan = (moonSet + 24f) - moonRise;
            float mt = (mh - moonRise) / Mathf.Max(0.01f, mSpan);
            moonP = new Vector2(
                cx + Mathf.LerpUnclamped(-arcHalfWidth, arcHalfWidth, mt),
                horizonY + Mathf.Sin(mt * Mathf.PI) * peak + yShift);
        }

        PlaceBody("Sun", sunP, new Color(k.sunColor.r, k.sunColor.g, k.sunColor.b, k.sun * Mathf.Clamp01((sunP.y - horizonY + 1.5f) / 2f)));
        PlaceBody("Sun Halo", sunP, new Color(k.sunColor.r, k.sunColor.g, k.sunColor.b, 0.6f * k.sun * Mathf.Clamp01((sunP.y - horizonY + 1.5f) / 2f)));
        PlaceBody("Moon", moonP, new Color(k.moonColor.r, k.moonColor.g, k.moonColor.b, k.moon * Mathf.Clamp01((moonP.y - horizonY + 1.5f) / 2f)));
        PlaceBody("Moon Halo", moonP, new Color(k.moonColor.r, k.moonColor.g, k.moonColor.b, 1.0f * k.moon * Mathf.Clamp01((moonP.y - horizonY + 1.5f) / 2f)));

        // Baskın ışık: bulut kenar ışığı, sis parıltısı ve su ışık yolu buna göre.
        float sw = k.sun / (k.sun + k.moon + 0.001f);
        Vector2 dom = Vector2.Lerp(moonP, sunP, sw);
        Shader.SetGlobalVector(MoonPosId, new Vector4(dom.x, dom.y, 0f, 0f));
        Shader.SetGlobalFloat(WaterMoonXId, dom.x);

        // ---- hüzme / sis / toz ----
        if (godRays != null)
            godRays.ApplyTint(k.rayColor, k.rays);

        if (mistLayers != null)
            mistLayers.SetLook(k.mist, k.mistAmount, horizonY);

        if (dustFx != null)
            dustFx.SetLook(k.dustColor, k.dust);

        // ---- karakter kenar ışığı ----
        if (rims != null)
        {
            Color rc = new Color(k.light.r * k.rimGain, k.light.g * k.rimGain, k.light.b * k.rimGain, 1f);

            foreach (CharacterRim r in rims)
                if (r != null && r.color != rc)
                    r.color = rc;
        }

        // ---- global ışık ----
        if (globalLight != null)
        {
            if (globalLight.color != k.ambient) globalLight.color = k.ambient;
            if (!Mathf.Approximately(globalLight.intensity, k.ambientIntensity)) globalLight.intensity = k.ambientIntensity;
        }

        // ---- post-process ----
        if (volume != null)
        {
            VolumeProfile p = Application.isPlaying ? volume.profile : volume.sharedProfile;

            if (p != null)
            {
                ColorAdjustments ca;
                if (p.TryGet(out ca))
                {
                    ca.colorFilter.Override(k.grade);
                    ca.postExposure.Override(k.exposure);
                    ca.saturation.Override(k.saturation);
                    ca.contrast.Override(k.contrast);
                }

                Bloom bl;
                if (p.TryGet(out bl))
                {
                    bl.intensity.Override(k.bloom);
                    bl.tint.Override(Color.Lerp(Color.white, k.light, 0.6f));
                }

                Vignette vg;
                if (p.TryGet(out vg))
                {
                    vg.intensity.Override(k.vignette);
                    vg.color.Override(k.skyTop * 0.25f);
                }
            }
        }
    }

    private void PlaceBody(string n, Vector2 pos, Color c)
    {
        SpriteRenderer r = Get(n);

        if (r == null)
            return;

        r.transform.position = new Vector3(pos.x, pos.y, r.transform.position.z);
        Col(r, c);
        r.enabled = c.a > 0.003f;
    }

    // =====================================================================
    // Hazır anahtarlar
    // =====================================================================

    public static List<TodKey> DefaultKeys()
    {
        var night = new TodKey
        {
            name = "Gece (mavi)", hour = 0f,
            skyTop = new Color(0.025f, 0.04f, 0.10f), skyMid = new Color(0.06f, 0.10f, 0.21f), skyHorizon = new Color(0.20f, 0.29f, 0.42f),
            glow = new Color(0.25f, 0.40f, 0.65f, 0.35f), stars = 1f,
            sunColor = new Color(1f, 0.9f, 0.7f), sun = 0f,
            moonColor = new Color(0.88f, 0.94f, 1f), moon = 1f,
            fogFar = new Color(0.28f, 0.40f, 0.58f, 0.30f), fogNear = new Color(0.10f, 0.16f, 0.26f, 0.25f),
            mist = new Color(0.30f, 0.42f, 0.58f), mistAmount = 0.75f,
            light = new Color(0.62f, 0.80f, 1.15f), rimGain = 1.0f,
            ambient = new Color(0.55f, 0.65f, 0.90f), ambientIntensity = 0.62f,
            groundLight = new Color(0.45f, 0.62f, 0.95f, 0.14f),
            bodyTint = new Color(0.85f, 0.95f, 1.2f), bodyLift = new Color(0f, 0.008f, 0.025f),
            hazeMul = new Color(0.30f, 0.42f, 0.68f), water = new Color(0.70f, 0.92f, 1.35f), glint = 1.1f,
            rays = 0.8f, rayColor = new Color(0.55f, 0.72f, 1f),
            dust = 1f, dustColor = new Color(0.65f, 0.80f, 1f),
            grade = new Color(0.80f, 0.90f, 1.10f), exposure = 0f, saturation = -8f, contrast = 12f, bloom = 1.4f, vignette = 0.45f
        };

        var dawn = new TodKey
        {
            name = "Şafak", hour = 6f,
            skyTop = new Color(0.20f, 0.27f, 0.48f), skyMid = new Color(0.62f, 0.47f, 0.60f), skyHorizon = new Color(1.0f, 0.74f, 0.58f),
            glow = new Color(1f, 0.66f, 0.52f, 0.65f), stars = 0.12f,
            sunColor = new Color(1f, 0.82f, 0.62f), sun = 1f,
            moonColor = new Color(0.9f, 0.92f, 1f), moon = 0.25f,
            fogFar = new Color(0.88f, 0.74f, 0.74f, 0.42f), fogNear = new Color(0.55f, 0.45f, 0.50f, 0.30f),
            mist = new Color(0.90f, 0.80f, 0.84f), mistAmount = 1.15f,
            light = new Color(1.25f, 0.86f, 0.76f), rimGain = 0.95f,
            ambient = new Color(0.95f, 0.85f, 0.90f), ambientIntensity = 0.80f,
            groundLight = new Color(1f, 0.72f, 0.60f, 0.18f),
            bodyTint = new Color(1f, 0.95f, 1f), bodyLift = new Color(0.03f, 0.02f, 0.04f),
            hazeMul = new Color(1.05f, 0.90f, 0.96f), water = new Color(1.15f, 1.0f, 1.2f), glint = 1.25f,
            rays = 1.3f, rayColor = new Color(1f, 0.80f, 0.68f),
            dust = 1.1f, dustColor = new Color(1f, 0.85f, 0.75f),
            grade = new Color(1.04f, 0.98f, 1.02f), exposure = 0f, saturation = -4f, contrast = 8f, bloom = 0.85f, vignette = 0.35f
        };

        var day = new TodKey
        {
            name = "Gündüz", hour = 8.5f,
            skyTop = new Color(0.25f, 0.50f, 0.86f), skyMid = new Color(0.46f, 0.69f, 0.93f), skyHorizon = new Color(0.82f, 0.91f, 0.98f),
            glow = new Color(1f, 0.95f, 0.85f, 0.22f), stars = 0f,
            sunColor = new Color(1f, 0.95f, 0.82f), sun = 1f,
            moonColor = new Color(0.9f, 0.95f, 1f), moon = 0f,
            fogFar = new Color(0.85f, 0.91f, 0.97f, 0.30f), fogNear = new Color(0.60f, 0.68f, 0.76f, 0.18f),
            mist = new Color(0.88f, 0.93f, 0.98f), mistAmount = 0.28f,
            light = new Color(1.15f, 1.08f, 0.95f), rimGain = 0.40f,
            ambient = new Color(1f, 1f, 1f), ambientIntensity = 1.15f,
            groundLight = new Color(1f, 0.95f, 0.8f, 0.05f),
            bodyTint = new Color(1.1f, 1.1f, 1.1f), bodyLift = new Color(0.07f, 0.09f, 0.08f),
            hazeMul = new Color(1.2f, 1.3f, 1.45f), water = new Color(1.8f, 2.4f, 3.0f), glint = 1.0f,
            rays = 0.55f, rayColor = new Color(1f, 0.96f, 0.82f),
            dust = 0.7f, dustColor = new Color(1f, 0.95f, 0.8f),
            grade = new Color(1f, 1f, 1f), exposure = 0.12f, saturation = 6f, contrast = 6f, bloom = 0.4f, vignette = 0.2f
        };

        var day2 = day.Clone();
        day2.hour = 15.5f;

        var sunsetK = new TodKey
        {
            name = "Gün batımı", hour = 18f,
            skyTop = new Color(0.16f, 0.14f, 0.32f), skyMid = new Color(0.64f, 0.30f, 0.30f), skyHorizon = new Color(1.0f, 0.52f, 0.20f),
            glow = new Color(1f, 0.45f, 0.18f, 0.9f), stars = 0.02f,
            sunColor = new Color(1f, 0.62f, 0.30f), sun = 1f,
            moonColor = new Color(0.95f, 0.92f, 0.9f), moon = 0.1f,
            fogFar = new Color(1f, 0.60f, 0.40f, 0.36f), fogNear = new Color(0.55f, 0.30f, 0.26f, 0.30f),
            mist = new Color(1f, 0.66f, 0.46f), mistAmount = 0.5f,
            light = new Color(1.45f, 0.86f, 0.46f), rimGain = 1.25f,
            ambient = new Color(1f, 0.80f, 0.66f), ambientIntensity = 0.88f,
            groundLight = new Color(1f, 0.62f, 0.34f, 0.24f),
            bodyTint = new Color(1.05f, 0.9f, 0.8f), bodyLift = new Color(0.04f, 0.014f, 0f),
            hazeMul = new Color(1.25f, 0.85f, 0.60f), water = new Color(1.4f, 0.95f, 0.75f), glint = 1.5f,
            rays = 1.5f, rayColor = new Color(1f, 0.70f, 0.40f),
            dust = 1.2f, dustColor = new Color(1f, 0.78f, 0.50f),
            grade = new Color(1.08f, 0.97f, 0.88f), exposure = 0f, saturation = 8f, contrast = 10f, bloom = 1.0f, vignette = 0.38f
        };

        var night2 = night.Clone();
        night2.hour = 19.75f;

        var nightStart = night.Clone();
        nightStart.name = "Gece (mavi)";
        nightStart.hour = 0f;

        var nightHold = night.Clone();
        nightHold.hour = 4.5f;

        var nightEnd = night.Clone();
        nightEnd.hour = 24f;

        return new List<TodKey> { nightStart, nightHold, dawn, day, day2, sunsetK, night2, nightEnd };
    }
}
