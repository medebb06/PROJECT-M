using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DAĞ KATMANLARI: gökyüzü ile uzak çalılar arasındaki arka plan dağları.
///
/// KURULUM (bir kez):
///   1) Hierarchy'de boş obje aç ("Mountains", [ATMOSPHERE]'in altına olabilir) → Add Component → Mountain Backdrop.
///   2) Sprite'lar (Mountain_Base / Mountain_Shade) Assets/Sprites/background/Mountains klasöründeyse kendiliğinden bulunur;
///      yoksa 'Base Sprite' ve 'Shade Sprite' alanlarına sürükle.
///   3) TimeOfDay > Hour kaydırıcısını oynat: dağların rengi saatle birlikte değişir.
///
/// NASIL ÇALIŞIR:
///   - Tek siluet sprite'ı, istediğin kadar katmanda (uzak / yakın...) farklı boyut, yükseklik, parallax ve renkle çizilir.
///   - Yatayda sonsuz tekrar eder; komşu parçalar aynalanır, dikiş görünmez (Mirror Tiles).
///   - Beyaz sprite'lar renklendirilir: gövde rengi + ayrı gölge rengi. Renkler 'Keys' tablosundan saate göre
///     yumuşak geçişle gelir (TimeOfDay ile aynı saat anahtarları). Uzak katmanlar 'Haze' kadar pus rengine solar.
///   - 'Color Source = FromSky' seçersen renkleri TimeOfDay'in gökyüzü renklerinden otomatik türetir.
///   - Ufuk Y'sini AtmosphereLight.horizonY'den, kamera dinlenme yüksekliğini TimeOfDay.camRestY'den alır.
///   - Sorting: Mist Back -42, ışık hüzmeleri -5. Dağlar varsayılan -90 / -72 (sisin ve uzak çalıların ARKASI).
///     Bulutlar dağın önünde/arkasında kalsın istiyorsan katmanın 'Sorting Order'ını oynat.
/// Edit modunda da çalışır (Hour kaydırıcısıyla Scene'de canlı görürsün).
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(10060)]
public class MountainBackdrop : MonoBehaviour
{
    public enum ColorSource
    {
        Table,
        FromSky
    }

    // ---------------------------------------------------------------
    // Saate göre renk anahtarı
    // ---------------------------------------------------------------
    [System.Serializable]
    public class ThemeKey
    {
        public string name = "Anahtar";

        [Range(0f, 24f)] public float hour;

        [Tooltip("Dağ gövdesi.")]
        public Color body = new Color(0.4f, 0.5f, 0.6f);

        [Tooltip("Gölge parçaları (yamaçların güneş almayan yüzü).")]
        public Color shade = new Color(0.3f, 0.4f, 0.5f);

        [Tooltip("Uzak katmanlar bu renge doğru solar (hava perspektifi). Genelde ufuk rengi.")]
        public Color haze = new Color(1f, 0.85f, 0.7f);
    }

    // ---------------------------------------------------------------
    // Katman ayarı
    // ---------------------------------------------------------------
    [System.Serializable]
    public class LayerCfg
    {
        public string name = "Dağ";
        public bool enabled = true;

        [Tooltip("Boş bırakılırsa ortak Base Sprite kullanılır.")]
        public Sprite baseSprite;

        [Tooltip("Boş bırakılırsa ortak Shade Sprite kullanılır. Base ile AYNI boyutta olmalı.")]
        public Sprite shadeSprite;

        [Header("Konum / hareket")]
        [Tooltip("Bir dağ parçasının dünya genişliği (birim). Büyüdükçe dağlar hem büyür hem daha seyrek tekrarlanır.")]
        [Min(1f)] public float widthUnits = 60f;

        [Tooltip("En yüksek tepenin ufuk çizgisinden yüksekliği (birim). Artırırsan dağ yukarı çıkar.")]
        public float yOffset = 4f;

        [Tooltip("Yatay kaydırma (birim). Katmanların tepeleri üst üste gelmesin diye farklı ver.")]
        public float xOffset = 0f;

        [Tooltip("Yatay kayma. 1 = kameraya yapışık (hiç kaymaz), 0 = zeminle birlikte kayar. Uzak katman 1'e yakın olmalı.")]
        [Range(0f, 1f)] public float parallaxX = 0.85f;

        [Tooltip("Dikey kayma (zıplarken / yükselirken). Genelde yatayın yarısı kadar.")]
        [Range(0f, 1f)] public float parallaxY = 0.4f;

        public bool flipX = false;

        [Tooltip("Komşu parçaları aynalar, dikiş görünmez. Kapalıysa parçalar aynen tekrarlanır (kenarlar uyuşmayabilir).")]
        public bool mirrorTiles = true;

        [Header("Görünüm")]
        [Tooltip("Hava perspektifi: 0 = dağ kendi rengi, 1 = tamamen pus rengi. Uzak katmanda yüksek, yakında düşük.")]
        [Range(0f, 1f)] public float haze = 0.4f;

        [Range(0f, 2f)] public float brightness = 1f;

        [Tooltip("Ek renk çarpanı (renk tablosunun üstüne).")]
        public Color tint = Color.white;

        [Range(0f, 1f)] public float opacity = 1f;

        [Tooltip("Gölge parçalarının görünürlüğü. 0 = gölge yok.")]
        [Range(0f, 1f)] public float shadeStrength = 1f;

        [Tooltip("Çizim sırası (gölge +1). Negatif = arkada.")]
        public int sortingOrder = -80;
    }

    // ---------------------------------------------------------------
    // Ayarlar
    // ---------------------------------------------------------------
    [Header("Sprite'lar")]
    [Tooltip("Beyaz dağ silueti (Mountain_Base).")]
    public Sprite baseSprite;

    [Tooltip("Gölge parçaları (Mountain_Shade). Boş olabilir.")]
    public Sprite shadeSprite;

    [Tooltip("Boşsa URP 2D Unlit Sprite shader'ı kullanılır (ışık sistemi rengi karartmaz; renk tamamen buradan gelir).")]
    public Shader shader;

    [Tooltip("Boş = Default.")]
    public string sortingLayerName = "";

    [Header("Renk kaynağı (zaman döngüsü)")]
    [Tooltip("Table: aşağıdaki saat tablosu. FromSky: TimeOfDay'in gökyüzü renklerinden otomatik.")]
    public ColorSource colorSource = ColorSource.Table;

    [Tooltip("TimeOfDay sahnede yoksa bu saat kullanılır.")]
    [Range(0f, 24f)] public float fallbackHour = 8.5f;

    [Tooltip("FromSky: gövde rengi gökyüzünün orta tonundan (0) ufuk tonuna (1) doğru.")]
    [Range(0f, 1f)] public float skyBodyBlend = 0.45f;

    [Tooltip("FromSky: gövde parlaklığı.")]
    [Range(0.2f, 1.5f)] public float skyBodyBrightness = 0.7f;

    [Tooltip("FromSky: gölge, gövdeden bu kadar koyu.")]
    [Range(0.4f, 1f)] public float skyShadeDarken = 0.78f;

    [Tooltip("Saat anahtarları (TimeOfDay ile aynı saatler). Aralarında yumuşak geçiş yapılır. Saate göre sıralı olmalı.")]
    public List<ThemeKey> keys = new List<ThemeKey>();

    [Header("Katmanlar (uzaktan yakına)")]
    public List<LayerCfg> layers = new List<LayerCfg>();

    [Header("Ufuk / kamera")]
    [Tooltip("Açıksa ufuk Y'si AtmosphereLight.horizonY'den alınır.")]
    public bool useAtmosphereHorizon = true;
    public float manualHorizonY = 0f;
    [Tooltip("TimeOfDay yoksa kamera dinlenme yüksekliği.")]
    public float manualCamRestY = 2.65f;

    // ---------------------------------------------------------------

    private class Tile
    {
        public GameObject root;
        public SpriteRenderer b;
        public SpriteRenderer s;
    }

    private class Built
    {
        public GameObject holder;
        public readonly List<Tile> tiles = new List<Tile>();
    }

    private const int MaxTiles = 12;

    private readonly List<Built> built = new List<Built>();
    private Material mat;
    private AtmosphereLight al;
    private float nextFind;
    private bool dirty = true;

#if UNITY_EDITOR
    private const string DefaultBasePath = "Assets/Sprites/background/Mountains/Mountain_Base.png";
    private const string DefaultShadePath = "Assets/Sprites/background/Mountains/Mountain_Shade.png";
#endif

    // =====================================================================
    // Varsayılanlar
    // =====================================================================

    public static List<ThemeKey> DefaultKeys()
    {
        ThemeKey night = new ThemeKey
        {
            name = "Gece", hour = 0f,
            body = new Color(0.075f, 0.115f, 0.215f),
            shade = new Color(0.050f, 0.075f, 0.150f),
            haze = new Color(0.20f, 0.29f, 0.42f)
        };

        ThemeKey dawn = new ThemeKey
        {
            name = "Şafak", hour = 6f,
            body = new Color(0.40f, 0.32f, 0.50f),
            shade = new Color(0.28f, 0.22f, 0.40f),
            haze = new Color(1.00f, 0.72f, 0.60f)
        };

        ThemeKey day = new ThemeKey
        {
            name = "Altın sabah", hour = 8.5f,
            body = new Color(0.42f, 0.57f, 0.68f),
            shade = new Color(0.32f, 0.46f, 0.60f),
            haze = new Color(1.00f, 0.88f, 0.72f)
        };

        ThemeKey sunset = new ThemeKey
        {
            name = "Gün batımı", hour = 18f,
            body = new Color(0.30f, 0.17f, 0.30f),
            shade = new Color(0.19f, 0.11f, 0.23f),
            haze = new Color(1.00f, 0.52f, 0.28f)
        };

        return new List<ThemeKey>
        {
            Copy(night, "Gece", 0f),
            Copy(night, "Gece", 4.5f),
            dawn,
            day,
            Copy(day, "Altın öğleden sonra", 15.5f),
            sunset,
            Copy(night, "Gece", 19.75f),
            Copy(night, "Gece", 24f)
        };
    }

    private static ThemeKey Copy(ThemeKey k, string name, float hour)
    {
        return new ThemeKey { name = name, hour = hour, body = k.body, shade = k.shade, haze = k.haze };
    }

    public static List<LayerCfg> DefaultLayers()
    {
        return new List<LayerCfg>
        {
            new LayerCfg
            {
                name = "Uzak dağlar", widthUnits = 72f, yOffset = 5.5f, xOffset = 0f,
                parallaxX = 0.90f, parallaxY = 0.45f, flipX = false,
                haze = 0.55f, brightness = 1.02f, sortingOrder = -90
            },
            new LayerCfg
            {
                name = "Yakın dağlar", widthUnits = 54f, yOffset = 3.0f, xOffset = 21f,
                parallaxX = 0.80f, parallaxY = 0.40f, flipX = true,
                haze = 0.20f, brightness = 0.90f, sortingOrder = -72
            }
        };
    }

    [ContextMenu("Varsayılan katmanları yükle")]
    public void ResetLayers()
    {
        layers = DefaultLayers();
        dirty = true;
    }

    [ContextMenu("Varsayılan renkleri yükle")]
    public void ResetKeys()
    {
        keys = DefaultKeys();
    }

    [ContextMenu("Sprite'ları klasörden bul")]
    public void FindSprites()
    {
#if UNITY_EDITOR
        baseSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(DefaultBasePath);
        shadeSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(DefaultShadePath);
        dirty = true;
#endif
    }

    /// <summary>Kodla yeniden kurmak için.</summary>
    public void Rebuild()
    {
        dirty = true;
    }

    // =====================================================================
    // Yaşam döngüsü
    // =====================================================================

    private void OnEnable()
    {
        if (keys == null || keys.Count == 0)
            keys = DefaultKeys();

        if (layers == null || layers.Count == 0)
            layers = DefaultLayers();

#if UNITY_EDITOR
        if (!Application.isPlaying && (baseSprite == null || shadeSprite == null))
        {
            if (baseSprite == null)
                baseSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(DefaultBasePath);

            if (shadeSprite == null)
                shadeSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(DefaultShadePath);
        }
#endif

        dirty = true;
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += OnBeginCamera;
    }

    private void OnDisable()
    {
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        Clear();
    }

#if UNITY_EDITOR
    private void Reset()
    {
        keys = DefaultKeys();
        layers = DefaultLayers();
        FindSprites();
    }

    private void OnValidate()
    {
        dirty = true;

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && isActiveAndEnabled && !Application.isPlaying)
                Tick();
        };
    }
#endif

    // Kamera çizilmeden hemen önce son kamera konumuna göre yeniden hizala (titreme olmasın).
    private void OnBeginCamera(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera c)
    {
        if (c != null && c == Camera.main && !dirty && built.Count == layers.Count)
            Tick();
    }

    private void LateUpdate()
    {
        Tick();
    }

    // =====================================================================
    // Kurulum
    // =====================================================================

    private void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);

            if (!c.name.StartsWith("~mountain"))
                continue;

            if (Application.isPlaying) Destroy(c.gameObject);
            else DestroyImmediate(c.gameObject);
        }

        built.Clear();

        if (mat != null)
        {
            if (Application.isPlaying) Destroy(mat);
            else DestroyImmediate(mat);

            mat = null;
        }
    }

    private Material MakeMaterial()
    {
        Shader sh = shader;

        if (sh == null)
            sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");

        if (sh == null)
            sh = Shader.Find("Sprites/Default");

        if (sh == null)
            return null;

        return new Material(sh) { name = "Mountains", hideFlags = HideFlags.DontSave };
    }

    private void Build()
    {
        Clear();
        dirty = false;

        mat = MakeMaterial();

        for (int i = 0; i < layers.Count; i++)
        {
            GameObject holder = new GameObject("~mountain " + layers[i].name);
            holder.hideFlags = HideFlags.DontSave;
            holder.transform.SetParent(transform, false);

            Built b = new Built { holder = holder };
            built.Add(b);
        }
    }

    private int SortingLayerId()
    {
        return string.IsNullOrEmpty(sortingLayerName) ? 0 : SortingLayer.NameToID(sortingLayerName);
    }

    private SpriteRenderer NewRenderer(string objName, Transform parent, Sprite sprite)
    {
        GameObject go = new GameObject(objName);
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(parent, false);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;

        if (mat != null)
            sr.sharedMaterial = mat;

        sr.sortingLayerID = SortingLayerId();

        return sr;
    }

    private void EnsureTiles(Built b, Sprite bs, Sprite ss, int count)
    {
        while (b.tiles.Count < count)
        {
            Tile t = new Tile();

            t.b = NewRenderer("~tile", b.holder.transform, bs);
            t.root = t.b.gameObject;

            if (ss != null)
                t.s = NewRenderer("~shade", t.root.transform, ss);

            b.tiles.Add(t);
        }
    }

    // =====================================================================
    // Renk (saat)
    // =====================================================================

    private static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private float CurrentHour()
    {
        TimeOfDay tod = TimeOfDay.Instance;
        return tod != null ? tod.hour : fallbackHour;
    }

    private void EvaluateTheme(float h, out Color body, out Color shade, out Color haze)
    {
        if (colorSource == ColorSource.FromSky && EvaluateSky(h, out body, out shade, out haze))
            return;

        EvaluateTable(h, out body, out shade, out haze);
    }

    private void EvaluateTable(float h, out Color body, out Color shade, out Color haze)
    {
        int n = keys != null ? keys.Count : 0;

        if (n == 0)
        {
            body = new Color(0.4f, 0.5f, 0.6f, 1f);
            shade = new Color(0.3f, 0.4f, 0.5f, 1f);
            haze = Color.white;
            return;
        }

        ThemeKey a = keys[0], b = keys[0];
        float t = 0f;

        if (n > 1)
        {
            if (h >= keys[n - 1].hour)
            {
                a = b = keys[n - 1];
            }
            else if (h > keys[0].hour)
            {
                for (int i = 0; i < n - 1; i++)
                {
                    if (h >= keys[i].hour && h <= keys[i + 1].hour)
                    {
                        a = keys[i];
                        b = keys[i + 1];
                        t = Smooth((h - a.hour) / Mathf.Max(0.0001f, b.hour - a.hour));
                        break;
                    }
                }
            }
        }

        body = Color.Lerp(a.body, b.body, t);
        shade = Color.Lerp(a.shade, b.shade, t);
        haze = Color.Lerp(a.haze, b.haze, t);
    }

    // TimeOfDay'in gökyüzü renklerinden türet. Başarısızsa false (tabloya düşer).
    private bool EvaluateSky(float h, out Color body, out Color shade, out Color haze)
    {
        TimeOfDay tod = TimeOfDay.Instance;

        if (tod == null || tod.keys == null || tod.keys.Count == 0)
        {
            EvaluateTable(h, out body, out shade, out haze);
            return false;
        }

        int n = tod.keys.Count;
        TimeOfDay.TodKey a = tod.keys[0], b = tod.keys[0];
        float t = 0f;

        if (n > 1)
        {
            if (h >= tod.keys[n - 1].hour)
            {
                a = b = tod.keys[n - 1];
            }
            else if (h > tod.keys[0].hour)
            {
                for (int i = 0; i < n - 1; i++)
                {
                    if (h >= tod.keys[i].hour && h <= tod.keys[i + 1].hour)
                    {
                        a = tod.keys[i];
                        b = tod.keys[i + 1];
                        t = Smooth((h - a.hour) / Mathf.Max(0.0001f, b.hour - a.hour));
                        break;
                    }
                }
            }
        }

        Color hor = Color.Lerp(a.skyHorizon, b.skyHorizon, t);
        Color mid = Color.Lerp(a.skyMid, b.skyMid, t);
        Color c = Color.Lerp(mid, hor, skyBodyBlend);

        body = new Color(c.r * skyBodyBrightness, c.g * skyBodyBrightness, c.b * skyBodyBrightness, 1f);
        shade = new Color(body.r * skyShadeDarken, body.g * skyShadeDarken, body.b * skyShadeDarken, 1f);
        haze = new Color(hor.r, hor.g, hor.b, 1f);

        return true;
    }

    private static Color Tinted(Color from, Color haze, LayerCfg cfg, float alpha)
    {
        Color c = Color.Lerp(from, haze, cfg.haze);
        float f = cfg.brightness;

        return new Color(c.r * f * cfg.tint.r, c.g * f * cfg.tint.g, c.b * f * cfg.tint.b, alpha);
    }

    // =====================================================================
    // Her kare
    // =====================================================================

    private void Tick()
    {
        if (layers == null)
            return;

        if (dirty || built.Count != layers.Count)
            Build();

        Camera cam = Camera.main;

        if (cam == null)
            return;

        if (al == null && Time.realtimeSinceStartup >= nextFind)
        {
            nextFind = Time.realtimeSinceStartup + 1.5f;
            al = FindFirstObjectByType<AtmosphereLight>();
        }

        Vector3 cp = cam.transform.position;
        float viewH = cam.orthographic ? cam.orthographicSize * 2f : 24f;
        float viewW = viewH * cam.aspect;

        float horizonY = useAtmosphereHorizon && al != null ? al.horizonY : manualHorizonY;
        float restY = TimeOfDay.Instance != null ? TimeOfDay.Instance.camRestY : manualCamRestY;

        Color body, shade, haze;
        EvaluateTheme(CurrentHour(), out body, out shade, out haze);

        Vector3 ps = transform.lossyScale;
        float psx = Mathf.Abs(ps.x) < 0.0001f ? 1f : Mathf.Abs(ps.x);
        float psy = Mathf.Abs(ps.y) < 0.0001f ? 1f : Mathf.Abs(ps.y);
        float z = transform.position.z;

        for (int i = 0; i < layers.Count; i++)
        {
            LayerCfg cfg = layers[i];
            Built bt = built[i];

            Sprite bs = cfg.baseSprite != null ? cfg.baseSprite : baseSprite;
            Sprite ss = cfg.shadeSprite != null ? cfg.shadeSprite : shadeSprite;

            bool on = cfg.enabled && bs != null;

            if (bt.holder.activeSelf != on)
                bt.holder.SetActive(on);

            if (!on)
                continue;

            Bounds bb = bs.bounds;
            float tileW = Mathf.Max(1f, cfg.widthUnits);
            float s = tileW / Mathf.Max(0.0001f, bb.size.x);

            // Parallax: desen kameranın parallaxX kadarını takip eder.
            float shift = cp.x * cfg.parallaxX + cfg.xOffset;
            float u = cp.x - shift;   // kameranın desen içindeki konumu

            int first = Mathf.FloorToInt((u - viewW * 0.5f) / tileW);
            int last = Mathf.FloorToInt((u + viewW * 0.5f) / tileW);
            int count = Mathf.Clamp(last - first + 1, 1, MaxTiles);

            EnsureTiles(bt, bs, ss, count);

            float topWorld = horizonY + cfg.yOffset + (cp.y - restY) * cfg.parallaxY;
            float posY = topWorld - bb.max.y * s;

            Color bc = Tinted(body, haze, cfg, cfg.opacity);
            Color sc = Tinted(shade, haze, cfg, cfg.opacity * cfg.shadeStrength);

            for (int k = 0; k < bt.tiles.Count; k++)
            {
                Tile t = bt.tiles[k];
                bool show = k < count;

                if (t.root.activeSelf != show)
                    t.root.SetActive(show);

                if (!show)
                    continue;

                int idx = first + k;
                bool flip = cfg.flipX ^ (cfg.mirrorTiles && (idx & 1) != 0);

                float left = shift + idx * tileW;
                float localLeft = flip ? -bb.max.x : bb.min.x;

                t.root.transform.position = new Vector3(left - localLeft * s, posY, z);
                t.root.transform.localScale = new Vector3(s / psx, s / psy, 1f);

                if (t.b.sprite != bs) t.b.sprite = bs;
                if (t.b.flipX != flip) t.b.flipX = flip;
                if (t.b.color != bc) t.b.color = bc;
                if (t.b.sortingOrder != cfg.sortingOrder) t.b.sortingOrder = cfg.sortingOrder;

                if (t.s != null)
                {
                    bool shadeOn = ss != null && cfg.shadeStrength > 0.001f;

                    if (t.s.enabled != shadeOn)
                        t.s.enabled = shadeOn;

                    if (shadeOn)
                    {
                        if (t.s.sprite != ss) t.s.sprite = ss;
                        if (t.s.flipX != flip) t.s.flipX = flip;
                        if (t.s.color != sc) t.s.color = sc;
                        if (t.s.sortingOrder != cfg.sortingOrder + 1) t.s.sortingOrder = cfg.sortingOrder + 1;
                    }
                }
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Ufuk çizgisi (turuncu).
        float hy = useAtmosphereHorizon && al != null ? al.horizonY : manualHorizonY;
        Camera cam = Camera.main;
        float cx = cam != null ? cam.transform.position.x : 0f;

        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
        Gizmos.DrawLine(new Vector3(cx - 30f, hy, 0f), new Vector3(cx + 30f, hy, 0f));
    }
#endif
}
