using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KARAKTER ŞEKLİNDE YERE DÜŞEN GÖLGE.
/// Karakterin her görünür parçasının (SpriteRenderer veya mesh) bir gölge kopyasını çıkarır;
/// ProjectM/ProjectedShadow shader'ı bunları ayak çizgisinde aynalayıp yere yatırır.
/// Işık arkadan (ufuktan) geldiği için gölge kameraya doğru, ışığın tersine yatık düşer.
/// Ronni gibi oyunda kurulan modeller için de çalışır (parçalar sonradan eklenince yakalanır).
/// Sadece Play modunda çalışır.
/// </summary>
// Parallax (10000) kameradan sonra kaydırır; bu da ondan sonra çalışsın.
[DefaultExecutionOrder(10020)]
public class ProjectedShadow : MonoBehaviour
{
    public Material shadowMaterial;
    public LayerMask groundMask;
    public int sortingOrder = 15;

    [Header("Görünüm")]
    [Range(0f, 1f)] public float opacity = 0.55f;
    [Tooltip("Gölge boyu / karakter boyu.")]
    public float length = 0.75f;
    [Tooltip("Işıktan uzaklık başına yana yatma.")]
    public float skewPerUnit = 0.06f;
    public float maxSkew = 0.9f;
    [Tooltip("Zıplarken bu yükseklikte gölge tamamen kaybolur.")]
    public float maxJumpHeight = 5f;

    private struct Pair
    {
        public Renderer src;
        public Renderer dst;
        public MeshFilter srcFilter;
        public MeshFilter dstFilter;
    }

    private readonly List<Pair> pairs = new List<Pair>();
    private readonly HashSet<Renderer> known = new HashSet<Renderer>();
    private Material spriteMat;
    private Material meshMat;
    private MaterialPropertyBlock mpb;
    private float nextScan;

    private static readonly int FeetYId = Shader.PropertyToID("_FeetY");
    private static readonly int SkewId = Shader.PropertyToID("_Skew");
    private static readonly int ScaleYId = Shader.PropertyToID("_ScaleY");
    private static readonly int FadeId = Shader.PropertyToID("_Fade");
    private static readonly int ColorId = Shader.PropertyToID("_ShadowColor");
    private static readonly int IsSpriteId = Shader.PropertyToID("_IsSprite");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int LightPosId = Shader.PropertyToID("_ProjectM_LightPos");

    private const string ShadowName = "~shadow";

    private void OnEnable()
    {
        if (shadowMaterial == null)
            return;

        spriteMat = new Material(shadowMaterial) { name = "Shadow (sprite) " + name };
        spriteMat.SetFloat(IsSpriteId, 1f);

        meshMat = new Material(shadowMaterial) { name = "Shadow (mesh) " + name };
        meshMat.SetFloat(IsSpriteId, 0f);

        mpb = new MaterialPropertyBlock();
        nextScan = 0f;
    }

    private void OnDisable()
    {
        for (int i = 0; i < pairs.Count; i++)
            if (pairs[i].dst != null)
                Destroy(pairs[i].dst.gameObject);

        pairs.Clear();
        known.Clear();

        if (spriteMat != null) Destroy(spriteMat);
        if (meshMat != null) Destroy(meshMat);
    }

    private void LateUpdate()
    {
        if (spriteMat == null)
            return;

        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 0.5f;
            Scan();
        }

        // ---- yer ve ışık ----
        Vector3 p = transform.position;
        RaycastHit2D hit = Physics2D.Raycast((Vector2)p + Vector2.up * 0.5f, Vector2.down, 30f, groundMask);
        float feetY = hit.collider != null ? hit.point.y : p.y;
        float jump = Mathf.Max(0f, p.y - feetY);
        float k = Mathf.Clamp01(1f - jump / Mathf.Max(0.01f, maxJumpHeight));

        // Su üstünde gölge olmaz (yansıma var).
        // Su üstünde gölge düşmez; sadece yansıma kalır (ikisi üst üste binince garip duruyordu).
        if (WaterSurface.Instance != null && WaterSurface.Instance.Covers(p.x))
            k = 0f;

        Vector4 light = Shader.GetGlobalVector(LightPosId);
        float skew = Mathf.Clamp((p.x - light.x) * skewPerUnit, -maxSkew, maxSkew);

        Bounds b = new Bounds(p, Vector3.zero);
        bool any = false;

        for (int i = 0; i < pairs.Count; i++)
        {
            Renderer s = pairs[i].src;

            if (s != null && s.enabled && s.gameObject.activeInHierarchy && !s.forceRenderingOff)
            {
                if (!any) { b = s.bounds; any = true; }
                else b.Encapsulate(s.bounds);
            }
        }

        float height = any ? Mathf.Max(0.5f, b.max.y - feetY) : 2f;

        Color c = new Color(0.05f, 0.04f, 0.035f, opacity * k);

        foreach (Material m in new[] { spriteMat, meshMat })
        {
            m.SetFloat(FeetYId, feetY);
            m.SetFloat(SkewId, skew);
            m.SetFloat(ScaleYId, length);
            m.SetFloat(FadeId, height);
            m.SetColor(ColorId, c);
        }

        // ---- kopyaları kaynakla eşitle ----
        for (int i = pairs.Count - 1; i >= 0; i--)
        {
            Pair pr = pairs[i];

            if (pr.src == null || pr.dst == null)
            {
                if (pr.dst != null)
                    Destroy(pr.dst.gameObject);

                known.Remove(pr.src);
                pairs.RemoveAt(i);
                continue;
            }

            bool visible = pr.src.enabled && pr.src.gameObject.activeInHierarchy && !pr.src.forceRenderingOff && k > 0.01f;
            pr.dst.enabled = visible;

            if (!visible)
                continue;

            SpriteRenderer ss = pr.src as SpriteRenderer;

            if (ss != null)
            {
                SpriteRenderer ds = (SpriteRenderer)pr.dst;
                ds.sprite = ss.sprite;
                ds.flipX = ss.flipX;
                ds.flipY = ss.flipY;
                ds.drawMode = ss.drawMode;

                if (ss.drawMode != SpriteDrawMode.Simple)
                    ds.size = ss.size;
            }
            else if (pr.srcFilter != null && pr.dstFilter != null)
            {
                if (pr.dstFilter.sharedMesh != pr.srcFilter.sharedMesh)
                    pr.dstFilter.sharedMesh = pr.srcFilter.sharedMesh;

                Material sm = pr.src.sharedMaterial;
                Texture tex = sm != null ? sm.mainTexture : null;

                pr.dst.GetPropertyBlock(mpb);
                if (tex != null) mpb.SetTexture(MainTexId, tex);
                pr.dst.SetPropertyBlock(mpb);
            }
        }
    }

    // Yeni parçaları (ör. Ronni'nin oyunda kurulan parçaları) bul.
    private void Scan()
    {
        Renderer[] rs = GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < rs.Length; i++)
        {
            Renderer r = rs[i];

            if (r == null || known.Contains(r) || !Eligible(r))
                continue;

            known.Add(r);

            GameObject g = new GameObject(ShadowName);
            g.hideFlags = HideFlags.DontSave;
            g.layer = r.gameObject.layer;
            g.transform.SetParent(r.transform, false);

            Pair pr = new Pair { src = r };

            if (r is SpriteRenderer)
            {
                SpriteRenderer ds = g.AddComponent<SpriteRenderer>();
                ds.sharedMaterial = spriteMat;
                ds.sortingOrder = sortingOrder;
                pr.dst = ds;
            }
            else
            {
                MeshFilter mf = g.AddComponent<MeshFilter>();
                MeshRenderer mr = g.AddComponent<MeshRenderer>();
                mr.sharedMaterial = meshMat;
                mr.sortingOrder = sortingOrder;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                pr.srcFilter = r.GetComponent<MeshFilter>();
                pr.dstFilter = mf;
                pr.dst = mr;
            }

            pairs.Add(pr);
        }
    }

    private static bool Eligible(Renderer r)
    {
        if (r.gameObject.name.StartsWith("~") || r.gameObject.name == "GroundShadow")
            return false;

        if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
            return false;

        // HUD / yazılar / efektler gölge düşürmesin.
        if (r.sortingOrder >= 400 || r.GetComponent<TextMesh>() != null)
            return false;

        if (r.GetComponentInParent<Canvas>() != null)
            return false;

        if (r is SpriteRenderer)
            return true;

        return r is MeshRenderer && r.GetComponent<MeshFilter>() != null;
    }
}
