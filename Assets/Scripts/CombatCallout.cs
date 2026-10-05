using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DÜNYA ÜZERİNDE KISA YAZILAR ("KUSURSUZ!", "VUR!", "KARŞI VURUŞ!").
/// Yazı vuruş noktasında belirir, yükselir ve söner. Gerçek zamanla akar:
/// hit-stop / slow-mo sırasında da okunur.
///
/// Kullanım:
///     CombatCallout.Popup(position, "KUSURSUZ!", renk, 1.2f);
///
/// KURULUM YOK: ilk çağrıda kendiliğinden oluşur.
/// EnemyDangerLabel da yazı üretmek için CreateText'i kullanır.
/// </summary>
public class CombatCallout : MonoBehaviour
{
    [Tooltip("Yazı boyutu (dünya birimi çarpanı).")]
    public float textSize = 0.045f;

    [Tooltip("Yazının ömrü boyunca yükselme mesafesi.")]
    public float rise = 0.7f;

    [Tooltip("Yazının ömrü (gerçek zaman, sn).")]
    public float life = 0.8f;

    public Color shadowColor = new Color(0.08f, 0.04f, 0f);

    private class Item
    {
        public GameObject obj;
        public TextMesh text;
        public TextMesh shadow;
        public Color color;
        public Vector3 start;
        public float scale;
        public float age;
        public float life;
    }

    private static CombatCallout instance;
    private static Font font;

    private readonly List<Item> active = new List<Item>();

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration
    )]
    private static void ResetStatics()
    {
        instance = null;
    }

    private static CombatCallout Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<CombatCallout>();

                if (instance == null)
                {
                    instance =
                        new GameObject("CombatCallout")
                            .AddComponent<CombatCallout>();
                }
            }

            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void OnDisable()
    {
        for (int i = 0; i < active.Count; i++)
        {
            if (active[i].obj != null)
                Destroy(active[i].obj);
        }

        active.Clear();
    }

    // =========================================================
    // API
    // =========================================================

    public static void Popup(
        Vector3 position,
        string label,
        Color color,
        float scale = 1f,
        int sortingLayer = 0,
        int sortingOrder = 600
    )
    {
        if (!Application.isPlaying || string.IsNullOrEmpty(label))
            return;

        Instance.Spawn(position, label, color, scale, sortingLayer, sortingOrder);
    }

    // Düşmanın sprite'ının tepesinde yazı çıkar.
    public static void PopupAbove(
        Component target,
        string label,
        Color color,
        float scale = 1f,
        float extraHeight = 0.35f
    )
    {
        if (target == null)
            return;

        SpriteRenderer sr = target.GetComponentInChildren<SpriteRenderer>();

        Vector3 pos = target.transform.position;

        if (sr != null)
            pos.y = sr.bounds.max.y;
        else
            pos.y += 1f;

        pos.y += extraHeight;

        Popup(
            pos,
            label,
            color,
            scale,
            sr != null ? sr.sortingLayerID : 0,
            (sr != null ? sr.sortingOrder : 0) + 600
        );
    }

    // =========================================================
    // ÜRETİM
    // =========================================================

    private void Spawn(
        Vector3 position,
        string label,
        Color color,
        float scale,
        int layer,
        int order
    )
    {
        Item item = new Item
        {
            color = color,
            start = position,
            scale = Mathf.Max(0.1f, scale),
            life = life
        };

        item.obj = new GameObject("Callout");
        item.obj.transform.position = position;

        item.shadow =
            CreateText(item.obj.transform, label, shadowColor, textSize, layer, order);

        item.shadow.transform.localPosition = new Vector3(0.04f, -0.04f, 0f);

        item.text =
            CreateText(item.obj.transform, label, color, textSize, layer, order + 1);

        active.Add(item);
    }

    private void LateUpdate()
    {
        if (active.Count == 0)
            return;

        float dt = Time.unscaledDeltaTime;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            Item item = active[i];

            if (item.obj == null)
            {
                active.RemoveAt(i);
                continue;
            }

            item.age += dt;

            float t = Mathf.Clamp01(item.age / Mathf.Max(0.01f, item.life));

            // Pop-in, yükselme, sonda solma.
            float pop =
                t < 0.12f
                    ? Mathf.Lerp(1.7f, 1f, t / 0.12f)
                    : 1f;

            float up = 1f - (1f - t) * (1f - t);

            item.obj.transform.position = item.start + Vector3.up * rise * up;
            item.obj.transform.localScale = Vector3.one * item.scale * pop;

            float alpha = 1f - Mathf.SmoothStep(0.65f, 1f, t);

            // İlk karelerde beyaza yakın parlasın.
            Color c = Color.Lerp(Color.white, item.color, Mathf.Clamp01(t * 6f));
            c.a = alpha;
            item.text.color = c;

            Color s = shadowColor;
            s.a = alpha;
            item.shadow.color = s;

            if (item.age >= item.life)
            {
                Destroy(item.obj);
                active.RemoveAt(i);
            }
        }
    }

    // =========================================================
    // ORTAK YARDIMCI (EnemyDangerLabel da kullanır)
    // =========================================================

    public static TextMesh CreateText(
        Transform parent,
        string label,
        Color color,
        float characterSize,
        int layer,
        int order
    )
    {
        GameObject obj = new GameObject("Text");

        obj.transform.SetParent(parent, false);

        TextMesh tm = obj.AddComponent<TextMesh>();

        Font f = GetFont();

        if (f != null)
            tm.font = f;

        tm.text = label;
        tm.fontSize = 64;
        tm.characterSize = characterSize * GameSettings.WorldTextScale;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
        tm.color = color;

        MeshRenderer mr = obj.GetComponent<MeshRenderer>();

        if (f != null)
            mr.sharedMaterial = f.material;

        mr.sortingLayerID = layer;
        mr.sortingOrder = order;

        return tm;
    }

    private static Font GetFont()
    {
        if (font != null)
            return font;

        // Unity 6 yerleşik fontu.
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return font;
    }
}
