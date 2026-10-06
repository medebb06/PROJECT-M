using UnityEngine;

/// <summary>
/// Sersemlemiş / zayıf düşmüş (infaz penceresi açık) düşmanın göstergesi:
/// sprite'ın ORTASINDA yanıp sönen kırmızı daire (Sekiro'daki gibi).
/// API aynı kaldı: EnemyController SetHighlighted'ı çağırır.
/// </summary>
public class FinisherTargetHighlight : MonoBehaviour
{
    [Header("Kırmızı daire")]
    [SerializeField] private Color markerColor = new Color(1f, 0.1f, 0.08f, 1f);

    [Tooltip("Dairenin boyu: sprite'ın küçük kenarı × bu.")]
    [SerializeField] private float sizeFactor = 0.55f;

    [SerializeField] private float minSize = 0.7f;
    [SerializeField] private float maxSize = 2.2f;

    [Tooltip("Saniyedeki yanıp sönme sayısı.")]
    [SerializeField] private float blinkRate = 4f;

    [SerializeField] private int sortingOrderOffset = 5;

    private SpriteRenderer sourceRenderer;
    private SpriteRenderer ring;
    private SpriteRenderer fill;
    private Transform marker;

    private bool highlighted;

    private static Sprite ringSprite;
    private static Sprite fillSprite;

    private void Awake()
    {
        sourceRenderer = GetComponent<SpriteRenderer>();

        if (sourceRenderer == null)
            sourceRenderer = GetComponentInChildren<SpriteRenderer>();

        if (sourceRenderer == null)
        {
            Debug.LogWarning("FINISHER HIGHLIGHT: SpriteRenderer bulunamadı.");
            return;
        }

        CreateMarker();
        SetHighlighted(false);
    }

    private static void BuildSprites()
    {
        if (ringSprite != null)
            return;

        const int N = 128;

        Texture2D ringTex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        Texture2D fillTex = new Texture2D(N, N, TextureFormat.RGBA32, false);

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f;
                float dy = (y + 0.5f) / N * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                float r = Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.12f);
                float f = d < 0.8f ? 0.55f * (1f - d / 0.8f) + 0.15f : 0f;

                ringTex.SetPixel(x, y, new Color(1f, 1f, 1f, r));
                fillTex.SetPixel(x, y, new Color(1f, 1f, 1f, f));
            }
        }

        ringTex.Apply();
        fillTex.Apply();

        ringSprite = Sprite.Create(ringTex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
        fillSprite = Sprite.Create(fillTex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
    }

    private void CreateMarker()
    {
        BuildSprites();

        GameObject go = new GameObject("FinisherMarker");
        go.hideFlags = HideFlags.HideInHierarchy;
        marker = go.transform;

        Shader shader = Shader.Find("Sprites/Default");
        Material mat = shader != null ? new Material(shader) : null;

        fill = NewPart("Fill", fillSprite, mat, 0);
        ring = NewPart("Ring", ringSprite, mat, 1);
    }

    private SpriteRenderer NewPart(string name, Sprite sprite, Material mat, int extra)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(marker, false);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;

        if (mat != null)
            sr.sharedMaterial = mat;

        sr.sortingLayerID = sourceRenderer.sortingLayerID;
        sr.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset + extra;

        return sr;
    }

    private void LateUpdate()
    {
        if (marker == null || sourceRenderer == null)
            return;

        if (!highlighted)
            return;

        Bounds b = sourceRenderer.bounds;

        float size = Mathf.Clamp(Mathf.Min(b.size.x, b.size.y) * sizeFactor, minSize, maxSize);

        // Yumuşak ama belirgin yanıp sönme (gerçek zaman: ağır çekimde de akar).
        float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * blinkRate * Mathf.PI * 2f);
        float alpha = Mathf.Lerp(0.15f, 1f, wave);
        float pulse = Mathf.Lerp(0.85f, 1.12f, wave);

        marker.position = new Vector3(b.center.x, b.center.y, transform.position.z);
        marker.localScale = new Vector3(size * pulse, size * pulse, 1f);

        Color c = markerColor;

        c.a = alpha;
        ring.color = c;

        c.a = alpha * 0.8f;
        fill.color = c;

        ring.sortingLayerID = sourceRenderer.sortingLayerID;
        ring.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset + 1;
        fill.sortingLayerID = sourceRenderer.sortingLayerID;
        fill.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset;
    }

    public void SetHighlighted(bool on)
    {
        highlighted = on;

        if (ring != null)
            ring.enabled = on;

        if (fill != null)
            fill.enabled = on;
    }

    private void OnDestroy()
    {
        if (marker != null)
            Destroy(marker.gameObject);
    }
}
