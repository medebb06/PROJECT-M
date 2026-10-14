using UnityEngine;

/// <summary>
/// SU ÜSTÜ DUMANI (stilize): suyun yüzeyinden yavaşça yükselen, yuvarlak, saydam duman topları.
/// Gerçekçi değil, çizgi film gibi: yumuşak kenarlı yuvarlak şekiller, üst üste binince arkası görünür.
/// İki katman: ARKA (büyük, soluk, yavaş) ve ÖN (daha küçük, biraz belirgin). İkisi de karakterlerin arkasında.
/// Sadece suyun olduğu yerde (WaterSurface.Covers) doğar. Play'de kendiliğinden kurulur.
/// Hepsi Inspector'dan açılıp kapanır ve duraklatma menüsünde (TEST > SU DUMANI) ayarlanır.
/// </summary>
[DefaultExecutionOrder(10046)]
[DisallowMultipleComponent]
public class WaterSmoke : MonoBehaviour
{
    public static WaterSmoke Instance { get; private set; }

    [Header("AÇ / KAPAT")]
    public bool smoke = true;
    public bool backLayer = true;
    public bool frontLayer = true;

    [Header("Görünüm")]
    [Tooltip("Miktar çarpanı (doğma sıklığı).")]
    [Range(0f, 3f)] public float amount = 1f;
    [Tooltip("En fazla ne kadar saydam olmayacağı. Düşük = daha silik, arkası daha görünür.")]
    [Range(0f, 1f)] public float opacity = 0.16f;
    [Tooltip("Top boyu çarpanı.")]
    [Range(0.3f, 3f)] public float size = 1f;
    [Tooltip("Yükselme / sürüklenme hızı çarpanı.")]
    [Range(0f, 3f)] public float speed = 1f;
    [Tooltip("Dumanın yatay yönde kayma hızı (birim/sn). Negatif = sola.")]
    public float drift = -0.25f;
    public Color color = new Color(0.78f, 0.85f, 0.95f, 1f);

    [Header("Çizim sırası (Default katmanı; su -4, karakterler ≥ 0)")]
    public int backOrder = -3;
    public int frontOrder = -2;

    // ------------------------------------------------------------------

    private class Layer
    {
        public ParticleSystem ps;
        public float perUnit;          // birim genişlik başına saniyede doğan
        public Vector2 sizeRange, riseRange, lifeRange;
        public float alphaMul;
        public float acc;
    }

    private Layer back, front;
    private Material mat;
    private Texture2D tex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<WaterSmoke>() != null)
            return;

        new GameObject("WaterSmoke").AddComponent<WaterSmoke>();
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
            Build();
    }

    private void OnDisable()
    {
        if (back != null && back.ps != null) Destroy(back.ps.gameObject);
        if (front != null && front.ps != null) Destroy(front.ps.gameObject);
        back = front = null;

        if (mat != null) Destroy(mat);
        if (tex != null) Destroy(tex);
    }

    // Yumuşak kenarlı yuvarlak: merkez dolu, kenara doğru söner (net bir top siluetinin okunduğu kadar).
    private Texture2D MakeBall(int n)
    {
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f;
                float dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);

                float edge = 1f - Mathf.SmoothStep(0.80f, 1f, r);
                float body = Mathf.Lerp(1f, 0.7f, r);              // ortası biraz daha dolu
                t.SetPixel(x, y, new Color(1f, 1f, 1f, edge * body));
            }
        }

        t.Apply(false, true);
        return t;
    }

    private ParticleSystem NewSystem(string name, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.useUnscaledTime = true;      // duraklatma menüsünde ayar yaparken de aksın
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.startLifetime = 8f;
        main.maxParticles = 500;

        var em = ps.emission;
        em.rateOverTime = 0f;

        var sh = ps.shape;
        sh.enabled = false;

        // Yavaş, dalgalı süzülme (üstüne rüzgâr ve yükselme EmitParams ile verilir).
        var nz = ps.noise;
        nz.enabled = true;
        nz.strength = 0.45f;
        nz.frequency = 0.12f;
        nz.scrollSpeed = 0.18f;
        nz.damping = true;
        nz.quality = ParticleSystemNoiseQuality.Low;

        // Doğarken belirir, ölürken kaybolur.
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0.75f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        // Yükseldikçe şişer (stilize "puf" hissi).
        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.55f), new Keyframe(0.4f, 0.95f), new Keyframe(1f, 1.45f)));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.sortingOrder = order;

        ps.Play();
        return ps;
    }

    private static Shader FindShader()
    {
        Shader s = Shader.Find("Sprites/Default");

        if (s == null) s = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Particles/Unlit");

        return s;
    }

    private void Build()
    {
        Shader sh = FindShader();

        if (sh == null)
        {
            Debug.LogWarning("WaterSmoke: parçacık shader'ı bulunamadı.");
            return;
        }

        tex = MakeBall(128);
        mat = new Material(sh) { name = "WaterSmoke (runtime)", mainTexture = tex };

        back = new Layer
        {
            ps = NewSystem("~smoke back", backOrder),
            perUnit = 0.05f,
            sizeRange = new Vector2(3.2f, 6.0f),
            riseRange = new Vector2(0.10f, 0.28f),
            lifeRange = new Vector2(11f, 16f),
            alphaMul = 0.65f
        };

        front = new Layer
        {
            ps = NewSystem("~smoke front", frontOrder),
            perUnit = 0.085f,
            sizeRange = new Vector2(1.6f, 3.4f),
            riseRange = new Vector2(0.16f, 0.42f),
            lifeRange = new Vector2(8f, 12f),
            alphaMul = 1f
        };
    }

    // Ekrana gerçekten çizilen kamera (en yüksek depth'li ortografik).
    private static Camera ViewCam()
    {
        Camera best = null;

        foreach (Camera c in Camera.allCameras)
        {
            if (!c.enabled || !c.orthographic || c.targetTexture != null)
                continue;

            if (best == null || c.depth > best.depth)
                best = c;
        }

        return best != null ? best : Camera.main;
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || mat == null)
            return;

        // Çizim sırası canlı değişebilsin.
        if (back != null && back.ps != null)
            back.ps.GetComponent<ParticleSystemRenderer>().sortingOrder = backOrder;

        if (front != null && front.ps != null)
            front.ps.GetComponent<ParticleSystemRenderer>().sortingOrder = frontOrder;

        WaterSurface water = WaterSurface.Instance;
        Camera cam = ViewCam();

        if (!smoke || water == null || cam == null)
            return;

        float h = cam.orthographicSize * 2f;
        float w = h * Mathf.Max(1.2f, cam.aspect);
        float cx = cam.transform.position.x;
        float dt = Time.unscaledDeltaTime;

        // Doğma bölgesi kameradan biraz geniş: kenarlardan içeri dalgalansın.
        float span = w * 1.15f;

        if (backLayer) Spawn(back, water, cx, span, dt);
        if (frontLayer) Spawn(front, water, cx, span, dt);
    }

    private void Spawn(Layer l, WaterSurface water, float cx, float span, float dt)
    {
        if (l == null || l.ps == null)
            return;

        l.acc += l.perUnit * span * amount * dt;

        int guard = 0;

        while (l.acc >= 1f && guard++ < 20)
        {
            l.acc -= 1f;

            float x = cx + Random.Range(-span * 0.5f, span * 0.5f);

            if (!water.Covers(x))
                continue;

            float sz = Random.Range(l.sizeRange.x, l.sizeRange.y) * size;
            Color c = color;
            c.a = opacity * l.alphaMul * Random.Range(0.6f, 1f);

            var ep = new ParticleSystem.EmitParams();
            ep.position = new Vector3(x, water.WaterY + Random.Range(-0.35f, 0.15f), 0f);
            ep.velocity = new Vector3(
                (drift + Random.Range(-0.18f, 0.18f)) * speed,
                Random.Range(l.riseRange.x, l.riseRange.y) * speed,
                0f);
            ep.startSize = sz;
            ep.startLifetime = Random.Range(l.lifeRange.x, l.lifeRange.y) / Mathf.Max(0.3f, speed);
            ep.startColor = c;
            l.ps.Emit(ep, 1);
        }
    }
}
