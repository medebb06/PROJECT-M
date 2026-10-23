using UnityEngine;

/// <summary>
/// GECE SESI: Gece olunca orman/kus ambiyansi ("Theme Sound") yumusakca kisilir, aralarla cekirge sesi calar.
/// Gunduz geri doner. Oyun baslayinca kendiliginden olusur; Resources/NightSfx icindeki klipleri kullanir.
/// </summary>
public class NightAmbience : MonoBehaviour
{
    [Header("Gece algisi (TimeOfDay.hour)")]
    [Tooltip("Gunes dogus/batisindan bu kadar saat once-sonra gecis baslar.")]
    public float fadeHours = 1.0f;

    [Header("Kus / orman sesi")]
    public string dayAmbienceObject = "Theme Sound";
    [Tooltip("Gece kus sesinin kalan orani (0 = tamamen kapali).")]
    [Range(0f, 1f)] public float dayAmbienceAtNight = 0f;

    [Header("Cekirge")]
    [Range(0f, 1.5f)] public float cricketVolume = 0.5f;
    public Vector2 gap = new Vector2(3f, 10f);
    public Vector2 pitch = new Vector2(0.95f, 1.05f);
    [Tooltip("Klibin basinda/sonunda yumusak giris-cikis (sn).")]
    public float edgeFade = 0.8f;
    public float fadeSpeed = 0.5f;

    AudioSource day, cricket;
    float dayBase = -1f;
    float night;          // 0 gunduz .. 1 gece
    float nextT, clipEnd;
    bool playing;
    AudioClip clip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindAnyObjectByType<NightAmbience>() != null) return;
        var go = new GameObject("NightAmbience");
        go.AddComponent<NightAmbience>();
    }

    void Awake()
    {
        clip = Resources.Load<AudioClip>("NightSfx/night_crickets");
        cricket = gameObject.AddComponent<AudioSource>();
        cricket.playOnAwake = false;
        cricket.spatialBlend = 0f;
        cricket.loop = false;
        cricket.volume = 0f;
        nextT = Time.time + 1f;
    }

    float NightTarget()
    {
        var tod = TimeOfDay.Instance;
        if (tod == null) return 0f;
        float h = tod.hour % 24f;
        float f = Mathf.Max(0.01f, fadeHours);
        // gunduz: sunrise..sunset
        float inDay = Mathf.Clamp01((h - tod.sunrise) / f + 0.5f) * Mathf.Clamp01((tod.sunset - h) / f + 0.5f);
        return 1f - inDay;
    }

    void Update()
    {
        if (day == null)
        {
            var go = GameObject.Find(dayAmbienceObject);
            if (go != null) { day = go.GetComponent<AudioSource>(); if (day != null) dayBase = day.volume; }
        }

        night = Mathf.MoveTowards(night, NightTarget(), Time.unscaledDeltaTime * fadeSpeed);

        if (day != null)
        {
            // Kullanici sesi degistirirse (baz) onu al: sadece gece degilken bazi guncelle.
            if (night < 0.001f) dayBase = day.volume;
            else day.volume = dayBase * Mathf.Lerp(1f, dayAmbienceAtNight, night);
        }

        if (clip == null || cricket == null) return;

        if (!playing)
        {
            if (night > 0.6f && Time.time >= nextT)
            {
                cricket.pitch = Random.Range(pitch.x, pitch.y);
                cricket.clip = clip;
                cricket.time = 0f;
                cricket.volume = 0f;
                cricket.Play();
                playing = true;
                clipEnd = Time.time + clip.length / cricket.pitch;
            }
            return;
        }

        float t = clipEnd - Time.time;
        float played = clip.length / cricket.pitch - t;
        float e = Mathf.Min(Mathf.Clamp01(played / Mathf.Max(0.01f, edgeFade)), Mathf.Clamp01(t / Mathf.Max(0.01f, edgeFade)));
        cricket.volume = cricketVolume * e * night;

        if (t <= 0f || night < 0.05f)
        {
            cricket.Stop();
            playing = false;
            nextT = Time.time + Random.Range(gap.x, gap.y);
        }
    }
}
