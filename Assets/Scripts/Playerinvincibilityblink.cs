using UnityEngine;

/// <summary>
/// Oyuncunun hasar tepkisinin GÖRSEL kısmı:
///  1) Vurulunca kısa beyaz flaş (PlayHitFlash)
///  2) Korumalı dönemde (PlayerController.hitInvincibilityTimer > 0)
///     sprite'ları yanıp söndürme
///
/// Blink, flaş bitene kadar BEKLER: Dimmed Alpha 0 ise sprite
/// vurulduğu an kaybolacağı için flaş hiç görünmezdi.
///
/// PlayerDamageReceiver kendiliğinden ekler.
/// </summary>
public class PlayerInvincibilityBlink : MonoBehaviour
{
    [Header("Hit Flash (vurulduğu an)")]
    [Tooltip(
        "Vurulunca sprite kısa süre tam beyaz olur. " +
        "SpriteWhiteFlash.shader Assets/Resources içinde olmalı.")]
    [SerializeField] private bool solidWhiteFlash = true;

    [SerializeField] private Color flashColor = Color.white;

    [Tooltip("Flaşın süresi (gerçek zaman, saniye).")]
    [SerializeField] private float flashDuration = 0.1f;

    [Header("Blink (korumalı dönem)")]
    [Tooltip("Görünür/sönük geçiş süresi (saniye).")]
    [SerializeField] private float blinkInterval = 0.06f;

    [Tooltip("Sönük anındaki saydamlık (0 = tamamen kapanır, 1 = hiç sönmez).")]
    [Range(0f, 1f)]
    [SerializeField] private float dimmedAlpha = 0.25f;

    private PlayerController player;

    // Blink
    private SpriteRenderer[] renderers;
    private float[] originalAlphas;
    private bool blinking;
    private float blinkClock;

    // Flaş
    private SpriteRenderer[] flashRenderers;
    private Material[] flashOriginalMaterials;
    private Color[] flashOriginalColors;
    private bool flashing;
    private float flashTimer;

    private static Material sharedFlashMaterial;
    private static bool warnedMissingShader;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
    }

    // LateUpdate: Animator ve diğer sistemlerden SONRA çalışsın.
    private void LateUpdate()
    {
        // ---------------- FLAŞ ----------------

        if (flashing)
        {
            flashTimer -= Time.unscaledDeltaTime;

            if (flashTimer <= 0f)
                EndFlash();
        }

        // ---------------- BLINK ----------------

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

    // =========================================================
    // HIT FLASH
    // =========================================================

    public void PlayHitFlash()
    {
        if (!solidWhiteFlash)
            return;

        Material material =
            GetFlashMaterial();

        if (material == null)
            return;

        // Önceki flaş sürüyorsa önce eski haline getir,
        // yoksa "orijinal" olarak flaşlı hali kaydederdik.
        if (flashing)
            EndFlash();

        flashRenderers =
            GetComponentsInChildren<SpriteRenderer>(true);

        flashOriginalMaterials =
            new Material[flashRenderers.Length];

        flashOriginalColors =
            new Color[flashRenderers.Length];

        for (int i = 0; i < flashRenderers.Length; i++)
        {
            SpriteRenderer sr = flashRenderers[i];

            if (sr == null)
                continue;

            flashOriginalMaterials[i] =
                sr.sharedMaterial;

            flashOriginalColors[i] =
                sr.color;

            sr.sharedMaterial = material;

            // Alpha'ya dokunma (ölüm fade'i / blink alpha'yı yönetiyor).
            Color color = sr.color;

            color.r = flashColor.r;
            color.g = flashColor.g;
            color.b = flashColor.b;

            sr.color = color;
        }

        flashTimer = flashDuration;
        flashing = true;
    }

    private void EndFlash()
    {
        flashing = false;

        if (flashRenderers == null)
            return;

        for (int i = 0; i < flashRenderers.Length; i++)
        {
            SpriteRenderer sr = flashRenderers[i];

            if (sr == null)
                continue;

            if (flashOriginalMaterials[i] != null)
                sr.sharedMaterial = flashOriginalMaterials[i];

            // RGB'yi geri ver, güncel alpha'yı koru.
            Color color = sr.color;

            color.r = flashOriginalColors[i].r;
            color.g = flashOriginalColors[i].g;
            color.b = flashOriginalColors[i].b;

            sr.color = color;
        }
    }

    private static Material GetFlashMaterial()
    {
        if (sharedFlashMaterial != null)
            return sharedFlashMaterial;

        Shader shader =
            Shader.Find("Custom/SpriteWhiteFlash");

        if (shader == null)
        {
            if (!warnedMissingShader)
            {
                warnedMissingShader = true;

                Debug.LogWarning(
                    "PlayerInvincibilityBlink: 'Custom/SpriteWhiteFlash' " +
                    "shader'ı bulunamadı. SpriteWhiteFlash.shader dosyasını " +
                    "Assets/Resources/ klasörüne koy."
                );
            }

            return null;
        }

        sharedFlashMaterial =
            new Material(shader);

        return sharedFlashMaterial;
    }

    // =========================================================
    // BLINK
    // =========================================================

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
        float factor;

        if (flashing)
        {
            // Flaş sürerken sprite görünür kalsın; blink saati de ilerlemesin.
            factor = 1f;
        }
        else
        {
            blinkClock += Time.deltaTime;

            // Flaşın hemen ardından sönük başlar.
            bool dimmed =
                Mathf.FloorToInt(
                    blinkClock / Mathf.Max(0.01f, blinkInterval)
                ) % 2 == 0;

            factor =
                dimmed
                    ? dimmedAlpha
                    : 1f;
        }

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
        if (flashing)
            EndFlash();

        if (blinking)
            EndBlink();
    }
}