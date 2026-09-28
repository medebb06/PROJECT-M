using UnityEngine;

public class EnemyHitFeedback : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Header("Posture Hit")]
    [SerializeField] private AudioClip[] postureHitSounds;
    [SerializeField] private GameObject postureHitVFX;

    [Header("Health Hit")]
    [SerializeField] private AudioClip[] healthHitSounds;
    [SerializeField] private GameObject healthHitVFX;

    [Header("Posture Break")]
    [SerializeField] private AudioClip postureBreakSound;
    [SerializeField] private GameObject postureBreakVFX;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    public void PlayPostureHit(Vector2 hitDirection)
    {
        PlayRandomSound(postureHitSounds);
        SpawnVFX(postureHitVFX, hitDirection);
    }

    public void PlayHealthHit(Vector2 hitDirection)
    {
        PlayRandomSound(healthHitSounds);
        SpawnVFX(healthHitVFX, hitDirection);
    }

    public void PlayPostureBreak(Vector2 hitDirection)
    {
        PlaySound(postureBreakSound);
        SpawnVFX(postureBreakVFX, hitDirection);
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

        Vector3 spawnPosition = transform.position;

        GameObject vfx = Instantiate(
            vfxPrefab,
            spawnPosition,
            Quaternion.identity
        );

        // VFX'in yönünü vuruş yönüne çevirmek istersen
        // prefabın forward eksenini buna göre kullanabiliriz.
        if (hitDirection.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(
                hitDirection.y,
                hitDirection.x
            ) * Mathf.Rad2Deg;

            vfx.transform.rotation =
                Quaternion.Euler(0f, 0f, angle);
        }
    }
}