using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// YAĞMUR SİSTEMİ (Play'de kendiliğinden kurulur, sahnede elle bir şey eklemen gerekmez).
///
/// Katmanlar:
///   ARKA   : ince, yavaş, soluk çizgiler (uzak derinlik)
///   ORTA   : karakterlerin arasında, orta hız
///   ÖN     : iri, hızlı, belirgin çizgiler (ön plan yapraklarının önünde)
///   Hepsi Stretched Billboard çizgi parçacığı, rüzgâr = hafif yatay hız.
///
/// Sıçrama: zemine düşünce minik damlacıklar, suya düşünce WaterSurface halkası + damla,
/// karakterin üstüne düşünce de küçük sıçramalar.
/// /// Şimşek: global ışığı salisede ikili parlatır + çok hafif beyaz flaş + kamera sarsıntısı + (varsa) gök gürültüsü.
///
/// Kısayollar:  F3 = yağmur aç/kapat,  F4 = şimşek çaktır.
/// </summary>
[DefaultExecutionOrder(10045)]
[DisallowMultipleComponent]
public class RainSystem : MonoBehaviour
{
    public static RainSystem Instance { get; private set; }

    [Header("AÇ / KAPAT (hepsi Play sırasında da canlı çalışır)")]
    [Tooltip("ANA ŞALTER: kapalıysa yağmur yumuşakça diner.")]
    public bool raining = true;
    [Tooltip("Arka katman (ince, uzak çizgiler).")]
    public bool backLayer = true;
    [Tooltip("Orta katman (karakterlerin arasında).")]
    public bool midLayer = true;
    [Tooltip("Ön katman (iri, hızlı çizgiler).")]
    public bool frontLayer = true;
    [Tooltip("Zemine düşünce damlacık sıçraması.")]
    public bool groundSplashes = true;
    [Tooltip("Suya düşünce halka + damla (WaterSurface).")]
    public bool waterSplashes = true;
    [Tooltip("Karakterin ve dummy'nin üstüne düşen sıçramalar.")]
    public bool characterSplashes = true;
    [Tooltip("Rüzgâr: kapalıysa yağmur dik düşer.")]
    public bool windEnabled = true;
    [Tooltip("Şimşek: kapalıysa hiç çakmaz (F4 ile yine tetiklenebilir).")]
    public bool lightning = true;
    [Tooltip("Şimşekte global ışık parlasın (su yansımaları dahil).")]
    public bool lightningLight = true;
    [Tooltip("Şimşekte hafif beyaz ekran flaşı.")]
    public bool lightningWhiteFlash = true;
    [Tooltip("Şimşekten sonra kamera sarsıntısı + gök gürültüsü.")]
    public bool thunder = true;

    [Header("Genel")]
    [Range(0f, 1f)] public float intensity = 0.8f;
    [Tooltip("Açma/kapama geçişi (sn).")]
    public float fadeTime = 3f;
    public bool hotkeys = true;

    [Header("Katman miktarları (1 = varsayılan)")]
    [Range(0f, 3f)] public float backAmount = 1f;
    [Range(0f, 3f)] public float midAmount = 1f;
    [Range(0f, 3f)] public float frontAmount = 1f;

    [Header("Çizgi uzunluğu")]
    [Tooltip("Yağmur çizgilerinin boyu (çarpan). 1 = eski, 3 = çok uzun.")]
    [Range(0.5f, 8f)] public float streakLength = 3.5f;

    [Header("Rüzgâr")]
    [Tooltip("Yatay hız (birim/sn). Negatif = sola savurur.")]
    public float wind = -2.5f;
    public float windGust = 1.2f;

    [Header("Çizim sırası (Default katmanı; zemin -5, ön plan yaprakları 450+)")]
    public int backOrder = -2;
    public int midOrder = 300;
    public int frontOrder = 520;
    public int splashOrder = 310;

    [Header("Renk")]
    public Color rainColor = new Color(0.74f, 0.84f, 1f, 1f);

    [Header("Sıçrama")]
    [Tooltip("Saniyede kaç zemin/su sıçraması (yoğunlukla çarpılır).")]
    public float splashRate = 26f;
    [Tooltip("Saniyede kaç sıçrama karakterin üstüne düşer.")]
    public float playerSplashRate = 9f;
    [Tooltip("Yağmurla suyun kendi halka sıklığı da artar.")]
    public float waterRippleBoost = 3f;

    [Header("Şimşek")]
    public Vector2 lightningInterval = new Vector2(7f, 18f);
    [Tooltip("Global ışığın şiddeti flaşta ne kadar katlanır.")]
    public float flashBoost = 2.2f;
    [Range(0f, 0.5f)] public float whiteFlash = 0.14f;
    public float thunderShake = 0.35f;
    public AudioClip[] thunderSounds;
    [Range(0f, 1f)] public float thunderVolume = 0.8f;

    // ------------------------------------------------------------------

    private class Layer
    {
        public ParticleSystem ps;
        public float baseRate, speed, floorOffset, windMul, lengthScale;
    }

    private Layer back, mid, front;
    private ParticleSystem splashPs;
    private Material rainMat, splashMat;
    private Texture2D discTex;
    private float level;           // 0..1 yumuşatılmış şiddet
    private float splashAcc, playerAcc;

    private PlayerController player;
    private Light2D globalLight;
    private float lightScan;

    // şimşek
    private float nextBolt;
    private float boltT = -1f;
    private float boltDur = 0.55f;
    private float flashLevel;       // 0..1 anlık parlama
    private float pendingThunderAt = -1f;
    private SpriteRenderer flashSr;
    private AudioSource audioSrc;
    private float baseRipple = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<RainSystem>() != null)
            return;

        new GameObject("RainSystem").AddComponent<RainSystem>();
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        Build();
        ScheduleBolt();
    }

    private void OnDisable()
    {
        if (WaterSurface.Instance != null && baseRipple >= 0f)
            WaterSurface.Instance.ambientRate = baseRipple;

        baseRipple = -1f;
        flashLevel = 0f;
        ApplyLight(); // ışığı bırak

        if (back != null && back.ps != null) Destroy(back.ps.gameObject);
        if (mid != null && mid.ps != null) Destroy(mid.ps.gameObject);
        if (front != null && front.ps != null) Destroy(front.ps.gameObject);
        if (splashPs != null) Destroy(splashPs.gameObject);
        if (flashSr != null) Destroy(flashSr.gameObject);

        back = mid = front = null;
        splashPs = null;
        flashSr = null;

        if (rainMat != null) Destroy(rainMat);
        if (splashMat != null) Destroy(splashMat);
        if (discTex != null) Destroy(discTex);
    }

    // ================= İNŞA =================

    private static Shader FindShader()
    {
        Shader s = Shader.Find("Sprites/Default");

        if (s == null)
            s = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");

        if (s == null)
            s = Shader.Find("Universal Render Pipeline/Particles/Unlit");

        return s;
    }

    private Texture2D MakeDisc(int n)
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
                float a = Mathf.Clamp01(1f - r);
                a = a * a * (3f - 2f * a);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        t.Apply(false, true);
        return t;
    }

    private ParticleSystem NewSystem(string name, Material mat, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.sortingOrder = order;
        return ps;
    }

    private Layer MakeLayer(string name, int order, float rate, float speed, Vector2 width, float lengthScale,
        float alpha, float floorOffset, float windMul)
    {
        Layer l = new Layer { baseRate = rate, speed = speed, floorOffset = floorOffset, windMul = windMul };
        l.ps = NewSystem(name, rainMat, order);

        var main = l.ps.main;
        main.useUnscaledTime = true;   // duraklatma menüsünde ayar yaparken de aksın
        main.loop = true;
        main.playOnAwake = false;
        main.prewarm = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(width.x, width.y);
        main.startLifetime = 1.5f; // her kare güncellenir
        main.maxParticles = 1800;
        main.startColor = new Color(rainColor.r, rainColor.g, rainColor.b, alpha);

        var em = l.ps.emission;
        em.rateOverTime = 0f;

        var sh = l.ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(30f, 0.1f, 0.1f);

        var vel = l.ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(wind * windMul, wind * windMul);
        vel.y = new ParticleSystem.MinMaxCurve(-speed, -speed);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var rend = l.ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Stretch;
        rend.lengthScale = lengthScale;
        rend.velocityScale = 0.02f;
        l.lengthScale = lengthScale;
        rend.cameraVelocityScale = 0f;

        l.ps.Play();
        return l;
    }

    private void Build()
    {
        Shader sh = FindShader();

        if (sh == null)
        {
            Debug.LogWarning("RainSystem: parçacık shader'ı bulunamadı.");
            return;
        }

        discTex = MakeDisc(64);

        rainMat = new Material(sh) { name = "Rain (runtime)", mainTexture = discTex };
        splashMat = new Material(sh) { name = "RainSplash (runtime)", mainTexture = discTex };

        //                       ad        sıra       adet/sn hız   kalınlık                 uzunluk alfa   zemin  rüzgâr
        back = MakeLayer("~rain back", backOrder, 240f, 11f, new Vector2(0.010f, 0.016f), 1.3f, 0.20f, 2.5f, 0.6f);
        mid = MakeLayer("~rain mid", midOrder, 170f, 17f, new Vector2(0.016f, 0.024f), 1.5f, 0.30f, 0.9f, 1.0f);
        front = MakeLayer("~rain front", frontOrder, 55f, 26f, new Vector2(0.030f, 0.046f), 1.8f, 0.34f, -1.6f, 1.5f);

        // Zemin / karakter sıçraması: küçük damlacıklar, yerçekimiyle yay çizer.
        splashPs = NewSystem("~rain splash", splashMat, splashOrder);
        var sm = splashPs.main;
        sm.useUnscaledTime = true;
        sm.loop = false;
        sm.playOnAwake = false;
        sm.simulationSpace = ParticleSystemSimulationSpace.World;
        sm.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.42f);
        sm.startSpeed = 0f;
        sm.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.075f);
        sm.startColor = new Color(0.85f, 0.92f, 1f, 0.8f);
        sm.gravityModifier = 2.4f;
        sm.maxParticles = 600;
        var se = splashPs.emission;
        se.rateOverTime = 0f;
        var ss = splashPs.shape;
        ss.enabled = false;
        var sc = splashPs.colorOverLifetime;
        sc.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        sc.color = g;
        var ssz = splashPs.sizeOverLifetime;
        ssz.enabled = true;
        ssz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.3f)));
        splashPs.Play();

        // Şimşek flaşı için tam ekran beyaz kare.
        GameObject fgo = new GameObject("~rain flash");
        fgo.transform.SetParent(transform, false);
        flashSr = fgo.AddComponent<SpriteRenderer>();
        flashSr.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(0.5f, 0.5f), Texture2D.whiteTexture.width);
        flashSr.sortingOrder = 950;
        flashSr.color = new Color(0.85f, 0.9f, 1f, 0f);

        if (GetComponent<RainLightningApply>() == null)
            gameObject.AddComponent<RainLightningApply>();

        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        audioSrc.spatialBlend = 0f;
    }

    // ================= YARDIMCILAR =================

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

    private float GroundY(Camera cam)
    {
        if (player == null)
            player = FindFirstObjectByType<PlayerController>();

        if (player != null)
            return player.transform.position.y;

        return cam.transform.position.y - cam.orthographicSize * 0.4f;
    }

    // ================= GÜNCELLEME =================

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (hotkeys)
        {
            if (Input.GetKeyDown(KeyCode.F3)) raining = !raining;
            if (Input.GetKeyDown(KeyCode.F4)) StartBolt();
        }

        if (rainMat == null)
            return;

        float dt = Time.unscaledDeltaTime;
        float target = raining ? Mathf.Clamp01(intensity) : 0f;
        level = Mathf.MoveTowards(level, target, dt / Mathf.Max(0.05f, fadeTime) * Mathf.Max(0.2f, intensity));

        // Su halkaları da yağmurla sıklaşsın.
        if (WaterSurface.Instance != null)
        {
            if (baseRipple < 0f)
                baseRipple = WaterSurface.Instance.ambientRate;

            WaterSurface.Instance.ambientRate = baseRipple + (waterSplashes ? waterRippleBoost * level : 0f);
        }

        UpdateLightning(dt);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || rainMat == null)
            return;

        Camera cam = ViewCam();

        if (cam == null)
            return;

        float h = cam.orthographicSize * 2f;
        float w = h * Mathf.Max(1.2f, cam.aspect);
        Vector3 cp = cam.transform.position;
        float top = cp.y + h * 0.5f + 1.5f;
        float ground = GroundY(cam);
        float gust = windGust * (Mathf.Sin(Time.time * 0.37f) + 0.6f * Mathf.Sin(Time.time * 0.93f + 1.1f));

        UpdateLayer(back, cam, cp, w, top, ground, gust);
        UpdateLayer(mid, cam, cp, w, top, ground, gust);
        UpdateLayer(front, cam, cp, w, top, ground, gust);

        UpdateSplashes(cam, cp, w, h, top);
        UpdateFlashQuad(cp, w, h);
    }

    private void UpdateLayer(Layer l, Camera cam, Vector3 cp, float w, float top, float ground, float gust)
    {
        if (l == null || l.ps == null)
            return;

        var rr = l.ps.GetComponent<ParticleSystemRenderer>();
        rr.lengthScale = l.lengthScale * streakLength;
        rr.velocityScale = 0.02f * streakLength;

        float wx = windEnabled ? (wind + gust) * l.windMul : 0f;
        float floor = ground + l.floorOffset;
        float fall = Mathf.Max(2f, top - floor);
        float life = fall / l.speed;

        var main = l.ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.85f, life * 1.05f);

        var vel = l.ps.velocityOverLifetime;
        vel.x = new ParticleSystem.MinMaxCurve(wx, wx);
        vel.y = new ParticleSystem.MinMaxCurve(-l.speed, -l.speed);

        // Rüzgârla kayan genişliği de kapsasın: kutuyu rüzgârın tersine kaydır, genişlet.
        float drift = Mathf.Abs(wx) * life;

        bool on = (l == back ? backLayer : l == mid ? midLayer : frontLayer);
        float amt = l == back ? backAmount : l == mid ? midAmount : frontAmount;
        var sh = l.ps.shape;
        sh.scale = new Vector3(w * 1.15f + drift, 0.1f, 0.1f);
        l.ps.transform.position = new Vector3(cp.x - Mathf.Sign(wx) * drift * 0.5f, top, 0f);

        var em = l.ps.emission;
        em.rateOverTime = on ? l.baseRate * level * amt * (w / 36f) : 0f;
    }

    private void UpdateSplashes(Camera cam, Vector3 cp, float w, float h, float top)
    {
        if (splashPs == null || level <= 0.01f)
            return;

        if (!groundSplashes && !waterSplashes && !characterSplashes)
            return;

        splashAcc += splashRate * level * Time.deltaTime;
        playerAcc += playerSplashRate * level * Time.deltaTime;

        int groundMask = LayerMask.GetMask("Ground");
        float half = w * 0.5f;
        int guard = 0;

        while (splashAcc >= 1f && guard++ < 12)
        {
            splashAcc -= 1f;

            float x = cp.x + Random.Range(-half, half);
            WaterSurface water = WaterSurface.Instance;

            if (water != null && water.Covers(x))
            {
                if (!waterSplashes)
                    continue;

                // Suya düşen damla: yüzeyde minik halka (+ ara sıra damlacık).
                water.RippleAtSurface(x, Random.Range(0.10f, 0.22f));

                if (Random.value < 0.25f)
                    water.Splash(new Vector2(x, water.WaterY), 1, 0.45f);

                continue;
            }

            if (!groundSplashes)
                continue;

            RaycastHit2D hit = Physics2D.Raycast(new Vector2(x, top), Vector2.down, h + 4f, groundMask);

            if (hit.collider == null)
                continue;

            EmitSplash(new Vector3(hit.point.x, hit.point.y + 0.02f, 0f), Random.Range(2, 4), 1f);
        }

        // Karakterin üstüne düşenler.
        if (!characterSplashes)
        {
            playerAcc = 0f;
            return;
        }

        if (player == null)
            player = FindFirstObjectByType<PlayerController>();

        guard = 0;

        while (playerAcc >= 1f && guard++ < 6)
        {
            playerAcc -= 1f;

            if (player == null)
                break;

            Collider2D pc = player.GetComponentInChildren<Collider2D>();

            if (pc == null)
                continue;

            Bounds b = pc.bounds;
            Vector3 p = new Vector3(Random.Range(b.min.x, b.max.x) , b.max.y - Random.Range(0f, 0.25f), 0f);
            EmitSplash(p, 2, 0.7f);
        }

        // Eğitim dummy'sine de düşsün.
        foreach (TrainingDummy d in TrainingDummy.All)
        {
            if (d == null || Random.value > 0.35f * level)
                continue;

            Collider2D dc = d.GetComponentInChildren<Collider2D>();

            if (dc == null)
                continue;

            Bounds b = dc.bounds;
            EmitSplash(new Vector3(Random.Range(b.min.x, b.max.x), b.max.y, 0f), 2, 0.7f);
        }
    }

    private void EmitSplash(Vector3 pos, int count, float power)
    {
        var ep = new ParticleSystem.EmitParams();

        for (int i = 0; i < count; i++)
        {
            ep.position = pos + new Vector3(Random.Range(-0.08f, 0.08f), 0f, 0f);
            ep.velocity = new Vector3(Random.Range(-1.3f, 1.3f) * power, Random.Range(1.4f, 3.2f) * power, 0f);
            ep.startLifetime = Random.Range(0.2f, 0.4f);
            ep.startSize = Random.Range(0.035f, 0.075f);
            splashPs.Emit(ep, 1);
        }
    }

    private void UpdateFlashQuad(Vector3 cp, float w, float h)
    {
        if (flashSr == null)
            return;

        flashSr.transform.position = new Vector3(cp.x, cp.y, 0f);
        flashSr.transform.localScale = new Vector3(w * 1.2f, h * 1.2f, 1f);

        Color c = flashSr.color;
        c.a = lightningWhiteFlash ? whiteFlash * flashLevel : 0f;
        flashSr.color = c;
    }

    // ================= ŞİMŞEK =================

    private void ScheduleBolt()
    {
        nextBolt = Time.unscaledTime + Random.Range(lightningInterval.x, lightningInterval.y);
    }

    public void StartBolt()
    {
        boltT = 0f;
        boltDur = Random.Range(0.5f, 0.75f);

        if (thunderSounds != null && thunderSounds.Length > 0 && audioSrc != null)
            pendingThunderAt = Time.unscaledTime + Random.Range(0.5f, 1.8f);
        else
            pendingThunderAt = Time.unscaledTime + Random.Range(0.5f, 1.8f); // sarsıntı için yine de
    }

    private void UpdateLightning(float dt)
    {
        if (lightning && raining && level > 0.3f && boltT < 0f && Time.unscaledTime >= nextBolt)
        {
            StartBolt();
            ScheduleBolt();
        }

        if (boltT >= 0f)
        {
            boltT += dt;
            float u = boltT / boltDur;

            if (u >= 1f)
            {
                boltT = -1f;
                flashLevel = 0f;
            }
            else
            {
                // İki ani parlama + sönen kuyruk.
                float f =
                    Pulse(u, 0.00f, 0.10f) * 1.0f +
                    Pulse(u, 0.16f, 0.30f) * 0.55f +
                    Pulse(u, 0.38f, 1.00f) * 0.7f * (1f - u);

                flashLevel = Mathf.Clamp01(f);
            }
        }

        if (pendingThunderAt > 0f && Time.unscaledTime >= pendingThunderAt)
        {
            pendingThunderAt = -1f;

            if (thunder && thunderSounds != null && thunderSounds.Length > 0 && audioSrc != null)
            {
                AudioClip c = thunderSounds[Random.Range(0, thunderSounds.Length)];

                if (c != null)
                    audioSrc.PlayOneShot(c, thunderVolume);
            }

            if (thunder && CameraShake.Instance != null && thunderShake > 0f)
                CameraShake.Instance.Shake(thunderShake);
        }
    }

    // a..b arası hızlı yükselip sönen tepe.
    private static float Pulse(float u, float a, float b)
    {
        if (u < a || u > b)
            return 0f;

        float t = (u - a) / (b - a);
        return t < 0.2f ? t / 0.2f : 1f - (t - 0.2f) / 0.8f;
    }

    // TimeOfDay global ışığı her kare ezdiği için, bu TimeOfDay'den SONRA çalışır.
    public void ApplyLight()
    {
        if (globalLight == null && Time.unscaledTime >= lightScan)
        {
            lightScan = Time.unscaledTime + 1f;

            foreach (Light2D l in FindObjectsByType<Light2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (l.lightType == Light2D.LightType.Global)
                {
                    globalLight = l;
                    break;
                }
            }
        }

        if (globalLight == null || flashLevel <= 0.001f || !lightningLight)
            return;

        globalLight.intensity *= 1f + (flashBoost - 1f) * flashLevel;
    }
}

/// <summary>Şimşek parlaması: TimeOfDay (10040) global ışığı yazdıktan sonra üstüne uygular.</summary>
[DefaultExecutionOrder(32000)]
public class RainLightningApply : MonoBehaviour
{
    private void LateUpdate()
    {
        if (RainSystem.Instance != null)
            RainSystem.Instance.ApplyLight();
    }
}
