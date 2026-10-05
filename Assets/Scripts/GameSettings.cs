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

    /// <summary>OnGUI ölçeği: ekran yüksekliği 720'ye göre × bileşen çarpanı × ayar.</summary>
    public static float GuiScale(float componentScale)
    {
        return Screen.height / 720f * componentScale * UiScale;
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
