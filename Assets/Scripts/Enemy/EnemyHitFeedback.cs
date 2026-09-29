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

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
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
        PlaySound(balanceBreakSound);
        SpawnVFX(balanceBreakVFX, hitDirection);
    }

    private void PlayRandomSound(AudioClip[] clips)
    {
        if (audioSource == null)
            return;

        if (clips == null || clips.Length == 0)
            return;

        AudioClip clip = clips[
            Random.Range(0, clips.Length)
        ];

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
            float angle = Mathf.Atan2(
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