using UnityEngine;

/// <summary>
/// TEK MERKEZ: oyuncunun düşmana vurduğu / parry-block yaptığı anların sesleri.
/// Resources/CombatFxSettings.asset içinden düzenlenir; tüm düşmanlar (EnemyUnit, legacy, boss)
/// kendi prefab'ındaki klipler yerine bunları kullanır. Boş bırakılan alan prefab'daki eski klibe düşer.
/// VFX zaten CombatVFXManager'da tek yerde.
/// </summary>
[CreateAssetMenu(menuName = "Project M/Combat Fx Settings")]
public class CombatFxSettings : ScriptableObject
{
    [Header("Cana vuruş")]
    public AudioClip[] healthHitSounds;
    [Header("Posture vuruşu")]
    public AudioClip[] balanceHitSounds;
    [Header("Posture kırılma")]
    public AudioClip balanceBreakSound;
    [Header("Parry")]
    public AudioClip parrySound;
    [Range(0f, 3f)] public float parryVolume = 2f;
    [Header("Block")]
    public AudioClip blockSound;

    private static CombatFxSettings cached;
    private static bool loaded;

    public static CombatFxSettings Get()
    {
        if (!loaded || cached == null)
        {
            cached = Resources.Load<CombatFxSettings>("CombatFxSettings");
            loaded = cached != null;
        }
        return cached;
    }

    private static AudioSource src2d;

    /// <summary>Parry sesi: düşmanın kendi AudioSource'una değil, tek merkezi 2D kaynağa çalınır (mesafe/mixer bağımsız).</summary>
    public static void PlayParry2D(AudioClip fallbackClip, float fallbackVolume)
    {
        CombatFxSettings s = Get();
        bool central = s != null && s.parrySound != null;
        AudioClip c = central ? s.parrySound : fallbackClip;
        float v = central ? s.parryVolume : fallbackVolume;
        if (c == null) { Debug.LogWarning("CombatFx: parry sesi atanmamış."); return; }
        if (src2d == null)
        {
            GameObject go = new GameObject("CombatFxAudio");
            Object.DontDestroyOnLoad(go);
            src2d = go.AddComponent<AudioSource>();
            src2d.playOnAwake = false;
            src2d.spatialBlend = 0f;
        }
        EnemyHitFeedback.PlayLoud(src2d, c, v);
    }

    public static void PlayBlock2D()
    {
        CombatFxSettings s = Get();
        if (s == null || s.blockSound == null) return;
        PlayParry2DClip(s.blockSound, 1f);
    }

    private static void PlayParry2DClip(AudioClip c, float v)
    {
        if (src2d == null)
        {
            GameObject go = new GameObject("CombatFxAudio");
            Object.DontDestroyOnLoad(go);
            src2d = go.AddComponent<AudioSource>();
            src2d.playOnAwake = false;
            src2d.spatialBlend = 0f;
        }
        EnemyHitFeedback.PlayLoud(src2d, c, v);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { cached = null; loaded = false; src2d = null; }
}
