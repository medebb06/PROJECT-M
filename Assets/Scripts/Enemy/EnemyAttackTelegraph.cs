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

    private Color originalColor;
    private bool isWarning;
    private bool isCommitted;
    private bool isUnblockable;
    private float suppressUntil;

    // Engellenemez vuruşta hız değişince sin() sıçramasın diye faz biriktirilir.
    private float unblockablePhase;
    private float unblockableProgress;
    private float cueUntil;

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

            // Baştan itibaren hızlı sarı; vuruşa yaklaştıkça hızlanır.
            float speed =
                unblockableFlashSpeed *
                Mathf.Lerp(
                    1f,
                    unblockableMaxSpeedMultiplier,
                    unblockableProgress
                );

            unblockablePhase += Time.deltaTime * speed;

            pulse =
                (Mathf.Sin(unblockablePhase) + 1f) * 0.5f;

            pulse =
                Mathf.Lerp(0.55f, 1f, pulse);

            targetColor = unblockableColor;
        }
        else if (isCommitted)
        {
            // Hızlı ve her zaman belirgin kırmızı:
            // "bu saldırı artık vazgeçmeyecek".
            pulse =
                (Mathf.Sin(Time.time * committedFlashSpeed) + 1f) * 0.5f;

            pulse =
                Mathf.Lerp(0.5f, 1f, pulse);

            targetColor = committedColor;
        }
        else
        {
            pulse =
                (Mathf.Sin(Time.time * flashSpeed) + 1f) * 0.5f;

            targetColor = warningColor;
        }

        spriteRenderer.color =
            Color.Lerp(
                originalColor,
                targetColor,
                pulse
            );
    }

    public void StartWarning(bool unblockable = false)
    {
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