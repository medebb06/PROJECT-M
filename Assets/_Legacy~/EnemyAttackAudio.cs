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

    [Header("Yumuşak uyarı (Resources/EnemyWarn)")]
    [Tooltip("Klasörde klip varsa normal uyarıda bunlardan biri rastgele çalar (eski uyarı sesi yerine).")]
    [SerializeField] private float softVolume = 0.8f;
    [SerializeField] private Vector2 softPitch = new Vector2(0.94f, 1.08f);
    [Tooltip("Engellenemez saldırıda aynı ses bu çarpanla daha tiz çalar.")]
    [SerializeField] private float unblockablePitchMul = 1.25f;

    private static AudioClip[] softClips;
    private static int lastSoft = -1;

    private bool PlaySoft(float pitchMul)
    {
        if (softClips == null)
            softClips = Resources.LoadAll<AudioClip>("EnemyWarn");

        if (softClips.Length == 0)
            return false;

        int i = Random.Range(0, softClips.Length);

        if (softClips.Length > 1 && i == lastSoft)
            i = (i + 1 + Random.Range(0, softClips.Length - 1)) % softClips.Length;

        lastSoft = i;

        audioSource.pitch = Random.Range(softPitch.x, softPitch.y) * pitchMul;
        audioSource.PlayOneShot(softClips[i], softVolume);

        return true;
    }

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
        if (PlaySoft(1f))
            return;

        if (warningSound == null)
            return;

        audioSource.pitch = 1f;
        audioSource.PlayOneShot(
            warningSound,
            warningVolume
        );
    }

    public void PlayUnblockableWarning()
    {
        if (unblockableWarningSound == null && PlaySoft(unblockablePitchMul))
            return;

        if (unblockableWarningSound == null)
        {
            PlayWarning();
            return;
        }

        audioSource.pitch = 1f;
        audioSource.PlayOneShot(
            unblockableWarningSound,
            unblockableWarningVolume
        );
    }

    public void PlayDodgeCue()
    {
        if (dodgeCueSound == null)
            return;

        audioSource.pitch = 1f;
        audioSource.PlayOneShot(
            dodgeCueSound,
            dodgeCueVolume
        );
    }

    public void PlayCommit()
    {
        if (commitSound == null)
            return;

        audioSource.pitch = 1f;
        audioSource.PlayOneShot(
            commitSound,
            commitVolume
        );
    }
}