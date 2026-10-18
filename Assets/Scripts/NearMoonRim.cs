using UnityEngine;

/// <summary>
/// Yakin objelere gece ince ay parlamasi: kuresel _ProjectM_NightRim (gece 1, gunduz 0) ve ay rengi.
/// Materyalde _NearRim > 0 olan (agac, cimen, on plan, tas, kutuk) objeler parlar.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(10070)]
public class NearMoonRim : MonoBehaviour
{
    [Range(0f, 2f)] public float strength = 1f;
    public Color moonColor = new Color(0.62f, 0.80f, 1.15f);
    [Tooltip("Gun batimi/dogumunda da hafif parlasin mi (0 = sadece gece).")]
    [Range(0f, 1f)] public float duskAmount = 0.25f;

    private TimeOfDay tod;
    private static readonly int NightId = Shader.PropertyToID("_ProjectM_NightRim");
    private static readonly int DirId = Shader.PropertyToID("_ProjectM_NearRimDir");
    private static readonly int MoonPosId = Shader.PropertyToID("_ProjectM_MoonPos");
    private static readonly int ColorId = Shader.PropertyToID("_ProjectM_NearRimColor");

    private void LateUpdate()
    {
        if (tod == null) tod = FindObjectOfType<TimeOfDay>();
        float h = tod != null ? Mathf.Repeat(tod.hour, 24f) : 0f;
        float night = (h <= 4.5f || h >= 20.5f) ? 1f
            : h < 6.5f ? Mathf.Lerp(1f, duskAmount, Mathf.InverseLerp(4.5f, 6.5f, h))
            : h < 8.5f ? Mathf.Lerp(duskAmount, 0f, Mathf.InverseLerp(6.5f, 8.5f, h))
            : h <= 16.5f ? 0f
            : h < 18.5f ? Mathf.Lerp(0f, duskAmount, Mathf.InverseLerp(16.5f, 18.5f, h))
            : Mathf.Lerp(duskAmount, 1f, Mathf.InverseLerp(18.5f, 20.5f, h));
        Shader.SetGlobalFloat(NightId, night * strength);
        Shader.SetGlobalVector(ColorId, moonColor);
        Vector2 d = new Vector2(0.3f, 1f);
        Camera cam = Camera.main;
        Vector4 mp = Shader.GetGlobalVector(MoonPosId);
        if (cam != null && (mp.x != 0f || mp.y != 0f))
        {
            Vector2 v = new Vector2(mp.x - cam.transform.position.x, mp.y - cam.transform.position.y);
            if (v.sqrMagnitude > 0.01f) d = v.normalized;
        }
        Shader.SetGlobalVector(DirId, new Vector4(d.x, d.y, 0f, 0f));
    }

    private void OnDisable()
    {
        Shader.SetGlobalFloat(NightId, 0f);
    }
}
