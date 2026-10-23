using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// INFAZ KAN EFEKTİ: düşman ikiye bölünürken kesik çizgisinden fışkıran kan.
/// Katmanlar: (1) ağır kan damlaları (gerilmiş, yerçekimli, kesikten iki yana fışkırır),
/// (2) ince kan çiseleyen sis bulutu, (3) kısa süreli parlak-koyu kırmızı flaş patlaması,
/// (4) damlaların düştüğü yerde kalıcı zemin lekeleri (yavaşça kururup solar).
/// Kod ile çizilir; prefab/materyal gerekmez. EnemyDeathFx.Play infaz ölümünde çağırır.
/// </summary>
public class SliceBloodFx : MonoBehaviour
{
    // ---- ayarlar (static: kod içinden tek yerden oynanır) ----
    public static float Power = 1f;           // genel güç çarpanı
    public static float Gravity = 95f;         // damla yerçekimi (EnemyDeathFx ile benzer)
    public static float EmitTime = 0.06f;      // fışkırma süresi (sn)
    public static int DropCount = 55;
    public static int MistCount = 8;
    public static int SplatCount = 26;
    public static float SplatLife = 6f;

    /// <summary>Gercek kan rengi: koyu kirmizi (r baskin, g/b cok az).</summary>
    public static Color BloodColor(float shade, float a)
    {
        return new Color(shade, shade * Random.Range(0.02f, 0.06f), shade * Random.Range(0.03f, 0.08f), a);
    }

    private float k = 1f;
    private int dropN, mistN, splatCap;

    private static Texture2D hardTex, softTex;
    private static Material hardMat, softMat;
    private static Sprite splatSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        hardTex = softTex = null;
        hardMat = softMat = null;
        splatSprite = null;
    }

    private struct Splat
    {
        public float time;
        public Vector2 pos;
        public float size;
    }

    private ParticleSystem drops, mist, flash;
    private Vector2 c, u, n;
    private float len, groundY, side, age, emitted, z, halfW;
    private int splatsMade;
    private int layerId, order;
    private readonly List<Splat> pending = new List<Splat>();
    private readonly List<SpriteRenderer> splats = new List<SpriteRenderer>();
    private readonly List<float> splatBorn = new List<float>();
    private readonly List<float> splatTarget = new List<float>();

    public static void Burst(Vector2 center, Vector2 cutDir, Vector2 cutNormal, Bounds bounds, float groundY, int sortingLayerId, int sortingOrder, float z, float side, float scale = 1f)
    {
        GameObject go = new GameObject("SliceBlood");
        SliceBloodFx fx = go.AddComponent<SliceBloodFx>();
        fx.k = scale;
        fx.Init(center, cutDir, cutNormal, bounds, groundY, sortingLayerId, sortingOrder, z, side);
    }

    private static Texture2D MakeCircle(int size, float hardness)
    {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        float r = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = Mathf.Clamp01((1f - d) / Mathf.Max(0.02f, 1f - hardness));
                a = a * a * (3f - 2f * a);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        t.Apply();
        return t;
    }

    private static Shader ParticleShader()
    {
        Shader sh = Shader.Find("Sprites/Default");

        if (sh == null)
            sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");

        return sh;
    }

    private static void EnsureAssets()
    {
        if (hardTex == null) hardTex = MakeCircle(64, 0.82f);
        if (softTex == null) softTex = MakeCircle(128, 0.0f);

        if (hardMat == null)
        {
            hardMat = new Material(ParticleShader());
            hardMat.mainTexture = hardTex;
        }

        if (softMat == null)
        {
            softMat = new Material(ParticleShader());
            softMat.mainTexture = softTex;
        }

        if (splatSprite == null)
            splatSprite = Sprite.Create(hardTex, new Rect(0, 0, hardTex.width, hardTex.height), new Vector2(0.5f, 0.5f), 64f);
    }

    private ParticleSystem MakeSystem(string name, Material mat, int maxParticles, bool stretch)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.maxParticles = maxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 1f;
        main.startSpeed = 0f;
        main.startSize = 1f;
        main.gravityModifier = 0f;

        var em = ps.emission;
        em.enabled = false;

        var sh = ps.shape;
        sh.enabled = false;

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.sortingLayerID = layerId;
        r.sortingOrder = order;
        r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;

        if (stretch)
        {
            r.lengthScale = 2.2f;
            r.velocityScale = 0.035f;
        }

        return ps;
    }

    private void Init(Vector2 center, Vector2 cutDir, Vector2 cutNormal, Bounds bounds, float ground, int sortingLayerId, int sortingOrder, float zPos, float s)
    {
        EnsureAssets();

        dropN = Mathf.Max(4, Mathf.RoundToInt(DropCount * k));
        mistN = Mathf.Max(1, Mathf.RoundToInt(MistCount * k));
        splatCap = Mathf.Max(3, Mathf.RoundToInt(SplatCount * k));

        c = center;
        u = cutDir.normalized;
        n = cutNormal.normalized;
        len = Mathf.Min(bounds.size.magnitude * 0.55f, Mathf.Max(2f, bounds.size.y));
        groundY = ground;
        side = s;
        z = zPos;
        halfW = bounds.extents.x;
        layerId = sortingLayerId;
        order = sortingOrder + 60;

        transform.position = new Vector3(c.x, c.y, z);

        drops = MakeSystem("Drops", hardMat, dropN * 2, true);
        var dm = drops.main;
        dm.gravityModifier = Gravity / Mathf.Max(0.1f, Mathf.Abs(Physics.gravity.y));

        mist = MakeSystem("Mist", softMat, mistN * 2, false);
        mist.GetComponent<ParticleSystemRenderer>().sortingOrder = order - 1;

        var col = mist.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.12f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var sz = mist.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(1f, 1.5f)));

        flash = MakeSystem("Flash", softMat, 8, false);
        flash.GetComponent<ParticleSystemRenderer>().sortingOrder = order + 1;

        // anlık parlak kırmızı patlama (kesik noktasında)
        for (int i = 0; i < 3; i++)
        {
            ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
            ep.position = new Vector3(c.x, c.y, z) + (Vector3)(u * Random.Range(-0.5f, 0.5f));
            ep.velocity = Vector3.zero;
            ep.startLifetime = Random.Range(0.12f, 0.2f);
            ep.startSize = Random.Range(0.7f, 1.3f) * Power;
            ep.startColor = new Color(0.78f, 0.04f, 0.05f, 0.75f);
            flash.Emit(ep, 1);
        }

        // sis
        for (int i = 0; i < mistN; i++)
        {
            ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
            ep.position = new Vector3(c.x + side * Random.Range(halfW * 0.5f, halfW * 1.0f), c.y, z) + (Vector3)(u * Random.Range(-len * 0.3f, len * 0.3f));
            Vector2 v = new Vector2(side * Random.Range(1f, 5f), Random.Range(-0.3f, 1f));
            ep.velocity = v;
            ep.startLifetime = Random.Range(0.55f, 1.0f);
            ep.startSize = Random.Range(0.5f, 1.1f) * Power;
            ep.startColor = new Color(Random.Range(0.35f, 0.55f), 0.02f, 0.03f, 1f);
            mist.Emit(ep, 1);
        }

    }

    private void PlanSplats()
    {
        for (int i = 0; i < SplatCount; i++)
        {
            Vector2 start = c + u * Random.Range(-len * 0.5f, len * 0.5f);
            Vector2 v = new Vector2(side * Random.Range(8f, 36f) * Power, Random.Range(-1f, 6f));

            float h = start.y - groundY;
            float disc = v.y * v.y + 2f * Gravity * Mathf.Max(0f, h);
            float tHit = (v.y + Mathf.Sqrt(Mathf.Max(0f, disc))) / Gravity;

            Splat s = new Splat();
            s.time = Random.Range(0f, EmitTime) + tHit;
            s.pos = new Vector2(start.x + v.x * tHit, groundY + 0.03f);
            s.size = Random.Range(0.25f, 1.1f);
            pending.Add(s);
        }
    }

    private void EmitDrops(int count)
    {
        for (int i = 0; i < count; i++)
        {
            float t = Random.Range(-0.5f, 0.5f);
            Vector2 pos = c + u * (t * len * 0.45f) + new Vector2(side * Random.Range(halfW * 0.5f, halfW * 1.1f), Random.Range(-0.4f, 0.4f));
            // yatay sicrama: dusmanin arkasina (oyuncudan uzaga), cok az dikey
            Vector2 v = new Vector2(side * Random.Range(5f, 18f) * Power, Random.Range(-1f, 4f))
                      + n * Random.Range(-1.5f, 1.5f);

            ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
            ep.position = new Vector3(pos.x, pos.y, z);
            ep.velocity = v;
            ep.startLifetime = Random.Range(0.5f, 0.9f);

            float big = Random.value;
            ep.startSize = big < 0.1f ? Random.Range(0.18f, 0.3f) : Random.Range(0.05f, 0.15f);
            ep.startSize *= Power;

            float shade = Random.Range(0.45f, 0.9f);
            ep.startColor = BloodColor(shade, 1f);

            drops.Emit(ep, 1);
        }
    }

    private ParticleSystem.Particle[] buf = new ParticleSystem.Particle[512];

    private void LateUpdate()
    {
        ClampToGround(drops, true);
        ClampToGround(mist, false);
    }

    private void ClampToGround(ParticleSystem ps, bool makeSplat)
    {
        if (ps == null) return;
        int cnt = ps.GetParticles(buf);
        bool changed = false;

        for (int i = 0; i < cnt; i++)
        {
            if (buf[i].position.y > groundY) continue;

            if (makeSplat && splatsMade < splatCap && Random.value < 0.7f)
            {
                Splat sp = new Splat();
                sp.time = 0f;
                sp.pos = new Vector2(buf[i].position.x, groundY + 0.03f);
                sp.size = Random.Range(0.2f, 0.8f);
                SpawnSplat(sp);
                splatsMade++;
            }

            buf[i].remainingLifetime = -1f;
            changed = true;
        }

        if (changed) ps.SetParticles(buf, cnt);
    }

    private void Update()
    {
        age += Time.deltaTime;

        // fışkırma: başta yoğun, sonra azalır
        if (emitted < dropN)
        {
            float k = Mathf.Clamp01(age / EmitTime);
            float target = dropN * (1f - (1f - k) * (1f - k));
            int add = Mathf.Max(0, Mathf.FloorToInt(target - emitted));

            if (age <= 0.0001f) add = Mathf.Max(add, dropN * 3 / 4);

            if (add > 0)
            {
                EmitDrops(add);
                emitted += add;
            }
        }

        // zemin lekeleri
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            if (age >= pending[i].time)
            {
                SpawnSplat(pending[i]);
                pending.RemoveAt(i);
            }
        }

        for (int i = splats.Count - 1; i >= 0; i--)
        {
            SpriteRenderer sr = splats[i];

            if (sr == null)
            {
                splats.RemoveAt(i); splatBorn.RemoveAt(i); splatTarget.RemoveAt(i);
                continue;
            }

            float a = age - splatBorn[i];
            float grow = Mathf.Clamp01(a / 0.12f);
            float fade = Mathf.Clamp01((SplatLife - a) / 2.5f);
            Color col = sr.color;
            col.a = 0.9f * fade;
            sr.color = col;

            Vector3 sc = sr.transform.localScale;
            float f = 1f - (1f - grow) * (1f - grow);
            sr.transform.localScale = new Vector3(splatTarget[i] * f, splatTarget[i] * 0.22f * f, 1f);

            if (a > SplatLife)
            {
                Destroy(sr.gameObject);
                splats.RemoveAt(i); splatBorn.RemoveAt(i); splatTarget.RemoveAt(i);
            }
        }

        if (age > SplatLife + 1f && pending.Count == 0 && splats.Count == 0)
            Destroy(gameObject);
    }

    private void SpawnSplat(Splat s)
    {
        GameObject go = new GameObject("BloodSplat");
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(s.pos.x, s.pos.y, z);
        go.transform.localScale = new Vector3(0.01f, 0.01f, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = splatSprite;
        sr.sharedMaterial = hardMat;
        sr.sortingLayerID = layerId;
        sr.sortingOrder = order - 5;
        float shade = Random.Range(0.35f, 0.7f);
        sr.color = BloodColor(shade * 0.85f, 0.9f);

        splats.Add(sr);
        splatBorn.Add(age);
        splatTarget.Add(s.size);
    }
}

/// <summary>HER VURUSTA KAN: oyuncu bir dusmana isabet ettirince (can/denge) kisa kan sicramasi.</summary>
public static class HitBloodFx
{
    public static bool Enabled = true;
    public static float HealthScale = 0.55f;
    public static float BalanceScale = 0.3f;
    public static float KillScale = 0.8f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { CombatEvents.EnemyHit -= OnHit; CombatEvents.EnemyHit += OnHit; }

    static void OnHit(EnemyBase e, DamageInfo d, HitResult r)
    {
        if (!Enabled || e == null || !r.hit || r.amount <= 0) return;
        float k = r.killed ? KillScale : (r.onBalance ? BalanceScale : HealthScale);
        if (r.critical) k *= 1.3f;

        Collider2D col = e.GetComponentInChildren<Collider2D>();
        Bounds b;
        if (col != null) b = col.bounds;
        else b = new Bounds(e.transform.position + Vector3.up * 0.8f, new Vector3(0.8f, 1.6f, 0.1f));

        Renderer rend = e.GetComponentInChildren<SpriteRenderer>();
        int layer = rend != null ? rend.sortingLayerID : 0;
        int order = rend != null ? rend.sortingOrder : 0;

        float s = Mathf.Abs(d.direction.x) > 0.01f ? Mathf.Sign(d.direction.x) : 0f;
        if (s == 0f)
        {
            var pc = Object.FindAnyObjectByType<PlayerController>();
            s = pc != null && !Mathf.Approximately(pc.transform.position.x, e.transform.position.x) ? Mathf.Sign(e.transform.position.x - pc.transform.position.x) : 1f;
        }

        Vector2 c = d.hitPosition != Vector3.zero ? (Vector2)d.hitPosition : (Vector2)b.center;
        c.x = Mathf.Clamp(c.x, b.min.x, b.max.x);
        c.y = Mathf.Clamp(c.y, b.min.y + 0.2f, b.max.y);

        float feetY = b.min.y;
        float groundY = feetY - 0.05f;
        var hits = Physics2D.RaycastAll(new Vector2(b.center.x, feetY + 0.4f), Vector2.down, 3f);
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i].collider;
            if (h == null || h.isTrigger || h.transform.IsChildOf(e.transform)) continue;
            if (h.GetComponentInParent<EnemyBase>() != null || h.GetComponentInParent<PlayerController>() != null) continue;
            groundY = hits[i].point.y; break;
        }

        Vector2 u = new Vector2(-0.35f * s, 1f).normalized;
        Vector2 n = new Vector2(1f, 0.35f * s).normalized;
        SliceBloodFx.Burst(c, u, n, b, groundY, layer, order, e.transform.position.z, s, k);
    }
}
