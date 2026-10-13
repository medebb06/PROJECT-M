using UnityEngine;

/// <summary>
/// KARAKTER ARKA PLANI (değer ayrımı). Karakterin hemen ARKASINDA, çok yumuşak ve soğuk bir ışık lekesi:
/// koyu saç / pantolon / ceket arkasındaki zemini bir kademe açar, böylece silüet "leke" gibi okunur.
/// Zeminin ve ön plandaki nesnelerin ÖNÜNE geçmez (karakterin sorting sırasının altında çizilir).
/// Oyuncu kökündeki bir bileşen olarak çalışır; Tools > Lighting > Character Separation > Apply kurar.
/// </summary>
[DefaultExecutionOrder(10060)]
public class CharacterBackdrop : MonoBehaviour
{
    public Sprite glowSprite;
    [Tooltip("ProjectM/SpriteAdditive malzemesi (Assets/Art/Atmosphere/Materials/M_Additive).")]
    public Material additiveMaterial;

    [Header("Görünüm")]
    public Color color = new Color(0.45f, 0.58f, 0.85f, 1f);
    [Range(0f, 3f)] public float intensity = 0.6f;
    [Tooltip("Dünya birimi cinsinden leke boyutu (genişlik, yükseklik).")]
    public Vector2 size = new Vector2(7.5f, 8.5f);
    [Tooltip("Karakterin gövde merkezine göre kayma.")]
    public Vector2 offset = new Vector2(0f, 0.2f);
    [Tooltip("Yavaş nefes alma (0 = sabit).")]
    [Range(0f, 0.3f)] public float breathe = 0.06f;
    public float breatheSpeed = 0.6f;
    [Tooltip("Takip yumuşatması (büyük = daha sıkı takip).")]
    public float followSpeed = 14f;
    [Tooltip("Karakterin en alt çizim sırasından bu kadar aşağıda çizilir.")]
    public int orderBelow = 12;

    private SpriteRenderer sr;
    private Material mat;
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private Collider2D body;
    private Vector3 pos;
    private bool placed;
    private float nextScan;
    private int layerId;
    private int minOrder = int.MinValue;
    private bool anyVisible;
    private float alpha = 1f;

    private void OnEnable()
    {
        placed = false;
        nextScan = 0f;
    }

    private void OnDisable()
    {
        if (sr != null)
            Destroy(sr.gameObject);

        sr = null;

        if (mat != null)
            Destroy(mat);

        mat = null;
    }

    private void Build()
    {
        if (glowSprite == null || additiveMaterial == null)
            return;

        GameObject go = new GameObject("~backdrop");
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(null, false); // dünyada serbest: karakter yatay çevrilince (scale -1) aynalanmasın

        sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = glowSprite;
        // Kendi malzeme kopyası: renk/parlaklık shader özelliklerinden verilir (SpriteRenderer.color'a bağlı kalmaz).
        mat = new Material(additiveMaterial);
        sr.sharedMaterial = mat;
        sr.color = Color.white;
    }

    private void Scan()
    {
        nextScan = Time.unscaledTime + 0.5f;
        minOrder = int.MaxValue;
        anyVisible = false;

        Renderer[] rs = GetComponentsInChildren<Renderer>(false);

        for (int i = 0; i < rs.Length; i++)
        {
            Renderer r = rs[i];

            if (r == null || !r.enabled || r.forceRenderingOff)
                continue;

            if (r.gameObject.name.StartsWith("~") || r.gameObject.name == "GroundShadow")
                continue;

            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                continue;

            if (r.sortingOrder >= 400 || r.GetComponentInParent<Canvas>() != null)
                continue;

            if (!(r is SpriteRenderer) && !(r is MeshRenderer))
                continue;

            if (r.sortingOrder < minOrder)
            {
                minOrder = r.sortingOrder;
                layerId = r.sortingLayerID;
            }

            anyVisible = true;
        }

        if (body == null)
            body = GetComponent<Collider2D>() ?? GetComponentInChildren<Collider2D>();
    }

    private void LateUpdate()
    {
        if (sr == null)
        {
            Build();

            if (sr == null)
                return;
        }

        if (Time.unscaledTime >= nextScan)
            Scan();

        Vector3 target = body != null ? (Vector3)body.bounds.center : transform.position;
        target += (Vector3)offset;
        target.z = 0f;

        if (!placed)
        {
            pos = target;
            placed = true;
        }
        else
        {
            pos = Vector3.Lerp(pos, target, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
        }

        alpha = Mathf.MoveTowards(alpha, anyVisible ? 1f : 0f, Time.deltaTime * 4f);

        float k = 1f + breathe * Mathf.Sin(Time.time * breatheSpeed * 2f * Mathf.PI * 0.5f);
        Vector2 spriteSize = glowSprite != null ? (Vector2)glowSprite.bounds.size : Vector2.one;

        sr.transform.position = pos;
        sr.transform.localScale = new Vector3(size.x * k / spriteSize.x, size.y * k / spriteSize.y, 1f);

        mat.SetColor(ColorId, new Color(color.r, color.g, color.b, 1f));
        mat.SetFloat(IntensityId, intensity * alpha);

        sr.sortingLayerID = layerId;
        sr.sortingOrder = (minOrder == int.MaxValue || minOrder == int.MinValue ? 0 : minOrder) - orderBelow;
    }
}
