using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// SMEAR IŞIK SIZINTISI. Kılıç izi (smear) parlarken çevreye de ışık yayar: geniş yumuşak mavi bir hale
/// + smear şeklinde hafif büyütülmüş ikinci bir parlama. Sorting katmanlarının EN ÜSTÜNDE toplamalı (additive)
/// çizilir; yani ağaçlara, çimene, zemine, düşmanlara ve ön plana da vurur. Su yansıması kamerası da
/// gördüğü için suda da yansır.
///
/// KURULUM YOK: oyun açılınca Player'a kendiliğinden eklenir ("~atk_glow" katmanını izler).
/// </summary>
[DefaultExecutionOrder(10040)]
public class SmearLightSpill : MonoBehaviour
{
    [Header("Hale (geniş yumuşak ışık)")]
    public bool halo = true;
    [ColorUsage(true, true)] public Color haloColor = new Color(0.35f, 0.75f, 2.0f, 1f);
    [Tooltip("Hale boyu = smear boyu x bu.")]
    public float haloScale = 2.6f;
    [Range(0f, 1f)] public float haloAlpha = 0.5f;

    [Header("Şekil yansıması (smear'ın büyütülmüş hali)")]
    public bool shape = true;
    [ColorUsage(true, true)] public Color shapeColor = new Color(0.6f, 1.2f, 3.0f, 1f);
    public float shapeScale = 1.35f;
    [Range(0f, 1f)] public float shapeAlpha = 0.4f;

    [Header("Zamanlama")]
    public float riseSpeed = 18f;
    public float fallSpeed = 5f;
    [Tooltip("Her şeyin üstünde: en üst sorting katmanı, bu sıra (HUD 400+).")]
    public int order = 399;

    private SpriteRenderer src, haloSR, shapeSR;
    private Material haloMat, shapeMat;
    private float level, nextFind;
    private Vector3 lastPos;
    private Vector3 lastScale = Vector3.one;
    private Sprite lastSprite;
    private bool lastFlip;
    private float lastSize = 2f;

    private static Sprite radial;
    private static bool autoInstalled;

    private static readonly int GlowId = Shader.PropertyToID("_GlowColor");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInstall()
    {
        if (autoInstalled)
            return;

        autoInstalled = true;
        SceneManager.sceneLoaded += (s, m) => Install();
        Install();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        autoInstalled = false;
        radial = null;
    }

    private static void Install()
    {
        PlayerController pc = FindFirstObjectByType<PlayerController>();

        if (pc != null && pc.GetComponent<SmearLightSpill>() == null)
            pc.gameObject.AddComponent<SmearLightSpill>();
    }

    private void OnEnable()
    {
        Shader sh = Resources.Load<Shader>("AtkGlow/SpriteGlowAdd");

        if (sh == null)
            sh = Shader.Find("ProjectM/SpriteGlowAdd");

        if (sh == null)
        {
            enabled = false;
            return;
        }

        haloMat = new Material(sh) { name = "SmearSpill halo" };
        shapeMat = new Material(sh) { name = "SmearSpill shape" };
    }

    private void OnDisable()
    {
        if (haloSR != null) Destroy(haloSR.gameObject);
        if (shapeSR != null) Destroy(shapeSR.gameObject);
        haloSR = shapeSR = null;
        if (haloMat != null) Destroy(haloMat);
        if (shapeMat != null) Destroy(shapeMat);
    }

    private SpriteRenderer Make(string n, Material m, Sprite s)
    {
        GameObject g = new GameObject(n);
        g.hideFlags = HideFlags.DontSave;
        g.transform.SetParent(null, false);   // dünyada serbest
        SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sharedMaterial = m;
        sr.enabled = false;
        return sr;
    }

    private void LateUpdate()
    {
        if (src == null && Time.unscaledTime >= nextFind)
        {
            nextFind = Time.unscaledTime + 0.25f;

            foreach (SpriteRenderer r in GetComponentsInChildren<SpriteRenderer>(true))
                if (r != null && r.gameObject.name == "~atk_glow") { src = r; break; }
        }

        bool on = src != null && src.enabled && src.gameObject.activeInHierarchy && src.sprite != null;
        float target = on ? Mathf.Clamp01(src.color.a) : 0f;

        if (on)
        {
            Bounds b = src.bounds;
            lastPos = b.center;
            lastScale = src.transform.lossyScale;
            lastSprite = src.sprite;
            lastFlip = src.flipX;
            lastSize = Mathf.Max(b.size.x, b.size.y);
            if (src.transform.parent != null) { }
        }

        level = Mathf.MoveTowards(level, target, (target > level ? riseSpeed : fallSpeed) * Time.unscaledDeltaTime);

        if (level <= 0.003f || lastSprite == null)
        {
            if (haloSR != null) haloSR.enabled = false;
            if (shapeSR != null) shapeSR.enabled = false;
            return;
        }

        int layerId = SortingLayer.layers.Length > 0 ? SortingLayer.layers[SortingLayer.layers.Length - 1].id : 0;
        float l = level * level * (3f - 2f * level);   // yumuşak giriş/çıkış

        if (halo)
        {
            if (haloSR == null) haloSR = Make("~spill_halo", haloMat, GetRadial());

            haloMat.SetColor(GlowId, haloColor);
            haloSR.enabled = true;
            haloSR.sortingLayerID = layerId;
            haloSR.sortingOrder = order;

            float d = Mathf.Max(1.5f, lastSize * haloScale);
            Vector2 sp = haloSR.sprite.bounds.size;

            haloSR.transform.position = new Vector3(lastPos.x, lastPos.y, 0f);
            haloSR.transform.localScale = new Vector3(d / sp.x, d * 0.8f / sp.y, 1f);
            haloSR.color = new Color(1f, 1f, 1f, haloAlpha * l);
        }
        else if (haloSR != null) haloSR.enabled = false;

        if (shape)
        {
            if (shapeSR == null) shapeSR = Make("~spill_shape", shapeMat, lastSprite);

            shapeMat.SetColor(GlowId, shapeColor);
            shapeSR.enabled = true;
            shapeSR.sprite = lastSprite;
            shapeSR.flipX = lastFlip;
            shapeSR.sortingLayerID = layerId;
            shapeSR.sortingOrder = order - 1;
            shapeSR.transform.position = src != null ? src.transform.position : lastPos;
            shapeSR.transform.localScale = new Vector3(Mathf.Abs(lastScale.x), Mathf.Abs(lastScale.y), 1f) * shapeScale;
            shapeSR.color = new Color(1f, 1f, 1f, shapeAlpha * l);
        }
        else if (shapeSR != null) shapeSR.enabled = false;
    }

    private static Sprite GetRadial()
    {
        if (radial != null)
            return radial;

        const int S = 128;
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
                a = a * a * a;   // uzun yumuşak kuyruk, parlak merkez
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }

        tex.Apply();
        radial = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        return radial;
    }
}
