using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DÜŞMAN ÖLÜMÜ: KILIÇ KESİĞİ.
///
/// Öldürme vuruşunda:
///   1) Dünya kısa süre donar (hit stop rampası, gerçek zaman) + kamera sarsıntısı + ses
///   2) Donma sırasında gövdede beyaz bir KESİK çizgisi çakar
///   3) Gövde bu çizgi boyunca iki yarıya ayrılır; yarılar vuruş yönünde savrulur,
///      döner, yere düşer, bir kez seker, sonra erir
///
/// Kesik açısı kombo adımına göre değişir (1: hafif yukarıdan, 2: ters çapraz,
/// 3: dik çapraz, 4: neredeyse dikey; slam: dikey). İNFAZ (sersemlemiş düşmanı bitirme)
/// daha uzun donma, daha güçlü savrulma ve daha sert sarsıntıyla gelir.
///
/// Çalışma şekli: düşmanın sprite'ları (gövde, kılıç ...) kesik çizgisinde poligon olarak
/// kırpılıp iki ayrı mesh'e dönüştürülür. Herhangi bir sprite ile çalışır; her düşman için
/// ayrı animasyon çizmek gerekmez.
///
/// Sahneye elle koymak gerekmez (oyun başlayınca kendiliğinden oluşur).
/// Ayarlar Play sırasında Hierarchy > EnemyDeathFx üzerinden oynanabilir.
///
/// SES: Assets/Resources/DeathFx/ klasörüne klip koy.
///   - adı "execute" veya "big" ile başlayan klipler İNFAZ ölümünde,
///   - diğerleri normal ölümde rastgele çalar.
///   Klasör boşsa geçici olarak posture kırılma sesi çalar.
///
/// Kapatmak: bir düşmanın EnemyDef'inde sliceDeath = false (boss'larda kapalı),
/// ya da bu bileşende fxEnabled = false.
/// </summary>
public class EnemyDeathFx : MonoBehaviour
{
    [Header("Genel")]
    public bool fxEnabled = true;

    [Header("Donma (hit stop rampası, gerçek zaman)")]
    [Tooltip("Normal ölüm: rampa süresi (sn). Zaman ~0.02 hızdan normale yükselir.")]
    public float freezeDuration = 0.22f;
    public float freezeStartScale = 0.02f;
    [Tooltip("Büyükse daha uzun donuk kalır, sonra hızlı bırakır.")]
    public float freezePower = 3f;
    [Tooltip("İnfaz ölümü.")]
    public float bigFreezeDuration = 0.55f;
    public float bigFreezeStartScale = 0.03f;
    public float bigFreezePower = 2.4f;
    public int freezePriority = 20;

    [Header("Kamera sarsıntısı")]
    public float shake = 0.45f;
    public float bigShake = 0.85f;

    [Header("Kesik çizgisi")]
    public float slashTime = 0.2f;
    [Tooltip("Çizgi kalınlığı = gövde yüksekliği x bu oran.")]
    public float slashThicknessRatio = 0.035f;
    [Tooltip("Çizgi uzunluğu = gövde köşegeni x bu çarpan.")]
    public float slashLengthMultiplier = 1.35f;
    public Color slashColor = Color.white;

    [Header("Yarılar: hareket")]
    public float gravity = 40f;
    public float airDrag = 0.4f;
    [Tooltip("Vuruş tarafındaki (üst) yarı: yatay itme.")]
    public float frontPush = 7f;
    [Tooltip("Üst yarı: kesik çizgisinin dışa doğru ayrılma hızı.")]
    public float separation = 3f;
    public float frontLift = 5f;
    public float frontSpin = 260f;
    [Tooltip("Alt yarı.")]
    public float backPush = 2.5f;
    public float backLift = 1.5f;
    public float backSpin = 90f;
    [Tooltip("İnfazda tüm hızlar bu çarpanla büyür.")]
    public float bigPower = 1.6f;
    [Range(0f, 1f)] public float bounce = 0.25f;
    public float bounceThreshold = 4f;
    [Tooltip("Yere değdikten sonra bu kadar sn durur, sonra erir.")]
    public float holdTime = 0.45f;
    public float fadeTime = 0.45f;
    public float maxLife = 3.5f;

    [Header("Ses")]
    [Range(0f, 1f)] public float volume = 1f;
    public Vector2 pitch = new Vector2(0.93f, 1.07f);
    public Vector2 bigPitch = new Vector2(0.85f, 0.95f);

    // =========================================================
    // STATIC
    // =========================================================

    private struct Ctx
    {
        public EnemyBase enemy;
        public Vector2 dir;
        public int step;
        public DamageSource source;
        public bool staggered;
        public float time;
    }

    private static Ctx ctx;
    private static EnemyDeathFx instance;
    private static readonly Dictionary<Texture, Material> mats = new Dictionary<Texture, Material>();
    private static Sprite whiteSprite;
    private static Material unlitMat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        ctx = default(Ctx);
        mats.Clear();
        whiteSprite = null;
        unlitMat = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        EnemyDeathFx touch = Instance;
    }

    private static EnemyDeathFx Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<EnemyDeathFx>();

                if (instance == null)
                {
                    GameObject go = new GameObject("EnemyDeathFx");
                    instance = go.AddComponent<EnemyDeathFx>();
                }
            }

            return instance;
        }
    }

    /// <summary>PlayerDamage, öldürme ihtimali olan her vuruşta çağırır: kesik yönü/açısı için bağlam.</summary>
    public static void SetContext(EnemyBase enemy, DamageInfo info, bool staggered)
    {
        ctx = new Ctx
        {
            enemy = enemy,
            dir = info.direction,
            step = info.comboStep,
            source = info.source,
            staggered = staggered,
            time = Time.unscaledTime
        };
    }

    /// <summary>Health.Die çağırır. true dönerse ölüm efekti devralındı (solma yok).</summary>
    public static bool TryPlay(Health health)
    {
        if (!Application.isPlaying || health == null)
            return false;

        EnemyUnit unit = health.GetComponent<EnemyUnit>();

        if (unit == null || unit.def == null || !unit.def.sliceDeath)
            return false;

        EnemyDeathFx fx = Instance;

        if (!fx.fxEnabled)
            return false;

        return fx.Play(unit);
    }

    // =========================================================
    // RUNTIME
    // =========================================================

    private class Part
    {
        public Mesh mesh;
        public Color[] baseCols;
        public Color[] cols;
    }

    private class Piece
    {
        public Transform t;
        public List<Part> parts = new List<Part>();
        public List<Vector2> hull = new List<Vector2>();
        public Vector2 vel;
        public float w, rot, groundY, settleT, fade, age;
        public bool settled;
        public int bounces;
    }

    private class Flash
    {
        public GameObject go;
        public SpriteRenderer sr;
        public float age, life, length, thick;
        public Color color;
    }

    private struct V
    {
        public Vector3 p;
        public Vector2 uv;
    }

    private readonly List<Piece> pieces = new List<Piece>();
    private readonly List<Flash> flashes = new List<Flash>();

    private AudioClip[] clips = new AudioClip[0];
    private AudioClip[] bigClips = new AudioClip[0];
    private AudioSource src;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;

        AudioClip[] all = Resources.LoadAll<AudioClip>("DeathFx");
        List<AudioClip> n = new List<AudioClip>();
        List<AudioClip> b = new List<AudioClip>();

        for (int i = 0; i < all.Length; i++)
        {
            string name = all[i].name.ToLowerInvariant();

            if (name.StartsWith("execute") || name.StartsWith("big"))
                b.Add(all[i]);
            else
                n.Add(all[i]);
        }

        clips = n.ToArray();
        bigClips = b.ToArray();
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);

        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            if (StepPiece(pieces[i], dt))
            {
                DestroyPiece(pieces[i]);
                pieces.RemoveAt(i);
            }
        }

        float udt = Time.unscaledDeltaTime;

        for (int i = flashes.Count - 1; i >= 0; i--)
        {
            if (StepFlash(flashes[i], udt))
            {
                if (flashes[i].go != null)
                    Destroy(flashes[i].go);

                flashes.RemoveAt(i);
            }
        }
    }

    // =========================================================
    // ÖLÜM
    // =========================================================

    private bool Play(EnemyUnit unit)
    {
        List<SpriteRenderer> parts = new List<SpriteRenderer>();

        foreach (SpriteRenderer sr in unit.GetComponentsInChildren<SpriteRenderer>(false))
        {
            if (sr == null || !sr.enabled || sr.forceRenderingOff || sr.sprite == null || sr.color.a < 0.02f)
                continue;

            parts.Add(sr);
        }

        if (parts.Count == 0)
            return false;

        Bounds bounds = parts[0].bounds;

        for (int i = 1; i < parts.Count; i++)
            bounds.Encapsulate(parts[i].bounds);

        // ---- bağlam: yön, kombo adımı, infaz mı ----

        bool hasCtx =
            ctx.enemy == (EnemyBase)unit &&
            Time.unscaledTime - ctx.time < 0.3f;

        float s = 1f;
        int step = 0;
        bool slam = false;
        bool big = unit.IsStaggered;

        if (hasCtx)
        {
            s = ctx.dir.x >= 0f ? 1f : -1f;
            step = ctx.step;
            slam = ctx.source == DamageSource.Slam;
            big = big || ctx.staggered;
        }
        else
        {
            GameObject player = GameObject.FindWithTag("Player");

            if (player != null)
                s = unit.transform.position.x >= player.transform.position.x ? 1f : -1f;
        }

        // ---- kesik çizgisi ----

        Vector2 u;

        if (big)
            u = new Vector2(s, -0.85f);
        else if (slam)
            u = new Vector2(0.18f * s, -1f);
        else
        {
            switch (step)
            {
                case 1: u = new Vector2(s, -0.30f); break;
                case 2: u = new Vector2(s, 0.32f); break;
                case 3: u = new Vector2(s, -0.75f); break;
                default: u = step >= 4 ? new Vector2(s, -1.25f) : new Vector2(s, -0.5f); break;
            }
        }

        u.Normalize();

        Vector2 n = new Vector2(-u.y, u.x);

        if (Vector2.Dot(n, new Vector2(0.25f * s, 1f)) < 0f)
            n = -n;

        Vector2 c = new Vector2(
            bounds.center.x,
            bounds.center.y + Random.Range(-0.06f, 0.06f) * bounds.size.y
        );

        float z = parts[0].transform.position.z;

        // ---- zemin ----

        float feetY = bounds.min.y;
        float groundY = feetY - 8f;

        RaycastHit2D[] hits = Physics2D.RaycastAll(
            new Vector2(bounds.center.x, feetY + 0.4f),
            Vector2.down,
            20f
        );

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i].collider;

            if (col == null || col.isTrigger || col.transform.IsChildOf(unit.transform))
                continue;

            if (col.GetComponentInParent<EnemyBase>() != null || col.GetComponentInParent<PlayerController>() != null)
                continue;

            groundY = hits[i].point.y;
            break;
        }

        // ---- yarıları üret ----

        Piece front = NewPiece(c, z, groundY);
        Piece back = NewPiece(c, z, groundY);

        for (int i = 0; i < parts.Count; i++)
        {
            List<V> quad = Quad(parts[i]);

            AddPart(front, parts[i], Clip(quad, c, n, 1f), c, z);
            AddPart(back, parts[i], Clip(quad, c, n, -1f), c, z);
        }

        float power = big ? bigPower : 1f;
        Vector2 side = new Vector2(s, 0f);

        if (front.parts.Count > 0)
        {
            front.vel = side * (frontPush * power) + n * (separation * power) + Vector2.up * (frontLift * power);
            front.w = -s * frontSpin * power * Random.Range(0.8f, 1.2f);
            pieces.Add(front);
        }
        else
            DestroyPiece(front);

        if (back.parts.Count > 0)
        {
            back.vel = side * (backPush * power) - n * (separation * 0.5f * power) + Vector2.up * (backLift * power);
            back.w = -s * backSpin * power * Random.Range(0.7f, 1.3f);
            pieces.Add(back);
        }
        else
            DestroyPiece(back);

        // ---- orijinali gizle (Health, ardından kapatıp yok eder) ----

        for (int i = 0; i < parts.Count; i++)
            parts[i].enabled = false;

        // ---- kesik çizgisi flaşı ----

        SpawnFlash(c, z, u, bounds, parts[0]);

        // ---- his: donma, sarsıntı, ses ----

        if (big)
            HitStop.RequestRamp(bigFreezeDuration, bigFreezeStartScale, 1f, bigFreezePower, freezePriority);
        else
            HitStop.RequestRamp(freezeDuration, freezeStartScale, 1f, freezePower, freezePriority);

        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(big ? bigShake : shake);

        PlaySound(big);

        return true;
    }

    // =========================================================
    // GEOMETRİ
    // =========================================================

    // Sprite'ın tam dikdörtgenini dünya uzayında 4 köşe olarak verir (UV'li).
    private static List<V> Quad(SpriteRenderer sr)
    {
        Sprite sp = sr.sprite;
        Texture tex = sp.texture;
        Rect tr = sp.textureRect;

        float ppu = sp.pixelsPerUnit;
        Vector2 pivot = sp.pivot;
        float minX = -pivot.x / ppu;
        float minY = -pivot.y / ppu;
        float w = sp.rect.width / ppu;
        float h = sp.rect.height / ppu;

        float[] fx = { 0f, 1f, 1f, 0f };
        float[] fy = { 0f, 0f, 1f, 1f };

        List<V> quad = new List<V>(4);

        for (int i = 0; i < 4; i++)
        {
            float lx = minX + fx[i] * w;
            float ly = minY + fy[i] * h;

            if (sr.flipX) lx = -lx;
            if (sr.flipY) ly = -ly;

            V v = new V();
            v.p = sr.transform.TransformPoint(lx, ly, 0f);
            v.uv = new Vector2(
                (tr.x + fx[i] * tr.width) / tex.width,
                (tr.y + fy[i] * tr.height) / tex.height
            );

            quad.Add(v);
        }

        return quad;
    }

    // Sutherland-Hodgman: poligonu çizginin bir yanına kırp. side = +1: dot >= 0, -1: dot <= 0.
    private static List<V> Clip(List<V> poly, Vector2 c, Vector2 n, float side)
    {
        List<V> res = new List<V>();

        for (int i = 0; i < poly.Count; i++)
        {
            V a = poly[i];
            V b = poly[(i + 1) % poly.Count];

            float da = side * Vector2.Dot((Vector2)a.p - c, n);
            float db = side * Vector2.Dot((Vector2)b.p - c, n);

            if (da >= 0f)
                res.Add(a);

            if ((da >= 0f) != (db >= 0f))
            {
                float t = da / (da - db);

                V m = new V();
                m.p = Vector3.Lerp(a.p, b.p, t);
                m.uv = Vector2.Lerp(a.uv, b.uv, t);
                res.Add(m);
            }
        }

        return res;
    }

    private Piece NewPiece(Vector2 c, float z, float groundY)
    {
        GameObject go = new GameObject("DeathHalf");
        go.transform.position = new Vector3(c.x, c.y, z);

        Piece p = new Piece();
        p.t = go.transform;
        p.groundY = groundY;

        return p;
    }

    private void AddPart(Piece piece, SpriteRenderer sr, List<V> poly, Vector2 c, float z)
    {
        if (poly == null || poly.Count < 3)
            return;

        Texture tex = sr.sprite.texture;

        List<Vector3> verts = new List<Vector3>(poly.Count);
        List<Vector2> uvs = new List<Vector2>(poly.Count);
        Color[] cols = new Color[poly.Count];
        List<int> tris = new List<int>();

        for (int i = 0; i < poly.Count; i++)
        {
            Vector3 local = new Vector3(poly[i].p.x - c.x, poly[i].p.y - c.y, poly[i].p.z - z);
            verts.Add(local);
            uvs.Add(poly[i].uv);
            cols[i] = sr.color;
            piece.hull.Add(new Vector2(local.x, local.y));
        }

        for (int i = 1; i < poly.Count - 1; i++)
        {
            tris.Add(0);
            tris.Add(i);
            tris.Add(i + 1);
        }

        Mesh mesh = new Mesh();
        mesh.MarkDynamic();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(cols);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();

        GameObject go = new GameObject("Part");
        go.transform.SetParent(piece.t, false);

        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = GetMat(sr, tex);
        mr.sortingLayerID = sr.sortingLayerID;
        mr.sortingOrder = sr.sortingOrder;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        Part part = new Part();
        part.mesh = mesh;
        part.baseCols = (Color[])cols.Clone();
        part.cols = cols;

        piece.parts.Add(part);
    }

    private static Material GetMat(SpriteRenderer sr, Texture tex)
    {
        Material m;

        if (mats.TryGetValue(tex, out m) && m != null)
            return m;

        m = new Material(sr.sharedMaterial);
        m.mainTexture = tex;
        m.name = "DeathFx_" + tex.name;

        mats[tex] = m;

        return m;
    }

    // =========================================================
    // HAREKET
    // =========================================================

    private bool StepPiece(Piece p, float dt)
    {
        if (p.t == null)
            return true;

        p.age += dt;

        if (p.age > maxLife)
            p.settled = true;

        if (!p.settled)
        {
            p.vel.y -= gravity * dt;
            p.vel.x *= Mathf.Max(0f, 1f - airDrag * dt);

            Vector3 pos = p.t.position;
            pos.x += p.vel.x * dt;
            pos.y += p.vel.y * dt;

            p.rot += p.w * dt;

            Quaternion q = Quaternion.Euler(0f, 0f, p.rot);

            float minY = float.MaxValue;

            for (int i = 0; i < p.hull.Count; i++)
            {
                Vector3 v = q * new Vector3(p.hull[i].x, p.hull[i].y, 0f);
                float wy = pos.y + v.y;

                if (wy < minY)
                    minY = wy;
            }

            if (minY < p.groundY)
            {
                pos.y += p.groundY - minY;

                if (p.vel.y < -bounceThreshold && p.bounces < 2)
                {
                    p.vel.y = -p.vel.y * bounce;
                    p.vel.x *= 0.6f;
                    p.w *= 0.5f;
                    p.bounces++;
                }
                else
                {
                    p.vel = Vector2.zero;
                    p.w = 0f;
                    p.settled = true;
                }
            }

            p.t.position = pos;
            p.t.rotation = q;
        }
        else
        {
            p.settleT += dt;

            if (p.settleT > holdTime)
                p.fade += dt / Mathf.Max(0.05f, fadeTime);
        }

        if (p.fade > 0f)
        {
            if (p.fade >= 1f)
                return true;

            float k = 1f - p.fade;

            for (int i = 0; i < p.parts.Count; i++)
            {
                Part part = p.parts[i];

                for (int j = 0; j < part.cols.Length; j++)
                {
                    Color col = part.baseCols[j];
                    col.a *= k;
                    part.cols[j] = col;
                }

                part.mesh.colors = part.cols;
            }
        }

        return false;
    }

    private void DestroyPiece(Piece p)
    {
        for (int i = 0; i < p.parts.Count; i++)
        {
            if (p.parts[i].mesh != null)
                Destroy(p.parts[i].mesh);
        }

        if (p.t != null)
            Destroy(p.t.gameObject);
    }

    // =========================================================
    // KESİK ÇİZGİSİ
    // =========================================================

    private void SpawnFlash(Vector2 c, float z, Vector2 u, Bounds bounds, SpriteRenderer layerSource)
    {
        if (whiteSprite == null)
        {
            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color[] px = new Color[16];

            for (int i = 0; i < 16; i++)
                px[i] = Color.white;

            tex.SetPixels(px);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;

            whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        }

        if (unlitMat == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");

            if (sh == null)
                sh = Shader.Find("Sprites/Default");

            unlitMat = new Material(sh);
        }

        GameObject go = new GameObject("DeathSlash");
        go.transform.position = new Vector3(c.x, c.y, z - 0.01f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(u.y, u.x) * Mathf.Rad2Deg);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = whiteSprite;
        sr.sharedMaterial = unlitMat;
        sr.sortingLayerID = layerSource.sortingLayerID;
        sr.sortingOrder = layerSource.sortingOrder + 50;

        Flash f = new Flash();
        f.go = go;
        f.sr = sr;
        f.life = Mathf.Max(0.05f, slashTime);
        f.length = bounds.size.magnitude * slashLengthMultiplier;
        f.thick = Mathf.Max(0.08f, bounds.size.y * slashThicknessRatio);
        f.color = slashColor;

        go.transform.localScale = new Vector3(0.001f, 0.001f, 1f);

        flashes.Add(f);
    }

    private bool StepFlash(Flash f, float dt)
    {
        if (f.go == null)
            return true;

        f.age += dt;

        float t = f.age / f.life;

        if (t >= 1f)
            return true;

        float lenK = 1f - Mathf.Pow(1f - Mathf.Clamp01(t * 5f), 2f);
        float th = f.thick * (t < 0.2f ? t / 0.2f : 1f - (t - 0.2f) / 0.8f);

        f.go.transform.localScale = new Vector3(f.length * lenK, Mathf.Max(0.001f, th), 1f);

        Color col = f.color;
        col.a *= 1f - t * t;
        f.sr.color = col;

        return false;
    }

    // =========================================================
    // SES
    // =========================================================

    [Header("Ses ayarı")]
    [Tooltip("Klibin başındaki sessizliği atlar (sn). Kılıç sesinin tepesi ~0.09 sn'de; 0.04 ile tepe ~0.05 sn'de gelir, kesik çizgisiyle çakışır.")]
    public float soundStartOffset = 0.04f;

    private AudioSource[] pool;
    private int poolIndex;

    private void PlaySound(bool big)
    {
        AudioClip[] set = big && bigClips.Length > 0 ? bigClips : clips;

        if (set.Length == 0)
        {
            // Klip yok: geçici olarak posture kırılma sesi.
            HitClickAudio.PlayPostureBreak();
            return;
        }

        if (pool == null)
        {
            // Art arda öldürmelerde ses birbirini kesmesin: 4'lü kaynak havuzu.
            pool = new AudioSource[4];

            for (int k = 0; k < pool.Length; k++)
            {
                pool[k] = gameObject.AddComponent<AudioSource>();
                pool[k].playOnAwake = false;
                pool[k].spatialBlend = 0f;
            }
        }

        AudioClip clip = set[Random.Range(0, set.Length)];
        AudioSource s = pool[poolIndex++ % pool.Length];

        Vector2 pr = big ? bigPitch : pitch;

        s.Stop();
        s.clip = clip;
        s.volume = volume;
        s.pitch = Random.Range(pr.x, pr.y);
        s.time = Mathf.Clamp(soundStartOffset, 0f, clip.length * 0.5f);
        s.Play();
    }
}
