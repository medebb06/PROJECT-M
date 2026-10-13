using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SİS KATMANLARI: dünya uzayında yavaşça yuvarlanan, birkaç derinlikte pus şeritleri.
///   Arka  = ufuktaki çalıların arkasında (ufuk silueti sisin içinden okunur)
///   Orta  = çalıların önünde, zeminin üstünde
///   Düşük = su çizgisinde, karakterlerin ARKASINDA, çalıların ve kıyının üstünde (su üstü sisi)
/// Rengi ve yoğunluğu TimeOfDay verir. Edit modunda sabit, Play'de akar.
/// Ayarlar: Layers listesi (yoğunluk, ölçek, hız, kaplama, parallax...).
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(10050)]
public class MistLayers : MonoBehaviour
{
    [System.Serializable]
    public class LayerCfg
    {
        public string name = "Mist";
        public int sortingOrder = -42;
        [Tooltip("Ufuktan (ya da suyun yüzeyinden) bu kadar yukarı.")]
        public float yOffset = 1f;
        public bool followWater = false;
        public float height = 6f;
        [Range(0f, 2f)] public float density = 0.5f;
        [Range(0.005f, 0.2f)] public float scale = 0.025f;
        public float speed = 0.15f;
        [Range(0f, 1f)] public float coverage = 0.55f;
        [Range(0.05f, 1f)] public float softness = 0.5f;
        [Range(0.3f, 4f)] public float falloff = 1.4f;
        [Range(0f, 1f)] public float bottom = 0f;
        [Range(0f, 1f)] public float parallax = 0.2f;
    }

    public Shader shader;
    public Texture2D noise;
    public Sprite quad;
    public float width = 90f;

    public List<LayerCfg> layers = new List<LayerCfg>();

    private class Built
    {
        public SpriteRenderer sr;
        public Material mat;
    }

    private readonly List<Built> built = new List<Built>();
    private Color color = new Color(0.6f, 0.65f, 0.75f);
    private float amount = 1f;
    private float horizonY;
    private bool dirty = true;
    private int builtCount = -1;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int DensityId = Shader.PropertyToID("_Density");

    public static List<LayerCfg> DefaultLayers()
    {
        return new List<LayerCfg>
        {
            new LayerCfg { name = "Mist Back", sortingOrder = -42, yOffset = 1.2f, height = 7.5f, density = 0.55f, scale = 0.022f, speed = 0.12f, coverage = 0.60f, falloff = 1.2f, parallax = 0.35f },
            new LayerCfg { name = "Mist Mid", sortingOrder = -8, yOffset = 0.3f, height = 4.5f, density = 0.45f, scale = 0.030f, speed = 0.20f, coverage = 0.55f, falloff = 1.5f, parallax = 0.12f, bottom = 0.25f },
            new LayerCfg { name = "Mist Water", sortingOrder = 14, yOffset = 0.2f, followWater = true, height = 3.2f, density = 0.50f, scale = 0.035f, speed = 0.25f, coverage = 0.50f, falloff = 1.8f, parallax = 0.05f },
        };
    }

    public void SetLook(Color c, float amt, float horizon)
    {
        color = c;
        amount = amt;
        horizonY = horizon;
    }

    private void OnEnable()
    {
        if (layers == null || layers.Count == 0)
            layers = DefaultLayers();

        dirty = true;
    }

    private void OnDisable()
    {
        Clear();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        dirty = true;
    }
#endif

    private void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);

            if (!c.name.StartsWith("~mist"))
                continue;

            if (Application.isPlaying) Destroy(c.gameObject);
            else DestroyImmediate(c.gameObject);
        }

        foreach (Built b in built)
            if (b.mat != null)
            {
                if (Application.isPlaying) Destroy(b.mat);
                else DestroyImmediate(b.mat);
            }

        built.Clear();
        builtCount = -1;
    }

    private void Build()
    {
        Clear();
        dirty = false;

        if (shader == null || quad == null)
            return;

        foreach (LayerCfg cfg in layers)
        {
            var go = new GameObject("~mist " + cfg.name);
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = quad;

            var mat = new Material(shader) { name = "Mist (" + cfg.name + ")", hideFlags = HideFlags.DontSave };
            if (noise != null) mat.SetTexture("_NoiseTex", noise);
            sr.sharedMaterial = mat;

            built.Add(new Built { sr = sr, mat = mat });
        }

        builtCount = layers.Count;
    }

    private void LateUpdate()
    {
        if (dirty || builtCount != layers.Count)
            Build();

        Camera cam = Camera.main;

        if (cam == null || built.Count != layers.Count)
            return;

        float waterY = WaterSurface.Instance != null ? WaterSurface.Instance.WaterY : horizonY;
        Vector2 qs = quad.bounds.size;

        for (int i = 0; i < built.Count; i++)
        {
            LayerCfg cfg = layers[i];
            Built b = built[i];

            if (b.sr == null)
                continue;

            float y = (cfg.followWater ? waterY : horizonY) + cfg.yOffset;

            b.sr.transform.position = new Vector3(cam.transform.position.x, y, 0f);
            b.sr.transform.localScale = new Vector3(width / qs.x, cfg.height / qs.y, 1f);
            b.sr.sortingOrder = cfg.sortingOrder;

            Material m = b.mat;
            m.SetColor(ColorId, new Color(color.r, color.g, color.b, 1f));
            m.SetFloat(DensityId, cfg.density * amount);
            m.SetFloat("_Scale", cfg.scale);
            m.SetFloat("_Speed", cfg.speed);
            m.SetFloat("_Coverage", cfg.coverage);
            m.SetFloat("_Softness", cfg.softness);
            m.SetFloat("_Falloff", cfg.falloff);
            m.SetFloat("_Bottom", cfg.bottom);
            m.SetFloat("_Parallax", cfg.parallax);
        }
    }
}
