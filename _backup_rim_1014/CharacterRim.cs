using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KARAKTER KENAR IŞIĞI (rim). Arkadan (ufuktan) gelen ışık karakterin dış kenarında
/// ince, sıcak bir hat olarak görünür. İki katman: içte parlak ince hat, dışta yumuşak hale.
/// Karakterin her görünür parçasının ışığa doğru kaydırılmış toplamalı bir kopyası,
/// karakterin arkasına çizilir (ProjectM/CharacterRim). Ronni gibi oyunda kurulan
/// modellerde de çalışır. Sadece Play modunda.
/// </summary>
public class CharacterRim : MonoBehaviour
{
    public Material rimMaterial;

    [Header("Görünüm")]
    public Color color = new Color(1f, 0.8f, 0.55f, 1f);
    [Tooltip("İç hat: kayma (dünya birimi) ve parlaklık.")]
    public float innerWidth = 0.05f;
    public float innerIntensity = 0.6f;
    [Tooltip("Dış hale: kayma ve parlaklık.")]
    public float outerWidth = 0.11f;
    public float outerIntensity = 0.22f;
    [Tooltip("Işık yönüne eklenen yukarı eğilim (üst kenarlar daha çok yansın).")]
    public float upBias = 0.6f;

    [Header("Ay konturu (soğuk, sabit)")]
    [Tooltip("Karakterin ARKA kenarında (baktığı yönün tersi) ince, soğuk, sabit bir kontur. Koyu saç/ceket arka plandan ayrılır.")]
    public bool moonRim = true;
    public Color moonColor = new Color(0.55f, 0.72f, 1f, 1f);
    public float moonWidth = 0.05f;
    public float moonIntensity = 1.0f;

    private struct Pair
    {
        public Renderer src;
        public Renderer inner;
        public Renderer outer;
        public Renderer moon;
        public MeshFilter moonFilter;
        public MeshFilter srcFilter;
        public MeshFilter innerFilter;
        public MeshFilter outerFilter;
    }

    private readonly List<Pair> pairs = new List<Pair>();
    private readonly HashSet<Renderer> known = new HashSet<Renderer>();
    private Material innerSprite, innerMesh, outerSprite, outerMesh, moonSpriteMat, moonMeshMat;
    private PlayerController facingSource;
    private MaterialPropertyBlock mpb;
    private float nextScan;

    private static readonly int OffsetId = Shader.PropertyToID("_Offset");
    private static readonly int ColorId = Shader.PropertyToID("_RimColor");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int IsSpriteId = Shader.PropertyToID("_IsSprite");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int LightPosId = Shader.PropertyToID("_ProjectM_LightPos");

    private void OnEnable()
    {
        if (rimMaterial == null)
            return;

        innerSprite = Make("Rim inner sprite", 1f);
        innerMesh = Make("Rim inner mesh", 0f);
        outerSprite = Make("Rim outer sprite", 1f);
        outerMesh = Make("Rim outer mesh", 0f);
        moonSpriteMat = Make("Rim moon sprite", 1f);
        moonMeshMat = Make("Rim moon mesh", 0f);

        mpb = new MaterialPropertyBlock();
        nextScan = 0f;
    }

    private Material Make(string n, float isSprite)
    {
        Material m = new Material(rimMaterial) { name = n + " " + name };
        m.SetFloat(IsSpriteId, isSprite);
        return m;
    }

    private void OnDisable()
    {
        for (int i = 0; i < pairs.Count; i++)
        {
            if (pairs[i].inner != null) Destroy(pairs[i].inner.gameObject);
            if (pairs[i].outer != null) Destroy(pairs[i].outer.gameObject);
            if (pairs[i].moon != null) Destroy(pairs[i].moon.gameObject);
        }

        pairs.Clear();
        known.Clear();

        foreach (Material m in new[] { innerSprite, innerMesh, outerSprite, outerMesh, moonSpriteMat, moonMeshMat })
            if (m != null)
                Destroy(m);
    }

    private void LateUpdate()
    {
        if (innerSprite == null)
            return;

        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 0.5f;
            Scan();
        }

        // ---- ışık yönü ----
        Vector3 center = transform.position;
        int minOrder = int.MaxValue;
        bool any = false;
        Bounds b = new Bounds(center, Vector3.zero);

        for (int i = 0; i < pairs.Count; i++)
        {
            Renderer s = pairs[i].src;

            if (!Visible(s))
                continue;

            if (!any) { b = s.bounds; any = true; }
            else b.Encapsulate(s.bounds);

            if (s.sortingOrder < minOrder)
                minOrder = s.sortingOrder;
        }

        if (any)
            center = b.center;

        Vector4 lp = Shader.GetGlobalVector(LightPosId);
        Vector2 dir = new Vector2(lp.x - center.x, lp.y - center.y);
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up;
        dir = (dir + Vector2.up * upBias).normalized;

        Color c = color;

        SetMat(innerSprite, dir * innerWidth, c, innerIntensity);
        SetMat(innerMesh, dir * innerWidth, c, innerIntensity);
        SetMat(outerSprite, dir * outerWidth, c, outerIntensity);
        SetMat(outerMesh, dir * outerWidth, c, outerIntensity);

        // Ay konturu: baktığı yönün tersine (arka kenara) hafif yukarı eğimli sabit kayma.
        Vector2 moonDir = new Vector2(-Facing(), 0.35f).normalized * moonWidth;
        SetMat(moonSpriteMat, moonDir, moonColor, moonIntensity);
        SetMat(moonMeshMat, moonDir, moonColor, moonIntensity);

        // İç hat önce çizilsin (stencil dış haleyi iç hattın dışına iter), ikisi de karakterin arkasında.
        int innerOrder = (minOrder == int.MaxValue ? 0 : minOrder) - 2;
        int outerOrder = innerOrder + 1;
        int moonOrder = innerOrder - 1;

        // ---- kopyaları kaynakla eşitle ----
        for (int i = pairs.Count - 1; i >= 0; i--)
        {
            Pair pr = pairs[i];

            if (pr.src == null || pr.inner == null || pr.outer == null || pr.moon == null)
            {
                if (pr.inner != null) Destroy(pr.inner.gameObject);
                if (pr.outer != null) Destroy(pr.outer.gameObject);
                if (pr.moon != null) Destroy(pr.moon.gameObject);
                known.Remove(pr.src);
                pairs.RemoveAt(i);
                continue;
            }

            bool vis = Visible(pr.src);
            pr.inner.enabled = vis;
            pr.outer.enabled = vis;
            pr.moon.enabled = vis && moonRim;

            if (!vis)
                continue;

            pr.inner.sortingLayerID = pr.src.sortingLayerID;
            pr.outer.sortingLayerID = pr.src.sortingLayerID;
            pr.inner.sortingOrder = innerOrder;
            pr.outer.sortingOrder = outerOrder;
            pr.moon.sortingLayerID = pr.src.sortingLayerID;
            pr.moon.sortingOrder = moonOrder;

            SpriteRenderer ss = pr.src as SpriteRenderer;

            if (ss != null)
            {
                Copy(ss, (SpriteRenderer)pr.inner);
                Copy(ss, (SpriteRenderer)pr.outer);
                Copy(ss, (SpriteRenderer)pr.moon);
            }
            else if (pr.srcFilter != null)
            {
                Mesh mesh = pr.srcFilter.sharedMesh;

                if (pr.innerFilter.sharedMesh != mesh) pr.innerFilter.sharedMesh = mesh;
                if (pr.outerFilter.sharedMesh != mesh) pr.outerFilter.sharedMesh = mesh;
                if (pr.moonFilter.sharedMesh != mesh) pr.moonFilter.sharedMesh = mesh;

                Material sm = pr.src.sharedMaterial;
                Texture tex = sm != null ? sm.mainTexture : null;

                if (tex != null)
                {
                    pr.inner.GetPropertyBlock(mpb);
                    mpb.SetTexture(MainTexId, tex);
                    pr.inner.SetPropertyBlock(mpb);

                    pr.outer.GetPropertyBlock(mpb);
                    mpb.SetTexture(MainTexId, tex);
                    pr.outer.SetPropertyBlock(mpb);

                    pr.moon.GetPropertyBlock(mpb);
                    mpb.SetTexture(MainTexId, tex);
                    pr.moon.SetPropertyBlock(mpb);
                }
            }
        }
    }

    // +1 = sağa, -1 = sola bakıyor.
    private float Facing()
    {
        if (facingSource == null)
            facingSource = GetComponentInParent<PlayerController>();

        if (facingSource != null && Mathf.Abs(facingSource.facingDir) > 0.01f)
            return Mathf.Sign(facingSource.facingDir);

        return transform.lossyScale.x < 0f ? 1f : -1f;
    }

    private static bool Visible(Renderer s)
    {
        return s != null && s.enabled && s.gameObject.activeInHierarchy && !s.forceRenderingOff;
    }

    private static void SetMat(Material m, Vector2 offset, Color c, float intensity)
    {
        m.SetVector(OffsetId, new Vector4(offset.x, offset.y, 0f, 0f));
        m.SetColor(ColorId, c);
        m.SetFloat(IntensityId, intensity);
    }

    private static void Copy(SpriteRenderer from, SpriteRenderer to)
    {
        to.sprite = from.sprite;
        to.flipX = from.flipX;
        to.flipY = from.flipY;
        to.drawMode = from.drawMode;

        if (from.drawMode != SpriteDrawMode.Simple)
            to.size = from.size;
    }

    private void Scan()
    {
        Renderer[] rs = GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < rs.Length; i++)
        {
            Renderer r = rs[i];

            if (r == null || known.Contains(r) || !Eligible(r))
                continue;

            known.Add(r);

            Pair pr = new Pair { src = r };
            bool sprite = r is SpriteRenderer;

            pr.inner = Create(r, "~rim_in", sprite ? innerSprite : innerMesh, out pr.innerFilter);
            pr.outer = Create(r, "~rim_out", sprite ? outerSprite : outerMesh, out pr.outerFilter);
            pr.moon = Create(r, "~rim_moon", sprite ? moonSpriteMat : moonMeshMat, out pr.moonFilter);

            if (!sprite)
                pr.srcFilter = r.GetComponent<MeshFilter>();

            pairs.Add(pr);
        }
    }

    private static Renderer Create(Renderer src, string n, Material mat, out MeshFilter filter)
    {
        GameObject g = new GameObject(n);
        g.hideFlags = HideFlags.DontSave;
        g.layer = src.gameObject.layer;
        g.transform.SetParent(src.transform, false);

        filter = null;

        if (src is SpriteRenderer)
        {
            SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = mat;
            return sr;
        }

        filter = g.AddComponent<MeshFilter>();
        MeshRenderer mr = g.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return mr;
    }

    private static bool Eligible(Renderer r)
    {
        // Yardımcı kopyalar (~shadow, ~rim) ve eski oval gölge kaynak sayılmaz.
        if (r.gameObject.name.StartsWith("~") || r.gameObject.name == "GroundShadow")
            return false;

        if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
            return false;

        if (r.sortingOrder >= 400 || r.GetComponent<TextMesh>() != null)
            return false;

        if (r.GetComponentInParent<Canvas>() != null)
            return false;

        if (r is SpriteRenderer)
            return true;

        return r is MeshRenderer && r.GetComponent<MeshFilter>() != null;
    }
}
