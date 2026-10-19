using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// SU YÜZEYİ (Tools > Water > Build Water kurar).
///   - Aynalanmış ikinci bir kamera ile yansımayı RenderTexture'a çizer (ProjectM/Water shader'ı kullanır).
///   - Ay ışığı yolunun X'ini verir (ayı takip eder).
///   - Halkalar: hareket eden her Rigidbody2D (oyuncu, düşman) suda yürürken / inerken otomatik halka ve sıçrama yapar,
///     ayrıca arada bir kendiliğinden halkalar oluşur. Dışarıdan: WaterSurface.Instance.Ripple(...) / Splash(...).
/// Ayarların çoğu materyalde (M_Water); buradakiler davranış ayarları.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(10030)]
[RequireComponent(typeof(SpriteRenderer))]
public class WaterSurface : MonoBehaviour
{
    public static WaterSurface Instance { get; private set; }

    [Header("Yansıma")]
    [Tooltip("Yansıma çözünürlüğü (1 = ekranla aynı, en net).")]
    [Range(0.25f, 1f)] public float reflectionResolution = 1f;
    [Tooltip("Bu sorting order ve üstü yansımaz (ön plan çalıları, HUD, yazılar).")]
    public int excludeOrderFrom = 450;
    [Tooltip("Yansima kamerasinin gorunen alana gore payi (1 = tam ekran, buyudukce kenarlar da yansir).")]
    [Range(1f, 3f)] public float reflectionMargin = 1.6f;
    [Tooltip("Yatayda ekstra genislik carpani (ultra genis / tam ekranda kenarlarin bos kalmamasi icin).")]
    [Range(1f, 3f)] public float reflectionWidthBoost = 1.6f;
    [Tooltip("Yansıma kamerasının en küçük ortho boyutu (ekranın tamamı yansısın diye).")]
    public float minReflectionSize = 12f;
    [Tooltip("Yansımada boş kalan yerlerin rengi. Kapalıysa ana kameranın arka plan rengi.")]
    public bool overrideClearColor = false;
    public Color reflectionClearColor = new Color(0.16f, 0.18f, 0.2f, 1f);

    [Header("Ay yolu")]
    [Tooltip("Ay objesi (boşsa ışık konumu kullanılır).")]
    public Transform moon;
    public float moonXOffset = 0f;

    [Header("Kendiliğinden halkalar")]
    [Tooltip("Saniyede ortalama kaç halka.")]
    [Range(0f, 4f)] public float ambientRate = 0.8f;
    [Range(0f, 1f)] public float ambientStrength = 0.35f;
    [Tooltip("En fazla bu derinliğe kadar (birim).")]
    public float ambientMaxDepth = 7f;

    [Header("Karakter etkileşimi")]
    public bool autoInteract = true;
    [Tooltip("Ayak su çizgisine bu kadar yakınsa suda sayılır.")]
    public float contactHeight = 0.4f;
    [Tooltip("Adım halkaları arasındaki en az mesafe (birim). Büyüdükçe daha nadir.")]
    public float stepSpacing = 6f;
    [Tooltip("Her aralıkta halka çıkma ihtimali (0-1). Düşük = daha nadir, daha doğal.")]
    [Range(0f, 1f)] public float stepChance = 0.45f;
    [Tooltip("Adım halkasının gücü (küçük, ince halka).")]
    [Range(0.05f, 1f)] public float stepStrength = 0.18f;
    [Tooltip("Bu hızdan sert inişler sıçratır.")]
    public float landSpeed = 7f;

    [Header("Sıçrama")]
    public Texture2D splashTexture;
    public Color splashColor = new Color(0.82f, 0.88f, 0.95f, 0.85f);
    public int splashSortingOrder = 140;

    public float WaterY { get; private set; }

    /// <summary>Bu X'te su var mı (su yüzeyinin yatay sınırları içinde mi).</summary>
    public bool Covers(float x)
    {
        if (sr == null) return false;
        Bounds b = sr.bounds;
        return x >= b.min.x && x <= b.max.x;
    }

    // ---- iç ----
    private SpriteRenderer sr;
    private Camera mainCam;
    private Camera reflCam;
    private float mainVisibleSize = -1f;   // ana kameranın GERÇEKTEN çizdiği ortho boyutu (Pixel Perfect / Cinemachine sonrası)
    private RenderTexture rt;
    private readonly Vector4[] ripples = new Vector4[16];
    private int nextRipple;
    private float ambientTimer;

    private readonly List<Renderer> excluded = new List<Renderer>();
    private readonly List<Renderer> hidden = new List<Renderer>();
    private float nextExcludeScan;

    private class Body
    {
        public Rigidbody2D rb;
        public Vector2 last;
        public float vy;
        public float walked;
        public float nextStep = 4f;
        public bool inWater;
    }
    private readonly List<Body> bodies = new List<Body>();
    private float nextBodyScan;

    private ParticleSystem splash;
    private Material splashMat;

    private static readonly int WaterYId = Shader.PropertyToID("_WaterY");
    private static readonly int HasReflId = Shader.PropertyToID("_WaterHasRefl");
    private static readonly int ReflTexId = Shader.PropertyToID("_WaterReflTex");
    private static readonly int ReflRectId = Shader.PropertyToID("_WaterReflRect");
    private static readonly int MoonXId = Shader.PropertyToID("_WaterMoonX");
    private static readonly int TimeId = Shader.PropertyToID("_WaterTime");
    private static readonly int RipplesId = Shader.PropertyToID("_WaterRipples");
    private static readonly int LightPosId = Shader.PropertyToID("_ProjectM_LightPos");

    [Tooltip("Kapalıysa Scene/Game ekranında (Play dışında) su sabit durur. Play'de her zaman akar.")]
    public bool animateInEditMode = false;

    private float Now => Application.isPlaying ? Time.time : (animateInEditMode ? Time.realtimeSinceStartup : 0f);

    // =====================================================================

    private void OnEnable()
    {
        Instance = this;
        sr = GetComponent<SpriteRenderer>();

        for (int i = 0; i < ripples.Length; i++)
            ripples[i] = new Vector4(0f, 0f, -1000f, 0f);

        Shader.SetGlobalFloat(HasReflId, 0f);

        if (Application.isPlaying)
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
            BuildSplash();
        }
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        RenderPipelineManager.endCameraRendering -= OnEndCamera;
        RestoreHidden();

        if (Instance == this)
            Instance = null;

        Shader.SetGlobalFloat(HasReflId, 0f);

        if (reflCam != null) DestroyImmediate(reflCam.gameObject);
        if (rt != null) { rt.Release(); DestroyImmediate(rt); }
        if (splash != null) DestroyImmediate(splash.gameObject);
        if (splashMat != null) DestroyImmediate(splashMat);

        reflCam = null; rt = null; splash = null; splashMat = null;
        bodies.Clear();
    }

    private void LateUpdate()
    {
        WaterY = sr.bounds.max.y;
        Shader.SetGlobalFloat(WaterYId, WaterY);
        Shader.SetGlobalFloat(TimeId, Now);

        float moonX = moon != null ? moon.position.x : Shader.GetGlobalVector(LightPosId).x;
        Shader.SetGlobalFloat(MoonXId, moonX + moonXOffset);

        if (Application.isPlaying)
        {
            UpdateReflectionCamera();
            UpdateAmbient();

            if (autoInteract)
                UpdateBodies();
        }

        Shader.SetGlobalVectorArray(RipplesId, ripples);
    }

    // ================= Yansıma =================

    private void UpdateReflectionCamera()
    {
        if (mainCam == null)
            mainCam = Camera.main;

        if (mainCam == null)
        {
            Shader.SetGlobalFloat(HasReflId, 0f);
            return;
        }

        int w = Mathf.Max(64, Mathf.RoundToInt(mainCam.pixelWidth * reflectionResolution));
        int h = Mathf.Max(64, Mathf.RoundToInt(mainCam.pixelHeight * reflectionResolution));

        if (rt == null || rt.width != w || rt.height != h)
        {
            if (rt != null) { rt.Release(); DestroyImmediate(rt); }
            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "WaterReflection", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            rt.Create();
        }

        if (reflCam == null)
        {
            var go = new GameObject("~WaterReflectionCamera") { hideFlags = HideFlags.HideAndDontSave };
            reflCam = go.AddComponent<Camera>();
            reflCam.enabled = true;
        }

        reflCam.CopyFrom(mainCam);
        reflCam.orthographic = true;
        // LateUpdate'te okunan boyut yanlış olabiliyor (CameraZoom / Pixel Perfect çizimde değiştiriyor):
        // ana kameranın son çizimdeki gerçek boyutunu kullan, biraz pay bırak.
        float baseSize = mainVisibleSize > 0f ? mainVisibleSize : mainCam.orthographicSize;
        reflCam.orthographicSize = Mathf.Max(baseSize * reflectionMargin, minReflectionSize);
        reflCam.aspect = mainCam.aspect * reflectionWidthBoost;
        reflCam.targetTexture = rt;
        reflCam.depth = mainCam.depth - 1f;
        reflCam.clearFlags = CameraClearFlags.SolidColor;
        reflCam.backgroundColor = overrideClearColor ? reflectionClearColor : mainCam.backgroundColor;

        // Ana kameranın su çizgisine göre aynası.
        Vector3 p = mainCam.transform.position;
        reflCam.transform.SetPositionAndRotation(new Vector3(p.x, 2f * WaterY - p.y, p.z), Quaternion.identity);

        var data = reflCam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (data != null)
        {
            data.renderPostProcessing = false;
            data.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Base;
            data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
        }

        float halfH = reflCam.orthographicSize;
        float halfW = halfH * reflCam.aspect;
        Vector3 rp = reflCam.transform.position;

        Shader.SetGlobalTexture(ReflTexId, rt);
        Shader.SetGlobalVector(ReflRectId, new Vector4(rp.x - halfW, rp.y - halfH, 1f / (2f * halfW), 1f / (2f * halfH)));
        Shader.SetGlobalFloat(HasReflId, 1f);

        if (Time.unscaledTime >= nextExcludeScan)
        {
            nextExcludeScan = Time.unscaledTime + 1f;
            ScanExcluded();
        }
    }

    private void ScanExcluded()
    {
        excluded.Clear();

        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if (r == sr || r.sortingOrder >= excludeOrderFrom || r.name == "~shadow")
                excluded.Add(r);
    }

    private void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam == mainCam && cam.orthographic)
            mainVisibleSize = cam.orthographicSize;

        if (cam != reflCam)
            return;

        hidden.Clear();

        for (int i = 0; i < excluded.Count; i++)
        {
            Renderer r = excluded[i];

            if (r != null && !r.forceRenderingOff)
            {
                r.forceRenderingOff = true;
                hidden.Add(r);
            }
        }
    }

    private void OnEndCamera(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam == mainCam && cam.orthographic)
            mainVisibleSize = cam.orthographicSize;

        if (cam == reflCam)
            RestoreHidden();
    }

    private void RestoreHidden()
    {
        for (int i = 0; i < hidden.Count; i++)
            if (hidden[i] != null)
                hidden[i].forceRenderingOff = false;

        hidden.Clear();
    }

    // ================= Halkalar =================

    /// <summary>Yüzeyde halka. strength ~0.3 adım, 1 iniş/vuruş.</summary>
    public void Ripple(Vector2 worldPos, float strength)
    {
        float depth = Mathf.Max(0.12f, WaterY - worldPos.y);
        ripples[nextRipple] = new Vector4(worldPos.x, depth, Now, Mathf.Clamp(strength, 0f, 2f));
        nextRipple = (nextRipple + 1) % ripples.Length;
    }

    /// <summary>Su çizgisinde, verilen X'te halka.</summary>
    public void RippleAtSurface(float x, float strength) => Ripple(new Vector2(x, WaterY - 0.25f), strength);

    /// <summary>Damlacık sıçrat (count adet).</summary>
    public void Splash(Vector2 worldPos, int count, float power = 1f)
    {
        if (splash == null || count <= 0)
            return;

        var ep = new ParticleSystem.EmitParams();

        for (int i = 0; i < count; i++)
        {
            ep.position = new Vector3(worldPos.x + Random.Range(-0.25f, 0.25f), WaterY + 0.02f, 0f);
            float a = Random.Range(-1f, 1f);
            ep.velocity = new Vector3(a * 2.2f * power, Random.Range(3f, 6.5f) * power, 0f);
            ep.startSize = Random.Range(0.07f, 0.17f);
            ep.startLifetime = Random.Range(0.35f, 0.7f);
            splash.Emit(ep, 1);
        }
    }

    /// <summary>Vuruş / patlama gibi darbe: halka + sıçrama.</summary>
    public void Impact(float x, float strength)
    {
        RippleAtSurface(x, strength);
        Splash(new Vector2(x, WaterY), Mathf.RoundToInt(4 + 6 * strength), 0.7f + 0.4f * strength);
    }

    private void UpdateAmbient()
    {
        if (ambientRate <= 0f || mainCam == null)
            return;

        ambientTimer -= Time.deltaTime;

        if (ambientTimer > 0f)
            return;

        ambientTimer = Random.Range(0.4f, 1.6f) / ambientRate;

        float halfW = mainCam.orthographicSize * mainCam.aspect;
        float x = mainCam.transform.position.x + Random.Range(-halfW, halfW);
        float depth = Mathf.Lerp(0.3f, ambientMaxDepth, Random.value * Random.value);
        Ripple(new Vector2(x, WaterY - depth), ambientStrength * Random.Range(0.5f, 1f));
    }

    // ================= Karakterler =================

    private void UpdateBodies()
    {
        if (Time.unscaledTime >= nextBodyScan)
        {
            nextBodyScan = Time.unscaledTime + 2f;
            ScanBodies();
        }

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        for (int i = bodies.Count - 1; i >= 0; i--)
        {
            Body b = bodies[i];

            if (b.rb == null)
            {
                bodies.RemoveAt(i);
                continue;
            }

            if (!b.rb.gameObject.activeInHierarchy)
                continue;

            if (!FeetOf(b.rb, out Vector2 feet))
                continue;

            float vy = (feet.y - b.last.y) / dt;
            float dx = Mathf.Abs(feet.x - b.last.x);
            float speedX = dx / dt;
            bool inWater = feet.y - WaterY < contactHeight;

            if (inWater)
            {
                // İniş
                if (!b.inWater && b.vy < -landSpeed)
                {
                    float s = Mathf.Clamp01(-b.vy / (landSpeed * 2.5f));
                    RippleAtSurface(feet.x, 0.6f + 0.6f * s);
                    Splash(new Vector2(feet.x, WaterY), Mathf.RoundToInt(5 + 9 * s), 0.8f + 0.5f * s);
                    b.walked = 0f;
                }

                // Adım
                if (speedX > 0.8f && dx < 3f)
                {
                    b.walked += dx;

                    if (b.walked >= b.nextStep)
                    {
                        b.walked = 0f;
                        b.nextStep = stepSpacing * Random.Range(0.8f, 1.6f);

                        if (Random.value < stepChance)
                        {
                            float s = stepStrength * Random.Range(0.7f, 1.15f) * (speedX > 9f ? 1.4f : 1f);
                            RippleAtSurface(feet.x + Random.Range(-0.2f, 0.2f), s);

                            if (speedX > 9f && Random.value < 0.3f)   // dash: ara sıra birkaç damla
                                Splash(new Vector2(feet.x, WaterY), 2, 0.5f);
                        }
                    }
                }
            }

            // Sudan sıçrayınca (zıplama)
            if (b.inWater && !inWater && vy > 4f)
                RippleAtSurface(feet.x, 0.45f);

            b.inWater = inWater;
            b.vy = vy;
            b.last = feet;
        }
    }

    private void ScanBodies()
    {
        foreach (Rigidbody2D rb in FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None))
        {
            if (rb.bodyType == RigidbodyType2D.Static)
                continue;

            bool known = false;
            for (int i = 0; i < bodies.Count; i++)
                if (bodies[i].rb == rb) { known = true; break; }

            if (known || !FeetOf(rb, out Vector2 f))
                continue;

            bodies.Add(new Body { rb = rb, last = f, inWater = f.y - WaterY < contactHeight });
        }
    }

    private static readonly List<Collider2D> tmpCols = new List<Collider2D>();

    private static bool FeetOf(Rigidbody2D rb, out Vector2 feet)
    {
        feet = rb.position;
        tmpCols.Clear();
        rb.GetAttachedColliders(tmpCols);

        bool any = false;
        Bounds b = default;

        foreach (Collider2D c in tmpCols)
        {
            if (c == null || c.isTrigger || !c.enabled)
                continue;

            if (!any) { b = c.bounds; any = true; }
            else b.Encapsulate(c.bounds);
        }

        if (!any || b.size.x > 20f)   // zemin gibi dev gövdeler değil
            return false;

        feet = new Vector2(b.center.x, b.min.y);
        return true;
    }

    // ================= Sıçrama parçacıkları =================

    private void BuildSplash()
    {
        var go = new GameObject("~WaterSplash") { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(transform, false);
        splash = go.AddComponent<ParticleSystem>();
        splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = splash.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 2.2f;
        main.startColor = splashColor;
        main.maxParticles = 400;

        var em = splash.emission;
        em.enabled = false;

        var shape = splash.shape;
        shape.enabled = false;

        var sol = splash.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

        var col = splash.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var r = go.GetComponent<ParticleSystemRenderer>();
        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        splashMat = new Material(sh) { name = "WaterSplash (runtime)" };
        if (splashTexture != null) splashMat.mainTexture = splashTexture;
        r.sharedMaterial = splashMat;
        r.sortingLayerID = sr.sortingLayerID;
        r.sortingOrder = splashSortingOrder;

        splash.Play();
    }
}
