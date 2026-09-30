using UnityEngine;

public class EnemyAttackTelegraph : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Warning Visual")]
    [SerializeField] private Color warningColor = Color.red;
    [SerializeField] private float flashSpeed = 8f;

    private Color originalColor;
    private bool isWarning;

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

        float pulse =
            (Mathf.Sin(Time.time * flashSpeed) + 1f) * 0.5f;

        spriteRenderer.color =
            Color.Lerp(
                originalColor,
                warningColor,
                pulse
            );
    }

    public void StartWarning()
    {
        isWarning = true;
    }

    public void StopWarning()
    {
        isWarning = false;

        if (spriteRenderer != null)
            spriteRenderer.color = originalColor;
    }
}