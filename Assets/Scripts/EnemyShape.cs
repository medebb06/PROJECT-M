using UnityEngine;

/// <summary>
/// Sprite'ı olmayan düşman tipleri için kodla çizilen basit gövde:
///   Uçan    → yatay KAPSÜL (gözlü)
///   Patlayan→ DAİRE (gözlü, üstte fitil)
/// Ana SpriteRenderer'ın sprite'ı değiştirilir ve Animator kapatılır
/// (animasyon sprite'ı geri yazmasın). Flaşlar / renk tonu aynen çalışır.
/// Kendi sprite'ını yapınca: EnemyArchetype'ta bu tip için ayrı prefab ata
/// (RunManager Flyer/Bomber alanları yoksa bu şekil kullanılır).
/// </summary>
public static class EnemyShape
{
    public enum Kind
    {
        Circle,
        Capsule
    }

    public static void Apply(EnemyController enemy, Kind kind, Color fill, bool fuse)
    {
        if (enemy == null)
            return;

        SpriteRenderer main = MainRenderer(enemy);

        if (main == null)
            return;

        Animator anim = main.GetComponent<Animator>();

        if (anim == null)
            anim = enemy.GetComponentInChildren<Animator>();

        if (anim != null)
            anim.enabled = false;

        Collider2D col = enemy.GetComponent<Collider2D>();

        Bounds b = col != null ? col.bounds : main.bounds;

        float worldW;
        float worldH;

        if (kind == Kind.Capsule)
        {
            worldW = Mathf.Max(b.size.x * 1.5f, b.size.y * 0.9f);
            worldH = worldW * 0.55f;
        }
        else
        {
            worldW = Mathf.Max(b.size.x * 1.1f, b.size.y * 0.75f);
            worldH = worldW;
        }

        const int texW = 64;
        int texH = Mathf.Max(8, Mathf.RoundToInt(texW * worldH / Mathf.Max(0.01f, worldW)));

        Texture2D tex = Draw(kind, texW, texH, fill, fuse);

        Vector3 ls = main.transform.lossyScale;

        float ppu = texW * Mathf.Abs(ls.x) / Mathf.Max(0.01f, worldW);

        // Pivot: gövde merkezi collider merkezine otursun (dikeyde).
        Vector3 local = main.transform.InverseTransformPoint(b.center);

        float localH = texH / ppu;

        Vector2 pivot = new Vector2(0.5f, Mathf.Clamp(0.5f - local.y / Mathf.Max(0.001f, localH), -2f, 3f));

        Sprite s = Sprite.Create(tex, new Rect(0, 0, texW, texH), pivot, ppu, 0, SpriteMeshType.FullRect);
        s.name = kind == Kind.Capsule ? "Uçan gövde" : "Patlayan gövde";

        main.sprite = s;
        main.drawMode = SpriteDrawMode.Simple;
    }

    // En büyük (gövde) SpriteRenderer.
    private static SpriteRenderer MainRenderer(EnemyController enemy)
    {
        SpriteRenderer[] all = enemy.GetComponentsInChildren<SpriteRenderer>(true);

        SpriteRenderer best = null;
        float bestArea = -1f;

        for (int i = 0; i < all.Length; i++)
        {
            SpriteRenderer r = all[i];

            if (r == null || r.sprite == null)
                continue;

            float area = r.bounds.size.x * r.bounds.size.y;

            if (area > bestArea)
            {
                bestArea = area;
                best = r;
            }
        }

        return best;
    }

    private static Texture2D Draw(Kind kind, int w, int h, Color fill, bool fuse)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color outline = new Color(fill.r * 0.25f, fill.g * 0.25f, fill.b * 0.25f, 1f);
        Color shade = new Color(fill.r * 0.75f, fill.g * 0.75f, fill.b * 0.75f, 1f);

        // Fitil payı (üstte).
        float topPad = fuse ? h * 0.18f : 0f;

        float cx = w * 0.5f;
        float cy = (h - topPad) * 0.5f;
        float rx = w * 0.5f - 1f;
        float ry = (h - topPad) * 0.5f - 1f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;

                float d;

                if (kind == Kind.Capsule)
                {
                    // Yatay kapsül: iki yarım daire + dikdörtgen.
                    float r = ry;
                    float left = cx - rx + r;
                    float right = cx + rx - r;
                    float qx = Mathf.Clamp(px, left, right);
                    d = Mathf.Sqrt((px - qx) * (px - qx) + (py - cy) * (py - cy)) / r;
                }
                else
                {
                    float dx = (px - cx) / rx;
                    float dy = (py - cy) / ry;
                    d = Mathf.Sqrt(dx * dx + dy * dy);
                }

                Color c = clear;

                if (d <= 1f)
                {
                    c = d > 0.86f ? outline : (py < cy - ry * 0.35f ? shade : fill);
                }

                tex.SetPixel(x, y, c);
            }
        }

        // Göz (ön tarafta: sağa bakan sprite; flipX ile döner).
        float ex = cx + rx * (kind == Kind.Capsule ? 0.45f : 0.3f);
        float ey = cy + ry * 0.2f;
        float er = Mathf.Max(2f, Mathf.Min(rx, ry) * 0.28f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = x + 0.5f - ex;
                float dy = y + 0.5f - ey;
                float dd = Mathf.Sqrt(dx * dx + dy * dy);

                if (dd <= er)
                    tex.SetPixel(x, y, dd <= er * 0.45f ? new Color(0.05f, 0.05f, 0.05f, 1f) : Color.white);
            }
        }

        // Fitil: üstte kısa koyu çizgi + sarı kıvılcım.
        if (fuse)
        {
            int fx = Mathf.RoundToInt(cx);
            int fy0 = Mathf.RoundToInt(cy + ry - 1f);

            for (int y = fy0; y < h - 2; y++)
            {
                tex.SetPixel(fx, y, outline);
                tex.SetPixel(fx + 1, y, outline);
            }

            for (int y = h - 3; y < h; y++)
            {
                for (int x = fx - 1; x <= fx + 2; x++)
                {
                    if (x >= 0 && x < w)
                        tex.SetPixel(x, y, new Color(1f, 0.85f, 0.2f, 1f));
                }
            }
        }

        tex.Apply();

        return tex;
    }
}
