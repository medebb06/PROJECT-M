using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// AYAKLARI ZEMİNE OTURTUR. İki şey yapar:
///  1) TEMAS GÖLGESİ: ayakların altında yumuşak, koyu mavi bir elips (çekirdek + hale). Zıplayınca küçülür/solar.
///     Su üstünde de çalışır (ProjectedShadow orada kapanıyor).
///  2) AYAK KOYULAŞTIRMA: karakterin kendi piksellerinin alt kısmı, ayaktan yukarı azalan koyu maviye çekilir
///     (ortam ışığı / ambient occlusion taklidi). Arka planı etkilemez.
///
/// KURULUM YOK: oyun açılınca PlayerController'a kendiliğinden eklenir. Ayar için Player'a elle ekleyebilirsin
/// (o zaman otomatik eklenmez). Sadece Play modunda çalışır.
/// </summary>
[DefaultExecutionOrder(10030)]
public class CharacterGrounding : MonoBehaviour
{
    public LayerMask groundMask = 0;

    [Header("Temas gölgesi")]
    public bool contactShadow = true;
    public Color shadowColor = new Color(0.01f, 0.03f, 0.09f, 1f);
    [Range(0f, 1f)] public float coreOpacity = 0.75f;
    [Range(0f, 1f)] public float haloOpacity = 0.4f;
    [Tooltip("Çekirdek genişliği = gövde genişliği x bu.")]
    public float coreWidth = 1.15f;
    public float haloWidth = 2.1f;
    [Tooltip("Elips yüksekliği / genişliği.")]
    public float flatten = 0.2f;
    [Tooltip("Gölgenin dikey kayması (dünya birimi, - = aşağı).")]
    public float yOffset = -0.02f;
    public float maxJumpHeight = 4f;

    [Header("Ayak koyulaştırma")]
    public bool footShade = true;
    public Color shadeColor = new Color(0.03f, 0.06f, 0.14f, 1f);
    [Range(0f, 1f)] public float shadeStrength = 0.55f;
    [Tooltip("Gradyan yüksekliği = karakter boyu x bu.")]
    [Range(0.05f, 0.6f)] public float shadeHeight = 0.2f;
    [Range(0.3f, 4f)] public float shadePower = 1.5f;
    [Tooltip("Ayak çizgisi ince ayarı (dünya birimi).")]
    public float feetOffset = 0f;

    private struct Pair
    {
        public Renderer src;
        public Renderer dst;
        public MeshFilter srcFilter;
        public MeshFilter dstFilter;
    }

    private readonly List<Pair> pairs = new List<Pair>();
    private readonly HashSet<Renderer> known = new HashSet<Renderer>();
    private Material spriteMat, meshMat, blobMat;
    private MaterialPropertyBlock mpb;
    private SpriteRenderer core, halo;
    private Collider2D body;
    private float nextScan;
    private float groundY;
    private bool haveGround;
    private int topOrder = int.MinValue, bottomOrder = int.MaxValue, layerId;

    private static Sprite blob;
    private static bool autoInstalled;

    private static readonly int FeetYId = Shader.PropertyToID("_FeetY");
    private static readonly int HeightId = Shader.PropertyToID("_GradHeight");
    private static readonly int StrengthId = Shader.PropertyToID("_Strength");
    private static readonly int PowerId = Shader.PropertyToID("_Power");
    private static readonly int ColorId = Shader.PropertyToID("_ShadeColor");
    private static readonly int IsSpriteId = Shader.PropertyToID("_IsSprite");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    // ---------------------------------------------------------
    // Otomatik kurulum
    // ---------------------------------------------------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInstall()
    {
        if (autoInstalled)
            return;

        autoInstalled = true;
        SceneManager.sceneLoaded += (s, m) => Install();
        Install();
    }

    private static void Install()
    {
        PlayerController pc = FindFirstObjectByType<PlayerController>();

        if (pc != null && pc.GetComponent<CharacterGrounding>() == null && pc.GetComponentInParent<CharacterGrounding>() == null)
            pc.gameObject.AddComponent<CharacterGrounding>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        autoInstalled = false;
        blob = null;
    }

    // ---------------------------------------------------------

    private void OnEnable()
    {
        if (groundMask.value == 0)
            groundMask = LayerMask.GetMask("Ground", "Wall");

        Shader sh = Resources.Load<Shader>("AtkGlow/FootShade");

        if (sh != null)
        {
            spriteMat = new Material(sh) { name = "FootShade (sprite) " + name };
            spriteMat.SetFloat(IsSpriteId, 1f);
            meshMat = new Material(sh) { name = "FootShade (mesh) " + name };
            meshMat.SetFloat(IsSpriteId, 0f);
        }

        Shader bs = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");

        if (bs == null)
            bs = Shader.Find("Sprites/Default");

        blobMat = new Material(bs) { name = "ContactShadow" };
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

        if (core != null) Destroy(core.gameObject);
        if (halo != null) Destroy(halo.gameObject);
        core = halo = null;

        if (spriteMat != null) Destroy(spriteMat);
        if (meshMat != null) Destroy(meshMat);
        if (blobMat != null) Destroy(blobMat);
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 0.5f;
            Scan();
        }

        if (body == null)
            body = GetComponent<Collider2D>() ?? GetComponentInChildren<Collider2D>();

        Vector3 p = transform.position;
        Bounds cb = body != null ? body.bounds : new Bounds(p, new Vector3(0.8f, 1.8f, 0f));

        // zemin
        RaycastHit2D hit = Physics2D.Raycast(new Vector2(cb.center.x, cb.min.y + 0.4f), Vector2.down, 30f, groundMask);
        haveGround = hit.collider != null;
        groundY = (haveGround ? hit.point.y : cb.min.y) + feetOffset;

        float jump = Mathf.Max(0f, cb.min.y - groundY);
        float air = Mathf.Clamp01(1f - jump / Mathf.Max(0.01f, maxJumpHeight));

        // görünür parçaların toplam kutusu
        Bounds b = new Bounds(p, Vector3.zero);
        bool any = false;
        topOrder = int.MinValue;
        bottomOrder = int.MaxValue;

        for (int i = 0; i < pairs.Count; i++)
        {
            Renderer s = pairs[i].src;

            if (!Vis(s))
                continue;

            if (!any) { b = s.bounds; any = true; }
            else b.Encapsulate(s.bounds);

            if (s.sortingOrder > topOrder) { topOrder = s.sortingOrder; layerId = s.sortingLayerID; }
            if (s.sortingOrder < bottomOrder) bottomOrder = s.sortingOrder;
        }

        // Not: Ronni mesh'lerinin renderer.bounds'u 100 birim (devasa) → boy için çarpışma kutusunu kullan.
        float charH = Mathf.Max(0.5f, cb.max.y - groundY);

        UpdateContact(cb, any, air);
        UpdateShade(any, charH, jump);
    }

    // ---------------------------------------------------------
    // 1) Temas gölgesi
    // ---------------------------------------------------------

    private void UpdateContact(Bounds cb, bool any, float air)
    {
        if (!contactShadow || blobMat == null)
        {
            if (core != null) core.enabled = false;
            if (halo != null) halo.enabled = false;
            return;
        }

        if (core == null) core = MakeBlob("~shadow");
        if (halo == null) halo = MakeBlob("~shadow");

        float baseW = Mathf.Max(0.5f, cb.size.x);
        float s = Mathf.Lerp(0.55f, 1f, air);
        Vector3 pos = new Vector3(cb.center.x, groundY + yOffset, 0f);
        int order = (bottomOrder == int.MaxValue ? 0 : bottomOrder) - 1;

        Place(halo, pos, baseW * haloWidth * s, haloOpacity * air, order - 1, any);
        Place(core, pos, baseW * coreWidth * s, coreOpacity * air, order, any);
    }

    private SpriteRenderer MakeBlob(string n)
    {
        GameObject g = new GameObject(n);
        g.hideFlags = HideFlags.DontSave;
        g.transform.SetParent(null, false);   // dünyada serbest: karakter aynalanınca etkilenmesin
        SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = GetBlob();
        sr.sharedMaterial = blobMat;
        return sr;
    }

    private void Place(SpriteRenderer sr, Vector3 pos, float width, float opacity, int order, bool any)
    {
        sr.enabled = any && opacity > 0.01f;

        if (!sr.enabled)
            return;

        Vector2 sp = sr.sprite.bounds.size;

        sr.transform.position = pos;
        sr.transform.localScale = new Vector3(width / sp.x, width * flatten / sp.y, 1f);
        sr.color = new Color(shadowColor.r, shadowColor.g, shadowColor.b, opacity);
        sr.sortingLayerID = layerId;
        sr.sortingOrder = order;
    }

    // ---------------------------------------------------------
    // 2) Ayak koyulaştırma
    // ---------------------------------------------------------

    private void UpdateShade(bool any, float charH, float jump)
    {
        if (spriteMat == null)
            return;

        float h = Mathf.Max(0.05f, charH * shadeHeight);
        // havadayken gradyan zeminde kalır → karakter yukarıda olunca kendiliğinden solar
        float strength = footShade ? shadeStrength : 0f;

        foreach (Material m in new[] { spriteMat, meshMat })
        {
            m.SetFloat(FeetYId, groundY);
            m.SetFloat(HeightId, h);
            m.SetFloat(StrengthId, strength);
            m.SetFloat(PowerId, shadePower);
            m.SetColor(ColorId, shadeColor);
        }

        int order = (topOrder == int.MinValue ? 0 : topOrder) + 1;

        for (int i = pairs.Count - 1; i >= 0; i--)
        {
            Pair pr = pairs[i];

            if (pr.src == null || pr.dst == null)
            {
                if (pr.dst != null) Destroy(pr.dst.gameObject);
                known.Remove(pr.src);
                pairs.RemoveAt(i);
                continue;
            }

            bool vis = Vis(pr.src) && strength > 0.001f;
            pr.dst.enabled = vis;

            if (!vis)
                continue;

            pr.dst.sortingLayerID = pr.src.sortingLayerID;
            pr.dst.sortingOrder = order;

            SpriteRenderer ss = pr.src as SpriteRenderer;

            if (ss != null)
            {
                SpriteRenderer ds = (SpriteRenderer)pr.dst;
                ds.sprite = ss.sprite;
                ds.flipX = ss.flipX;
                ds.flipY = ss.flipY;
                ds.color = ss.color;
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

                if (tex != null)
                {
                    pr.dst.GetPropertyBlock(mpb);
                    mpb.SetTexture(MainTexId, tex);
                    pr.dst.SetPropertyBlock(mpb);
                }
            }
        }
    }

    private void Scan()
    {
        if (spriteMat == null)
            return;

        Renderer[] rs = GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < rs.Length; i++)
        {
            Renderer r = rs[i];

            if (r == null || known.Contains(r) || !Eligible(r))
                continue;

            known.Add(r);

            GameObject g = new GameObject("~footshade");
            g.hideFlags = HideFlags.DontSave;
            g.layer = r.gameObject.layer;
            g.transform.SetParent(r.transform, false);

            Pair pr = new Pair { src = r };

            if (r is SpriteRenderer)
            {
                SpriteRenderer ds = g.AddComponent<SpriteRenderer>();
                ds.sharedMaterial = spriteMat;
                pr.dst = ds;
            }
            else
            {
                MeshFilter mf = g.AddComponent<MeshFilter>();
                MeshRenderer mr = g.AddComponent<MeshRenderer>();
                mr.sharedMaterial = meshMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                pr.srcFilter = r.GetComponent<MeshFilter>();
                pr.dstFilter = mf;
                pr.dst = mr;
            }

            pairs.Add(pr);
        }
    }

    private static bool Vis(Renderer s)
    {
        return s != null && s.enabled && s.gameObject.activeInHierarchy && !s.forceRenderingOff;
    }

    private static bool Eligible(Renderer r)
    {
        if (r.gameObject.name.StartsWith("~") || r.gameObject.name == "GroundShadow")
            return false;

        if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
            return false;

        if (r.sortingOrder >= 400 || r.GetComponent<TextMesh>() != null)
            return false;

        if (r.GetComponentInParent<Canvas>() != null)
            return false;

        // kılıç izi şeridi ve parlama katmanları koyulaşmasın
        if (r.gameObject.name.Contains("smear") || r.gameObject.name.Contains("glow"))
            return false;

        if (r is SpriteRenderer)
            return true;

        return r is MeshRenderer && r.GetComponent<MeshFilter>() != null;
    }

    // ---------------------------------------------------------

    private static Sprite GetBlob()
    {
        if (blob != null)
            return blob;

        const int S = 64;
        Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(S * 0.5f, S * 0.5f)) / (S * 0.5f);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);   // smoothstep: kenarlar yumuşak
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }

        tex.Apply();
        blob = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        return blob;
    }
}
