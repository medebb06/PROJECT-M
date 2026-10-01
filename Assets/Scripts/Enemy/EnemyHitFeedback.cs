
using UnityEngine;

public class EnemyHitFeedback : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Header("Balance Hit")]
    [SerializeField] private AudioClip[] balanceHitSounds;
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
    [SerializeField] private GameObject parryVFX;

    [Header("Block")]
    [SerializeField] private AudioClip blockSound;
    [SerializeField] private GameObject blockVFX;

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
            transform.position,
            hitDirection
        );
    }

    public void PlayBalanceHit(
        Vector3 hitPosition,
        Vector2 hitDirection)
    {
        PlayRandomSound(balanceHitSounds);

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
        PlayRandomSound(healthHitSounds);

        if (CombatVFXManager.Instance != null)
        {
            CombatVFXManager.Instance.PlayHealthHit(
                transform.position,
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
        else
        {
            balanceBreakAudioSource.PlayOneShot(
                balanceBreakSound
            );

            Debug.Log(
                "BALANCE BREAK SOUND PLAYED!"
            );
        }

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
        PlaySound(
            parrySound
        );

        if (CombatVFXManager.Instance != null)
        {
            CombatVFXManager.Instance.PlayParry(
                transform.position,
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
        PlaySound(
            blockSound
        );

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

