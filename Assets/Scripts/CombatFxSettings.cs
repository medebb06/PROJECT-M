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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { cached = null; loaded = false; }
}
