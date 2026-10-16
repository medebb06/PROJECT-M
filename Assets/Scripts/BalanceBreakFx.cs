using UnityEngine;

/// <summary>
/// Posture (denge) kirilma efekti: genisleyen sok halkasi + isinsal kirik parcalari + ekran parlamasi +
/// "DENGE KIRILDI!" yazisi. Slow-motion sirasinda da akici kalsin diye unscaled zamanla calisir.
/// Sahneye/prefab'a ihtiyac duymaz; BalanceBreakFx.Play(pozisyon) yeter.
/// </summary>
public class BalanceBreakFx : MonoBehaviour
{
    static Sprite disc, ring, bar;

    const float Life = 0.9f;
    const int Shards = 16;

    float t;
    Transform ringT;
    SpriteRenderer ringSr, ring2Sr, coreSr;
    Transform ring2T, coreT;
    Transform[] shardT = new Transform[Shards];
    SpriteRenderer[] shardSr = new SpriteRenderer[Shards];
    float[] shardAngle = new float[Shards];
    float[] shardSpeed = new float[Shards];
    float[] shardLen = new float[Shards];
    float flash;

    static readonly Color Hot = new Color(1f, 0.55f, 0.1f, 1f);
    static readonly Color White = new Color(1f, 0.97f, 0.85f, 1f);

    public static void Play(Vector3 position, string label = "DENGE KIRILDI!")
    {
        if (!Application.isPlaying)
            return;

        GameObject go = new GameObject("BalanceBreakFx");
        go.transform.position = position;
        go.AddComponent<BalanceBreakFx>();

        if (!string.IsNullOrEmpty(label))
        {
            CombatCallout.Popup(
                position + new Vector3(0f, 2.2f, 0f),
                label,
                new Color(1f, 0.6f, 0.15f),
                2.2f
            );
        }
    }

    static void BuildSprites()
    {
        if (disc != null)
            return;

        const int N = 64;
        Texture2D td = new Texture2D(N, N, TextureFormat.RGBA32, false);
        Texture2D tr = new Texture2D(N, N, TextureFormat.RGBA32, false);
        td.wrapMode = tr.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f;
                float dy = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);

                float a = Mathf.Clamp01((1f - r) * 6f);
                td.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));

                float ra = Mathf.Clamp01(1f - Mathf.Abs(r - 0.86f) / 0.12f);
                tr.SetPixel(x, y, new Color(1f, 1f, 1f, ra * Mathf.Clamp01((1f - r) * 20f + 0.5f)));
            }
        }

        td.Apply();
        tr.Apply();

        Texture2D tb = Texture2D.whiteTexture;

        disc = Sprite.Create(td, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
        ring = Sprite.Create(tr, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
        bar = Sprite.Create(tb, new Rect(0, 0, tb.width, tb.height), new Vector2(0f, 0.5f), tb.width);
    }

    SpriteRenderer Make(string n, Sprite s, int order, out Transform tr)
    {
        GameObject g = new GameObject(n);
        g.transform.SetParent(transform, false);
        SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingOrder = order;
        sr.material = new Material(Shader.Find("Sprites/Default"));
        tr = g.transform;
        return sr;
    }

    void Start()
    {
        BuildSprites();

        coreSr = Make("Core", disc, 905, out coreT);
        ringSr = Make("Ring", ring, 904, out ringT);
        ring2Sr = Make("Ring2", ring, 903, out ring2T);

        for (int i = 0; i < Shards; i++)
        {
            shardSr[i] = Make("Shard" + i, bar, 902, out shardT[i]);
            shardAngle[i] = (i + Random.Range(-0.35f, 0.35f)) / Shards * 360f;
            shardSpeed[i] = Random.Range(16f, 34f);
            shardLen[i] = Random.Range(2.2f, 4.5f);
            shardT[i].rotation = Quaternion.Euler(0f, 0f, shardAngle[i]);
        }

        flash = 1f;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        t += dt;

        float k = Mathf.Clamp01(t / Life);
        float ease = 1f - (1f - k) * (1f - k) * (1f - k);

        if (coreSr != null)
        {
            float cs = Mathf.Lerp(3f, 11f, ease);
            coreT.localScale = new Vector3(cs, cs, 1f);
            Color c = Color.Lerp(White, Hot, k);
            c.a = Mathf.Clamp01(1f - k * 1.8f);
            coreSr.color = c;
        }

        if (ringSr != null)
        {
            float rs = Mathf.Lerp(2f, 34f, ease);
            ringT.localScale = new Vector3(rs, rs, 1f);
            Color c = Color.Lerp(White, Hot, k);
            c.a = Mathf.Clamp01(1f - k);
            ringSr.color = c;

            float r2 = Mathf.Lerp(0.5f, 22f, ease * ease);
            ring2T.localScale = new Vector3(r2, r2, 1f);
            Color c2 = Hot;
            c2.a = Mathf.Clamp01(0.8f - k);
            ring2Sr.color = c2;
        }

        for (int i = 0; i < Shards; i++)
        {
            if (shardSr[i] == null)
                continue;

            float dist = shardSpeed[i] * (1f - Mathf.Pow(1f - k, 3f)) * 0.9f;
            Vector3 dir = Quaternion.Euler(0f, 0f, shardAngle[i]) * Vector3.right;
            shardT[i].localPosition = dir * (2.5f + dist);
            shardT[i].localScale = new Vector3(shardLen[i] * (1f - k * 0.7f), 0.22f * (1f - k), 1f);

            Color c = Color.Lerp(White, Hot, k * 1.4f);
            c.a = Mathf.Clamp01(1f - k * 1.25f);
            shardSr[i].color = c;
        }

        flash = Mathf.Max(0f, flash - dt * 3.2f);

        if (t >= Life)
            Destroy(gameObject);
    }

    void OnGUI()
    {
        if (flash <= 0.01f || Event.current.type != EventType.Repaint)
            return;

        GUI.depth = -1000;
        GUI.color = new Color(1f, 0.85f, 0.55f, flash * 0.45f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
