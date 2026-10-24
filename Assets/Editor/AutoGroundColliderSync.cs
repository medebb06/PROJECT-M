using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// OTOMATİK ZEMİN COLLIDER'I eşitleyici (edit modu). Ayrıntı: Assets/Scripts/AutoGroundColliders.cs
//
// Her ~0.2 sn'de (ve hiyerarşi değişince) AutoGroundColliders bileşenli klasörlerin altındaki her SpriteRenderer için
// istenen collider hesaplanır; mevcut collider farklıysa güncellenir (aynıysa dokunulmaz: sahne boşuna "dirty" olmaz).
[InitializeOnLoad]
public static class AutoGroundColliderSync
{
    private static double nextCheck;

    static AutoGroundColliderSync()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.hierarchyChanged -= Poke;
        EditorApplication.hierarchyChanged += Poke;
    }

    private static void Poke() { nextCheck = 0; }

    private static void Tick()
    {
        if (Application.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        if (EditorApplication.timeSinceStartup < nextCheck)
            return;

        nextCheck = EditorApplication.timeSinceStartup + 0.2;

        AutoGroundColliders[] roots = Object.FindObjectsByType<AutoGroundColliders>(FindObjectsSortMode.None);

        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null && roots[i].enabled)
                SyncRoot(roots[i]);
        }
    }

    [MenuItem("Tools/Auto Ground Colliders/Sync Now")]
    private static void SyncNow()
    {
        int changed = 0;
        AutoGroundColliders[] roots = Object.FindObjectsByType<AutoGroundColliders>(FindObjectsSortMode.None);

        foreach (AutoGroundColliders r in roots)
            changed += SyncRoot(r);

        Debug.Log("[AutoGroundColliders] " + roots.Length + " klasör eşitlendi, " + changed + " değişiklik.");
    }

    [MenuItem("Tools/Auto Ground Colliders/Add To Selected Object")]
    private static void AddToSelected()
    {
        foreach (GameObject go in Selection.gameObjects)
        {
            if (go.GetComponent<AutoGroundColliders>() == null)
                Undo.AddComponent<AutoGroundColliders>(go);
        }
    }

    // ---------------------------------------------------------------- eşitleme

    private static int SyncRoot(AutoGroundColliders root)
    {
        int changed = 0;
        int groundLayer = LayerMask.NameToLayer(root.groundLayerName);
        SpriteRenderer[] srs = root.GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < srs.Length; i++)
        {
            SpriteRenderer sr = srs[i];

            if (sr == null || sr.sprite == null || sr.gameObject == root.gameObject)
                continue;

            if (sr.gameObject.name.StartsWith("_"))
                continue;

            changed += SyncObject(root, sr, groundLayer);
        }

        return changed;
    }

    private struct Desired
    {
        public bool isBox;
        public Vector2 boxSize;
        public Vector2 boxOffset;
        public List<List<Vector2>> paths;
    }

    private static int SyncObject(AutoGroundColliders root, SpriteRenderer sr, int groundLayer)
    {
        GameObject go = sr.gameObject;
        int changed = 0;

        // Başka tür collider (daire, kapsül, edge...) elle konmuşsa dokunma.
        Collider2D[] existing = go.GetComponents<Collider2D>();

        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] == null) continue;

            if (!(existing[i] is BoxCollider2D) && !(existing[i] is PolygonCollider2D))
                return 0;
        }

        if (root.forceGroundLayer && groundLayer >= 0 && go.layer != groundLayer)
        {
            Undo.RecordObject(go, "Auto Ground Collider");
            go.layer = groundLayer;
            EditorUtility.SetDirty(go);
            changed++;
        }

        Desired d;

        if (!Compute(sr, root.rectTolerance, out d))
            return changed;

        BoxCollider2D box = go.GetComponent<BoxCollider2D>();
        PolygonCollider2D poly = go.GetComponent<PolygonCollider2D>();

        if (d.isBox)
        {
            if (poly != null) { Undo.DestroyObjectImmediate(poly); changed++; }

            if (box == null)
            {
                box = Undo.AddComponent<BoxCollider2D>(go);
                changed++;
            }

            if (!Approx(box.size, d.boxSize) || !Approx(box.offset, d.boxOffset))
            {
                Undo.RecordObject(box, "Auto Ground Collider");
                box.size = d.boxSize;
                box.offset = d.boxOffset;
                EditorUtility.SetDirty(box);
                changed++;
            }
        }
        else
        {
            if (box != null) { Undo.DestroyObjectImmediate(box); changed++; }

            if (poly == null)
            {
                poly = Undo.AddComponent<PolygonCollider2D>(go);
                changed++;
            }

            if (!PathsEqual(poly, d.paths))
            {
                Undo.RecordObject(poly, "Auto Ground Collider");
                poly.pathCount = d.paths.Count;

                for (int p = 0; p < d.paths.Count; p++)
                    poly.SetPath(p, d.paths[p]);

                EditorUtility.SetDirty(poly);
                changed++;
            }
        }

        if (changed > 0)
            EditorUtility.SetDirty(go);

        return changed;
    }

    // Sprite + renderer ayarlarından istenen collider'ı hesaplar.
    private static bool Compute(SpriteRenderer sr, float rectTol, out Desired d)
    {
        d = new Desired();
        Sprite sprite = sr.sprite;
        float fx = sr.flipX ? -1f : 1f;
        float fy = sr.flipY ? -1f : 1f;

        // Sliced / Tiled: çizilen alan renderer.size kadar bir dikdörtgendir.
        if (sr.drawMode != SpriteDrawMode.Simple)
        {
            Vector2 size = sr.size;
            Vector2 pivot01 = new Vector2(
                sprite.rect.width > 0f ? sprite.pivot.x / sprite.rect.width : 0.5f,
                sprite.rect.height > 0f ? sprite.pivot.y / sprite.rect.height : 0.5f);

            d.isBox = true;
            d.boxSize = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
            d.boxOffset = new Vector2((0.5f - pivot01.x) * size.x * fx, (0.5f - pivot01.y) * size.y * fy);
            return true;
        }

        int count = sprite.GetPhysicsShapeCount();

        if (count <= 0)
        {
            Bounds b = sprite.bounds;
            d.isBox = true;
            d.boxSize = new Vector2(b.size.x, b.size.y);
            d.boxOffset = new Vector2(b.center.x * fx, b.center.y * fy);
            return true;
        }

        d.paths = new List<List<Vector2>>();
        List<Vector2> tmp = new List<Vector2>();

        for (int i = 0; i < count; i++)
        {
            tmp.Clear();
            sprite.GetPhysicsShape(i, tmp);
            List<Vector2> path = new List<Vector2>(tmp.Count);

            for (int k = 0; k < tmp.Count; k++)
                path.Add(new Vector2(tmp[k].x * fx, tmp[k].y * fy));

            if (path.Count >= 3)
                d.paths.Add(path);
        }

        if (d.paths.Count == 0)
        {
            Bounds b = sprite.bounds;
            d.isBox = true;
            d.boxSize = new Vector2(b.size.x, b.size.y);
            d.boxOffset = new Vector2(b.center.x * fx, b.center.y * fy);
            return true;
        }

        // Tek parça ve eksenlere paralel dörtgen: BoxCollider2D daha temiz ve ucuz.
        if (d.paths.Count == 1 && d.paths[0].Count == 4 && IsAxisRect(d.paths[0], rectTol))
        {
            Vector2 min = d.paths[0][0], max = d.paths[0][0];

            foreach (Vector2 v in d.paths[0])
            {
                min = Vector2.Min(min, v);
                max = Vector2.Max(max, v);
            }

            d.isBox = true;
            d.boxSize = max - min;
            d.boxOffset = (min + max) * 0.5f;
            d.paths = null;
        }

        return true;
    }

    private static bool IsAxisRect(List<Vector2> pts, float tol)
    {
        Vector2 min = pts[0], max = pts[0];

        foreach (Vector2 v in pts)
        {
            min = Vector2.Min(min, v);
            max = Vector2.Max(max, v);
        }

        Vector2 size = max - min;
        float t = Mathf.Max(0.0005f, tol * Mathf.Max(size.x, size.y));
        int cornerMask = 0;

        foreach (Vector2 v in pts)
        {
            bool onX = Mathf.Abs(v.x - min.x) <= t || Mathf.Abs(v.x - max.x) <= t;
            bool onY = Mathf.Abs(v.y - min.y) <= t || Mathf.Abs(v.y - max.y) <= t;

            if (!onX || !onY)
                return false;

            int cx = Mathf.Abs(v.x - min.x) <= t ? 0 : 1;
            int cy = Mathf.Abs(v.y - min.y) <= t ? 0 : 1;
            cornerMask |= 1 << (cx * 2 + cy);
        }

        return cornerMask == 0xF;   // dört köşe de ayrı ayrı bulundu
    }

    private static bool Approx(Vector2 a, Vector2 b)
    {
        return Mathf.Abs(a.x - b.x) < 0.0005f && Mathf.Abs(a.y - b.y) < 0.0005f;
    }

    private static bool PathsEqual(PolygonCollider2D poly, List<List<Vector2>> want)
    {
        if (poly.pathCount != want.Count)
            return false;

        List<Vector2> cur = new List<Vector2>();

        for (int p = 0; p < want.Count; p++)
        {
            cur.Clear();
            poly.GetPath(p, cur);

            if (cur.Count != want[p].Count)
                return false;

            for (int k = 0; k < cur.Count; k++)
            {
                if (!Approx(cur[k], want[p][k]))
                    return false;
            }
        }

        return true;
    }
}
