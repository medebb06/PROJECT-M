using UnityEngine;

/// <summary>
/// HAVA DURUMU YÖNETMENİ (Play'de kendiliğinden çalışır).
///  - Yağmur rastgele başlar ve bir süre sürer (varsayılan 1.5 – 4 dk), sonra diner; aralarda açık hava.
///  - Başlama/bitiş yumuşak geçişlidir; şiddet her seferinde farklıdır.
///  - Rüzgâr yönü ve gücü zamanla yavaşça değişir: yağmur çizgileri, ağaç/çalı/ot sallanması buna uyar.
/// Hâlâ F3 ile yağmuru elle açıp kapatabilirsin (bir sonraki kararda yönetmen devralır).
/// Kapatmak için: WeatherDirector bileşenindeki kontrol kutularını kaldır.
/// </summary>
[DefaultExecutionOrder(10040)]
[DisallowMultipleComponent]
public class WeatherDirector : MonoBehaviour
{
    public static WeatherDirector Instance { get; private set; }

    [Header("Yağmur")]
    public bool controlRain = true;
    [Tooltip("Oyun başında yağmurlu olma olasılığı.")]
    [Range(0f, 1f)] public float startRainingChance = 0.35f;
    [Tooltip("Yağmur süresi (sn): en kısa / en uzun.")]
    public Vector2 rainDuration = new Vector2(90f, 240f);
    [Tooltip("Açık hava süresi (sn): en kısa / en uzun.")]
    public Vector2 clearDuration = new Vector2(70f, 200f);
    [Tooltip("Yağmurun şiddet aralığı.")]
    public Vector2 rainIntensity = new Vector2(0.4f, 1f);
    [Tooltip("Başlarken/biterken geçiş süresi (sn).")]
    public Vector2 fadeTime = new Vector2(6f, 12f);

    [Header("Rüzgâr")]
    public bool controlWind = true;
    [Tooltip("Yön değişimi aralığı (sn).")]
    public Vector2 windChangeEvery = new Vector2(20f, 60f);
    [Tooltip("Yağmur çizgilerinin en fazla yatay hızı (birim/sn).")]
    public float maxRainWind = 4.5f;
    [Tooltip("Ağaçların yönlü eğilme payı (0..1).")]
    [Range(0f, 1f)] public float swayBias = 0.7f;
    [Tooltip("Yön geçişi ne kadar yumuşak (sn).")]
    public float windSmooth = 9f;

    [Header("Şu anki durum (salt okunur)")]
    public bool raining;
    [Range(-1f, 1f)] public float windDir;

    private static readonly int WindDirId = Shader.PropertyToID("_ProjectM_WindDir");

    private float nextRainChange, nextWindChange;
    private float windTarget, windVel;
    private float strengthTarget = 0.6f, strength = 0.6f, strengthVel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<WeatherDirector>() != null)
            return;

        new GameObject("WeatherDirector").AddComponent<WeatherDirector>();
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        raining = Random.value < startRainingChance;
        windTarget = Random.Range(-1f, 1f);
        windDir = windTarget * 0.5f;
        PickNextRain(true);
        PickNextWind();

        RainSystem rs = RainSystem.Instance;
        if (controlRain && rs != null)
        {
            rs.intensity = Random.Range(rainIntensity.x, rainIntensity.y);
            rs.raining = raining;
        }
    }

    private void OnDisable()
    {
        Shader.SetGlobalFloat(WindDirId, 0f);
    }

    private void PickNextRain(bool first = false)
    {
        Vector2 d = raining ? rainDuration : clearDuration;
        float t = Random.Range(d.x, d.y);

        if (first)
            t *= Random.Range(0.3f, 1f);

        nextRainChange = Time.time + t;
    }

    private void PickNextWind()
    {
        nextWindChange = Time.time + Random.Range(windChangeEvery.x, windChangeEvery.y);
        // Bazen sakin (0 civarı), bazen belirgin bir yön.
        windTarget = Random.value < 0.2f ? Random.Range(-0.15f, 0.15f) : Random.Range(0.35f, 1f) * (Random.value < 0.5f ? -1f : 1f);
        strengthTarget = Random.Range(0.4f, 1f);
    }

    private void Update()
    {
        RainSystem rs = RainSystem.Instance;

        if (controlRain && rs != null && Time.time >= nextRainChange)
        {
            raining = !raining;

            if (raining)
                rs.intensity = Random.Range(rainIntensity.x, rainIntensity.y);

            rs.fadeTime = Random.Range(fadeTime.x, fadeTime.y);
            rs.raining = raining;
            PickNextRain();
        }

        if (!controlWind)
            return;

        if (Time.time >= nextWindChange)
            PickNextWind();

        // Yağmurda rüzgâr biraz daha güçlü.
        float st = strengthTarget * (rs != null && rs.raining ? 1.0f : 0.75f);
        strength = Mathf.SmoothDamp(strength, st, ref strengthVel, windSmooth);
        windDir = Mathf.SmoothDamp(windDir, windTarget, ref windVel, windSmooth);

        float signed = windDir * strength;
        Shader.SetGlobalFloat(WindDirId, signed * swayBias);

        if (rs != null)
        {
            rs.windEnabled = true;
            rs.wind = signed * maxRainWind;
            rs.windGust = 0.4f + 0.8f * Mathf.Abs(signed);
        }
    }
}
