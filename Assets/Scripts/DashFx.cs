using System.Collections.Generic;
using UnityEngine;

// DASH EFEKTLERI: ozel dash animasyonu yerine
//  - basta squash & stretch (ileri uzama), sonda sikisma,
//  - karakterin o anki cizimini (Ronni mesh parcalari) kopyalayip solduran AFTER IMAGE.
// DashState Enter/Exit/HandleAfterImage'ten cagrilir. Hicbir sahne kurulumu gerekmez.
public static class DashFx
{
    // ---- Ayarlar (kodla degistirilebilir) ----
    public static bool Enabled = true;

    public static Vector2 StartStretch = new Vector2(1.45f, 0.72f);   // x uzar, y ezilir
    public static float StartStretchTime = 0.18f;

    public static Vector2 EndSquash = new Vector2(0.80f, 1.22f);      // durunca x kisalir, y kabarir
    public static float EndSquashTime = 0.16f;

    public static float GhostLife = 0.45f;
    public static Color GhostColor = new Color(0.55f, 0.85f, 1f, 1f); // parlak mavi-beyaz
    public static float GhostAlpha = 0.85f;
    public static float GhostSpacing = 0.035f;                        // iz araligi (sn)
    public static int GhostOrderBehind = 1;                           // gercek karakterin hemen arkasinda (arka plana gitmesin)

    public static void Begin(PlayerController p)
    {
        if (!Enabled || p == null) return;

        Host(p).Punch(p, StartStretch, StartStretchTime);
    }

    public static void End(PlayerController p)
    {
        if (!Enabled || p == null) return;

        Host(p).Punch(p, EndSquash, EndSquashTime);

        Ghost(p);   // son iz
    }

    static DashGhostHost Host(PlayerController p)
    {
        DashGhostHost host = p.GetComponent<DashGhostHost>();
        if (host == null) host = p.gameObject.AddComponent<DashGhostHost>();
        return host;
    }

    public static void Ghost(PlayerController p)
    {
        if (!Enabled || p == null) return;

        Host(p).Spawn(p);
    }
}

// Iz kopyalarini tutar, her karede soldurur ve siler.
public class DashGhostHost : MonoBehaviour
{
    class Ghost
    {
        public GameObject root;
        public Mesh[] meshes;
        public Color[][] colors;
        public float age;
        public SpriteRenderer sr;   // dash karesi (sprite) izi
    }

    // ---- squash & stretch (SquashStretch bilesenine bagimli degil: o oyuncuda kapali) ----
    Transform pivot;
    Vector3 basePivotScale = Vector3.one;
    Transform restPivot;      // dinlenme ölçeği yakalanan pivot
    Vector3 restScale = Vector3.one;
    Vector2 punch = Vector2.one;
    float punchTime, punchTimer;
    bool punching;

    public void Punch(PlayerController p, Vector2 scale, float duration)
    {
        if (p == null || p.modelPivot == null) return;

        if (!punching)
        {
            pivot = p.modelPivot;

            // Dinlenme ölçeği İLK punch'ta bir kez alınır: başka bir script ölçeği o an
            // bozmuş olsa bile punch bitince oyuncu kalıcı büyümüş / incelmiş kalmaz.
            if (restPivot != pivot)
            {
                restPivot = pivot;
                restScale = pivot.localScale;
            }

            basePivotScale = restScale;
        }

        punch = scale;
        punchTime = Mathf.Max(0.02f, duration);
        punchTimer = punchTime;
        punching = true;
    }

    void LateUpdate()
    {
        if (!punching || pivot == null) return;

        punchTimer -= Time.deltaTime;

        float k = Mathf.Clamp01(punchTimer / punchTime);   // 1 -> 0
        k = k * k * (3f - 2f * k);

        float sx = Mathf.Lerp(1f, punch.x, k);
        float sy = Mathf.Lerp(1f, punch.y, k);

        pivot.localScale = new Vector3(basePivotScale.x * sx, basePivotScale.y * sy, basePivotScale.z);

        if (punchTimer <= 0f)
        {
            pivot.localScale = basePivotScale;
            punching = false;
        }
    }

    readonly List<Ghost> ghosts = new List<Ghost>();
    readonly List<MeshRenderer> srcBuf = new List<MeshRenderer>();

    public void Spawn(PlayerController p)
    {
        // Dash karesi (tek sprite) gosteriliyorsa iz de o sprite'in kopyasi olur.
        SpriteRenderer[] srs = p.GetComponentsInChildren<SpriteRenderer>(false);
        for (int i = 0; i < srs.Length; i++)
        {
            SpriteRenderer f = srs[i];
            if (f == null || !f.enabled || f.sprite == null || !f.sprite.name.StartsWith("DashHD")) continue;

            Ghost sg = new Ghost();
            sg.root = new GameObject("DashGhost");
            sg.root.transform.SetParent(null, false);
            sg.root.transform.SetPositionAndRotation(f.transform.position, f.transform.rotation);
            sg.root.transform.localScale = f.transform.lossyScale;

            SpriteRenderer gr = sg.root.AddComponent<SpriteRenderer>();
            gr.sprite = f.sprite;
            gr.flipX = f.flipX;
            gr.sharedMaterial = f.sharedMaterial;
            gr.sortingLayerID = f.sortingLayerID;
            gr.sortingOrder = f.sortingOrder - DashFx.GhostOrderBehind;

            sg.sr = gr;
            sg.meshes = new Mesh[0];
            sg.colors = new Color[0][];
            Paint(sg, 0f);
            ghosts.Add(sg);
            return;
        }

        srcBuf.Clear();

        MeshRenderer[] all = p.GetComponentsInChildren<MeshRenderer>(false);

        for (int i = 0; i < all.Length; i++)
        {
            MeshRenderer mr = all[i];
            if (mr == null || !mr.enabled || !mr.gameObject.activeInHierarchy) continue;
            if (mr.sharedMaterial == null) continue;
            if (mr.name.StartsWith("~")) continue;    // rim / golge / footshade katmanlari

            MeshFilter mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || mf.sharedMesh.vertexCount == 0) continue;

            srcBuf.Add(mr);
        }

        if (srcBuf.Count == 0) return;

        Ghost g = new Ghost();
        g.root = new GameObject("DashGhost");
        g.root.transform.SetParent(null, false);
        g.meshes = new Mesh[srcBuf.Count];
        g.colors = new Color[srcBuf.Count][];

        for (int i = 0; i < srcBuf.Count; i++)
        {
            MeshRenderer src = srcBuf[i];
            Mesh srcMesh = src.GetComponent<MeshFilter>().sharedMesh;

            Mesh m = new Mesh();
            m.name = "DashGhostMesh";
            m.vertices = srcMesh.vertices;
            m.uv = srcMesh.uv;
            m.triangles = srcMesh.triangles;
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 100f);

            Color[] cols = new Color[m.vertexCount];
            m.colors = cols;
            m.MarkDynamic();

            GameObject go = new GameObject(src.name);
            go.transform.SetParent(g.root.transform, false);
            go.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
            go.transform.localScale = src.transform.lossyScale;

            MeshFilter gf = go.AddComponent<MeshFilter>();
            gf.sharedMesh = m;

            MeshRenderer gr = go.AddComponent<MeshRenderer>();
            gr.sharedMaterial = src.sharedMaterial;
            gr.sortingLayerID = src.sortingLayerID;
            gr.sortingOrder = src.sortingOrder - DashFx.GhostOrderBehind;
            gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gr.receiveShadows = false;

            g.meshes[i] = m;
            g.colors[i] = cols;
        }

        Paint(g, 0f);
        ghosts.Add(g);
    }

    void Paint(Ghost g, float t01)
    {
        float a = DashFx.GhostAlpha * (1f - t01) * (1f - t01);
        Color c = DashFx.GhostColor;
        c.a = a;

        if (g.sr != null) g.sr.color = c;

        for (int i = 0; i < g.meshes.Length; i++)
        {
            Color[] cols = g.colors[i];
            for (int k = 0; k < cols.Length; k++) cols[k] = c;
            g.meshes[i].colors = cols;
        }
    }

    void Update()
    {
        float life = Mathf.Max(0.05f, DashFx.GhostLife);

        for (int i = ghosts.Count - 1; i >= 0; i--)
        {
            Ghost g = ghosts[i];
            g.age += Time.deltaTime;

            if (g.age >= life)
            {
                for (int k = 0; k < g.meshes.Length; k++)
                    if (g.meshes[k] != null) Destroy(g.meshes[k]);

                if (g.root != null) Destroy(g.root);
                ghosts.RemoveAt(i);
                continue;
            }

            Paint(g, g.age / life);
        }
    }

    void OnDestroy()
    {
        for (int i = 0; i < ghosts.Count; i++)
        {
            Ghost g = ghosts[i];
            for (int k = 0; k < g.meshes.Length; k++)
                if (g.meshes[k] != null) Destroy(g.meshes[k]);
            if (g.root != null) Destroy(g.root);
        }

        ghosts.Clear();
    }
}
