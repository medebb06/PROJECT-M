using UnityEngine;

/// <summary>
/// Fener parlaması: gece ve akşam açılır, gündüz söner; hafif titreşir (mum gibi).
/// Gece değeri NearMoonRim'in global _ProjectM_NightRim değerinden gelir.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(10080)]
public class NightLanternGlow : MonoBehaviour
{
    [Range(0f, 1.5f)] public float strength = 1f;
    [Range(0f, 1f)] public float maxAlpha = 0.75f;
    [Tooltip("Gündüz de hafifçe görünsün (0..1).")]
    [Range(0f, 1f)] public float dayAmount = 0.06f;
    [Range(0f, 0.5f)] public float flicker = 0.12f;
    public float flickerSpeed = 2.3f;

    private static readonly int NightId = Shader.PropertyToID("_ProjectM_NightRim");
    private SpriteRenderer sr;
    private float baseScale = -1f;

    private void LateUpdate()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;
        if (baseScale < 0f) baseScale = transform.localScale.x;

        float night = Mathf.Clamp01(Shader.GetGlobalFloat(NightId));
        float t = Application.isPlaying ? Time.time : 0f;
        float f = 1f - flicker * (0.5f + 0.5f * Mathf.Sin(t * flickerSpeed) * Mathf.Sin(t * flickerSpeed * 0.37f + 1.3f));
        Color c = sr.color;
        c.a = Mathf.Clamp01(Mathf.Lerp(dayAmount, 1f, night) * strength * maxAlpha * f);
        sr.color = c;
    }
}
