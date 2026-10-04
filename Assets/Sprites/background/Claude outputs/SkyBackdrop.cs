using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// GÖKYÜZÜ (Assets/Sprites/background). En arkada, kameraya sabit duran
/// katmanlı gökyüzü + bulutlar. Bulutlar çok hafif parallax ve yavaş
/// rüzgârla kayar. Perdeye göre gökyüzü değişir:
///   Perde 1 = Gündüz (c*), Perde 2 = Gün batımı (a*), Perde 3 = Alacakaranlık (b*),
///   Boss = Gece (1-4).
///
/// KURULUM: Managers'a 'Sky Backdrop' ekle. Eklenince katmanlar
/// Assets/Sprites/background'dan OTOMATİK dolar (yoksa sağ tık →
/// "Gökyüzlerini otomatik doldur"). Başka bir şey gerekmez.
///
/// Görseller 576x324 (PPU 16 → 36 x 20.25 birim); yatayda sonsuz tekrar eder.
/// Dikeyde kamerayla birlikte gider (zıplayınca oynamaz).
/// </summary>
[DefaultExecutionOrder(10000)]
public class SkyBackdrop : MonoBehaviour
{
    [System.Serializable]
    public class SkyLayer
    {
        public Texture2D texture;

        [Tooltip("1 = kameraya sabit (gökyüzü), 0.9 = biraz parallax (yakın bulut).")]
        [Range(0f, 1f)] public float follow = 1f;

        [Tooltip("Rüzgâr: saniyede kaç birim kayar (bulutlar için).")]
        public float drift;

        [Tooltip("Ek dikey kayma (birim, + yukarı).")]
        public float yOffset;

        public SkyLayer()
        {
        }

        public SkyLayer(Texture2D texture, float follow, float drift, float yOffset = 0f)
        {
            this.texture = texture;
            this.follow = follow;
            this.drift = drift;
            this.yOffset = yOffset;
        }
    }

    [System.Serializable]
    public class SkySet
    {
        public string name = "Gökyüzü";

        [Tooltip("ARKADAN ÖNE sırayla.")]
        public SkyLayer[] layers;
    }

    public SkySet[] skies;

    [Header("Hangi gökyüzü ne zaman (skies listesindeki sıra)")]
    [Tooltip("Perde 1, 2, 3 …")]
    public int[] skyForAct = { 0, 1, 2 };

    [Tooltip("Boss odasında (-1 = perdenin gökyüzü).")]
    public int bossSky = 3;

    public int lobbySky = 0;

    [Header("Yerleşim")]
    [Min(1f)] public float pixelsPerUnit = 16f;

    [Tooltip(
        "Görselin ALT kenarı, ekranın alt kenarına göre (birim). Negatif = biraz aşağıda " +
        "(ufuk çalıların arkasında kalsın).")]
    public float bottomOffset = -2f;

    [Tooltip("Çizim sırası (en arkada kalsın).")]
    public int baseSortingOrder = -500;

    [Tooltip("Kameranın arka plan rengini gökyüzünün en arka rengine çek (kenarlarda boşluk görünmesin).")]
    public bool tintCameraBackground = true;

    // =========================================================

    private class Built
    {
        public GameObject root;
        public readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        public readonly List<SkyLayer> layers = new List<SkyLayer>();
        public readonly List<float> widths = new List<float>();
        public Color backColor = Color.black;
    }

    private readonly List<Built> built = new List<Built>();
    private int activeSky = -1;
    private int sortingLayerId;

    private void Start()
    {
        // Zemin Tilemap'inin sıralama katmanını kullan (en arkada, ama aynı katmanda).
        UnityEngine.Tilemaps.TilemapRenderer[] maps =
            FindObjectsByType<UnityEngine.Tilemaps.TilemapRenderer>(FindObjectsSortMode.None);

        if (maps.Length > 0 && maps[0] != null)
            sortingLayerId = maps[0].sortingLayerID;

        Build();
    }

    private void OnEnable()
    {
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += OnBeginCamera;
    }

    private void OnDisable()
    {
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
    }

    private void Build()
    {
        built.Clear();

        if (skies == null)
            return;

        GameObject parent = new GameObject("Gökyüzü");

        for (int s = 0; s < skies.Length; s++)
        {
            Built b = new Built();

            built.Add(b);

            SkySet set = skies[s];

            b.root = new GameObject(set != null ? set.name : "Gökyüzü " + s);
            b.root.transform.SetParent(parent.transform, false);
            b.root.SetActive(false);

            if (set == null || set.layers == null)
                continue;

            for (int i = 0; i < set.layers.Length; i++)
            {
                SkyLayer layer = set.layers[i];

                if (layer == null || layer.texture == null)
                    continue;

                Texture2D tex = layer.texture;

                tex.filterMode = FilterMode.Point;

                Sprite sprite =
                    Sprite.Create(
                        tex,
                        new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0.5f, 0f),   // alt-orta
                        pixelsPerUnit,
                        0,
                        SpriteMeshType.FullRect
                    );

                GameObject go = new GameObject(tex.name);
                go.transform.SetParent(b.root.transform, false);

                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.tileMode = SpriteTileMode.Continuous;

                float w = tex.width / pixelsPerUnit;
                float h = tex.height / pixelsPerUnit;

                sr.size = new Vector2(w * 3f, h);
                sr.sortingLayerID = sortingLayerId;
                sr.sortingOrder = baseSortingOrder + i;

                b.renderers.Add(sr);
                b.layers.Add(layer);
                b.widths.Add(w);

                // En arka katmanın alt kenar rengi = kamera arka planı.
                if (b.renderers.Count == 1)
                    b.backColor = SampleBottomColor(tex);
            }
        }
    }

    private static Color SampleBottomColor(Texture2D tex)
    {
        try
        {
            if (tex.isReadable)
                return tex.GetPixel(tex.width / 2, 2);
        }
        catch
        {
            // Okunamıyorsa geç.
        }

        return Color.black;
    }

    private int WantedSky()
    {
        RunManager run = RunManager.Instance;

        if (run == null || run.State == RunState.Lobby)
            return lobbySky;

        if (bossSky >= 0 && run.CurrentRoom == RoomType.Boss)
            return bossSky;

        if (skyForAct == null || skyForAct.Length == 0)
            return 0;

        int act = Mathf.Clamp(run.Act, 1, skyForAct.Length);

        return skyForAct[act - 1];
    }

    private void OnBeginCamera(UnityEngine.Rendering.ScriptableRenderContext context, Camera cam)
    {
        if (cam != null && cam == Camera.main)
            Apply(cam);
    }

    private void LateUpdate()
    {
        Apply(Camera.main);
    }

    private void Apply(Camera cam)
    {
        if (cam == null || built.Count == 0)
            return;

        int want = Mathf.Clamp(WantedSky(), 0, built.Count - 1);

        if (want != activeSky)
        {
            for (int i = 0; i < built.Count; i++)
            {
                if (built[i].root != null)
                    built[i].root.SetActive(i == want);
            }

            activeSky = want;

            if (tintCameraBackground && built[want].backColor != Color.black)
                cam.backgroundColor = built[want].backColor;
        }

        Built b = built[want];

        Vector3 c = cam.transform.position;

        float halfH = cam.orthographic ? cam.orthographicSize : 6f;
        float bottom = c.y - halfH + bottomOffset;

        // Görüntü yüksekliği (ekranın tamamını kaplamak için gereken).
        float needed = halfH * 2f - bottomOffset + 0.5f;

        float t = Time.time;

        for (int i = 0; i < b.renderers.Count; i++)
        {
            SpriteRenderer sr = b.renderers[i];
            SkyLayer layer = b.layers[i];
            float w = b.widths[i];

            if (sr == null || w <= 0f)
                continue;

            // Görsel ekrandan kısaysa TAM SAYI katla büyüt (piksel bozulmasın).
            float h = sr.size.y;
            float scale = h > 0f ? Mathf.Max(1f, Mathf.Ceil(needed / h)) : 1f;

            if (!Mathf.Approximately(sr.transform.localScale.x, scale))
                sr.transform.localScale = new Vector3(scale, scale, 1f);

            w *= scale;

            // Desenin kameraya göre kayması (parallax + rüzgâr).
            float scroll = c.x * (1f - layer.follow) + t * layer.drift;

            float shift = Mathf.Repeat(scroll, w);

            // Piksele hizala (kameraya göre): titreme olmasın.
            shift = Mathf.Round(shift * pixelsPerUnit) / pixelsPerUnit;

            sr.transform.position =
                new Vector3(c.x - shift, bottom + layer.yOffset, 0f);
        }
    }

    // =========================================================
    // EDITOR: Assets/Sprites/background'dan otomatik doldur
    // =========================================================

#if UNITY_EDITOR
    private const string Folder = "Assets/Sprites/background/";

    private void Reset()
    {
        AutoFill();
    }

    [ContextMenu("Gökyüzlerini otomatik doldur")]
    private void AutoFill()
    {
        Texture2D T(string n)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + n + ".png");
        }

        skies = new[]
        {
            new SkySet
            {
                name = "Gündüz",
                layers = new[]
                {
                    new SkyLayer(T("c1"), 1f, 0f),
                    new SkyLayer(T("c2"), 0.99f, 0.05f),
                    new SkyLayer(T("c3"), 0.98f, 0.12f),
                    new SkyLayer(T("c4"), 0.96f, 0f)
                }
            },
            new SkySet
            {
                name = "Gün batımı",
                layers = new[]
                {
                    new SkyLayer(T("a"), 1f, 0f),
                    new SkyLayer(T("a5"), 0.995f, 0.3f),
                    new SkyLayer(T("a4"), 0.985f, 0.04f),
                    new SkyLayer(T("a3"), 0.975f, 0.08f),
                    new SkyLayer(T("a2"), 0.96f, 0.12f)
                }
            },
            new SkySet
            {
                name = "Alacakaranlık",
                layers = new[]
                {
                    new SkyLayer(T("b2"), 1f, 0f),
                    new SkyLayer(T("b5"), 0.99f, 0.15f),
                    new SkyLayer(T("b3"), 0.98f, 0.1f),
                    new SkyLayer(T("b4"), 0.965f, 0.06f)
                }
            },
            new SkySet
            {
                name = "Gece",
                layers = new[]
                {
                    new SkyLayer(T("1"), 1f, 0f),
                    new SkyLayer(T("2"), 1f, 0f),
                    new SkyLayer(T("4"), 0.985f, 0.05f),
                    new SkyLayer(T("3"), 0.965f, 0.08f)
                }
            }
        };

        EditorUtility.SetDirty(this);

        int missing = 0;

        foreach (SkySet set in skies)
        {
            foreach (SkyLayer l in set.layers)
            {
                if (l.texture == null)
                    missing++;
            }
        }

        if (missing > 0)
            Debug.LogWarning("SkyBackdrop: " + missing + " görsel " + Folder + " içinde bulunamadı.");
        else
            Debug.Log("SkyBackdrop: 4 gökyüzü dolduruldu.");
    }
#endif
}
