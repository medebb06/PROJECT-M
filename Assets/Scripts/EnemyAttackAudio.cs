using UnityEngine;

public class EnemyAttackAudio : MonoBehaviour
{
    [Header("Attack Warning")]
    [SerializeField] private AudioClip warningSound;

    [Range(0f, 1f)]
    [SerializeField] private float warningVolume = 1f;

    [Header("Unblockable Warning")]
    [Tooltip("Engellenemez vuruşun uyarı sesi. Boşsa normal uyarı sesi çalar.")]
    [SerializeField] private AudioClip unblockableWarningSound;

    [Range(0f, 1f)]
    [SerializeField] private float unblockableWarningVolume = 1f;

    [Header("Dodge Cue (ŞİMDİ KAÇ)")]
    [Tooltip(
        "Engellenemez vuruşa kısa süre kala çalar: dash zamanı geldi. " +
        "Boşsa sessiz.")]
    [SerializeField] private AudioClip dodgeCueSound;

    [Range(0f, 1f)]
    [SerializeField] private float dodgeCueVolume = 1f;

    [Header("Attack Commit")]
    [Tooltip("Saldırı kesilemez hale geçtiğinde çalar. Boşsa sessiz.")]
    [SerializeField] private AudioClip commitSound;

    [Range(0f, 1f)]
    [SerializeField] private float commitVolume = 1f;

    [SerializeField] private AudioSource audioSource;

    void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
    }

    public void PlayWarning()
    {
        if (warningSound == null)
            return;

        audioSource.PlayOneShot(
            warningSound,
            warningVolume
        );
    }

    public void PlayUnblockableWarning()
    {
        if (unblockableWarningSound == null)
        {
            PlayWarning();
            return;
        }

        audioSource.PlayOneShot(
            unblockableWarningSound,
            unblockableWarningVolume
        );
    }

    public void PlayDodgeCue()
    {
        if (dodgeCueSound == null)
            return;

        audioSource.PlayOneShot(
            dodgeCueSound,
            dodgeCueVolume
        );
    }

    public void PlayCommit()
    {
        if (commitSound == null)
            return;

        audioSource.PlayOneShot(
            commitSound,
            commitVolume
        );
    }
}