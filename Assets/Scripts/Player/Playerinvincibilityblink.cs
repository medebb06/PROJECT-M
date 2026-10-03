using UnityEngine;

/// <summary>
/// Hasar sonrası korumalı dönemde (PlayerController.hitInvincibilityTimer > 0)
/// oyuncunun sprite'larını yanıp söndürür.
///
/// Oyuncuya "şu an dokunulmazsın" bilgisini verir; yoksa neden
/// vurulmadığını anlamaz. PlayerDamageReceiver kendiliğinden ekler,
/// elle eklemene gerek yok.
/// </summary>
public class PlayerInvincibilityBlink : MonoBehaviour
{
    [Header("Blink")]
    [Tooltip("Görünür/sönük geçiş süresi (saniye).")]
    [SerializeField] private float blinkInterval = 0.06f;

    [Tooltip("Sönük anındaki saydamlık (1 = hiç sönmez).")]
    [Range(0f, 1f)]
    [SerializeField] private float dimmedAlpha = 0.25f;

    private PlayerController player;

    private SpriteRenderer[] renderers;
    private float[] originalAlphas;

    private bool blinking;
    private float blinkClock;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
    }

    // LateUpdate: Animator ve diğer sistemlerden SONRA çalışsın.
    private void LateUpdate()
    {
        bool shouldBlink =
            player != null &&
            player.hitInvincibilityTimer > 0f &&
            !IsDeathState();

        if (shouldBlink)
        {
            if (!blinking)
                BeginBlink();

            ApplyBlink();
        }
        else if (blinking)
        {
            EndBlink();
        }
    }

    private bool IsDeathState()
    {
        return
            player.stateMachine != null &&
            player.stateMachine.CurrentState is PlayerDeathState;
    }

    private void BeginBlink()
    {
        renderers =
            GetComponentsInChildren<SpriteRenderer>(true);

        originalAlphas =
            new float[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            originalAlphas[i] =
                renderers[i].color.a;
        }

        blinkClock = 0f;
        blinking = true;
    }

    private void ApplyBlink()
    {
        blinkClock += Time.deltaTime;

        // Vurulduğu an sönük başlar: vuruşun anlık geri bildirimi.
        bool dimmed =
            Mathf.FloorToInt(
                blinkClock / Mathf.Max(0.01f, blinkInterval)
            ) % 2 == 0;

        float factor =
            dimmed
                ? dimmedAlpha
                : 1f;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            Color color =
                renderers[i].color;

            color.a =
                originalAlphas[i] * factor;

            renderers[i].color =
                color;
        }
    }

    private void EndBlink()
    {
        blinking = false;

        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            Color color =
                renderers[i].color;

            color.a =
                originalAlphas[i];

            renderers[i].color =
                color;
        }
    }

    // Ölüm gibi durumlarda alfa'yı hemen eski haline getirir.
    public void ResetVisuals()
    {
        if (blinking)
            EndBlink();
    }

    private void OnDisable()
    {
        if (blinking)
            EndBlink();
    }
}