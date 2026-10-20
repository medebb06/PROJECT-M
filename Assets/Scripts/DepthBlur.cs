using UnityEngine;

// PROJECT M — Derinlik bulanıklığı: arka plandan öne doğru katman katman blur.
// ProjectM/SilhouetteRim kullanan renderer'lara sorting order'a göre _Blur (ekran pikseli) verir.
// Yeni katman/obje ekleyince Inspector'da sağ tık -> "Apply Depth Blur" ya da bileşeni kapat-aç.
[ExecuteAlways]
public class DepthBlur : MonoBehaviour
{
    [System.Serializable]
    public struct Stop { public int order; public float blur; public Stop(int o, float b) { order = o; blur = b; } }

    [Tooltip("Sorting order -> blur (ekran px). En arkadan öne; aradaki değerler interpolasyonla.")]
    public Stop[] stops = {
        new Stop(-200, 3.2f),
        new Stop(-80, 3.2f),   // bulutlar
        new Stop(-70, 3.0f),   // uzak dağlar
        new Stop(-64, 2.5f),   // orta dağlar
        new Stop(-58, 2.2f),   // yakın dağlar
        new Stop(-50, 2.0f),   // uzak gövdeler
        new Stop(-40, 1.6f),   // arka gövdeler
        new Stop(-31, 1.2f),   // uzun ağaç
        new Stop(-26, 1.0f),   // uzak çalılar
        new Stop(-21, 0.8f),   // küçük ağaç
        new Stop(-19, 0.6f),   // orta çalılar
        new Stop(-17, 0.35f),  // yakın çalılar
        new Stop(-15, 0f),     // arka plan bitti
        new Stop(3, 0f),
        new Stop(4, 0.18f),    // ağaç/çadır
        new Stop(10, 0.28f),   // demirci eşyaları, çimen
        new Stop(11, 0f),      // taş duvar, gölgeler, karakterler net
        new Stop(29, 0f),
        new Stop(30, 1.6f),    // en ön yapraklar
    };
    [Header("Özel blur (derinlik tablosunu ezer) — örn. çit")]
    public Transform[] overrideRoots;
    [Range(0f, 8f)] public float overrideBlur = 3f;
    [Range(0f, 2f)] public float strength = 1f;
    [Tooltip("Ön plan (order > -15) blur çarpanı. 0 = ön plan net.")]
    [Range(0f, 3f)] public float foregroundStrength = 1f;
    public string[] shaderNames = { "ProjectM/SilhouetteRim", "ProjectM/SpriteLitGray" };

    [Header("Ay kenar ışığı (rim) — yakından uzağa azalır")]
    public bool driveRim = true;
    [Tooltip("Sorting order -> rim şiddeti çarpanı (1 = materyaldeki değer).")]
    public Stop[] rimStops = {
        new Stop(-200, 0f), new Stop(-75, 0f), new Stop(-50, 0.12f), new Stop(-40, 0.25f),
        new Stop(-31, 0.45f), new Stop(-21, 0.7f), new Stop(-15, 1f), new Stop(40, 1f),
    };
    [Range(0.02f, 1f), Tooltip("Rim kalınlığı çarpanı (materyaldeki RimWidth ile çarpılır). Küçük = ince.")]
    public float rimWidthScale = 0.3f;
    [Range(0f, 2f)] public float rimStrength = 1f;

    static readonly int RimIntId = Shader.PropertyToID("_RimIntensity");
    static readonly int RimWidId = Shader.PropertyToID("_RimWidth");
    static readonly int BlurId = Shader.PropertyToID("_Blur");
    MaterialPropertyBlock mpb;

    void OnEnable() { Apply(); }
    void OnValidate() { if (isActiveAndEnabled) Apply(); }
    void OnDisable() { Clear(); }

    float Eval(int order) { return EvalStops(stops, order); }

    static float EvalStops(Stop[] stops, int order)
    {
        if (stops == null || stops.Length == 0) return 0f;
        if (order <= stops[0].order) return stops[0].blur;
        for (int i = 1; i < stops.Length; i++)
            if (order <= stops[i].order)
            {
                float t = Mathf.InverseLerp(stops[i - 1].order, stops[i].order, order);
                return Mathf.Lerp(stops[i - 1].blur, stops[i].blur, t);
            }
        return stops[stops.Length - 1].blur;
    }

    bool IsOverridden(Transform tr)
    {
        if (overrideRoots == null) return false;
        foreach (var r in overrideRoots) if (r != null && tr.IsChildOf(r)) return true;
        return false;
    }

    [ContextMenu("Apply Depth Blur")]
    public void Apply()
    {
        if (mpb == null) mpb = new MaterialPropertyBlock();
        foreach (var sr in FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
        {
            var m = sr.sharedMaterial;
            if (m == null || m.shader == null || System.Array.IndexOf(shaderNames, m.shader.name) < 0) continue;
            sr.GetPropertyBlock(mpb);
            if (driveRim && m.shader.name == "ProjectM/SilhouetteRim" && m.HasProperty(RimIntId) && m.HasProperty(RimWidId))
            {
                float k = EvalStops(rimStops, sr.sortingOrder);
                mpb.SetFloat(RimIntId, m.GetFloat(RimIntId) * k * rimStrength);
                mpb.SetFloat(RimWidId, m.GetFloat(RimWidId) * rimWidthScale * Mathf.Lerp(0.6f, 1f, k));
            }
            float bl = Eval(sr.sortingOrder) * strength * (sr.sortingOrder > -15 ? foregroundStrength : 1f);
            if (IsOverridden(sr.transform)) bl = overrideBlur;
            mpb.SetFloat(BlurId, bl);
            sr.SetPropertyBlock(mpb);
        }
    }

    void Clear()
    {
        if (mpb == null) mpb = new MaterialPropertyBlock();
        foreach (var sr in FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
        {
            var m = sr.sharedMaterial;
            if (m == null || m.shader == null || System.Array.IndexOf(shaderNames, m.shader.name) < 0) continue;
            sr.GetPropertyBlock(mpb);
            mpb.SetFloat(BlurId, 0f);
            if (m.HasProperty(RimIntId)) { mpb.SetFloat(RimIntId, m.GetFloat(RimIntId)); mpb.SetFloat(RimWidId, m.GetFloat(RimWidId)); }
            sr.SetPropertyBlock(mpb);
        }
    }
}
