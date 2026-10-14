using UnityEngine;

public class EnemyAttackTelegraph : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Warning Visual")]
    [SerializeField] private Color warningColor = Color.red;
    [SerializeField] private float flashSpeed = 8f;

    [Header("Commit Visual (saldırı artık kesilemez)")]
    [SerializeField]
    private Color committedColor =
        new Color(1f, 0.15f, 0f);

    [SerializeField] private float committedFlashSpeed = 22f;

    [Header("Unblockable Visual (engellenemez vuruş)")]
    [Tooltip("Parry/block işe yaramayan vuruşun uyarı rengi. Normal uyarıdan çok farklı olsun.")]
    [SerializeField]
    private Color unblockableColor =
        new Color(1f, 0.85f, 0.1f);

    [SerializeField] private float unblockableFlashSpeed = 14f;

    [Tooltip("Vuruşa yaklaştıkça yanıp sönme bu kata kadar hızlanır.")]
    [SerializeField] private float unblockableMaxSpeedMultiplier = 2.4f;

    [Header("Okunabilirlik (tek net işaret)")]
    [Tooltip("Hazırlık başında parlak kırmızı çakma süresi.")]
    [SerializeField] private float warnPopTime = 0.14f;

    [Tooltip("Çakmadan sonra kırmızının yoğunluğu: başlangıç → vuruşa yakın.")]
    [SerializeField] private float warnBaseIntensity = 0.6f;
    [SerializeField] private float warnEndIntensity = 0.9f;

    [Tooltip("Vuruş kesinleşince kısa beyaz çakma süresi.")]
    [SerializeField] private float commitFlashTime = 0.09f;

    private float warnElapsed;
    private float warnDuration = 0.5f;
    private float commitElapsed;

    private Color originalColor;
    private bool isWarning;
    private bool isCommitted;
    private bool isUnblockable;
    private float suppressUntil;

    // Engellenemez vuruşta hız değişince sin() sıçramasın diye faz biriktirilir.
    private float unblockablePhase;
    private float unblockableProgress;
    private float cueUntil;

    public bool IsWarning => isWarning;
    public bool IsCommitted => isCommitted;

    // Gövde başka bir renderer'a taşındığında (kapsül) flaş orada görünsün.
    public void SetRenderer(SpriteRenderer sr)
    {
        if (sr == null)
            return;

        spriteRenderer = sr;
        originalColor = sr.color;
    }

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;
    }

    private void Update()
    {
        if (!isWarning)
            return;

        if (spriteRenderer == null)
            return;

        // Vuruş flaşı görünsün diye kısa süre renk yazmayı bırak.
        if (Time.time < suppressUntil)
            return;

        float pulse;
        Color targetColor;

        if (isUnblockable)
        {
            // "ŞİMDİ KAÇ" işareti: kısa süre tam beyaz.
            if (Time.time < cueUntil)
            {
                spriteRenderer.color = Color.white;
                return;
            }

            // Çakmadan SABİT parlak sarı: "kaçman gerek". Sadece başta kısa bir çakma.
            warnElapsed += EnemyTime.DeltaTime;

            pulse = warnElapsed < warnPopTime ? 1f : 0.9f;

            targetColor = unblockableColor;
        }
        else if (isCommitted)
        {
            // Vuruş kesinleşti: kısa beyaz çakma, sonra sabit parlak kırmızı.
            commitElapsed += EnemyTime.DeltaTime;

            if (commitElapsed < commitFlashTime)
            {
                spriteRenderer.color = Color.white;
                return;
            }

            pulse = 1f;
            targetColor = committedColor;
        }
        else
        {
            // Başta bir kez parlak kırmızı çakma, sonra vuruşa doğru yavaşça yoğunlaşan sabit kırmızı.
            warnElapsed += EnemyTime.DeltaTime;

            if (warnElapsed < warnPopTime)
            {
                pulse = 1f;
            }
            else
            {
                float t = Mathf.Clamp01((warnElapsed - warnPopTime) / Mathf.Max(0.05f, warnDuration - warnPopTime));
                pulse = Mathf.Lerp(warnBaseIntensity, warnEndIntensity, t);
            }

            targetColor = warningColor;
        }

        spriteRenderer.color =
            Color.Lerp(
                originalColor,
                targetColor,
                pulse
            );
    }

    public void StartWarning(bool unblockable = false, float duration = 0.5f)
    {
        warnElapsed = 0f;
        warnDuration = Mathf.Max(0.1f, duration);
        commitElapsed = 0f;
        isWarning = true;
        isCommitted = false;
        isUnblockable = unblockable;
        suppressUntil = 0f;

        unblockablePhase = 0f;
        unblockableProgress = 0f;
        cueUntil = 0f;
    }

    // Uyarı ilerlemesi (0..1): yanıp sönme hızlanır.
    public void SetProgress(float progress)
    {
        unblockableProgress = Mathf.Clamp01(progress);
    }

    // "ŞİMDİ KAÇ": kısa süre tam beyaz.
    public void TriggerCue()
    {
        cueUntil = Time.time + 0.2f;
    }

    // Saldırı kararlı aşamaya geçti.
    // Engellenemez vuruşta renk zaten sabit sarı; kırmızıya dönmesin.
    public void SetCommitted()
    {
        if (isUnblockable)
            return;

        isCommitted = true;
        commitElapsed = 0f;
    }

    // Kararlı saldırı vurulduğunda flaş görünsün diye
    // telegraph rengini kısa süre susturur.
    public void Suppress(float duration)
    {
        suppressUntil = Time.time + duration;
    }

    public void StopWarning()
    {
        isWarning = false;
        isCommitted = false;
        isUnblockable = false;

        if (spriteRenderer != null)
            spriteRenderer.color = originalColor;
    }
}