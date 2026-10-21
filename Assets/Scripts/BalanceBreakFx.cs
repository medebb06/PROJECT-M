using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Posture (denge) kirilma efekti: genisleyen sok halkasi + isinsal kirik parcalari + ekran parlamasi +
/// "DENGE KIRILDI!" yazisi. Slow-motion sirasinda da akici kalsin diye unscaled zamanla calisir.
/// Sahneye/prefab'a ihtiyac duymaz; BalanceBreakFx.Play(pozisyon) yeter.
///
/// PARRY: ayni efektin bembeyaz hali (yazi yok, ekran parlamasi hafif, olcek kucuk):
/// BalanceBreakFx.PlayParry(pozisyon). Efektler havuzlanir (Destroy/new GameObject yok).
/// </summary>
public class BalanceBreakFx : MonoBehaviour
{
    static Sprite disc, ring, bar, tri;
    static Material sharedMat;
    static readonly Stack<BalanceBreakFx> pool = new Stack<BalanceBreakFx>();

    const float Life = 0.6f;
    const float ParryLife = 0.22f;
    const int Shards = 16;

    // Parry ayarlari
    public const float ParryScale = 1.3f;
    public const float BreakScale = 0.5f;   // normal denge kirilmasi (eskiden 1)
    const float ParryFlashAlpha = 0f;

    float t;
    Transform ringT;
    SpriteRenderer ringSr, ring2Sr, coreSr;
    Transform ring2T, coreT;
    Transform[] shardT = new Transform[Shards];
    SpriteRenderer[] shardSr = new SpriteRenderer[Shards];
    float[] shardAngle = new float[Shards];
    float[] shardSpeed = new float[Shards];
    float[] shardLen = new float[Shards];
    float[] shardSpin = new float[Shards];
    float flash;
    bool built;

    bool whiteMode;
    float scaleMul = 1f;
    float flashAlpha = 0.45f;

    static readonly Color BreakGold = new Color(1f, 0.82f, 0.3f, 1f);
    static readonly Color BreakOrange = new Color(1f, 0.38f, 0.05f, 1f);
    static readonly Color HotC = new Color(1f, 0.55f, 0.1f, 1f);
    static readonly Color WhiteC = new Color(1f, 0.97f, 0.85f, 1f);

    Color Hot { get { return whiteMode ? Color.white : HotC; } }
    Color White { get { return whiteMode ? Color.white : WhiteC; } }

    static BalanceBreakFx Take(Vector3 position)
    {
        BalanceBreakFx fx = null;

        while (fx == null && pool.Count > 0)
            fx = pool.Pop();

        if (fx == null)
            fx = new GameObject("BalanceBreakFx").AddComponent<BalanceBreakFx>();

        fx.transform.position = position;
        fx.gameObject.SetActive(true);
        return fx;
    }

    void Release()
    {
        gameObject.SetActive(false);
        pool.Push(this);
    }

    public static void Play(Vector3 position, string label = "DENGE KIRILDI!")
    {
        if (!Application.isPlaying)
            return;

        BalanceBreakFx fx = Take(position);
        fx.whiteMode = false;
        fx.scaleMul = BreakScale;
        fx.flashAlpha = 0.2f;
        fx.Setup();

        if (!string.IsNullOrEmpty(label))
        {
            NoCallout.Popup(
                position + new Vector3(0f, 2.2f, 0f),
                label,
                new Color(1f, 0.6f, 0.15f),
                2.2f
            );
        }
    }

    /// <summary>Parry: ayni efekt, bembeyaz, yazisiz, kucultulmus.</summary>
    public static void PlayParry(Vector3 position, float scale = ParryScale)
    {
        if (!Application.isPlaying)
            return;

        BalanceBreakFx fx = Take(position);
        fx.whiteMode = true;
        fx.scaleMul = scale;
        fx.flashAlpha = ParryFlashAlpha;
        fx.Setup();
    }

    static void BuildSprites()
    {
        if (disc != null)
            return;

        const int N = 128;
        Texture2D td = new Texture2D(N, N, TextureFormat.RGBA32, false);
        Texture2D tr = new Texture2D(N, N, TextureFormat.RGBA32, false);
        td.wrapMode = tr.wrapMode = TextureWrapMode.Clamp;
        td.filterMode = tr.filterMode = FilterMode.Bilinear;

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
        Texture2D tt = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tt.wrapMode = TextureWrapMode.Clamp;
        tt.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N;
                float v = (y + 0.5f) / N;
                float half = 0.5f * (1f - u);
                float a = Mathf.Clamp01((half - Mathf.Abs(v - 0.5f)) * N * 0.5f);
                tt.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tt.Apply();
        tri = Sprite.Create(tt, new Rect(0, 0, N, N), new Vector2(0.4f, 0.5f), N);

        bar = Sprite.Create(tb, new Rect(0, 0, tb.width, tb.height), new Vector2(0f, 0.5f), tb.width);
    }

    SpriteRenderer Make(string n, Sprite s, int order, out Transform tr)
    {
        GameObject g = new GameObject(n);
        g.transform.SetParent(transform, false);
        SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingOrder = order;

        if (sharedMat == null)
            sharedMat = new Material(Shader.Find("Sprites/Default"));

        sr.sharedMaterial = sharedMat;
        tr = g.transform;
        return sr;
    }

    void Start()
    {
        if (!built)
            Setup();
    }

    // Ilk kullanimda parcalari uretir; her kullanimda yonleri/zamani sifirlar.
    void Setup()
    {
        if (!built)
        {
            BuildSprites();

            coreSr = Make("Core", disc, 905, out coreT);
            ringSr = Make("Ring", ring, 904, out ringT);
            ring2Sr = Make("Ring2", ring, 903, out ring2T);

            for (int i = 0; i < Shards; i++)
                shardSr[i] = Make("Shard" + i, bar, 902, out shardT[i]);

            built = true;
        }

        transform.localScale = Vector3.one * scaleMul;

        for (int i = 0; i < Shards; i++)
        {
            shardAngle[i] = (i + Random.Range(-0.35f, 0.35f)) / Shards * 360f;
            shardSpeed[i] = Random.Range(16f, 34f);
            shardLen[i] = Random.Range(2.2f, 4.5f);
            shardSpin[i] = Random.Range(360f, 900f) * (Random.value < 0.5f ? -1f : 1f);
            shardSr[i].sprite = whiteMode ? bar : tri;
            shardT[i].rotation = Quaternion.Euler(0f, 0f, shardAngle[i]);
        }

        for (int i = 0; i < Shards; i++)
            shardSr[i].enabled = !whiteMode;   // parry: sadece yuvarlak cekirdek + halkalar

        // Denge kirilmasi: yuvarlak yok (o parry'nin dili), sadece acili turuncu-altin kirik parcalar.
        coreSr.enabled = whiteMode;
        ringSr.enabled = whiteMode;
        ring2Sr.enabled = whiteMode;

        t = 0f;
        flash = 1f;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        t += dt;

        float life = whiteMode ? ParryLife : Life;
        float k = Mathf.Clamp01(t / life);
        float ease = 1f - (1f - k) * (1f - k) * (1f - k);

        if (coreSr != null)
        {
            float cs = whiteMode ? Mathf.Lerp(0.7f, 2.2f, ease) : Mathf.Lerp(3f, 11f, ease);
            coreT.localScale = new Vector3(cs, cs, 1f);
            Color c = Color.Lerp(White, Hot, k);
            c.a = Mathf.Clamp01(1f - k * 1.8f);
            coreSr.color = c;
        }

        if (ringSr != null)
        {
            float rs = whiteMode ? Mathf.Lerp(0.8f, 6.5f, ease) : Mathf.Lerp(2f, 34f, ease);
            ringT.localScale = new Vector3(rs, rs, 1f);
            Color c = Color.Lerp(White, Hot, k);
            c.a = Mathf.Clamp01(1f - k);
            ringSr.color = c;

            float r2 = whiteMode ? Mathf.Lerp(0.3f, 4.2f, ease * ease) : Mathf.Lerp(0.5f, 22f, ease * ease);
            ring2T.localScale = new Vector3(r2, r2, 1f);
            Color c2 = Hot;
            c2.a = Mathf.Clamp01(0.8f - k);
            ring2Sr.color = c2;
        }

        for (int i = 0; i < Shards; i++)
        {
            if (shardSr[i] == null)
                continue;

            if (!whiteMode)
            {
                float d2 = shardSpeed[i] * (1f - Mathf.Pow(1f - k, 3f)) * 0.4f;
                Vector3 dir2 = Quaternion.Euler(0f, 0f, shardAngle[i]) * Vector3.right;
                Vector3 pos2 = dir2 * (1.2f + d2);
                pos2.y -= 38f * t * t;
                shardT[i].localPosition = pos2;
                shardT[i].localRotation = Quaternion.Euler(0f, 0f, shardAngle[i] + shardSpin[i] * t);
                float sz = shardLen[i] * 0.55f * (1f - k * 0.5f);
                shardT[i].localScale = new Vector3(sz, sz * 0.8f, 1f);
                Color cb = Color.Lerp(BreakGold, BreakOrange, k * 1.2f);
                cb.a = Mathf.Clamp01((1f - k) * 2.2f);
                shardSr[i].color = cb;
                continue;
            }

            float dist = shardSpeed[i] * (1f - Mathf.Pow(1f - k, 3f)) * 0.9f;
            Vector3 dir = Quaternion.Euler(0f, 0f, shardAngle[i]) * Vector3.right;
            shardT[i].localPosition = dir * (2.5f + dist);
            shardT[i].localScale = new Vector3(shardLen[i] * (1f - k * 0.7f), 0.22f * (1f - k), 1f);

            Color c = Color.Lerp(White, Hot, k * 1.4f);
            c.a = Mathf.Clamp01(1f - k * 1.25f);
            shardSr[i].color = c;
        }

        flash = Mathf.Max(0f, flash - dt * 3.2f);

        if (t >= life)
            Release();
    }

    void OnGUI()
    {
        if (flash <= 0.01f || Event.current.type != EventType.Repaint)
            return;

        GUI.depth = -1000;
        GUI.color = whiteMode
            ? new Color(1f, 1f, 1f, flash * flashAlpha)
            : new Color(1f, 0.85f, 0.55f, flash * flashAlpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
