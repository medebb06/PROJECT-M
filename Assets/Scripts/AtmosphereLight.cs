using UnityEngine;

/// <summary>
/// AY IŞIĞI ATMOSFERİ (Tools > Lighting > Build Moonlit Atmosphere kurar).
///   - Gökyüzü/ay katmanını kameraya sabitler (SkyAnchor).
///   - Ufuk parıltısı, ışık hüzmeleri ve sisi kameranın X'ine, ufkun Y'sine sabitler (HorizonAnchor).
///   - Siluet shader'ının kenar ışığı yönü için ışık konumunu global olarak verir.
///   - Işık hüzmelerini çok hafif dalgalandırır.
/// Edit modunda da çalışır, sahnede görünür.
/// </summary>
// Parallax (10000) kameradan sonra kaydırır; bu da ondan sonra çalışsın.
[DefaultExecutionOrder(10010)]
[ExecuteAlways]
public class AtmosphereLight : MonoBehaviour
{
    public Transform skyAnchor;
    public Transform horizonAnchor;

    [Tooltip("Ufuk çizgisinin dünya Y'si (arka çalıların üstü).")]
    public float horizonY = 0f;

    [Tooltip("Kenar ışığının geldiği nokta: kameranın X'i + bu, ufuk Y'si + bu.")]
    public Vector2 lightOffset = new Vector2(0f, 3f);

    [Header("Rüzgâr")]
    [Tooltip("Bütün ağaç/çalı/ot salınımının genel gücü. 0 = rüzgâr yok.")]
    [Range(0f, 3f)] public float windStrength = 1f;
    [Tooltip("Kapalıysa Scene/Game ekranında (Play dışında) ağaçlar ve çalılar sabit durur. Play'de her zaman oynar.")]
    public bool animateInEditMode = false;

    [Header("Işık hüzmeleri")]
    public SpriteRenderer[] rays;
    [Range(0f, 1f)] public float rayPulse = 0.35f;
    public float raySpeed = 0.25f;

    private float[] rayBaseAlpha;
    private Transform moonT;

    private static readonly int LightPosId = Shader.PropertyToID("_ProjectM_LightPos");
    private static readonly int WindStrengthId = Shader.PropertyToID("_ProjectM_WindStrength");
    private static readonly int MoonPosId = Shader.PropertyToID("_ProjectM_MoonPos");
    private static readonly int SkyTimeId = Shader.PropertyToID("_ProjectM_SkyTime");

    private void OnEnable()
    {
        CacheRays();
        LateUpdate();
    }

    private void CacheRays()
    {
        if (rays == null)
            return;

        rayBaseAlpha = new float[rays.Length];

        for (int i = 0; i < rays.Length; i++)
            rayBaseAlpha[i] = rays[i] != null ? rays[i].color.a : 0f;
    }

    private void LateUpdate()
    {
        Shader.SetGlobalFloat(WindStrengthId, Application.isPlaying || animateInEditMode ? windStrength : 0f);
        Shader.SetGlobalFloat(SkyTimeId, Application.isPlaying ? Time.time : (animateInEditMode ? Time.realtimeSinceStartup : 0f));

        Camera cam = Camera.main;

        if (cam == null)
            return;

        Vector3 p = cam.transform.position;

        if (skyAnchor != null)
            skyAnchor.position = new Vector3(p.x, p.y, 0f);

        if (horizonAnchor != null)
            horizonAnchor.position = new Vector3(p.x, horizonY, 0f);

        Shader.SetGlobalVector(LightPosId, new Vector4(p.x + lightOffset.x, horizonY + lightOffset.y, 0f, 0f));

        // Ayın dünya konumu (bulutlar ona göre aydınlanır / saydamlaşır).
        if (moonT == null && skyAnchor != null)
            moonT = skyAnchor.Find("Moon");

        Vector3 mp = moonT != null ? moonT.position : new Vector3(p.x, horizonY + 6f, 0f);
        Shader.SetGlobalVector(MoonPosId, new Vector4(mp.x, mp.y, 0f, 0f));

        if (rays == null || rayBaseAlpha == null || rayBaseAlpha.Length != rays.Length)
        {
            CacheRays();
            return;
        }

        // Sadece Play'de dalgalan (Edit modunda sahne kirlenmesin).
        if (!Application.isPlaying)
            return;

        float t = Time.time * raySpeed;

        for (int i = 0; i < rays.Length; i++)
        {
            SpriteRenderer r = rays[i];

            if (r == null)
                continue;

            float w = 1f - rayPulse * 0.5f * (1f + Mathf.Sin(t * (1f + i * 0.37f) + i * 1.7f));
            Color c = r.color;
            c.a = rayBaseAlpha[i] * w;
            r.color = c;
        }
    }
}
