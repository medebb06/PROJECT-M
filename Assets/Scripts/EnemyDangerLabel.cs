using UnityEngine;

/// <summary>
/// ENGELLENEMEZ VURUŞ UYARISI: uyarı boyunca düşmanın başının üstünde
/// NE YAPMAN GEREKTİĞİNİ yazar:
///   Yakalama (sarı)  → "DASH!"   (son anda düşmana doğru: KUSURSUZ)
///   Süpürme  (mavi)  → "ZIPLA!"  (üstünden atla, sonra VUR)
/// "Şimdi kaç" anında yazı büyür ve beyaz yanıp söner: kusursuz kaçış
/// penceresi o andır.
///
/// KURULUM YOK: EnemyAttackState gerektiğinde kendisi ekler.
/// Yazıları/renkleri değiştirmek istersen prefab'a elle ekle.
/// </summary>
public class EnemyDangerLabel : MonoBehaviour
{
    [Header("Yazılar")]
    public string grabText = "DASH!";
    public string sweepText = "ZIPLA!";

    public Color grabColor = new Color(1f, 0.85f, 0.1f);

    [Tooltip("Süpürme rengi hamle setinden (sweepColor) gelir; yoksa bu.")]
    public Color sweepColor = new Color(0.35f, 0.75f, 1f);

    [Header("Görünüm")]
    [Tooltip("Yazı boyutu (dünya birimi çarpanı).")]
    public float textSize = 0.05f;

    [Tooltip("Sprite'ın tepesinden yükseklik (kombo noktalarının üstünde kalsın).")]
    public float heightOffset = 0.75f;

    [Tooltip("'Şimdi' anında yazının büyüme çarpanı.")]
    public float urgentScale = 1.35f;

    public Color shadowColor = new Color(0.08f, 0.04f, 0f);

    private GameObject root;
    private TextMesh text;
    private TextMesh shadow;

    private SpriteRenderer mainRenderer;

    private Color color;
    private bool visible;
    private bool urgent;
    private float shownAt;
    private float urgentAt;

    // =========================================================
    // API
    // =========================================================

    public void Show(MoveHitType type, Color? sweepOverride = null)
    {
        if (type == MoveHitType.Normal)
        {
            Hide();
            return;
        }

        bool sweep = type == MoveHitType.Sweep;

        color =
            sweep
                ? (sweepOverride ?? sweepColor)
                : grabColor;

        EnsureObjects();

        string label = sweep ? sweepText : grabText;

        text.text = label;
        shadow.text = label;

        visible = true;
        urgent = false;
        shownAt = Time.time;

        root.SetActive(true);

        LateUpdate();
    }

    // "Şimdi kaç" anı: kusursuz kaçış penceresi.
    public void SetUrgent()
    {
        if (!visible || urgent)
            return;

        urgent = true;
        urgentAt = Time.time;
    }

    public void Hide()
    {
        visible = false;
        urgent = false;

        if (root != null)
            root.SetActive(false);
    }

    private void OnDisable()
    {
        Hide();
    }

    private void OnDestroy()
    {
        if (root != null)
            Destroy(root);
    }

    // =========================================================
    // GÜNCELLEME
    // =========================================================

    private void LateUpdate()
    {
        if (!visible || root == null)
            return;

        if (mainRenderer == null)
            mainRenderer = FindMainRenderer();

        Vector3 pos = transform.position;

        float topY =
            mainRenderer != null
                ? mainRenderer.bounds.max.y
                : pos.y + 1f;

        root.transform.position = new Vector3(pos.x, topY + heightOffset, pos.z);

        float time = Time.time;
        float t = time - shownAt;

        // Belirirken pop.
        float pop =
            t < 0.12f
                ? Mathf.Lerp(1.6f, 1f, t / 0.12f)
                : 1f;

        float scale;
        Color c;

        if (urgent)
        {
            float u = time - urgentAt;

            // Büyür ve hızlı beyaz yanıp söner.
            float grow =
                u < 0.08f
                    ? Mathf.Lerp(1f, urgentScale * 1.15f, u / 0.08f)
                    : urgentScale + 0.06f * Mathf.Sin(time * 30f);

            scale = pop * grow;

            bool flash = Mathf.Repeat(time * 14f, 1f) < 0.5f;

            c = flash ? Color.white : color;
        }
        else
        {
            // Sakin nabız.
            scale = pop * (1f + 0.05f * Mathf.Sin(time * 10f));
            c = color;
        }

        root.transform.localScale = Vector3.one * scale;

        text.color = c;
        shadow.color = shadowColor;
    }

    // =========================================================
    // ÜRETİM
    // =========================================================

    private void EnsureObjects()
    {
        if (mainRenderer == null)
            mainRenderer = FindMainRenderer();

        int layer = mainRenderer != null ? mainRenderer.sortingLayerID : 0;
        int order = (mainRenderer != null ? mainRenderer.sortingOrder : 0) + 550;

        if (root != null)
        {
            text.GetComponent<MeshRenderer>().sortingLayerID = layer;
            text.GetComponent<MeshRenderer>().sortingOrder = order + 1;
            shadow.GetComponent<MeshRenderer>().sortingLayerID = layer;
            shadow.GetComponent<MeshRenderer>().sortingOrder = order;
            return;
        }

        // Düşmana bağlanmaz (flip ölçeğinden etkilenmesin); her karede taşınır.
        root = new GameObject("DangerLabel");

        shadow =
            CombatCallout.CreateText(root.transform, "", shadowColor, textSize, layer, order);

        shadow.transform.localPosition = new Vector3(0.04f, -0.04f, 0f);

        text =
            CombatCallout.CreateText(root.transform, "", grabColor, textSize, layer, order + 1);
    }

    private SpriteRenderer FindMainRenderer()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];

            if (sr.enabled && sr.sprite != null)
                return sr;
        }

        return null;
    }
}
