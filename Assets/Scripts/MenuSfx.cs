using UnityEngine;

/// <summary>
/// MENU SESLERI: gezinme (W/S) ve secim/tiklama icin Resources/MenuSfx/menu_click kullanilir.
/// START sesi ayri: menu_start.
/// </summary>
public static class MenuSfx
{
    public static float NavVolume = 1f;
    public static float SelectVolume = 1f;
    public static Vector2 NavPitch = new Vector2(0.97f, 1.03f);

    static AudioClip click;
    static AudioSource src;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { click = null; src = null; startClip = null; }

    static void Init()
    {
        if (click == null) click = Resources.Load<AudioClip>("MenuSfx/menu_click");
        if (src == null)
        {
            var go = new GameObject("MenuSfx");
            Object.DontDestroyOnLoad(go);
            src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.ignoreListenerPause = true;
        }
    }

    static void Play(float vol, float pitch)
    {
        if (click == null || src == null) return;
        src.pitch = pitch;
        src.PlayOneShot(click, vol);
    }

    /// <summary>START'a basinca calan giris sesi (Resources/MenuSfx/menu_start).</summary>
    public static float StartVolume = 1f;
    static AudioClip startClip;
    public static void StartGame()
    {
        Init();
        if (src == null) return;
        if (startClip == null) startClip = Resources.Load<AudioClip>("MenuSfx/menu_start");
        if (startClip == null) return;
        src.pitch = 1f;
        src.PlayOneShot(startClip, StartVolume);
    }

    public static void Nav() { Init(); Play(NavVolume, Random.Range(NavPitch.x, NavPitch.y)); }
    public static void Select() { Init(); Play(SelectVolume, 1f); }
}
