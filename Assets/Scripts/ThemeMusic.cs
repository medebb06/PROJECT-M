using UnityEngine;

// Resources/Music/Theme_01..N.mp3 dosyalarini sirayla calar.
// Her sarki yavasca baslar (fade in), sonunda yavasca biter (fade out); liste bitince basa doner.
public class ThemeMusic : MonoBehaviour
{
    public static ThemeMusic Instance;

    [Range(0f, 1f)] public float volume = 0.45f;
    [Tooltip("Sarki basinda ses acilma suresi (sn).")] public float fadeIn = 3f;
    [Tooltip("Sarki sonunda ses kisilma suresi (sn).")] public float fadeOut = 4f;
    [Tooltip("Sarkilar arasi sessizlik (sn).")] public float gap = 0.8f;

    AudioSource src;
    AudioClip[] clips;
    int index = -1;
    float gapT;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        var go = new GameObject("ThemeMusic");
        DontDestroyOnLoad(go);
        go.AddComponent<ThemeMusic>();
    }

    void Awake()
    {
        Instance = this;
        clips = Resources.LoadAll<AudioClip>("Music");
        System.Array.Sort(clips, (a, b) => string.CompareOrdinal(a.name, b.name));
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false; src.loop = false; src.spatialBlend = 0f; src.volume = 0f;
        src.ignoreListenerPause = true;
        gapT = 0.5f;
    }

    void Update()
    {
        if (clips == null || clips.Length == 0) return;

        if (src.clip == null || (!src.isPlaying && Application.isFocused))
        {
            gapT -= Time.unscaledDeltaTime;
            if (gapT > 0f) return;
            index = (index + 1) % clips.Length;
            src.clip = clips[index];
            src.time = 0f; src.volume = 0f; src.Play();
            gapT = gap;
            return;
        }

        float len = src.clip.length, t = src.time;
        float g = 1f;
        if (fadeIn > 0f) g = Mathf.Min(g, t / fadeIn);
        if (fadeOut > 0f) g = Mathf.Min(g, (len - t) / fadeOut);
        g = Mathf.Clamp01(g);
        src.volume = volume * g * g * (3f - 2f * g);
    }
}
