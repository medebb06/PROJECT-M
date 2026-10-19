using UnityEngine;

/// <summary>
/// Çite ay ışığı parlaması: her parçanın altındaki "~moonrim" çocukları (ince, soğuk, toplamalı kenar ışığı)
/// gece parlar, gündüz söner. Gece/gündüz ve renk NearMoonRim'in global değerlerinden gelir
/// (_ProjectM_NightRim, _ProjectM_NearRimColor). Kenar ışığını açıp kapatmak için strength'i 0 yap.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(10080)]
public class FenceMoonGlow : MonoBehaviour
{
    [Range(0f, 2f)] public float strength = 1f;
    [Tooltip("Gece parlamanın en fazla ne kadar belirgin olacağı (0..1 alfa).")]
    [Range(0f, 1f)] public float maxAlpha = 0.9f;

    private static readonly int NightId = Shader.PropertyToID("_ProjectM_NightRim");
    private static readonly int ColorId = Shader.PropertyToID("_ProjectM_NearRimColor");
    private SpriteRenderer[] rims;
    private float rescan;

    private void LateUpdate()
    {
        if (rims == null || Time.realtimeSinceStartup > rescan)
        {
            rescan = Time.realtimeSinceStartup + 2f;
            var list = new System.Collections.Generic.List<SpriteRenderer>();
            foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
                if (sr.gameObject.name == "~moonrim") list.Add(sr);
            rims = list.ToArray();
        }

        float night = Mathf.Clamp01(Shader.GetGlobalFloat(NightId));
        Color moon = Shader.GetGlobalVector(ColorId);
        if (moon.r + moon.g + moon.b < 0.01f) moon = new Color(0.62f, 0.80f, 1f);
        moon = new Color(Mathf.Clamp01(moon.r), Mathf.Clamp01(moon.g), Mathf.Clamp01(moon.b), Mathf.Clamp01(night * strength * maxAlpha));

        foreach (SpriteRenderer sr in rims)
            if (sr != null) sr.color = moon;
    }
}
