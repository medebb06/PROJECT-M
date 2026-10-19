using UnityEngine;

/// <summary>
/// YAGMUR HALKALARI: yagmur yagarken suyun uzerinde yuvarlak (basik) halkalar acilip buyuyerek solar.
/// Play'de kendiliginden kurulur. RainSystem acik/kapali ve siddetini takip eder.
/// Ayarlar: sahnedeki 'RainWaterRings' objesinden.
/// </summary>
[DefaultExecutionOrder(10046)]
[DisallowMultipleComponent]
public class RainWaterRings : MonoBehaviour
{
    [Header("Halkalar")]
    public bool enabledRings = true;
    [Tooltip("Saniyedeki halka sayisi (tam siddette).")]
    public float ringsPerSecond = 40f;
    public Vector2 finalWidth = new Vector2(0.7f, 2.0f);
    [Tooltip("Halka basikligi (yukseklik / genislik).")]
    [Range(0.1f, 0.8f)] public float flatness = 0.3f;
    public Vector2 life = new Vector2(1.0f, 1.6f);
    [Range(0f, 1f)] public float alpha = 0.85f;
    public Color color = new Color(0.82f, 0.92f, 1f, 1f);
    public int sortingOrder = 12;
    [Tooltip("Yakindaki (ekranin altindaki) halkalar ne kadar buyuk.")]
    public float nearScale = 1.5f;
    public float farScale = 0.55f;

    private ParticleSystem ps;
    private Material mat;
    private Texture2D tex;
    private float acc;
    private float level;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<RainWaterRings>() != null) return;
        new GameObject("RainWaterRings").AddComponent<RainWaterRings>();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        Build();
    }

    private void OnDisable()
    {
        if (ps != null) Destroy(ps.gameObject);
        if (mat != null) Destroy(mat);
        if (tex != null) Destroy(tex);
        ps = null; mat = null; tex = null;
    }

    private static Texture2D MakeRing(int n)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f;
                float dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float outer = Mathf.Exp(-Mathf.Pow((r - 0.86f) / 0.07f, 2f));
                float inner = 0.55f * Mathf.Exp(-Mathf.Pow((r - 0.52f) / 0.06f, 2f));
                float edge = Mathf.Clamp01((1f - r) * 8f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(outer + inner) * edge));
            }
        t.Apply(false, true);
        return t;
    }

    private void Build()
    {
        tex = MakeRing(128);
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        mat = new Material(sh) { name = "RainRing (runtime)", mainTexture = tex };

        var go = new GameObject("~rings");
        go.transform.SetParent(transform, false);
        ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.useUnscaledTime = true;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.maxParticles = 400;

        var em = ps.emission; em.enabled = false;
        var shp = ps.shape; shp.enabled = false;

        // buyuyerek: 0.12 -> 1 (genislik)
        var sol = ps.sizeOverLifetime; sol.enabled = true; sol.separateAxes = false;
        var sc = new AnimationCurve(new Keyframe(0f, 0.12f), new Keyframe(0.25f, 0.55f), new Keyframe(1f, 1f));
        sol.size = new ParticleSystem.MinMaxCurve(1f, sc);

        // solarak
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.55f, 0.55f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.sortingOrder = sortingOrder;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        ps.Play();
    }

    private void Update()
    {
        if (!Application.isPlaying || ps == null) return;

        var rain = RainSystem.Instance;
        float target = (enabledRings && rain != null && rain.raining && rain.waterSplashes) ? Mathf.Clamp01(rain.intensity) : 0f;
        level = Mathf.MoveTowards(level, target, Time.unscaledDeltaTime / 2f);
        if (level <= 0.001f) return;

        var water = WaterSurface.Instance;
        Camera cam = Camera.main;
        if (water == null || cam == null) return;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        float top = water.WaterY - 0.18f;
        float bottom = cam.transform.position.y - halfH;
        if (bottom >= top - 0.2f) return;

        acc += ringsPerSecond * level * Time.unscaledDeltaTime;
        int guard = 0;
        while (acc >= 1f && guard++ < 20)
        {
            acc -= 1f;
            float x = cam.transform.position.x + Random.Range(-halfW, halfW);
            if (!water.Covers(x)) continue;

            float t = Random.value;
            t = t * t * (3f - 2f * t) * 0.5f + t * 0.5f;     // hafif orta yogun
            float y = Mathf.Lerp(top, bottom, t);
            float depthScale = Mathf.Lerp(farScale, nearScale, t);
            float w = Random.Range(finalWidth.x, finalWidth.y) * depthScale;

            var ep = new ParticleSystem.EmitParams();
            ep.position = new Vector3(x, y, 0f);
            ep.startSize3D = new Vector3(w, w * flatness, 1f);
            ep.startLifetime = Random.Range(life.x, life.y);
            ep.startColor = new Color(color.r, color.g, color.b, alpha * Mathf.Lerp(0.6f, 1f, t));
            ps.Emit(ep, 1);
        }
    }
}
