using UnityEngine;

/// <summary>
/// Engellenemez vuruşun OKUNURLUĞUNU artıran görsel işaretler:
///  1) Düşmanın başında "!" simgesi (açılırken zıplar)
///  2) Zeminde vuruşun erişim alanı (bu bandın dışına çıkarsan isabet etmez)
///  3) "ŞİMDİ KAÇ" işareti: vuruştan kısa süre önce simge ve bant beyaza döner
///
/// Simge ve bant koddan üretilir (ek görsel dosyası gerekmez).
/// EnemyAttackState gerektiğinde kendiliğinden ekler.
/// </summary>
public class EnemyDangerIndicator : MonoBehaviour
{
    // Bitmap: '#' dolu piksel. Üstten alta.
    private static readonly string[] IconRows =
    {
        "..###..",
        ".#####.",
        ".#####.",
        ".#####.",
        ".#####.",
        "..###..",
        "..###..",
        "..###..",
        ".......",
        "..###..",
        ".#####.",
        "..###..",
    };

    private static Sprite iconSprite;
    private static Sprite zoneSprite;
    private static Material unlitMaterial;

    private const float PopDuration = 0.18f;
    private const float CueDuration = 0.45f;
    private const float ZoneHeight = 0.18f;

    private EnemyController enemy;
    private SpriteRenderer mainRenderer;
    private Collider2D bodyCollider;

    private SpriteRenderer iconRenderer;
    private SpriteRenderer zoneRenderer;

    private bool visible;
    private float shownAt;
    private float progress;
    private float cueUntil;
    private float reach;

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        bodyCollider = GetComponent<Collider2D>();
    }

    // =========================================================
    // API
    // =========================================================

    public void Show(float reachDistance)
    {
        if (enemy == null)
            return;

        EnsureObjects();

        mainRenderer = FindMainRenderer();

        reach = Mathf.Max(0.1f, reachDistance);

        visible = true;
        shownAt = Time.time;
        progress = 0f;
        cueUntil = 0f;

        int layer =
            mainRenderer != null
                ? mainRenderer.sortingLayerID
                : 0;

        int order =
            mainRenderer != null
                ? mainRenderer.sortingOrder
                : 0;

        // Simge her şeyin üstünde; bant düşmanla aynı sırada.
        iconRenderer.sortingLayerID = layer;
        iconRenderer.sortingOrder = order + 500;

        zoneRenderer.sortingLayerID = layer;
        zoneRenderer.sortingOrder = order;

        iconRenderer.enabled = enemy.showDangerIcon;
        zoneRenderer.enabled = enemy.showDangerZone;
    }

    public void SetProgress(float value)
    {
        progress = Mathf.Clamp01(value);
    }

    // Vuruşa kısa süre kala: "ŞİMDİ KAÇ".
    public void TriggerNowCue()
    {
        cueUntil = Time.time + CueDuration;
    }

    public void Hide()
    {
        visible = false;

        if (iconRenderer != null)
            iconRenderer.enabled = false;

        if (zoneRenderer != null)
            zoneRenderer.enabled = false;
    }

    // =========================================================
    // GÜNCELLEME
    // =========================================================

    private void LateUpdate()
    {
        if (!visible || enemy == null)
            return;

        float time = Time.time;
        bool cue = time < cueUntil;

        Color baseColor = enemy.dangerColor;
        Vector3 position = transform.position;

        // ---------------- SİMGE ----------------

        if (iconRenderer.enabled)
        {
            float topY =
                mainRenderer != null
                    ? mainRenderer.bounds.max.y
                    : position.y + 1f;

            float t = time - shownAt;

            float pop =
                t < PopDuration
                    ? EaseOutBack(t / PopDuration)
                    : 1f;

            float bob =
                Mathf.Sin(time * 12f) * 0.04f;

            float cueScale =
                cue
                    ? 1.35f + 0.15f * Mathf.Sin(time * 40f)
                    : 1f;

            iconRenderer.transform.position =
                new Vector3(
                    position.x,
                    topY + enemy.dangerIconHeightOffset + bob,
                    position.z
                );

            iconRenderer.transform.localScale =
                Vector3.one *
                enemy.dangerIconScale *
                pop *
                cueScale;

            iconRenderer.color =
                cue
                    ? Color.white
                    : baseColor;
        }

        // ---------------- ZEMİN BANDI ----------------

        if (zoneRenderer.enabled)
        {
            float feetY =
                bodyCollider != null
                    ? bodyCollider.bounds.min.y
                    : (mainRenderer != null
                        ? mainRenderer.bounds.min.y
                        : position.y);

            zoneRenderer.transform.position =
                new Vector3(
                    position.x,
                    feetY + ZoneHeight * 0.5f + 0.04f,
                    position.z
                );

            zoneRenderer.transform.localScale =
                new Vector3(
                    reach * 2f,
                    ZoneHeight,
                    1f
                );

            // Vuruşa yaklaştıkça parlaklaşır.
            float alpha =
                Mathf.Lerp(0.18f, 0.5f, progress) +
                0.08f * Mathf.Sin(time * 10f);

            if (cue)
                alpha += 0.2f;

            Color zoneColor =
                cue
                    ? Color.white
                    : baseColor;

            zoneColor.a =
                Mathf.Clamp01(alpha);

            zoneRenderer.color = zoneColor;
        }
    }

    private static float EaseOutBack(float x)
    {
        x = Mathf.Clamp01(x);

        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        float p = x - 1f;

        return 1f + c3 * p * p * p + c1 * p * p;
    }

    // =========================================================
    // NESNE ÜRETİMİ
    // =========================================================

    private SpriteRenderer FindMainRenderer()
    {
        SpriteRenderer[] renderers =
            GetComponentsInChildren<SpriteRenderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];

            // Kendi ürettiğimiz sprite'ları ve görünmeyenleri atla.
            if (
                sr == iconRenderer ||
                sr == zoneRenderer ||
                !sr.enabled ||
                sr.sprite == null
            )
            {
                continue;
            }

            return sr;
        }

        return null;
    }

    private void EnsureObjects()
    {
        if (iconRenderer != null && zoneRenderer != null)
            return;

        Sprite icon = GetIconSprite();
        Sprite zone = GetZoneSprite();
        Material material = GetUnlitMaterial();

        iconRenderer = CreateRenderer("DangerIcon", icon, material);
        zoneRenderer = CreateRenderer("DangerZone", zone, material);

        iconRenderer.enabled = false;
        zoneRenderer.enabled = false;
    }

    // Parent'sız oluşturulur (düşmanın scale'inden etkilenmesin);
    // OnDestroy'da elle yok edilir.
    private static SpriteRenderer CreateRenderer(
        string objectName,
        Sprite sprite,
        Material material
    )
    {
        GameObject obj = new GameObject(objectName);

        SpriteRenderer sr =
            obj.AddComponent<SpriteRenderer>();

        sr.sprite = sprite;

        if (material != null)
            sr.sharedMaterial = material;

        return sr;
    }

    private static Material GetUnlitMaterial()
    {
        if (unlitMaterial != null)
            return unlitMaterial;

        // 2D ışıktan etkilenmesin: uyarı her zaman parlak görünsün.
        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
            return null;

        unlitMaterial = new Material(shader);

        return unlitMaterial;
    }

    private static Sprite GetIconSprite()
    {
        if (iconSprite != null)
            return iconSprite;

        int rows = IconRows.Length;
        int cols = IconRows[0].Length;

        // 1 piksellik koyu çerçeve için kenarlara boşluk.
        int width = cols + 2;
        int height = rows + 2;

        bool[,] fill = new bool[width, height];

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                if (IconRows[row][col] != '#')
                    continue;

                // Metin üstten alta; texture alttan üste.
                fill[col + 1, height - 2 - row] = true;
            }
        }

        Texture2D texture =
            new Texture2D(width, height, TextureFormat.RGBA32, false);

        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color outline = new Color(0.1f, 0.05f, 0f, 1f);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (fill[x, y])
                {
                    // Beyaz: renk SpriteRenderer.color ile verilir.
                    texture.SetPixel(x, y, Color.white);
                    continue;
                }

                bool nearFill = false;

                for (int dx = -1; dx <= 1 && !nearFill; dx++)
                {
                    for (int dy = -1; dy <= 1 && !nearFill; dy++)
                    {
                        int nx = x + dx;
                        int ny = y + dy;

                        if (
                            nx >= 0 && nx < width &&
                            ny >= 0 && ny < height &&
                            fill[nx, ny]
                        )
                        {
                            nearFill = true;
                        }
                    }
                }

                texture.SetPixel(
                    x,
                    y,
                    nearFill ? outline : clear
                );
            }
        }

        texture.Apply();

        // Tint (sarı) çerçeveyi de boyamasın diye çerçeve zaten koyu;
        // dolgu beyaz olduğu için tint dolguya uygulanır.
        iconSprite =
            Sprite.Create(
                texture,
                new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f),
                16f
            );

        return iconSprite;
    }

    private static Sprite GetZoneSprite()
    {
        if (zoneSprite != null)
            return zoneSprite;

        Texture2D texture =
            new Texture2D(1, 1, TextureFormat.RGBA32, false);

        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        // 1x1 piksel, PPU 1 = 1 dünya birimi: scale = boyut.
        zoneSprite =
            Sprite.Create(
                texture,
                new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f),
                1f
            );

        return zoneSprite;
    }

    private void OnDisable()
    {
        Hide();
    }

    private void OnDestroy()
    {
        if (iconRenderer != null)
            Destroy(iconRenderer.gameObject);

        if (zoneRenderer != null)
            Destroy(zoneRenderer.gameObject);
    }
}