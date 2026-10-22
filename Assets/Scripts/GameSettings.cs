using UnityEngine;

/// <summary>
/// OYUN AYARLARI (kalıcı, PlayerPrefs). Ayarlar menüsü (RunUI) buradan okur/yazar.
///
///   UiScale        : TÜM ekran arayüzlerinin (RunUI, PlayerHud, düşman
///                    çubukları) ortak ölçeği. Her bileşenin kendi 'UI Scale'
///                    alanı bunun ÜSTÜNE çarpan olarak kalır.
///   WorldTextScale : dünyadaki yazılar (İNFAZ!, DASH!, kapı yazıları,
///                    kritik sayıları) — TextMesh characterSize çarpanı.
///   Volume         : ana ses (AudioListener.volume).
///   Fullscreen     : tam ekran.
/// </summary>
public static class GameSettings
{
    private const string KeyUi = "projectm_ui_scale";
    private const string KeyWorld = "projectm_world_text_scale";
    private const string KeyVolume = "projectm_volume";
    private const string KeyFullscreen = "projectm_fullscreen";
    private const string KeyCine = "projectm_cinematic_cam";
    private const string KeyBars = "projectm_cinematic_bars";
    private const string KeyMusic = "projectm_music_volume";
    private const string KeyRain = "projectm_rain_on";
    public const float MaxBarHeight = 0.35f;

    public const float MinUiScale = 0.6f;
    public const float MaxUiScale = 1.6f;

    private static bool loaded;
    private static float uiScale = 1f;
    private static float worldTextScale = 1f;
    private static float volume = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        loaded = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyOnStart()
    {
        Load();
        AudioListener.volume = volume;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => ApplyAmbience();
        ApplyAmbience();
    }

    private static void Load()
    {
        if (loaded)
            return;

        loaded = true;

        uiScale = Mathf.Clamp(PlayerPrefs.GetFloat(KeyUi, 1f), MinUiScale, MaxUiScale);
        worldTextScale = Mathf.Clamp(PlayerPrefs.GetFloat(KeyWorld, 1f), MinUiScale, MaxUiScale);
        volume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyVolume, 1f));
    }

    public static float UiScale
    {
        get { Load(); return uiScale; }
        set
        {
            Load();
            uiScale = Mathf.Clamp(Round(value), MinUiScale, MaxUiScale);
            PlayerPrefs.SetFloat(KeyUi, uiScale);
        }
    }

    public static float WorldTextScale
    {
        get { Load(); return worldTextScale; }
        set
        {
            Load();
            worldTextScale = Mathf.Clamp(Round(value), MinUiScale, MaxUiScale);
            PlayerPrefs.SetFloat(KeyWorld, worldTextScale);
        }
    }

    public static float Volume
    {
        get { Load(); return volume; }
        set
        {
            Load();
            volume = Mathf.Clamp01(Round(value));
            AudioListener.volume = volume;
            PlayerPrefs.SetFloat(KeyVolume, volume);
        }
    }

    public static bool Fullscreen
    {
        get => Screen.fullScreen;
        set
        {
            Screen.fullScreen = value;
            PlayerPrefs.SetInt(KeyFullscreen, value ? 1 : 0);
        }
    }

    /// <summary>Sinematik kamera (bekleyince nefes alan, sallanan kamera). Varsayılan açık.</summary>
    public static bool CinematicCamera
    {
        get { return PlayerPrefs.GetInt(KeyCine, 1) != 0; }
        set { PlayerPrefs.SetInt(KeyCine, value ? 1 : 0); }
    }

    /// <summary>Sinematik şerit yüksekliği (ekran yüksekliğine oran, her biri). 0..MaxBarHeight; varsayılan 0.16.</summary>
    public static float BarHeight
    {
        get { return Mathf.Clamp(PlayerPrefs.GetFloat(KeyBars, 0.16f), 0f, MaxBarHeight); }
        set { PlayerPrefs.SetFloat(KeyBars, Mathf.Clamp(Mathf.Round(value * 100f) / 100f, 0f, MaxBarHeight)); }
    }


    /// <summary>Müzik sesi (0..1). Sahnedeki 'Theme Music' kaynağını açar/ayarlar. Varsayılan 0.5.</summary>
    public static float MusicVolume
    {
        get { return Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMusic, 0.5f)); }
        set { PlayerPrefs.SetFloat(KeyMusic, Mathf.Clamp01(Round(value))); ApplyAmbience(); }
    }

    /// <summary>Yağmur açık/kapalı (varsayılan açık).</summary>
    public static bool RainEnabled
    {
        get { return PlayerPrefs.GetInt(KeyRain, 1) != 0; }
        set { PlayerPrefs.SetInt(KeyRain, value ? 1 : 0); ApplyAmbience(); }
    }

    /// <summary>Müzik + yağmur ayarını sahneye uygular.</summary>
    public static void ApplyAmbience()
    {
        if (!Application.isPlaying)
            return;

        float mv = MusicVolume;

        foreach (AudioSource s in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (s == null || s.gameObject.name != "Theme Music")
                continue;

            s.volume = mv;
            if (s.GetComponent<MusicShuffle>() == null)
                s.gameObject.AddComponent<MusicShuffle>();

            s.enabled = mv > 0.001f;   // çalma sırası MusicShuffle'da (rastgele)
        }

        bool rain = RainEnabled;

        foreach (RainSystem r in Object.FindObjectsByType<RainSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r == null)
                continue;

            if (rain && !r.gameObject.activeSelf)
                r.gameObject.SetActive(true);

            r.raining = rain;
        }
    }

    /// <summary>OnGUI ölçeği: ekran yüksekliği 720'ye göre × bileşen çarpanı × ayar.</summary>
    public static float GuiScale(float componentScale)
    {
        return Screen.height / 720f * componentScale * UiScale * MobileInput.HudMul;
    }

    /// <summary>Menüden çıkarken bir kez diske yaz.</summary>
    public static void Save()
    {
        PlayerPrefs.Save();
    }

    // Kaydırıcı titremesin: 0.05 adım.
    private static float Round(float v)
    {
        return Mathf.Round(v * 20f) / 20f;
    }
}
