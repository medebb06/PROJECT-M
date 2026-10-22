using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// OYUNCU SALDIRI IZI (beyaz, cok parlak).
/// Yaylar oyuncunun kare kare saldiri animasyonundaki smear'lardan olculdu (Resources/AtkGlow/AttackN_glow
/// karelerine cember/elips oturtuldu) ve ayni yonde suprulur:
///   1: alttan-onden yukari, ustten arkaya donen yay (saat yonu tersi)
///   2: onden-alttan asagi, arkadan yukari donen genis yay (saat yonu)
///   3: saplama: arkadan one ince, sivri cizgi (havada asagi vurusta dikey)
///   4: donus: belin etrafinda yassi elips (saat yonu)
/// Kordinatlar Ronni HD kare biriminde (ayak = pivot, saga bakis). Efekt saldiri karesinin
/// (Ronni_Atk1Frames) olcek / yon bilgisini kullanir; boylece cizilen smear ile ust uste oturur.
/// Renk ve parlaklik Impact Mat ile ayni (HDR beyaz 8.57). Kisa omurlu: vurus bitince biter.
/// PlayerWeapon.SpawnSlash cagirir.
/// </summary>
public static class SlashInkFx
{
    public struct Arc
    {
        public bool line;
        public Vector2 c;
        public float rx, ry, a0, a1;
        public Vector2 p0, p1;
        public float thick;
        public float life;
        public float scale;
        public float peak;   // kalinligin en fazla oldugu yer (0 kuyruk .. 1 bas)
        public Vector2 shift; // efekti kare biriminde kaydir (x: one +, geriye -; y: yukari +)
    }

    // Olcumler: AttackNG_k karelerinden (ppu 100, ayak pivotu).
    public static readonly Arc[] Arcs =
    {
        // 1: Attack1G_3 merkez (5.28, 8.15) R~5.3; 280° -> 0° -> 90° -> 190°
        new Arc { c = new Vector2(4.6f, 5.6f), rx = 8.2f, ry = 4.4f, a0 = -80f, a1 = 190f, thick = 2.3f, life = 0.13f, scale = 1.1f, peak = 0.65f, shift = new Vector2(-1.8f, 0f) },
        // 2: Attack2G_3 merkez (2.05, 4.39) elips 8 x 4; 350° -> 270° -> 180° -> 100°
        new Arc { c = new Vector2(2.05f, 4.39f), rx = 8.0f, ry = 4.0f, a0 = 350f, a1 = 100f, thick = 2.4f, life = 0.14f, scale = 1f, peak = 0.7f },
        // 3: Attack3G_2 saplama, gogus hizasi y~3.9, x -3.5 .. 10.2
        new Arc { line = true, p0 = new Vector2(-9f, 3.9f), p1 = new Vector2(10.2f, 3.9f), thick = 0.55f, life = 0.1f, scale = 1f, peak = 0.3f },
        // 4: Attack4G_4 merkez (2.19, 2.56) yassi elips 8.5 x 2.45; 60° -> 0° -> 270° -> 150° (biraz kucuk)
        new Arc { c = new Vector2(2.19f, 2.56f), rx = 8.5f, ry = 2.45f, a0 = 60f, a1 = -210f, thick = 1.6f, life = 0.155f, scale = 1.2f, peak = 0.6f, shift = new Vector2(0f, 2.2f) },
    };

    [Tooltip("Ronni karesi bulunamazsa: 1 kare birimi = bu kadar dunya birimi.")]
    public static float FallbackUnit = 0.555f;

    private static Material mat;
    private static Material hazeMat;
    private static readonly Dictionary<Transform, SpriteRenderer> frameCache = new Dictionary<Transform, SpriteRenderer>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        mat = null;
        hazeMat = null;
        frameCache.Clear();
    }

    private static Material Mat()
    {
        if (mat == null)
        {
            Shader sh = Resources.Load<Shader>("Shaders/SlashSmear");

            if (sh == null)
                sh = Shader.Find("ProjectM/SlashSmear");

            if (sh != null)
                mat = new Material(sh) { name = "SlashSmear" };
        }

        return mat;
    }

    private static SpriteRenderer FrameSR(Transform player)
    {
        if (player == null)
            return null;

        SpriteRenderer sr;

        if (frameCache.TryGetValue(player, out sr) && sr != null)
            return sr;

        sr = null;

        foreach (Transform t in player.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Ronni_Atk1Frames")
            {
                sr = t.GetComponent<SpriteRenderer>();
                break;
            }
        }

        if (sr != null)
            frameCache[player] = sr;

        return sr;
    }

    private static Material HazeMat()
    {
        if (hazeMat == null)
        {
            Shader sh = Resources.Load<Shader>("Shaders/SlashSmear");

            if (sh == null)
                sh = Shader.Find("ProjectM/SlashSmear");

            if (sh != null)
            {
                hazeMat = new Material(sh) { name = "SlashSmearHaze" };
                hazeMat.SetColor("_Color", new Color(3f, 3f, 3f, 1f));
                hazeMat.SetFloat("_InnerSoft", 1f);
                hazeMat.SetFloat("_OuterSoft", 0.3f);
                hazeMat.SetFloat("_CoreBoost", 0f);
            }
        }

        return hazeMat;
    }

    private static void MakeLayer(Arc arc, Transform anchor, Vector3 origin, Vector3 scale, Material material, int sortingLayerId, int sortingOrder, bool haze)
    {
        GameObject go = new GameObject(haze ? "~slash_haze" : "~slash_smear");
        go.transform.position = origin;
        go.transform.localScale = scale;

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.sortingLayerID = sortingLayerId;
        mr.sortingOrder = Mathf.Max(sortingOrder, 0);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        SlashSmear smear = go.AddComponent<SlashSmear>();
        smear.Init(arc, anchor, origin, haze);
    }
    public static bool Spawn(Vector2 center, Vector2 box, Vector2 dir, int step, int sortingLayerId, int sortingOrder, Transform follow = null)
    {
        Material m = Mat();

        if (m == null)
            return false;

        bool down = dir.y < -0.5f && Mathf.Abs(dir.x) < 0.5f;
        bool left = dir.x < 0f;

        Arc arc;
        Vector3 origin;
        float unit;
        Transform anchor = follow;

        if (down)
        {
            // Havada asagi saplama: vurus kutusundan gecen dikey cizgi (dunya biriminde).
            float h = Mathf.Max(1f, box.y) * 0.9f;
            arc = new Arc { line = true, p0 = new Vector2(0f, h), p1 = new Vector2(0f, -h), thick = Mathf.Max(0.3f, box.x * 0.22f), life = 0.1f, scale = 1f, peak = 0.3f };
            origin = new Vector3(center.x, center.y, 0f);
            unit = 1f;
            left = false;
        }
        else
        {
            int idx = Mathf.Clamp(step, 1, Arcs.Length) - 1;
            arc = Arcs[idx];

            SpriteRenderer frame = FrameSR(follow);

            if (frame != null && frame.enabled && frame.gameObject.activeInHierarchy)
            {
                // Saldiri karesiyle ayni yer / olcek / yon.
                origin = frame.transform.position;
                unit = Mathf.Abs(frame.transform.lossyScale.y);
                left = frame.flipX;
                sortingLayerId = frame.sortingLayerID;
                sortingOrder = frame.sortingOrder + 3;
            }
            else
            {
                // Kare yoksa (ornegin havada rig oynarken): Ronni rig'inin ayak noktasi, ayni olcek (0.001 x 1.5 x 3.7 x 100).
                RonniSkin.RonniSkinRig rig = follow != null ? follow.GetComponentInChildren<RonniSkin.RonniSkinRig>() : null;
                origin = rig != null ? rig.transform.position : (follow != null ? follow.position : new Vector3(center.x, center.y, 0f));
                unit = FallbackUnit;
            }

            origin += new Vector3((left ? -1f : 1f) * arc.shift.x * unit, arc.shift.y * unit, 0f);
            unit *= arc.scale;
        }

        Vector3 scl = new Vector3(left ? -unit : unit, unit, 1f);

        MakeLayer(arc, anchor, origin, scl, m, sortingLayerId, sortingOrder, false);

        // Sis katmani: animasyondaki smear dagilmasi gibi daha buyuk, daha soluk, yumusak kopya.
        Material hm = HazeMat();

        // Sis katmani kapali: iz keskin kenarli kalir (bicak izi gibi).

        return true;
    }
}

/// <summary>Tek bir smear seridi: bas yay boyunca hizla ilerler, kuyruk arkasindan gelip siler, biter.</summary>
public class SlashSmear : MonoBehaviour
{
    private const int N = 120;

    private SlashInkFx.Arc arc;
    private Transform anchor;
    private Vector3 offset;
    private float t;

    private Mesh mesh;
    private readonly Vector3[] verts = new Vector3[(N + 1) * 2];
    private readonly Vector2[] uvs = new Vector2[(N + 1) * 2];
    private readonly Color[] cols = new Color[(N + 1) * 2];
    private int[] tris;
    private float peakPow;

    // Sis dagilmasi (animasyondaki SmearGhost ile ayni degerler).
    private const float FogLife = 0.4f;     // sisin dagilma suresi
    private const float FogWiden = 3.6f;     // her parca kendi yerinde bu kata kadar genisler
    private const float HazeWiden = 4.8f;    // arka sis katmani daha da genis
    private MaterialPropertyBlock mpb;
    private const float HazeAlpha = 0.45f;
    private bool haze;
    private bool fog;
    private float fogT;
    private Vector3 fogP0, fogS0;

public void Init(SlashInkFx.Arc a, Transform follow, Vector3 origin, bool isHaze)
    {
        arc = a;
        anchor = follow;
        haze = isHaze;
        offset = follow != null ? origin - follow.position : Vector3.zero;

        // sin(pi * u^p) en yuksek noktasi u = 0.5^(1/p) = peak
        float pk = Mathf.Clamp(a.peak, 0.1f, 0.9f);
        peakPow = Mathf.Log(0.5f) / Mathf.Log(pk);

        mesh = new Mesh { name = "SlashSmear" };
        mesh.MarkDynamic();

        tris = new int[N * 6];

        for (int i = 0; i < N; i++)
        {
            int v = i * 2;
            int k = i * 6;
            tris[k] = v; tris[k + 1] = v + 1; tris[k + 2] = v + 2;
            tris[k + 3] = v + 1; tris[k + 4] = v + 3; tris[k + 5] = v + 2;
        }

        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        Build();
    }

private void LateUpdate()
    {
        if (fog)
        {
            fogT += Time.deltaTime;
            float fk = Mathf.Clamp01(fogT / FogLife);

            if (fk >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            // Yerinde kalir (yukari kaymaz, olcek degismez); kenarlar yumusar.
            float e = 1f - (1f - fk) * (1f - fk);
            MeshRenderer mr = GetComponent<MeshRenderer>();

            if (false && mr != null)
            {
                if (mpb == null) mpb = new MaterialPropertyBlock();
                mr.GetPropertyBlock(mpb);
                mpb.SetFloat("_OuterSoft", Mathf.Lerp(haze ? 0.3f : 0.06f, 0.5f, e));
                mpb.SetFloat("_InnerSoft", Mathf.Lerp(haze ? 1f : 0.6f, 1f, e));
                mpb.SetFloat("_CoreBoost", Mathf.Lerp(haze ? 0f : 0.6f, 0f, e));
                mr.SetPropertyBlock(mpb);
            }

            Build();
            return;
        }

        t += Time.deltaTime;

        if (t >= arc.life)
        {
            // Vurus bitti: iz yerinde kalir, sis gibi genisleyip soluklasir.
            fog = true;
            anchor = null;
            fogP0 = transform.position;
            fogS0 = transform.localScale;
            Build();
            return;
        }

        if (anchor != null)
            transform.position = anchor.position + offset;

        Build();
    }

    private void OnDestroy()
    {
        if (mesh != null)
            Destroy(mesh);
    }

private void Build()
    {
        float k = fog ? 1f : Mathf.Clamp01(t / Mathf.Max(0.01f, arc.life));

        // Bas: hizli cikis (ease-out). Kuyruk: gecikmeli ama basa yetismez, iz sise donusur.
        float head = 1f - Mathf.Pow(1f - Mathf.Clamp01(k / 0.5f), 3f);
        float tail = Mathf.Clamp01((k - 0.2f) / 0.8f);
        tail = tail * tail * 0.3f;

        // Sis: kare kare animasyondaki gibi basta hizli, sonda uzun kuyruklu solus.
        float fade = 1f;

        if (fog)
        {
            float fk = Mathf.Clamp01(fogT / FogLife);
            fade = 1f - fk * fk;                       // sonuna kadar parlak, sonra keskin kesilir
            // (kuyruk silme kaldirildi: iz ucta V'ye donusuyordu; sadece incelip solar)
        }

        if (haze)
            fade *= HazeAlpha;

        float grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / 0.12f));

        // Sis: her parca kendi orta cizgisi etrafinda iki yana genisler (yerinde yayilir).
        float widen = 1f;

        if (fog)
        {
            float fk2 = Mathf.Clamp01(fogT / FogLife);
            float e2 = 1f - (1f - fk2) * (1f - fk2);
            widen = Mathf.Lerp(1f, 0.3f, e2);               // kenarlar keskin kalir, iz incelerek biter
        }

        Vector2 lineDir = (arc.p1 - arc.p0).normalized;
        Vector2 lineN = new Vector2(-lineDir.y, lineDir.x);

        for (int i = 0; i <= N; i++)
        {
            float u = i / (float)N;
            float s = Mathf.Lerp(tail, head, u);

            float w = arc.thick * grow * Mathf.Sin(Mathf.PI * Mathf.Pow(u, peakPow));

            Vector2 outer, inner;

            if (arc.line)
            {
                Vector2 p = Vector2.Lerp(arc.p0, arc.p1, s);
                outer = p + lineN * w * widen * 0.5f;
                inner = p - lineN * w * widen * 0.5f;
            }
            else
            {
                float a = Mathf.Lerp(arc.a0, arc.a1, s) * Mathf.Deg2Rad;
                Vector2 p = arc.c + new Vector2(Mathf.Cos(a) * arc.rx, Mathf.Sin(a) * arc.ry);
                Vector2 tang = new Vector2(-Mathf.Sin(a) * arc.rx, Mathf.Cos(a) * arc.ry);
                Vector2 inward = new Vector2(-tang.y, tang.x).normalized;   // egrinin gercek normali (uclarda V olmasin)
                if (Vector2.Dot(inward, arc.c - p) < 0f) inward = -inward;
                Vector2 mid = p + inward * (w * 0.5f);
                float half = w * widen * 0.5f;
                // Egrilik yaricapindan genis serit kendi uzerine katlanip V olur: yaricapla sinirla.
                float sa = Mathf.Sin(a), ca = Mathf.Cos(a);
                float rad = Mathf.Pow(arc.rx * arc.rx * sa * sa + arc.ry * arc.ry * ca * ca, 1.5f) / Mathf.Max(0.001f, arc.rx * arc.ry);
                { float lim = Mathf.Max(0.001f, rad * 0.7f); half = lim * (float)System.Math.Tanh(half / lim); }   // yumusak sinir (kirik/girinti yok)
                outer = mid - inward * half;
                inner = mid + inward * half;
            }

            int v = i * 2;
            verts[v] = outer;
            verts[v + 1] = inner;
            uvs[v] = new Vector2(u, 1f);
            uvs[v + 1] = new Vector2(u, 0f);

            float alpha = fade * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 0.35f));
            Color c = new Color(1f, 1f, 1f, alpha);
            cols[v] = c;
            cols[v + 1] = c;
        }

        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.colors = cols;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
    }
}
