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

    public void PlayBalanceHit(Vector2 hitDirection)
    {
        PlayRandomSound(balanceHitSounds);
        SpawnVFX(balanceHitVFX, hitDirection);
    }

    public void PlayHealthHit(Vector2 hitDirection)
    {
        PlayRandomSound(healthHitSounds);
        SpawnVFX(healthHitVFX, hitDirection);
    }

    public void PlayBalanceBreak(Vector2 hitDirection)
    {
        Debug.Log("BALANCE BREAK FEEDBACK!");

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

        SpawnVFX(
            balanceBreakVFX,
            hitDirection
        );
    }

    public void PlayParry(Vector2 hitDirection)
    {
        PlaySound(parrySound);
        SpawnVFX(parryVFX, hitDirection);
    }

    public void PlayBlock(Vector2 hitDirection)
    {
        PlaySound(blockSound);
        SpawnVFX(blockVFX, hitDirection);
    }

    private void PlayRandomSound(AudioClip[] clips)
    {
        if (audioSource == null)
            return;

        if (clips == null || clips.Length == 0)
            return;

        AudioClip clip =
            clips[Random.Range(0, clips.Length)];

        if (clip == null)
            return;

        audioSource.PlayOneShot(clip);
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource == null)
            return;

        if (clip == null)
            return;

        audioSource.PlayOneShot(clip);
    }

    private void SpawnVFX(
        GameObject vfxPrefab,
        Vector2 hitDirection
    )
    {
        if (vfxPrefab == null)
            return;

        Vector3 spawnPosition =
            transform.position;

        GameObject vfx = Instantiate(
            vfxPrefab,
            spawnPosition,
            Quaternion.identity
        );

        if (hitDirection.sqrMagnitude > 0.01f)
        {
            float angle =
                Mathf.Atan2(
                    hitDirection.y,
                    hitDirection.x
                ) * Mathf.Rad2Deg;

            vfx.transform.rotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    angle
                );
        }
    }
}