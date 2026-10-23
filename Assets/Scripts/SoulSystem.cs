using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================
// RUH SİSTEMİ (v1: sadece TOPLAMA)
//   - Düşman ölünce vücudundan ruh(lar) çıkar, yükselir, kıvrılarak kılıca akar ve içine emilir.
//   - Emilme anında kılıç parlar (halka, kıvılcım, ses). Sayaç sağ üstte.
//   - Oyuncu ölünce ruhların küçük bir kısmı (%DeathLossFraction) kaybolur.
//   - Harcama (şehir / yetenek) sonra eklenecek: Souls.Add / Souls.LoseFraction / Souls.Count hazır.
// Tamamen kod ile kurulur (sahneye dokunmaz): oyun açılınca otomatik başlar.
// =====================================================================

/// <summary>Kalıcı ruh bakiyesi (PlayerPrefs).</summary>
public static class Souls
{
    const string Key = "pm_souls";
    static int count;
    static bool loaded;

    /// <summary>(yeni bakiye, değişim). Değişim negatifse kayıp.</summary>
    public static event System.Action<int, int> Changed;

    public static int Count { get { Load(); return count; } }

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        try { count = PlayerPrefs.GetInt(Key, 0); } catch { count = 0; }
    }

    static void Save()
    {
        try { PlayerPrefs.SetInt(Key, count); } catch { }
    }

    public static void Add(int n)
    {
        Load();
        if (n <= 0) return;
        count += n;
        Save();
        if (Changed != null) Changed(count, n);
    }

    /// <summary>Harcama (ileride şehirde). Yetmiyorsa false.</summary>
    public static bool TrySpend(int n)
    {
        Load();
        if (n <= 0 || count < n) return false;
        count -= n;
        Save();
        if (Changed != null) Changed(count, -n);
        return true;
    }

    /// <summary>Bakiyenin belirli oranını kaybettirir (en az 1). Kaybedilen miktarı döner.</summary>
    public static int LoseFraction(float fraction)
    {
        Load();
        if (count <= 0) return 0;
        int lost = Mathf.Min(count, Mathf.Max(1, Mathf.FloorToInt(count * fraction)));
        count -= lost;
        Save();
        if (Changed != null) Changed(count, -lost);
        return lost;
    }

    public static void FlushToDisk()
    {
        try { PlayerPrefs.Save(); } catch { }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        loaded = false;
        Changed = null;
    }
}

/// <summary>Ruh efektleri + kılıç parlaması + sayaç. Tek mesh, tek çizim; tek nesne (DontDestroyOnLoad).</summary>
public class SoulFx : MonoBehaviour
{
    // ---------------- AYARLAR ----------------
    /// <summary>Oyuncu ölünce kaybedilen oran (küçük ceza).</summary>
    public static float DeathLossFraction = 0.10f;

    // Kılıç noktaları (oyuncu konumuna göre, SAĞA bakarken; sola bakınca x aynalanır). Birim: dünya.
    static readonly Vector2 HiltLocal = new Vector2(0.29f, 1.45f);    // kabza (kuşak önü; ekran görüntüsünden ölçüldü)
    static readonly Vector2 TipLocal = new Vector2(-1.00f, 0.66f);    // kın ucu (arkada, aşağıda)
    const float AbsorbAlong = 0.12f;                                  // ruhun gireceği nokta: kabzadan kına %30

    const float TailLife = 0.5f;      // kuyruk en çok bu kadar sn yaşar
    const float TailMaxLen = 6.5f;    // ... ya da bu kadar uzunlukta kesilir
    const float HeadSize = 1.15f;

    static readonly Color CoreCol = new Color(0.80f, 1.00f, 1.00f);   // sıcak beyaz-camgöbeği
    static readonly Color MidCol = new Color(0.30f, 0.85f, 1.00f);    // camgöbeği
    static readonly Color DeepCol = new Color(0.36f, 0.42f, 1.00f);   // mavi-mor (kuyruk ucu)

    static SoulFx inst;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (inst != null || !Application.isPlaying) return;
        var go = new GameObject("SoulFx");
        DontDestroyOnLoad(go);
        inst = go.AddComponent<SoulFx>();
    }

    // ---------------- DURUM ----------------
    struct TrailPt { public Vector2 p; public float age; }

    class Wisp
    {
        public Vector2 pos, vel;
        public float t, delay, riseT, seed, swirl;
        public int value;
        public bool released, dying;
        public float dieT, glitter;
        public List<TrailPt> trail = new List<TrailPt>(40);
    }

    struct Spark { public Vector2 p, v; public float t, life, size; public bool warm; }
    struct Ring { public Vector2 p; public float t, life, s0, s1, power; }

    readonly List<Wisp> wisps = new List<Wisp>();
    readonly List<Spark> sparks = new List<Spark>();
    readonly List<Ring> rings = new List<Ring>();

    PlayerController pl;
    PlayerCombatController combat;
    Health plHealth;
    SpriteRenderer sortRef;

    float charge;        // yavaş sönen birikmiş parlaklık
    float pulse;         // emilme anındaki hızlı parlama
    float atkBlend;      // saldırı sırasında çubuk, kabzadan saldırı noktasına uzanır
    float chainT = -10f; int chain;

    Mesh mesh;
    MeshRenderer mr;
    Material mat;
    readonly List<Vector3> vs = new List<Vector3>(2048);
    readonly List<Color> cs = new List<Color>(2048);
    readonly List<Vector2> uv0 = new List<Vector2>(2048);
    readonly List<Vector2> uv1 = new List<Vector2>(2048);
    readonly List<int> tris = new List<int>(4096);

    AudioSource sfx;
    AudioClip bellClip, releaseClip, loseClip;

    // HUD
    Canvas hudCanvas;
    CanvasGroup hudGroup;
    RectTransform hudRoot;
    Text countText, deltaText;
    Image iconImg;
    float displayCount;
    float hudPunch;      // 1 -> 0 (sayaç büyüme)
    float lossFlash;     // 1 -> 0 (kırmızı)
    float deltaT = 99f;
    float lastDeathLoss = -99f;

    // ===================================================================
    void OnEnable()
    {
        CombatEvents.EnemyKilled += OnEnemyKilled;
        CombatEvents.PlayerDamaged += OnPlayerDamaged;
        Souls.Changed += OnSoulsChanged;
    }

    void OnDisable()
    {
        CombatEvents.EnemyKilled -= OnEnemyKilled;
        CombatEvents.PlayerDamaged -= OnPlayerDamaged;
        Souls.Changed -= OnSoulsChanged;
        Souls.FlushToDisk();
    }

    void OnApplicationQuit() { Souls.FlushToDisk(); }

    void Awake()
    {
        mesh = new Mesh { name = "SoulFxMesh" };
        mesh.MarkDynamic();
        var mf = gameObject.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        mr = gameObject.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        Shader sh = Resources.Load<Shader>("Shaders/SoulFx");
        if (sh == null) sh = Shader.Find("ProjectM/SoulFx");
        if (sh != null) { mat = new Material(sh) { name = "SoulFx" }; mr.sharedMaterial = mat; }
        else { Debug.LogWarning("[SoulFx] shader bulunamadi (Resources/Shaders/SoulFx)."); mr.enabled = false; }

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false; sfx.spatialBlend = 0f;
        bellClip = MakeBell();
        var absorbFile = Resources.Load<AudioClip>("SoulSfx/soul_absorb");   // gerçek ruh emilim sesi (yoksa kodla üretilen çan)
        if (absorbFile != null) bellClip = absorbFile;
        releaseClip = MakeRelease();
        loseClip = MakeLose();

        displayCount = Souls.Count;
        BuildHud();
    }

    // ===================================================================
    // OLAYLAR
    // ===================================================================
    static int SoulValue(EnemyBase e)
    {
        string n = e != null ? e.name.ToLowerInvariant() : "";
        if (n.Contains("boss")) return 20;
        if (n.Contains("heavy")) return 4;
        if (n.Contains("quick")) return 1;
        return 2;   // duelist, leaper, archer, diğerleri
    }

    void OnEnemyKilled(EnemyBase e)
    {
        if (e == null) return;
        int value = SoulValue(e);
        int n = Mathf.Clamp(value, 1, 6);
        float sy = Mathf.Max(0.5f, Mathf.Abs(e.transform.lossyScale.y));
        Vector2 c = e.transform.position + Vector3.up * (2.2f * Mathf.Clamp(sy, 0.8f, 2.5f));   // göğüs hizası (ayak noktası + ~2.2)
        var col = e.GetComponentInChildren<Collider2D>();
        if (col != null && col.enabled && col.bounds.size.y > 1.2f)
            c = new Vector2(col.bounds.center.x, col.bounds.min.y + col.bounds.size.y * 0.64f);

        int left = value;
        for (int i = 0; i < n; i++)
        {
            int v = Mathf.Max(1, Mathf.RoundToInt((float)left / (n - i)));
            left -= v;
            var w = new Wisp();
            w.pos = c + new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(-0.6f, 0.6f));
            float ang = Random.Range(55f, 125f) * Mathf.Deg2Rad;
            float sp = Random.Range(5.5f, 9.5f);
            w.vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * sp;
            w.delay = 0.22f + i * 0.085f + Random.Range(0f, 0.04f);   // ölüm efekti (kesik/dağılma) önce okunsun
            w.riseT = Random.Range(0.42f, 0.72f);
            w.seed = Random.Range(0f, 6.28f);
            w.swirl = Random.Range(0.55f, 1.0f) * (Random.value < 0.5f ? -1f : 1f);
            w.value = v;
            w.trail.Add(new TrailPt { p = w.pos, age = 0f });
            wisps.Add(w);
        }
    }

    void OnPlayerDamaged(PlayerDamageReport r)
    {
        if (!r.lethal || Time.time - lastDeathLoss < 3f) return;
        lastDeathLoss = Time.time;
        int lost = Souls.LoseFraction(DeathLossFraction);
        if (lost > 0) PlayOne(loseClip, 0.5f, 1f);
    }

    void OnSoulsChanged(int now, int delta)
    {
        if (delta > 0) { hudPunch = 1f; }
        else { lossFlash = 1f; hudPunch = 0.6f; deltaT = 0f; if (deltaText != null) deltaText.text = delta.ToString(); }
    }

    // ===================================================================
    // ANA DÖNGÜ
    // ===================================================================
    void LateUpdate()
    {
        float dt = Time.deltaTime;
        AcquirePlayer();

        if (Time.time >= nextScan) ScanSwordParts();
        PickLiveSword();
        Vector2 hilt, tip, atkEnd; bool haveSword = GetSword(out hilt, out tip, out atkEnd);
        Vector2 target = haveSword ? Vector2.Lerp(hilt, tip, AbsorbAlong) : Vector2.zero;

        // --- ruhlar ---
        for (int i = wisps.Count - 1; i >= 0; i--)
        {
            Wisp w = wisps[i];
            if (!UpdateWisp(w, dt, target, haveSword, hilt)) wisps.RemoveAt(i);
        }

        // --- kıvılcım / halka ---
        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            Spark s = sparks[i];
            s.t += dt;
            if (s.t >= s.life) { sparks.RemoveAt(i); continue; }
            s.v *= Mathf.Exp(-3.2f * dt);
            s.v.y += 0.8f * dt;
            s.p += s.v * dt;
            sparks[i] = s;
        }
        for (int i = rings.Count - 1; i >= 0; i--)
        {
            Ring r = rings[i];
            r.t += dt;
            if (r.t >= r.life) { rings.RemoveAt(i); continue; }
            rings[i] = r;
        }

        // --- kılıç parlaması durumu ---
        charge = Mathf.Max(0f, charge - dt * 0.9f);
        pulse = Mathf.Max(0f, pulse - dt * 4.2f);
        bool attacking = combat != null && combat.IsAttacking;
        atkBlend = Mathf.MoveTowards(atkBlend, attacking ? 1f : 0f, dt * 12f);

        BuildMesh(haveSword, hilt, tip, atkEnd);
        SetBladeGlow(bladeI);
        UpdateHud(dt);
    }

    // ===================================================================
    // KILIÇ PARLAMASI: rig parçasının (metal bıçak) kendi mesh'ine ikinci "soul" materyali
    // ===================================================================
    // Bıçak (metal) parçaları; kınlı durumda bıçak görünmediği için kılıç/kın parçası daha hafif parlar.
    static readonly string[] BladePartNames = { "idle_kilic", "atk_blade" };
    static readonly string[] SheathPartNames = { "idle_katana", "katana", "katana_sheathed" };
    const float BladeGlowK = 2.6f;     // HDR çarpanı (Bloom'u besler)
    const float SheathGlowMul = 0.85f; // kınlı kılıç parçası bıçaktan daha az parlar

    class SwordPart { public RonniSkin.RonniSoftPart part; public MeshRenderer r; public MeshFilter f; public Material m; public bool blade; }
    readonly List<SwordPart> swordParts = new List<SwordPart>();
    Shader bladeShader; float nextScan; SwordPart liveSword; float bladeI;

    void ScanSwordParts()
    {
        nextScan = Time.time + 0.5f;
        for (int i = swordParts.Count - 1; i >= 0; i--)
            if (swordParts[i].part == null) { if (swordParts[i].m != null) Destroy(swordParts[i].m); swordParts.RemoveAt(i); }
        if (bladeShader == null)
        {
            bladeShader = Resources.Load<Shader>("Shaders/SoulBlade");
            if (bladeShader == null) bladeShader = Shader.Find("ProjectM/SoulBlade");
            if (bladeShader == null) { nextScan = Time.time + 5f; return; }
        }
        var all = FindObjectsByType<RonniSkin.RonniSoftPart>(FindObjectsInactive.Include);
        for (int i = 0; i < all.Length; i++)
        {
            var p = all[i];
            if (p == null) continue;
            bool blade = System.Array.IndexOf(BladePartNames, p.name) >= 0;
            bool sheath = !blade && System.Array.IndexOf(SheathPartNames, p.name) >= 0;
            if (!blade && !sheath) continue;
            bool have = false;
            for (int k = 0; k < swordParts.Count; k++) if (swordParts[k].part == p) { have = true; break; }
            if (have) continue;
            var r = p.GetComponent<MeshRenderer>(); var f = p.GetComponent<MeshFilter>();
            if (r == null || f == null || r.sharedMaterial == null) continue;
            var orig = r.sharedMaterial;
            var m = new Material(bladeShader) { name = "SoulBlade_" + p.name, mainTexture = orig.mainTexture };
            m.SetFloat("_Glow", 0f);
            r.sharedMaterials = new Material[] { orig, m };   // 2. materyal aynı mesh'i toplamalı (additive) çizer
            swordParts.Add(new SwordPart { part = p, r = r, f = f, m = m, blade = blade });
        }
    }

    static bool PartVisible(SwordPart s) { return s.part != null && s.r != null && s.r.enabled && s.r.gameObject.activeInHierarchy; }

    void PickLiveSword()
    {
        liveSword = null;
        for (int i = 0; i < swordParts.Count; i++)
        {
            var s = swordParts[i];
            if (!PartVisible(s)) continue;
            if (s.blade) { liveSword = s; return; }
            if (liveSword == null) liveSword = s;
        }
    }

    /// <summary>Görünen kılıç parçasının kabza ve uç noktaları (dünya, parçanın anlık mesh'inden: animasyonu izler).</summary>
    bool RigSword(out Vector2 hilt, out Vector2 tip)
    {
        hilt = tip = Vector2.zero;
        if (liveSword == null || liveSword.f == null || pl == null) return false;
        var mesh = liveSword.f.sharedMesh;
        if (mesh == null) return false;
        var v = mesh.vertices; var uv = mesh.uv;
        if (v.Length < 3 || uv.Length != v.Length) return false;
        Matrix4x4 M = liveSword.r.transform.localToWorldMatrix;
        Vector3 A, B;
        if (liveSword.blade)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < uv.Length; i++) { lo = Mathf.Min(lo, uv[i].x); hi = Mathf.Max(hi, uv[i].x); }
            float mid = (lo + hi) * 0.5f; Vector3 sa = Vector3.zero, sb = Vector3.zero; int na = 0, nb = 0;
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 w = M.MultiplyPoint3x4(v[i]);
                if (uv[i].x <= mid) { sa += w; na++; } else { sb += w; nb++; }
            }
            if (na == 0 || nb == 0) return false;
            A = sa / na; B = sb / nb;
            // kabza = gövdeye yakın uç
            float s = Mathf.Max(0.01f, Mathf.Abs(pl.transform.lossyScale.y));
            Vector3 chest = pl.transform.position + Vector3.up * 2.0f * s;
            if ((B - chest).sqrMagnitude < (A - chest).sqrMagnitude) { Vector3 tmp = A; A = B; B = tmp; }
        }
        else
        {
            // kılıç/kın çizimi: kabza sol-üst köşe (u küçük, v büyük), uç sağ-alt köşe
            int ia = 0, ib = 0; float mn = float.MaxValue, mx = float.MinValue;
            for (int i = 0; i < uv.Length; i++) { float k = uv[i].x - uv[i].y; if (k < mn) { mn = k; ia = i; } if (k > mx) { mx = k; ib = i; } }
            A = M.MultiplyPoint3x4(v[ia]); B = M.MultiplyPoint3x4(v[ib]);
        }
        hilt = A; tip = B;
        return true;
    }

    void SetBladeGlow(float I)
    {
        Color glow = Color.Lerp(MidCol, CoreCol, Mathf.Clamp01(I * 0.8f));
        float g = I > 0.02f ? (1.0f + 0.9f * Mathf.Clamp(I, 0f, 1.3f)) * BladeGlowK : 0f;
        for (int i = 0; i < swordParts.Count; i++)
        {
            var s = swordParts[i];
            if (s.m == null) continue;
            s.m.SetFloat("_Glow", s.blade ? g : g * SheathGlowMul);
            s.m.SetColor("_Tint", glow);
            s.m.SetColor("_Core", CoreCol);
        }
    }

    void AcquirePlayer()
    {
        if (pl == null)
        {
            pl = FindFirstObjectByType<PlayerController>();
            combat = pl != null ? pl.GetComponent<PlayerCombatController>() : null;
            plHealth = pl != null ? pl.GetComponent<Health>() : null;
            sortRef = null;
        }
        if (pl != null && sortRef == null)
        {
            sortRef = pl.playerSprite != null ? pl.playerSprite : pl.GetComponentInChildren<SpriteRenderer>();
            if (sortRef != null && mr != null)
            {
                mr.sortingLayerID = sortRef.sortingLayerID;
                mr.sortingOrder = sortRef.sortingOrder + 300;
            }
        }
    }

    bool GetSword(out Vector2 hilt, out Vector2 tip, out Vector2 atkEnd)
    {
        hilt = tip = atkEnd = Vector2.zero;
        if (pl == null) return false;
        float f = pl.facingDir >= 0f ? 1f : -1f;
        float s = Mathf.Abs(pl.transform.lossyScale.y);
        if (s < 0.01f) s = 1f;
        Vector2 p = pl.transform.position;
        hilt = p + new Vector2(HiltLocal.x * f, HiltLocal.y) * s;
        tip = p + new Vector2(TipLocal.x * f, TipLocal.y) * s;
        Vector2 rh, rt;
        if (RigSword(out rh, out rt)) { hilt = rh; tip = rt; }   // görünen kılıç parçasından (animasyonu izler); yoksa yukarıdaki sabit noktalar
        if (pl.attackPoint != null)
        {
            Vector2 a = pl.attackPoint.position;
            Vector2 d = a - hilt;
            atkEnd = a + (d.sqrMagnitude > 0.01f ? d.normalized : new Vector2(f, 0f)) * 1.1f;
        }
        else atkEnd = hilt + new Vector2(f * 3.2f, 0f);
        return true;
    }

    // ===================================================================
    // RUH HAREKETİ
    // ===================================================================
    bool UpdateWisp(Wisp w, float dt, Vector2 target, bool haveSword, Vector2 hilt)
    {
        // Doğum gecikmesi (ölüm efekti önce görünsün).
        if (!w.released)
        {
            w.delay -= dt;
            if (w.delay > 0f) return true;
            w.released = true;
            Release(w);
        }

        // Kuyruk yaşlandır.
        for (int i = 0; i < w.trail.Count; i++) { var tp = w.trail[i]; tp.age += dt; w.trail[i] = tp; }

        if (w.dying)
        {
            // Kuyruk kılıca doğru emilir (iplik gibi çekilir).
            w.dieT += dt;
            float k = 1f - Mathf.Exp(-16f * dt);
            for (int i = 0; i < w.trail.Count; i++) { var tp = w.trail[i]; tp.p = Vector2.Lerp(tp.p, target, k); w.trail[i] = tp; }
            TrimTrail(w);
            return w.dieT < TailLife * 0.6f && w.trail.Count > 1;
        }

        w.t += dt;
        float dist = haveSword ? Vector2.Distance(w.pos, target) : 999f;

        if (w.t < w.riseT)
        {
            // YÜKSELİŞ: vücuttan süzülerek çıkar, yavaşlar, hafifçe salınır.
            w.vel *= Mathf.Exp(-1.7f * dt);
            Vector2 perp = new Vector2(-w.vel.y, w.vel.x);
            if (perp.sqrMagnitude > 1e-4f) perp.Normalize();
            w.vel += perp * (Mathf.Sin(w.t * 10f + w.seed) * 6f * dt);
            w.pos += w.vel * dt;
        }
        else if (haveSword)
        {
            // ÇEKİLİŞ: yavaş başlar, hızlanır; geniş kavisle yaklaşıp kılıca kilitlenir.
            float s = w.t - w.riseT;
            float ramp = Mathf.Clamp01(s / 0.75f);
            float speed = Mathf.Lerp(3.5f, 52f, ramp * ramp * ramp);
            float homing = Mathf.Lerp(2.6f, 34f, ramp);
            Vector2 dir = (target - w.pos);
            float d = dir.magnitude;
            dir = d > 1e-4f ? dir / d : Vector2.right;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            float swirl = (1f - ramp) * w.swirl * (0.65f + 0.35f * Mathf.Sin(s * 9f + w.seed));
            Vector2 desired = (dir + perp * swirl).normalized * speed;
            w.vel = Vector2.Lerp(w.vel, desired, 1f - Mathf.Exp(-homing * dt));
            w.pos += w.vel * dt;

            float reach = Mathf.Max(0.5f, w.vel.magnitude * dt * 1.4f);
            if (d < reach || s > 3.2f) { Absorb(w, target); return true; }
        }
        else
        {
            w.vel *= Mathf.Exp(-1.5f * dt);
            w.pos += w.vel * dt;
        }

        // İz bırak (kıvılcım serpintisi).
        w.glitter -= dt;
        if (w.glitter <= 0f && w.t > 0.05f)
        {
            w.glitter = 0.028f;
            sparks.Add(new Spark
            {
                p = w.pos + Random.insideUnitCircle * 0.12f,
                v = Random.insideUnitCircle * 1.4f - w.vel * 0.04f,
                life = Random.Range(0.28f, 0.5f),
                size = Random.Range(0.10f, 0.22f),
                warm = false
            });
        }

        w.trail.Insert(0, new TrailPt { p = w.pos, age = 0f });
        TrimTrail(w);
        return true;
    }

    static void TrimTrail(Wisp w)
    {
        float len = 0f;
        for (int i = 0; i < w.trail.Count; i++)
        {
            if (i > 0) len += Vector2.Distance(w.trail[i].p, w.trail[i - 1].p);
            if (w.trail[i].age > TailLife || len > TailMaxLen)
            {
                w.trail.RemoveRange(i, w.trail.Count - i);
                break;
            }
        }
    }

    void Release(Wisp w)
    {
        // Ruhun bedenden ayrılışı: yumuşak halka + yukarı süzülen kıvılcımlar.
        rings.Add(new Ring { p = w.pos, t = 0f, life = 0.45f, s0 = 0.4f, s1 = 2.4f, power = 0.55f });
        for (int i = 0; i < 6; i++)
            sparks.Add(new Spark
            {
                p = w.pos + Random.insideUnitCircle * 0.35f,
                v = new Vector2(Random.Range(-1.6f, 1.6f), Random.Range(0.8f, 3.4f)),
                life = Random.Range(0.4f, 0.8f),
                size = Random.Range(0.12f, 0.26f),
                warm = false
            });
        PlayOne(releaseClip, 0.32f, Random.Range(0.92f, 1.08f));
    }

    void Absorb(Wisp w, Vector2 target)
    {
        w.dying = true; w.dieT = 0f;
        w.pos = target;
        w.trail.Insert(0, new TrailPt { p = target, age = 0f });

        Souls.Add(w.value);

        charge = Mathf.Min(2.2f, charge + 0.42f);
        pulse = 1f;

        // Kılıçtaki giriş noktasında halka + kıvılcım patlaması.
        rings.Add(new Ring { p = target, t = 0f, life = 0.38f, s0 = 0.5f, s1 = 2.6f, power = 1f });
        for (int i = 0; i < 7; i++)
        {
            float a = Random.Range(0f, 6.283f);
            float sp = Random.Range(2.2f, 6.0f);
            sparks.Add(new Spark
            {
                p = target,
                v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sp,
                life = Random.Range(0.25f, 0.5f),
                size = Random.Range(0.12f, 0.26f),
                warm = true
            });
        }

        // Peş peşe girişlerde ses perdesi yükselir (zincir hissi).
        if (Time.time - chainT < 0.5f) chain = Mathf.Min(chain + 1, 7); else chain = 0;
        chainT = Time.time;
        PlayOne(bellClip, 0.55f, 1f + 0.055f * chain);
    }

    // ===================================================================
    // MESH
    // ===================================================================
    void BuildMesh(bool haveSword, Vector2 hilt, Vector2 tip, Vector2 atkEnd)
    {
        bladeI = 0f;
        if (mr == null || !mr.enabled || mat == null) return;
        vs.Clear(); cs.Clear(); uv0.Clear(); uv1.Clear(); tris.Clear();

        bool show = !MainMenu.Active;
        bool alive = plHealth == null || !plHealth.IsDead;

        if (show)
        {
            // --- kılıç parlaması ---
            if (haveSword && alive)
            {
                float idle = Souls.Count > 0 ? 0.05f : 0.0f;
                float flick = 0.92f + 0.08f * Mathf.Sin(Time.time * 7.3f) * Mathf.Sin(Time.time * 3.1f + 1.3f);
                float I = (idle + charge * 0.62f + pulse * 0.9f) * flick;
                // Parlama artık kılıç parçasının kendi materyaliyle çizilir (SetBladeGlow); burada sadece şiddet hesaplanır.
                if (I > 0.02f) bladeI = I;
            }

            // --- ruhlar ---
            for (int i = 0; i < wisps.Count; i++) DrawWisp(wisps[i], haveSword);

            // --- halkalar ---
            for (int i = 0; i < rings.Count; i++)
            {
                Ring r = rings[i];
                float k = r.t / r.life;
                float e = 1f - (1f - k) * (1f - k);
                float size = Mathf.Lerp(r.s0, r.s1, e);
                AddQuad(r.p, size, size, 3, Color.Lerp(CoreCol, MidCol, k), r.power * (1f - k) * (1f - k));
            }

            // --- kıvılcımlar ---
            for (int i = 0; i < sparks.Count; i++)
            {
                Spark s = sparks[i];
                float k = s.t / s.life;
                Color c = s.warm ? Color.Lerp(CoreCol, MidCol, k) : Color.Lerp(MidCol, DeepCol, k);
                float fade = (1f - k) * (1f - k);
                AddBlob(s.p, s.size * (0.6f + 0.6f * (1f - k)), c, 0.9f * fade);
            }
        }

        mesh.Clear();
        if (vs.Count == 0) return;
        mesh.SetVertices(vs);
        mesh.SetColors(cs);
        mesh.SetUVs(0, uv0);
        mesh.SetUVs(1, uv1);
        mesh.SetTriangles(tris, 0);
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(4000f, 4000f, 10f));
    }

    void DrawWisp(Wisp w, bool haveSword)
    {
        if (!w.released) return;

        int n = w.trail.Count;
        if (n >= 2)
        {
            // Kuyruk şeridi: baş geniş/parlak, uç ince/soluk; baştan uca beyaz -> camgöbeği -> mavi-mor.
            float len = 0f;
            int baseV = vs.Count;
            Vector2 prev = w.trail[0].p;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = w.trail[i].p;
                if (i > 0) len += Vector2.Distance(p, prev);
                prev = p;

                Vector2 dir;
                if (i == 0) dir = p - w.trail[1].p;
                else if (i == n - 1) dir = w.trail[i - 1].p - p;
                else dir = w.trail[i - 1].p - w.trail[i + 1].p;
                if (dir.sqrMagnitude < 1e-8f) dir = Vector2.right;
                dir.Normalize();
                Vector2 nrm = new Vector2(-dir.y, dir.x);

                float f = Mathf.Max(w.trail[i].age / TailLife, len / TailMaxLen);
                f = Mathf.Clamp01(f);
                float width = HeadSize * 0.52f * Mathf.Pow(1f - f, 1.25f);
                float alpha = Mathf.Pow(1f - f, 1.6f);
                Color col = f < 0.35f ? Color.Lerp(CoreCol, MidCol, f / 0.35f) : Color.Lerp(MidCol, DeepCol, (f - 0.35f) / 0.65f);

                Vector3 v0 = p + nrm * width, v1 = p - nrm * width;
                float u = (float)i / (n - 1);
                AddVert(v0, col, alpha, new Vector2(u, 0f), 0);
                AddVert(v1, col, alpha, new Vector2(u, 1f), 0);
            }
            for (int i = 0; i < n - 1; i++)
            {
                int a = baseV + i * 2;
                tris.Add(a); tris.Add(a + 1); tris.Add(a + 2);
                tris.Add(a + 1); tris.Add(a + 3); tris.Add(a + 2);
            }
        }

        // Baş: yaklaştıkça küçülür (sürekli kılıca "sızar"), emilince söner.
        float headMul = 1f;
        if (w.dying) headMul = Mathf.Clamp01(1f - w.dieT / 0.10f);
        else if (haveSword && pl != null)
        {
            Vector2 tgt = Vector2.zero;
            // yaklaşma ölçüsü için kuyruk başı ile son bilinen hedef yeterli; yakın mesafede küçült
            Vector2 hilt, tip, ae; GetSword(out hilt, out tip, out ae);
            tgt = Vector2.Lerp(hilt, tip, AbsorbAlong);
            float d = Vector2.Distance(w.pos, tgt);
            headMul = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(d / 3f));
        }
        if (headMul > 0.01f)
        {
            float pulseK = 0.92f + 0.08f * Mathf.Sin(Time.time * 18f + w.seed);
            AddBlob(w.pos, HeadSize * 2.6f * headMul * pulseK, MidCol, 0.38f);       // dış hale
            AddBlob(w.pos, HeadSize * 1.25f * headMul * pulseK, CoreCol, 0.95f);     // iç çekirdek
        }
    }

    // ---- mesh yardımcıları ----
    void AddVert(Vector3 p, Color c, float alpha, Vector2 uv, int mode)
    {
        vs.Add(p);
        cs.Add(new Color(c.r, c.g, c.b, Mathf.Clamp01(alpha)));
        uv0.Add(uv);
        uv1.Add(new Vector2(mode, 0f));
    }

    void AddQuad(Vector2 c, float w, float h, int mode, Color col, float alpha)
    {
        int b = vs.Count;
        float hw = w * 0.5f, hh = h * 0.5f;
        AddVert(new Vector3(c.x - hw, c.y - hh, 0), col, alpha, new Vector2(0, 0), mode);
        AddVert(new Vector3(c.x - hw, c.y + hh, 0), col, alpha, new Vector2(0, 1), mode);
        AddVert(new Vector3(c.x + hw, c.y + hh, 0), col, alpha, new Vector2(1, 1), mode);
        AddVert(new Vector3(c.x + hw, c.y - hh, 0), col, alpha, new Vector2(1, 0), mode);
        tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
        tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
    }

    void AddBlob(Vector2 c, float size, Color col, float alpha) { AddQuad(c, size, size, 1, col, alpha); }

    void AddBar(Vector2 a, Vector2 b, float width, Color col, float alpha)
    {
        Vector2 d = b - a;
        if (d.sqrMagnitude < 1e-6f) return;
        Vector2 n = new Vector2(-d.y, d.x).normalized * (width * 0.5f);
        int i0 = vs.Count;
        AddVert((Vector3)(a + n), col, alpha, new Vector2(0, 0), 2);
        AddVert((Vector3)(a - n), col, alpha, new Vector2(0, 1), 2);
        AddVert((Vector3)(b - n), col, alpha, new Vector2(1, 1), 2);
        AddVert((Vector3)(b + n), col, alpha, new Vector2(1, 0), 2);
        tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
        tris.Add(i0); tris.Add(i0 + 2); tris.Add(i0 + 3);
    }

    // ===================================================================
    // SES (kodla üretilir; ileride gerçek ses dosyasıyla değiştirilebilir)
    // ===================================================================
    void PlayOne(AudioClip c, float vol, float pitch)
    {
        if (sfx == null || c == null) return;
        sfx.pitch = pitch;
        sfx.PlayOneShot(c, vol);
    }

    const int SR = 44100;

    static AudioClip MakeBell()
    {
        // Çan/kristal "tıııng": uyumsuz üst tonlar + çok kısa parıltı. Kılıca giriş sesi.
        int n = (int)(SR * 0.7f);
        float[] d = new float[n];
        float f0 = 1318f;
        float[] ratio = { 1f, 2.76f, 5.4f, 8.9f };
        float[] amp = { 1f, 0.55f, 0.28f, 0.12f };
        float[] dec = { 7f, 10f, 15f, 24f };
        var rng = new System.Random(7);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float s = 0f;
            for (int k = 0; k < 4; k++) s += Mathf.Sin(2f * Mathf.PI * f0 * ratio[k] * t) * amp[k] * Mathf.Exp(-dec[k] * t);
            float atk = Mathf.Clamp01(t / 0.004f);
            float shimmer = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 60f) * 0.12f;
            d[i] = (s * 0.32f + shimmer) * atk;
        }
        var c = AudioClip.Create("soul_bell", n, 1, SR, false);
        c.SetData(d, 0);
        return c;
    }

    static AudioClip MakeRelease()
    {
        // Ruh çıkışı: nefes gibi yükselen yumuşak "huuu" (filtrelenmiş gürültü + hafif yükselen ton).
        int n = (int)(SR * 0.55f);
        float[] d = new float[n];
        var rng = new System.Random(11);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float k = t / 0.55f;
            float env = Mathf.Sin(Mathf.PI * Mathf.Pow(k, 0.7f));
            env *= env;
            float noise = ((float)rng.NextDouble() * 2f - 1f);
            float cut = Mathf.Lerp(0.04f, 0.2f, k);
            lp += (noise - lp) * cut;
            float tone = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(420f, 760f, k) * t) * 0.12f;
            d[i] = (lp * 1.6f + tone) * env * 0.5f;
        }
        var c = AudioClip.Create("soul_release", n, 1, SR, false);
        c.SetData(d, 0);
        return c;
    }

    static AudioClip MakeLose()
    {
        // Kayıp: alçalan, loş "vuuum".
        int n = (int)(SR * 0.6f);
        float[] d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float f = Mathf.Lerp(330f, 120f, t / 0.6f);
            float env = Mathf.Exp(-t * 5f) * Mathf.Clamp01(t / 0.01f);
            d[i] = Mathf.Sin(2f * Mathf.PI * f * t) * 0.35f * env;
        }
        var c = AudioClip.Create("soul_lose", n, 1, SR, false);
        c.SetData(d, 0);
        return c;
    }

    // ===================================================================
    // HUD (sağ üst sayaç)
    // ===================================================================
    void BuildHud()
    {
        var go = new GameObject("SoulHud", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        hudCanvas = go.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 19900;   // HudCanvas (20000) altında, menünün (30000+) altında
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        hudGroup = go.AddComponent<CanvasGroup>();
        hudGroup.interactable = false; hudGroup.blocksRaycasts = false; hudGroup.alpha = 0f;

        var rootGo = new GameObject("Root", typeof(RectTransform));
        rootGo.transform.SetParent(go.transform, false);
        hudRoot = rootGo.GetComponent<RectTransform>();
        hudRoot.anchorMin = hudRoot.anchorMax = new Vector2(1f, 1f);
        hudRoot.pivot = new Vector2(1f, 1f);
        hudRoot.anchoredPosition = new Vector2(-54f, -44f);
        hudRoot.sizeDelta = new Vector2(260f, 64f);

        // ikon (ruh damlası)
        var icGo = new GameObject("Icon", typeof(RectTransform));
        icGo.transform.SetParent(hudRoot, false);
        var icRt = icGo.GetComponent<RectTransform>();
        icRt.anchorMin = icRt.anchorMax = new Vector2(1f, 0.5f);
        icRt.pivot = new Vector2(1f, 0.5f);
        icRt.anchoredPosition = new Vector2(0f, 0f);
        icRt.sizeDelta = new Vector2(54f, 54f);
        iconImg = icGo.AddComponent<Image>();
        iconImg.sprite = MakeOrbSprite();
        iconImg.raycastTarget = false;

        // sayı
        var txGo = new GameObject("Count", typeof(RectTransform));
        txGo.transform.SetParent(hudRoot, false);
        var txRt = txGo.GetComponent<RectTransform>();
        txRt.anchorMin = txRt.anchorMax = new Vector2(1f, 0.5f);
        txRt.pivot = new Vector2(1f, 0.5f);
        txRt.anchoredPosition = new Vector2(-66f, 0f);
        txRt.sizeDelta = new Vector2(200f, 64f);
        countText = txGo.AddComponent<Text>();
        countText.font = GameFont.Or(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        countText.fontSize = 46;
        countText.alignment = TextAnchor.MiddleRight;
        countText.color = new Color(0.82f, 0.98f, 1f, 1f);
        countText.raycastTarget = false;
        countText.horizontalOverflow = HorizontalWrapMode.Overflow;
        var sh = txGo.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0.05f, 0.12f, 0.8f); sh.effectDistance = new Vector2(2f, -2f);

        // kayıp göstergesi (-n)
        var dGo = new GameObject("Delta", typeof(RectTransform));
        dGo.transform.SetParent(hudRoot, false);
        var dRt = dGo.GetComponent<RectTransform>();
        dRt.anchorMin = dRt.anchorMax = new Vector2(1f, 0.5f);
        dRt.pivot = new Vector2(1f, 1f);
        dRt.anchoredPosition = new Vector2(-66f, -26f);
        dRt.sizeDelta = new Vector2(200f, 40f);
        deltaText = dGo.AddComponent<Text>();
        deltaText.font = countText.font;
        deltaText.fontSize = 30;
        deltaText.alignment = TextAnchor.MiddleRight;
        deltaText.color = new Color(1f, 0.45f, 0.4f, 0f);
        deltaText.raycastTarget = false;
        deltaText.horizontalOverflow = HorizontalWrapMode.Overflow;
    }

    static Sprite MakeOrbSprite()
    {
        int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s * 2f - 1f, dy = (y + 0.5f) / s * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float halo = Mathf.Exp(-d * d * 3.4f) * 0.7f;
                float core = Mathf.Pow(Mathf.Clamp01(1f - d * 1.6f), 2f);
                float edge = 1f - Mathf.SmoothStep(0.8f, 1f, d);
                float a = Mathf.Clamp01((halo + core) * edge);
                float wcore = Mathf.Clamp01(core * 1.4f);
                px[y * s + x] = new Color(Mathf.Lerp(0.3f, 0.95f, wcore), Mathf.Lerp(0.8f, 1f, wcore), 1f, a);
            }
        tex.SetPixels32(px); tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 64f);
    }

    void UpdateHud(float dt)
    {
        if (hudGroup == null) return;
        float udt = Time.unscaledDeltaTime;

        bool visible = !MainMenu.Active && pl != null;
        hudGroup.alpha = Mathf.MoveTowards(hudGroup.alpha, visible ? 1f : 0f, udt * 3f);

        // sayı akıcı artar/azalır
        float target = Souls.Count;
        if (Mathf.Abs(displayCount - target) < 0.05f) displayCount = target;
        else displayCount = Mathf.MoveTowards(displayCount, target, Mathf.Max(1f, Mathf.Abs(target - displayCount) * 8f) * udt);
        countText.text = Mathf.RoundToInt(displayCount).ToString();

        hudPunch = Mathf.MoveTowards(hudPunch, 0f, udt * 3.2f);
        lossFlash = Mathf.MoveTowards(lossFlash, 0f, udt * 1.6f);
        float sc = 1f + 0.22f * hudPunch * hudPunch;
        hudRoot.localScale = new Vector3(sc, sc, 1f);

        Color baseCol = new Color(0.82f, 0.98f, 1f, 1f);
        countText.color = Color.Lerp(baseCol, new Color(1f, 0.42f, 0.38f, 1f), lossFlash);
        float glow = 0.75f + 0.25f * Mathf.Clamp01(hudPunch + charge * 0.5f);
        iconImg.color = new Color(glow, glow, 1f, 1f);

        deltaT += udt;
        float da = Mathf.Clamp01(1f - deltaT / 1.6f);
        deltaText.color = new Color(1f, 0.45f, 0.4f, da * Mathf.Clamp01(deltaT < 1.6f ? 1f : 0f));
        var drt = deltaText.rectTransform;
        drt.anchoredPosition = new Vector2(-66f, -26f - Mathf.Min(deltaT, 1.6f) * 14f);
    }
}
