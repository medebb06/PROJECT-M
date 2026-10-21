using UnityEngine;

// Okçunun kaçış sırasında bıraktığı yer tuzağı: delay sn sonra patlar, alan hasarı verir.
// Zıplayarak (alanın dışına çıkarak) veya dash/parry dokunulmazlığıyla kaçınılır; parry'lenemez.
public class EnemyTrap : MonoBehaviour
{
    private float delay = 2f, radius = 3.5f, hitPercent = 0.3f, t;
    private EnemyBase owner;
    private SpriteRenderer ring;
    private bool exploded;
    private static Sprite whiteSprite;

    public static EnemyTrap Spawn(Vector3 pos, float delay, float radius, float hitPercent, EnemyBase owner)
    {
        var go = new GameObject("EnemyTrap");
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        var tr = go.AddComponent<EnemyTrap>();
        tr.delay = Mathf.Max(0.1f, delay); tr.radius = radius; tr.hitPercent = hitPercent; tr.owner = owner;
        tr.Build();
        return tr;
    }

    private static Sprite White()
    {
        if (whiteSprite == null)
        {
            // Yumuşak kenarlı daire (küre görünümü).
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(N / 2f, N / 2f)) / (N / 2f);
                    float al = Mathf.Clamp01((1f - d) * 6f);
                    float shade = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(d));
                    tex.SetPixel(x, y, new Color(shade, shade, shade, al));
                }
            tex.Apply();
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
        }
        return whiteSprite;
    }

    private const float OrbSize = 0.9f;

    private void Build()
    {
        // Yerde küçük küre (yanıp söner); patlamada alan büyüyen daire olarak gösterilir.
        var g = new GameObject("Orb"); g.transform.SetParent(transform, false);
        ring = g.AddComponent<SpriteRenderer>(); ring.sprite = White(); ring.sortingOrder = 300;
        g.transform.localPosition = new Vector3(0f, OrbSize * 0.5f, 0f);
        g.transform.localScale = new Vector3(OrbSize, OrbSize, 1f);
    }

    private void Update()
    {
        if (exploded) return;
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / delay);
        float blink = 0.5f + 0.5f * Mathf.Sin(t * Mathf.Lerp(8f, 45f, k));
        ring.color = Color.Lerp(new Color(1f, 0.75f, 0.2f, 0.9f), new Color(1f, 0.15f, 0.05f, 1f), blink);
        float s = OrbSize * (1f + 0.25f * k + 0.1f * blink);
        ring.transform.localScale = new Vector3(s, s, 1f);
        if (t >= delay) Explode();
    }

    private void Explode()
    {
        exploded = true;
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null)
        {
            var pc = p.GetComponent<PlayerController>();
            var col = p.GetComponent<Collider2D>();
            var rec = p.GetComponent<PlayerDamageReceiver>();
            Vector2 c = new Vector2(transform.position.x, transform.position.y + 1f);
            bool inside = col != null && Vector2.Distance(col.ClosestPoint(c), c) <= radius;
            if (pc != null && pc.isInvincible) { if (inside && owner != null) CombatEvents.RaiseDodge(owner, true); }
            else if (inside && rec != null)
            {
                float dx = p.transform.position.x - transform.position.x;
                rec.hitPercentOverrideOnce = hitPercent;
                rec.TakeDamage(1, new Vector2(Mathf.Sign(dx == 0f ? 1f : dx), 0f), 10f, 5f, 0.3f, -1f, owner, PlayerHitKind.Unblockable, false);
                rec.hitPercentOverrideOnce = 0f;
            }
        }
        StartCoroutine(BoomFx());
    }

    private System.Collections.IEnumerator BoomFx()
    {
        float s = 0f;
        Vector3 basePos = ring.transform.localPosition;
        while (s < 0.3f)
        {
            s += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(s / 0.3f);
            float e = 1f - (1f - k) * (1f - k);
            ring.transform.localScale = new Vector3(Mathf.Lerp(OrbSize, radius * 2f, e), Mathf.Lerp(OrbSize, radius * 1.6f, e), 1f);
            ring.transform.localPosition = new Vector3(0f, Mathf.Lerp(OrbSize * 0.5f, radius * 0.8f, e), 0f);
            ring.color = new Color(1f, 0.55f, 0.15f, 0.9f * (1f - k));
            yield return null;
        }
        Destroy(gameObject);
    }
}
