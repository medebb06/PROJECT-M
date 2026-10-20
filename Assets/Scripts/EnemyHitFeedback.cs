
using UnityEngine;

public class EnemyHitFeedback : MonoBehaviour
{
    public Vector3 vfxOffset = Vector3.zero; // vurus efekti kaymasi (pivot ayaktaysa govdeye cikar)

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Header("Balance Hit")]
    [SerializeField] private AudioClip[] balanceHitSounds;
    [Tooltip("Kapalı: eski posture vuruş sesi çalmaz (yerine HitClickAudio adım sesleri).")]
    [SerializeField] private bool playOldBalanceHitSound = false;
    [Tooltip("Kapalı: eski posture kırılma sesi çalmaz (yerine HitClickAudio break sesi).")]
    [SerializeField] private bool playOldBalanceBreakSound = false;
    [SerializeField] private GameObject balanceHitVFX;

    [Header("Health Hit")]
    [SerializeField] private AudioClip[] healthHitSounds;
    [SerializeField] private GameObject healthHitVFX;

    [Header("Balance Break")]
    [SerializeField] private AudioClip balanceBreakSound;
    [SerializeField] private GameObject balanceBreakVFX;

    [Header("Balance Break Audio")]
    [SerializeField] private AudioSource balanceBreakAudioSource;

    [Header("Parry")]
    [SerializeField] private AudioClip parrySound;
    [Tooltip("Parry sesi seviyesi. 1 = klibin kendi sesi; 1'in uzeri ayni klibi ust uste calarak yukseltir (en fazla 3).")]
    [SerializeField, Range(0f, 3f)] private float parryVolume = 2f;
    [SerializeField] private GameObject parryVFX;

    [Header("Block")]
    [SerializeField] private AudioClip blockSound;
    [SerializeField] private GameObject blockVFX;

    // Dışarıdan (ör. TrainingDummy) aynı sesleri kullanmak için.
    // Merkezi ayar (Resources/CombatFxSettings) doluysa o kullanilir; yoksa prefab'daki klip.
    private static AudioClip[] Pick(AudioClip[] central, AudioClip[] local) { return central != null && central.Length > 0 ? central : local; }
    private static AudioClip Pick(AudioClip central, AudioClip local) { return central != null ? central : local; }
    public AudioClip[] BalanceHitSounds { get { var s = CombatFxSettings.Get(); return Pick(s != null ? s.balanceHitSounds : null, balanceHitSounds); } }
    public AudioClip[] HealthHitSounds { get { var s = CombatFxSettings.Get(); return Pick(s != null ? s.healthHitSounds : null, healthHitSounds); } }
    public AudioClip BalanceBreakSound { get { var s = CombatFxSettings.Get(); return Pick(s != null ? s.balanceBreakSound : null, balanceBreakSound); } }
    public AudioClip ParrySound { get { var s = CombatFxSettings.Get(); return Pick(s != null ? s.parrySound : null, parrySound); } }
    public float ParryVolume { get { var s = CombatFxSettings.Get(); return s != null && s.parrySound != null ? s.parryVolume : parryVolume; } }

    // Klibi 'volume' seviyesinde cal: 1'in ustu icin ayni klip katman katman calinir (PlayOneShot olcegi 1'i asmaz).
    public static void PlayLoud(AudioSource src, AudioClip clip, float volume)
    {
        if (src == null || clip == null) return;
        float remaining = Mathf.Clamp(volume, 0f, 3f);
        for (int i = 0; i < 3 && remaining > 0.001f; i++)
        {
            src.PlayOneShot(clip, Mathf.Min(1f, remaining));
            remaining -= 1f;
        }
    }
    public AudioClip BlockSound => blockSound;
    public AudioSource Source => audioSource;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (balanceBreakAudioSource == null)
        {
            balanceBreakAudioSource =
                gameObject.AddComponent<AudioSource>();

            balanceBreakAudioSource.playOnAwake = false;
            balanceBreakAudioSource.loop = false;

            if (audioSource != null)
            {
                balanceBreakAudioSource.outputAudioMixerGroup =
                    audioSource.outputAudioMixerGroup;

                balanceBreakAudioSource.volume =
                    audioSource.volume;

                balanceBreakAudioSource.pitch =
                    audioSource.pitch;
            }
        }
    }

    // =========================================================
    // BALANCE HIT
    // =========================================================

    public void PlayBalanceHit(Vector2 hitDirection)
    {
        PlayBalanceHit(
            transform.position + vfxOffset,
            hitDirection
        );
    }

    public void PlayBalanceHit(
        Vector3 hitPosition,
        Vector2 hitDirection)
    {
        // Posture vuruş sesi artık HitClickAudio (adım sesleri) tarafından çalınıyor.
        if (playOldBalanceHitSound)
            PlayRandomSound(BalanceHitSounds);

        if (CombatVFXManager.Instance != null)
        {
            CombatVFXManager.Instance.PlayBalanceHit(
                hitPosition,
                hitDirection
            );
        }
    }

    // =========================================================
    // HEALTH HIT
    // =========================================================

    public void PlayHealthHit(Vector2 hitDirection)
    {
        PlayRandomSound(HealthHitSounds);

        if (CombatVFXManager.Instance != null)
        {
            CombatVFXManager.Instance.PlayHealthHit(
                transform.position + vfxOffset,
                hitDirection
            );
        }
    }

    // =========================================================
    // BALANCE BREAK
    // =========================================================

    public void PlayBalanceBreak(
        Vector2 hitDirection
    )
    {
        Debug.Log(
            "BALANCE BREAK FEEDBACK!"
        );

        if (balanceBreakSound == null)
        {
            Debug.LogWarning(
                "Balance Break Sound atanmadı!"
            );
        }
        else if (balanceBreakAudioSource == null)
        {
            Debug.LogWarning(
                "Balance Break AudioSource bulunamadı!"
            );
        }
        else if (playOldBalanceBreakSound)
        {
            balanceBreakAudioSource.PlayOneShot(
                balanceBreakSound
            );

            Debug.Log(
                "BALANCE BREAK SOUND PLAYED!"
            );
        }

        HitClickAudio.PlayPostureBreak();

        BalanceBreakFx.Play(transform.position + Vector3.up * 1.2f);

        if (CombatVFXManager.Instance != null)
        {
            CombatVFXManager.Instance.PlayBalanceBreak(
                transform.position,
                hitDirection
            );
        }
    }

    // =========================================================
    // PARRY
    // =========================================================

    public void PlayParry(
        Vector2 hitDirection
    )
    {
        PlayLoud(
            audioSource,
            ParrySound,
            ParryVolume
        );

        if (CombatVFXManager.Instance != null)
        {
            // Efekt kılıçların çarpıştığı noktada (düşmanın ayağında değil).
            CombatVFXManager.Instance.PlayParry(
                ParryVFX.ContactPoint(transform),
                hitDirection
            );
        }
    }

    // =========================================================
    // BLOCK
    // =========================================================

    public void PlayBlock(
        Vector2 hitDirection
    )
    {
        {
            var cs = CombatFxSettings.Get();
            PlaySound(cs != null && cs.blockSound != null ? cs.blockSound : blockSound);
        }

        if (CombatVFXManager.Instance != null)
        {
            CombatVFXManager.Instance.PlayBlock(
                transform.position,
                hitDirection
            );
        }
    }

    // =========================================================
    // AUDIO
    // =========================================================

    private void PlayRandomSound(
        AudioClip[] clips
    )
    {
        if (audioSource == null)
            return;

        if (
            clips == null ||
            clips.Length == 0
        )
            return;

        AudioClip clip =
            clips[
                Random.Range(
                    0,
                    clips.Length
                )
            ];

        if (clip == null)
            return;

        audioSource.PlayOneShot(
            clip
        );
    }

    private void PlaySound(
        AudioClip clip
    )
    {
        if (audioSource == null)
            return;

        if (clip == null)
            return;

        audioSource.PlayOneShot(
            clip
        );
    }
}

