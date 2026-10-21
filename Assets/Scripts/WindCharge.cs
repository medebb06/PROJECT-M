using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RÜZGÂR BİRİKİMİ (yetenek: AbilityType.WindStore).
/// Düz vuruşlar ve parry düşmanın CAN BARININ ÜSTÜNDE açık mavi "/ / / /"
/// çizgileri biriktirir (yan yana, her yük bir çizgi). [Q] (kılıcı kınına
/// sok) hepsini patlatır; hasar, düşman başına biriken çizgi sayısıyla
/// orantılıdır (PlayerAbility hesaplar).
///
///   Düz vuruş +1 (kombo 4. vuruş +2), parry +3.
///   Oyuncu hasar alırsa TÜM çizgilerin yarısı dağılır (risk).
///   Düşman başına en fazla MaxStacks.
///
/// Çizgiler EnemyOverheadBars ile aynı şekilde OnGUI'de çizilir (ekran
/// uzayı), bu yüzden ölçek / kamera / sıralama sorunu olmaz.
/// Sadece yetenek donatılıyken var olur (PlayerAbility ekler / kaldırır).
/// </summary>
public class WindCharge : MonoBehaviour
{
    public const int MaxStacks = 8;

    public static WindCharge Instance { get; private set; }

    /// <summary>false: vuruş / parry yük biriktirmez (Atılım Kesiği sadece işaret koyar).</summary>
    public bool stacking = true;

    private readonly Dictionary<EnemyBase, int> stacks = new Dictionary<EnemyBase, int>();
    private readonly List<EnemyBase> scratch = new List<EnemyBase>();

    private static Sprite lineSprite;
    private static Sprite softSprite;

    private static readonly Color PipColor = new Color(0.6f, 0.88f, 1f, 1f);
    private static readonly Color GlowColor = new Color(0.95f, 1f, 1f, 1f);

    private EnemyOverheadBars bars;
    private PlayerController player;

    private float glow;
    private float nextGather;

    public int Total
    {
        get
        {
            int t = 0;

            foreach (KeyValuePair<EnemyBase, int> kv in stacks)
            {
                if (kv.Key != null && !kv.Key.IsDead)
                    t += kv.Value;
            }

            return t;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    private void Awake()
    {
        Instance = this;
        player = GetComponent<PlayerController>();
    }

    private void OnEnable()
    {
        CombatEvents.EnemyHit += OnEnemyHit;
        CombatEvents.ParrySucceeded += OnParry;
        CombatEvents.EnemyKilled += OnKilled;
        CombatEvents.PlayerDamaged += OnPlayerDamaged;
    }

    private void OnDisable()
    {
        CombatEvents.EnemyHit -= OnEnemyHit;
        CombatEvents.ParrySucceeded -= OnParry;
        CombatEvents.EnemyKilled -= OnKilled;
        CombatEvents.PlayerDamaged -= OnPlayerDamaged;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Kına sokma sırasında çizgileri parlat (0..1).</summary>
    public void SetGlow(float k)
    {
        glow = Mathf.Clamp01(k);
    }

    // ---------------------------------------------------------
    // BİRİKİM
    // ---------------------------------------------------------

    private void OnEnemyHit(EnemyBase enemy, DamageInfo info, HitResult result)
    {
        if (!stacking)
            return;

        if (enemy == null || !result.hit || result.killed)
            return;

        // Yeteneğin kendi hasarı / zehir yük biriktirmez.
        if (info.source != DamageSource.Attack && info.source != DamageSource.Slam)
            return;

        Add(enemy, info.source == DamageSource.Attack && info.comboStep >= 4 ? 2 : 1);
    }

    private void OnParry(EnemyBase enemy, bool brokeBalance)
    {
        if (!stacking)
            return;

        Add(enemy, 3);
    }

    private void OnKilled(EnemyBase enemy)
    {
        if (enemy != null)
            stacks.Remove(enemy);
    }

    private void OnPlayerDamaged(PlayerDamageReport report)
    {
        if (report.amount <= 0 || report.kind == PlayerHitKind.BlockCost)
            return;

        scratch.Clear();

        foreach (KeyValuePair<EnemyBase, int> kv in stacks)
            scratch.Add(kv.Key);

        for (int i = 0; i < scratch.Count; i++)
        {
            EnemyBase e = scratch[i];

            if (e == null)
                continue;

            int half = stacks[e] / 2;

            if (half <= 0)
                stacks.Remove(e);
            else
                stacks[e] = half;
        }

        scratch.Clear();
    }

    /// <summary>Özel saldırılar (Atılım Kesiği...) düşmana Kın yükü ekler.</summary>
    public void AddCharge(EnemyBase enemy, int amount)
    {
        Add(enemy, amount);
    }

    public void ClearStacks()
    {
        stacks.Clear();
        glow = 0f;
    }

    private void Add(EnemyBase enemy, int amount)
    {
        if (enemy == null || enemy.IsDead || amount <= 0)
            return;

        int current;
        stacks.TryGetValue(enemy, out current);

        stacks[enemy] = Mathf.Min(MaxStacks, current + amount);
    }

    /// <summary>
    /// Patlatma: tüm yükleri düşman + sayı olarak verir ve sıfırlar.
    /// </summary>
    public List<KeyValuePair<EnemyBase, int>> Consume()
    {
        List<KeyValuePair<EnemyBase, int>> result = new List<KeyValuePair<EnemyBase, int>>();

        foreach (KeyValuePair<EnemyBase, int> kv in stacks)
        {
            if (kv.Key != null && !kv.Key.IsDead)
                result.Add(kv);
        }

        stacks.Clear();
        glow = 0f;

        return result;
    }

    // ---------------------------------------------------------
    // ÇİZGİLER (can barının üstünde, OnGUI)
    // ---------------------------------------------------------

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || stacks.Count == 0)
            return;

        Camera cam = GameCamera.Get();

        if (cam == null)
            return;

        if (bars == null)
            bars = FindAnyObjectByType<EnemyOverheadBars>();

        float uiScale = bars != null ? bars.uiScale : 0.7f;
        float scale = GameSettings.GuiScale(uiScale);

        // Can barı + denge barı + boşluk (boss'ta çubuk yok).
        float barsHeight =
            bars != null
                ? bars.healthHeight + 2f + bars.balanceHeight + 4f
                : 13f;

        float worldOffset = bars != null ? bars.worldOffset : 0.12f;

        GUI.depth = 11;

        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;

        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        foreach (KeyValuePair<EnemyBase, int> kv in stacks)
        {
            EnemyBase enemy = kv.Key;

            if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled || kv.Value <= 0)
                continue;

            float bh = enemy.GetComponent<GiantWarriorEnemy>() != null ? 0f : barsHeight;

            DrawPips(cam, enemy, kv.Value, scale, worldOffset, bh);
        }

        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
    }

    private void DrawPips(Camera cam, EnemyBase enemy, int count, float scale, float worldOffset, float barsHeight)
    {
        SpriteRenderer sprite = MainRenderer(enemy);

        Vector3 top = enemy.transform.position;

        top.y = (sprite != null ? sprite.bounds.max.y : top.y + 1f) + worldOffset;

        Vector3 screen = cam.WorldToScreenPoint(top);

        if (screen.z < 0f)
            return;

        float cx = screen.x / scale;
        float cy = (Screen.height - screen.y) / scale;

        const float pipW = 3f;
        const float pipH = 13f;
        const float step = 8f;

        float rowW = (count - 1) * step;
        float x0 = cx - rowW * 0.5f;
        float yTop = cy - barsHeight - pipH;

        float t = Time.unscaledTime;

        for (int i = 0; i < count; i++)
        {
            float px = x0 + i * step;

            float pulse = 0.85f + 0.15f * Mathf.Sin(t * 5f + i * 0.9f);

            Color c = Color.Lerp(PipColor, GlowColor, glow);

            // Sınıra ulaştıysa hafif parlak.
            if (count >= MaxStacks)
                c = Color.Lerp(c, GlowColor, 0.5f);

            if (glow > 0f)
                pulse = 0.7f + 0.3f * Mathf.Sin(t * 35f + i);

            Matrix4x4 m = GUI.matrix;

            Vector2 pivot = new Vector2(px, yTop + pipH * 0.5f);

            // "/" eğimi: dikey çubuğu saat yönünde ~28 derece yatır.
            // (RotateAroundPivot ölçekli GUI matrisinde pivotu yanlış yere koyuyordu.)
            GUI.matrix =
                m *
                Matrix4x4.TRS(new Vector3(pivot.x, pivot.y, 0f), Quaternion.Euler(0f, 0f, 28f), Vector3.one) *
                Matrix4x4.TRS(new Vector3(-pivot.x, -pivot.y, 0f), Quaternion.identity, Vector3.one);

            // Koyu kontur (açık zeminde de okunsun).
            GUI.color = new Color(0.05f, 0.2f, 0.35f, 0.55f * pulse);
            GUI.DrawTexture(new Rect(px - pipW * 0.5f - 1f, yTop - 1f, pipW + 2f, pipH + 2f), Texture2D.whiteTexture);

            GUI.color = new Color(c.r, c.g, c.b, pulse);
            GUI.DrawTexture(new Rect(px - pipW * 0.5f, yTop, pipW, pipH), Texture2D.whiteTexture);

            GUI.matrix = m;
        }
    }

    private static SpriteRenderer MainRenderer(EnemyBase enemy)
    {
        SpriteRenderer[] renderers = enemy.GetComponentsInChildren<SpriteRenderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].enabled && renderers[i].sprite != null)
                return renderers[i];
        }

        return null;
    }

    // ---------------------------------------------------------
    // KINA SOKMA TOPLANMA EFEKTİ (rüzgâr çizgileri oyuncuya akar)
    // ---------------------------------------------------------

    // ---------------------------------------------------------
    // KINA SOKMA ANİMASYONU (geçici/prosedürel; gerçek kareler gelince değişir)
    // Gövde geriye eğilir (SADECE dönüş: ölçeğe dokunma, DashFx punch'ı ölçeği yakalayıp kalıcı bozuyordu), kılıç ucu geri çekilip kına girer,
    // sonunda "çın": hızlı ileri sıçrama + parlama.
    // ---------------------------------------------------------

    private bool poseActive;
    private Quaternion baseRot = Quaternion.identity;
    private SpriteRenderer blade;
    private float snapT;

    private const float SnapTime = 0.2f;
    private const float BladeLength = 3.4f;

    private void LateUpdate()
    {
        if (player == null || player.playerSprite == null)
            return;

        Transform pt = player.playerSprite.transform;

        float dir = player.facingDir >= 0f ? 1f : -1f;

        if (glow > 0f)
        {
            if (!poseActive)
            {
                poseActive = true;
                baseRot = pt.localRotation;
                CreateBlade();
            }

            float p = Mathf.SmoothStep(0f, 1f, glow);

            pt.localRotation = baseRot * Quaternion.Euler(0f, 0f, dir * 9f * p);

            UpdateBlade(dir, glow);
        }
        else if (poseActive)
        {
            poseActive = false;

            pt.localRotation = baseRot;

            DestroyBlade();
        }

        if (snapT > 0f && !poseActive)
        {
            snapT -= Time.unscaledDeltaTime;

            float k = Mathf.Clamp01(snapT / SnapTime);

            pt.localRotation = baseRot * Quaternion.Euler(0f, 0f, -dir * 9f * k);

            if (snapT <= 0f)
                pt.localRotation = baseRot;
        }
    }

    private void CreateBlade()
    {
        DestroyBlade();

        GameObject obj = new GameObject("WindBlade");

        blade = obj.AddComponent<SpriteRenderer>();
        blade.sprite = LineSprite();
        blade.color = GlowColor;

        ApplySorting(blade, 36);
    }

    private void UpdateBlade(float dir, float p)
    {
        if (blade == null)
            return;

        float len = Mathf.Max(0.15f, BladeLength * (1f - Mathf.SmoothStep(0f, 1f, p)));

        float tilt = 18f * Mathf.Deg2Rad;

        Vector3 vec = new Vector3(dir * Mathf.Cos(tilt), -Mathf.Sin(tilt), 0f);

        Vector3 anchor = transform.position + new Vector3(dir * 0.3f, 0.85f, 0f);

        blade.transform.position = anchor + vec * (len * 0.5f);
        blade.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(vec.y, vec.x) * Mathf.Rad2Deg);
        blade.transform.localScale = new Vector3(len, 1.4f, 1f);

        float pulse = 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 40f);

        blade.color = new Color(0.8f, 0.95f, 1f, pulse);
    }

    private void DestroyBlade()
    {
        if (blade != null)
            Destroy(blade.gameObject);

        blade = null;
    }

    /// <summary>Kına tamamen girdi: "çın" anı (hızlı sıçrama + parlama).</summary>
    public void PlaySnap()
    {
        snapT = SnapTime;

        float dir = player != null && player.facingDir < 0f ? -1f : 1f;

        Vector3 hip = transform.position + new Vector3(dir * 0.3f, 0.85f, 0f);

        SpawnFlash(hip, 2.2f);

        SpawnStreak(
            hip,
            hip + new Vector3(dir * 2.4f, -0.4f, 0f),
            dir > 0f ? -10f : 190f,
            new Vector3(0.5f, 1.2f, 1f),
            new Vector3(3.2f, 0.4f, 1f),
            Color.white,
            0.18f,
            1f
        );
    }

    private void Update()
    {
        if (glow <= 0f || player == null || Time.unscaledTime < nextGather)
            return;

        nextGather = Time.unscaledTime + 0.035f;

        Vector3 center = transform.position + Vector3.up * 0.9f;

        float ang = Random.value * Mathf.PI * 2f;
        float dist = Mathf.Lerp(3.5f, 6f, Random.value);

        Vector3 from = center + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang) * 0.7f, 0f) * dist;
        float rot = Mathf.Atan2(center.y - from.y, center.x - from.x) * Mathf.Rad2Deg;

        SpawnStreak(
            from,
            center,
            rot,
            new Vector3(1.8f, 0.5f, 1f),
            new Vector3(0.3f, 0.2f, 1f),
            PipColor,
            0.22f,
            0.8f
        );
    }

    // ---------------------------------------------------------
    // PATLAMA EFEKTİ
    // ---------------------------------------------------------

    /// <summary>Bir düşmanın üstünde çapraz rüzgâr kesikleri.</summary>
    public void PlayBurst(EnemyBase enemy, int stackCount)
    {
        if (enemy == null)
            return;

        SpriteRenderer body = MainRenderer(enemy);

        Vector3 center = body != null ? body.bounds.center : enemy.transform.position + Vector3.up;
        Vector3 size = body != null ? body.bounds.size : new Vector3(1f, 2f, 1f);

        int cuts = Mathf.Clamp(2 + stackCount, 3, 10);

        for (int i = 0; i < cuts; i++)
        {
            float rot = Mathf.Lerp(-55f, 55f, Random.value) + (i % 2 == 0 ? 25f : -25f);

            Vector3 at = center + new Vector3(
                (Random.value - 0.5f) * size.x * 0.6f,
                (Random.value - 0.5f) * size.y * 0.6f,
                0f
            );

            float len = size.x * Mathf.Lerp(1.4f, 2.4f, Random.value);

            SpawnStreak(
                at - Quaternion.Euler(0f, 0f, rot) * Vector3.right * len * 0.15f,
                at,
                rot,
                new Vector3(len * 0.2f, 1.1f, 1f),
                new Vector3(len, 0.35f, 1f),
                Color.Lerp(PipColor, Color.white, Random.value * 0.6f),
                0.38f,
                1f,
                i * 0.025f
            );
        }

        // Çarpma halkası + beyaz parlama.
        if (PlayerAbility.Instance != null)
            PlayerAbility.Instance.SpawnRingFx(center, Mathf.Max(size.x * 0.9f, 1.8f), new Color(0.7f, 0.92f, 1f, 0.9f));

        SpawnFlash(center, Mathf.Max(size.x, size.y) * 0.9f);
    }

    /// <summary>Atılım izi: başlangıçtan bitişe uzun rüzgâr çizgileri + varışta parlama.</summary>
    public void PlayDashTrail(Vector3 from, Vector3 to)
    {
        float dir = to.x >= from.x ? 1f : -1f;
        float len = Mathf.Abs(to.x - from.x);

        Vector3 mid = (from + to) * 0.5f + Vector3.up * 0.9f;

        for (int i = 0; i < 4; i++)
        {
            float y = (i - 1.5f) * 0.55f + (Random.value - 0.5f) * 0.2f;

            SpawnStreak(
                mid + new Vector3(0f, y, 0f),
                mid + new Vector3(dir * len * 0.1f, y, 0f),
                dir > 0f ? 0f : 180f,
                new Vector3(Mathf.Max(1f, len * 0.9f), 0.8f, 1f),
                new Vector3(Mathf.Max(1f, len * 1.0f), 0.25f, 1f),
                Color.Lerp(PipColor, Color.white, 0.35f),
                0.3f,
                0.9f,
                i * 0.015f
            );
        }

        SpawnFlash(to + Vector3.up * 0.9f, 2.6f);
    }

    /// <summary>Oyuncudan iki yana yayılan büyük rüzgâr dalgası.</summary>
    public void PlayWave()
    {
        Vector3 center = transform.position + Vector3.up * 0.9f;

        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 5; i++)
            {
                float y = (i - 2) * 0.9f + (Random.value - 0.5f) * 0.4f;
                float reach = Mathf.Lerp(9f, 15f, Random.value);

                Vector3 start = center + new Vector3(side * 0.8f, y, 0f);
                Vector3 end = center + new Vector3(side * reach, y * 1.4f, 0f);

                SpawnStreak(
                    start,
                    end,
                    side > 0 ? 6f * (i - 2) * 0.3f : 180f - 6f * (i - 2) * 0.3f,
                    new Vector3(1.2f, 0.9f, 1f),
                    new Vector3(Mathf.Lerp(5f, 9f, Random.value), 0.25f, 1f),
                    Color.Lerp(PipColor, Color.white, 0.4f),
                    0.45f,
                    1f,
                    i * 0.02f
                );
            }
        }

        if (PlayerAbility.Instance != null)
        {
            PlayerAbility.Instance.SpawnRingFx(center, 7f, new Color(0.75f, 0.93f, 1f, 0.85f));
            PlayerAbility.Instance.SpawnRingFx(center, 11f, new Color(0.55f, 0.85f, 1f, 0.5f));
        }

        SpawnFlash(center, 6f);
    }

    private void SpawnFlash(Vector3 at, float size)
    {
        GameObject obj = new GameObject("WindFlash");
        obj.transform.position = at;

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = SoftSprite();
        sr.color = new Color(0.9f, 0.98f, 1f, 0.9f);
        ApplySorting(sr, 40);

        StartCoroutine(Animate(
            obj.transform, sr, at, at, 0f,
            Vector3.one * size * 0.4f, Vector3.one * size * 1.6f,
            sr.color, 0.25f, 0f, true
        ));
    }

    private void SpawnStreak(
        Vector3 from, Vector3 to, float rotation,
        Vector3 fromScale, Vector3 toScale,
        Color color, float life, float alpha, float delay = 0f
    )
    {
        GameObject obj = new GameObject("WindStreak");
        obj.transform.position = from;
        obj.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
        obj.transform.localScale = fromScale;

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = LineSprite();
        sr.color = new Color(color.r, color.g, color.b, 0f);
        ApplySorting(sr, 35);

        color.a = alpha;

        StartCoroutine(Animate(obj.transform, sr, from, to, rotation, fromScale, toScale, color, life, delay, false));
    }

    private void ApplySorting(SpriteRenderer sr, int add)
    {
        if (player != null && player.playerSprite != null)
        {
            sr.sortingLayerID = player.playerSprite.sortingLayerID;
            sr.sortingOrder = player.playerSprite.sortingOrder + add;
        }
        else
        {
            sr.sortingOrder = 100;
        }
    }

    // Konum + ölçek + solma (gerçek zaman: hit stop'ta da akar).
    private static IEnumerator Animate(
        Transform t, SpriteRenderer sr, Vector3 from, Vector3 to, float rotation,
        Vector3 fromScale, Vector3 toScale, Color color, float life, float delay, bool flashFade
    )
    {
        float start = Time.unscaledTime + delay;

        while (t != null && Time.unscaledTime < start)
            yield return null;

        while (t != null && sr != null)
        {
            float k = Mathf.Clamp01((Time.unscaledTime - start) / life);

            float ease = 1f - (1f - k) * (1f - k);

            t.position = Vector3.Lerp(from, to, ease);
            t.localScale = Vector3.Lerp(fromScale, toScale, ease);

            // Hızlı belir, yavaş sön (flaş: hemen sön).
            float a = flashFade ? 1f - k : (k < 0.15f ? k / 0.15f : 1f - (k - 0.15f) / 0.85f);

            sr.color = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(a));

            if (k >= 1f)
                break;

            yield return null;
        }

        if (t != null)
            Object.Destroy(t.gameObject);
    }

    // 1 birim uzun yatay çizgi: ortası dolu, uçlara doğru söner.
    private static Sprite LineSprite()
    {
        if (lineSprite != null)
            return lineSprite;

        const int w = 64;
        const int h = 8;

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // Uçlar sivri: soldan sağa kalınlık kuyruğa doğru incelir.
                float u = x / (float)(w - 1);
                float taper = Mathf.Lerp(0.25f, 1f, Mathf.Min(1f, u * 1.5f)) * Mathf.Clamp01((1f - u) * 6f);
                float ey = 1f - Mathf.Abs((y + 0.5f) / h - 0.5f) * 2f;

                float a = Mathf.Clamp01(ey * 2.5f) * taper;

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply();

        lineSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);

        return lineSprite;
    }

    // Yumuşak yuvarlak parlama.
    private static Sprite SoftSprite()
    {
        if (softSprite != null)
            return softSprite;

        const int size = 64;

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        float r = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = Mathf.Clamp01(1f - d);

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        }

        tex.Apply();

        softSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);

        return softSprite;
    }
}
