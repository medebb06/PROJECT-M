using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KOMBO OKUNURLUĞU: çok vuruşlu hamle başlarken düşmanın başının üstünde
/// her vuruş için bir nokta gösterir. Nokta rengi vuruşun türünü söyler:
///   kırmızı = normal (parry), mavi = süpürme (zıpla), sarı = yakalama (kaç)
/// Sıradaki vuruşun noktası büyüyüp yanıp söner; atılan vuruşlar söner.
///
/// Böylece oyuncu "3 vuruş geliyor, sonuncusu süpürme" bilgisini İLK
/// vuruştan önce görür. Koddan üretilir, ek görsel gerekmez.
/// EnemyAttackState gerektiğinde kendiliğinden ekler.
/// </summary>
public class EnemyComboIndicator : MonoBehaviour
{
    public static readonly Color NormalColor = new Color(1f, 0.25f, 0.2f);
    public static readonly Color GrabColor = new Color(1f, 0.85f, 0.1f);
    public static readonly Color ShotColor = new Color(1f, 0.55f, 0.15f);

    private const float PopDuration = 0.15f;

    [Tooltip(
        "Noktaların düşman CAN/DENGE ÇUBUĞUNUN üstünden yüksekliği " +
        "(çubuk yoksa sprite'ın üstünden).")]
    public float heightOffset = 0.15f;

    [Tooltip("Noktalar arası mesafe (dünya birimi).")]
    public float spacing = 0.28f;

    public float dotScale = 1f;

    private static Sprite dotSprite;
    private static Material unlitMaterial;

    private readonly List<SpriteRenderer> dots = new List<SpriteRenderer>();
    private readonly List<Color> colors = new List<Color>();

    private EnemyController enemy;
    private SpriteRenderer mainRenderer;

    private int count;
    private int current;
    private bool visible;
    private float shownAt;

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
    }

    // =========================================================
    // API
    // =========================================================

    public void Show(IList<MoveHit> hits, Color sweepColor)
    {
        if (hits == null || hits.Count <= 1)
        {
            Hide();
            return;
        }

        mainRenderer = FindMainRenderer();

        count = hits.Count;
        current = 0;
        visible = true;
        shownAt = Time.time;

        colors.Clear();

        for (int i = 0; i < count; i++)
        {
            switch (hits[i].type)
            {
                case MoveHitType.Sweep:
                    colors.Add(sweepColor);
                    break;

                case MoveHitType.Grab:
                    colors.Add(GrabColor);
                    break;

                case MoveHitType.Shot:
                    colors.Add(ShotColor);
                    break;

                default:
                    colors.Add(NormalColor);
                    break;
            }
        }

        EnsureDots(count);

        int layer = mainRenderer != null ? mainRenderer.sortingLayerID : 0;
        int order = mainRenderer != null ? mainRenderer.sortingOrder : 0;

        for (int i = 0; i < dots.Count; i++)
        {
            SpriteRenderer sr = dots[i];

            sr.enabled = i < count;
            sr.sortingLayerID = layer;
            sr.sortingOrder = order + 501;
        }
    }

    // Sıradaki vuruş (0'dan). Öncekiler "atıldı" olarak söner.
    public void SetCurrent(int index)
    {
        current = Mathf.Clamp(index, 0, Mathf.Max(0, count - 1));
    }

    public void Hide()
    {
        visible = false;

        for (int i = 0; i < dots.Count; i++)
        {
            if (dots[i] != null)
                dots[i].enabled = false;
        }
    }

    // =========================================================
    // GÜNCELLEME
    // =========================================================

    private void LateUpdate()
    {
        if (!visible)
            return;

        float time = Time.time;

        Vector3 position = transform.position;

        float topY =
            mainRenderer != null
                ? mainRenderer.bounds.max.y
                : position.y + 1f;

        float t = time - shownAt;

        // Başın üstündeki can/denge çubuğunun ÜSTÜNE çık.
        float barsHeight = EnemyOverheadBars.ReservedWorldHeight;

        float pop =
            t < PopDuration
                ? Mathf.SmoothStep(0f, 1f, t / PopDuration)
                : 1f;

        float width = (count - 1) * spacing;
        float startX = position.x - width * 0.5f;

        // Düşmanın baktığı yönden başla: soldan sağa ya da sağdan sola okunur.
        float dir =
            enemy != null && enemy.FacingDirection < 0f
                ? -1f
                : 1f;

        for (int i = 0; i < count && i < dots.Count; i++)
        {
            SpriteRenderer sr = dots[i];

            float x =
                dir > 0f
                    ? startX + i * spacing
                    : startX + (count - 1 - i) * spacing;

            sr.transform.position =
                new Vector3(x, topY + barsHeight + heightOffset, position.z);

            Color c = colors[i];
            float scale = dotScale * pop;

            if (i < current)
            {
                // Atıldı: küçük ve soluk.
                c.a = 0.25f;
                scale *= 0.7f;
            }
            else if (i == current)
            {
                // Sıradaki: büyük ve nabız gibi.
                c.a = 1f;
                scale *= 1.35f + 0.15f * Mathf.Sin(time * 18f);
            }
            else
            {
                c.a = 0.85f;
            }

            sr.color = c;
            sr.transform.localScale = Vector3.one * scale;
        }
    }

    // =========================================================
    // NESNE ÜRETİMİ
    // =========================================================

    private void EnsureDots(int needed)
    {
        Sprite sprite = GetDotSprite();
        Material material = GetUnlitMaterial();

        while (dots.Count < needed)
        {
            GameObject obj = new GameObject("ComboDot");

            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();

            sr.sprite = sprite;

            if (material != null)
                sr.sharedMaterial = material;

            sr.enabled = false;

            dots.Add(sr);
        }
    }

    private SpriteRenderer FindMainRenderer()
    {
        SpriteRenderer[] renderers =
            GetComponentsInChildren<SpriteRenderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];

            if (sr.enabled && sr.sprite != null && !dots.Contains(sr))
                return sr;
        }

        return null;
    }

    private static Material GetUnlitMaterial()
    {
        if (unlitMaterial != null)
            return unlitMaterial;

        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
            return null;

        unlitMaterial = new Material(shader);

        return unlitMaterial;
    }

    // 7x7 piksel daire, koyu çerçeveli (her zeminde okunsun).
    private static Sprite GetDotSprite()
    {
        if (dotSprite != null)
            return dotSprite;

        string[] rows =
        {
            "..ooo..",
            ".o###o.",
            "o#####o",
            "o#####o",
            "o#####o",
            ".o###o.",
            "..ooo.."
        };

        int size = rows.Length;

        Texture2D texture =
            new Texture2D(size, size, TextureFormat.RGBA32, false);

        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color outline = new Color(0.08f, 0.04f, 0.02f, 1f);
        Color clear = new Color(0f, 0f, 0f, 0f);

        for (int row = 0; row < size; row++)
        {
            for (int col = 0; col < size; col++)
            {
                char ch = rows[row][col];

                // Metin üstten alta; texture alttan üste.
                int y = size - 1 - row;

                texture.SetPixel(
                    col,
                    y,
                    ch == '#' ? Color.white
                    : ch == 'o' ? outline
                    : clear
                );
            }
        }

        texture.Apply();

        dotSprite =
            Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                24f
            );

        return dotSprite;
    }

    private void OnDisable()
    {
        Hide();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < dots.Count; i++)
        {
            if (dots[i] != null)
                Destroy(dots[i].gameObject);
        }

        dots.Clear();
    }
}
