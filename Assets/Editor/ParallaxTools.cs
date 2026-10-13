#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Parallax
///   Apply Depth Presets : her katmana derinliğine (sorting order) göre oran verir.
///   Report              : sahnedeki bütün parallax'ları Console'a yazar.
/// </summary>
public static class ParallaxTools
{
    // sortingOrder -> (X, Y). Sahnedeki katman düzeni:
    //  -50 bulut | -40 arka çalı | -31/-30 uzak ağaç | -21/-20 yakın ağaç | -10 zemin çalısı
    //   0..449 oyun düzlemi | 450+ ön plan
    private static Vector2 PresetFor(int order)
    {
        if (order <= -50) return new Vector2(0.90f, 0.80f);  // bulut: neredeyse gökyüzü
        if (order <= -40) return new Vector2(0.60f, 0.35f);  // arka çalı şeridi (ufuk)
        if (order <= -30) return new Vector2(0.42f, 0.22f);  // uzak ağaç
        if (order <= -20) return new Vector2(0.30f, 0.15f);  // yakın ağaç
        if (order <= -10) return new Vector2(0.15f, 0.06f);  // zemin arkası çalı
        if (order < 450)  return Vector2.zero;               // oyun düzlemi
        return new Vector2(-0.25f, -0.08f);                   // ön plan
    }

    [MenuItem("Tools/Parallax/Apply Depth Presets")]
    private static void ApplyPresets()
    {
        ParallaxEffect[] all = Object.FindObjectsByType<ParallaxEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var sb = new StringBuilder("Parallax presetleri uygulandı:\n");
        int changed = 0, removed = 0;

        Undo.SetCurrentGroupName("Apply Parallax Presets");
        int group = Undo.GetCurrentGroup();

        foreach (ParallaxEffect p in all)
        {
            if (p == null)
                continue;

            // Bulut katmanları kendi oranlarını taşır (Tools > Lighting > Build Cloud Layers).
            if (p.name.StartsWith("Clouds_"))
                continue;

            // Üst objesinde de parallax varsa bu ikinci kez kayar (çift parallax hatası).
            if (p.transform.parent != null && p.transform.parent.GetComponentInParent<ParallaxEffect>(true) != null)
            {
                sb.AppendLine($"  [SİLİNDİ – çift] {Path(p.transform)}");
                Undo.DestroyObjectImmediate(p);
                removed++;
                continue;
            }

            SpriteRenderer own = p.GetComponent<SpriteRenderer>();
            int order = own != null ? own.sortingOrder : MinChildOrder(p.transform);
            Vector2 v = PresetFor(order);
            bool isGroup = own == null;

            Undo.RecordObject(p, "Parallax Preset");
            p.parallaxX = v.x;
            p.parallaxY = v.y;
            // Gruplar ve ekrana yakın katmanlar başlangıç kamerasına göre, tekil objeler kendi yerine göre.
            p.reference = isGroup || v.x >= 0.75f ? ParallaxEffect.Reference.CameraStart : ParallaxEffect.Reference.PlacedPosition;
            EditorUtility.SetDirty(p);
            changed++;

            sb.AppendLine($"  {Path(p.transform)}  order {order}  ->  X {v.x:0.##}  Y {v.y:0.##}  ({p.reference})");
        }

        Undo.CollapseUndoOperations(group);
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log(sb.Append($"\n{changed} ayarlandı, {removed} çift parallax silindi. Sahneyi kaydet (Ctrl+S).").ToString());
    }

    [MenuItem("Tools/Parallax/Report")]
    private static void Report()
    {
        ParallaxEffect[] all = Object.FindObjectsByType<ParallaxEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var list = new List<ParallaxEffect>(all);
        list.Sort((a, b) => b.parallaxX.CompareTo(a.parallaxX));

        var sb = new StringBuilder($"Parallax ({list.Count}):\n");

        foreach (ParallaxEffect p in list)
        {
            SpriteRenderer own = p.GetComponent<SpriteRenderer>();
            int order = own != null ? own.sortingOrder : MinChildOrder(p.transform);
            sb.AppendLine($"  X {p.parallaxX,5:0.###}  Y {p.parallaxY,5:0.###}  order {order,4}  {p.reference}  {Path(p.transform)}");
        }

        Debug.Log(sb.ToString());
    }


    // ---------- Derinlik modeli ----------

    private static float DepthFor(int order)
    {
        if (order <= -50) return 108f;   // gökyüzüne yakın
        if (order <= -40) return 18f;    // arka çalı şeridi (ufuk)
        if (order <= -30) return 8.7f;   // uzak ağaç
        if (order <= -20) return 5.1f;   // yakın ağaç
        if (order <= -10) return 2.1f;   // zemin arkası çalı
        if (order < 450)  return 0f;     // oyun düzlemi
        return -2.4f;                    // ön plan
    }

    [MenuItem("Tools/Parallax/Apply Depth Model (derinlikten hesapla)")]
    private static void ApplyDepthModel()
    {
        ParallaxEffect[] all = Object.FindObjectsByType<ParallaxEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var sb = new StringBuilder("Derinlik modeli uygulandı (oran = derinlik / (derinlik + odak)):\n");
        int n = 0;

        Undo.SetCurrentGroupName("Apply Parallax Depth Model");
        int group = Undo.GetCurrentGroup();

        foreach (ParallaxEffect p in all)
        {
            if (p == null)
                continue;

            if (p.transform.parent != null && p.transform.parent.GetComponentInParent<ParallaxEffect>(true) != null)
                continue;   // çift parallax: önce Apply Depth Presets silsin

            SpriteRenderer own = p.GetComponent<SpriteRenderer>();
            int order = own != null ? own.sortingOrder : MinChildOrder(p.transform);
            float depth = DepthFor(order);

            if (p.name == "Clouds_Far") depth = 300f;
            else if (p.name == "Clouds_Mid") depth = 108f;
            else if (p.name == "Clouds_Near") depth = 54f;

            Undo.RecordObject(p, "Parallax Depth");
            p.driveFromDepth = true;
            p.focal = 12f;
            p.depth = depth;
            p.yRatio = order >= 450 ? 0.35f : 0.5f;

            float f = depth / (depth + p.focal);
            p.parallaxX = Mathf.Clamp(f, -1f, 1f);
            p.parallaxY = Mathf.Clamp(p.parallaxX * p.yRatio, -1f, 1f);
            EditorUtility.SetDirty(p);
            n++;

            sb.AppendLine($"  {Path(p.transform)}  derinlik {depth:0.#}  ->  X {p.parallaxX:0.##}  Y {p.parallaxY:0.##}");
        }

        Undo.CollapseUndoOperations(group);
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log(sb.Append($"\n{n} katman. Artık her katmanda tek ayar: Depth. Sahneyi kaydet (Ctrl+S).").ToString());
    }

    // ---------- Kenar boşluğu denetimi ----------

    [MenuItem("Tools/Parallax/Check Edge Coverage (kenarda boşluk var mı)")]
    private static void CheckCoverage()
    {
        Camera cam = Camera.main;

        if (cam == null)
        {
            Debug.LogError("Parallax: Main Camera bulunamadı.");
            return;
        }

        Collider2D ground = null;

        foreach (Collider2D c in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
            if (!c.isTrigger && c.name == "Ground" && (ground == null || c.bounds.size.x > ground.bounds.size.x))
                ground = c;

        if (ground == null)
        {
            Debug.LogError("Parallax: 'Ground' collider bulunamadı (kamera aralığı bilinemiyor).");
            return;
        }

        float halfH = cam.orthographic ? cam.orthographicSize : 6f;
        float halfW = halfH * Mathf.Max(1.6f, cam.aspect);
        float camStartX = cam.transform.position.x;
        float cMin = Mathf.Min(camStartX, ground.bounds.min.x + halfW * 0.5f);
        float cMax = Mathf.Max(camStartX, ground.bounds.max.x - halfW * 0.5f);

        var sb = new StringBuilder($"Kenar denetimi: kamera X aralığı {cMin:0.#} .. {cMax:0.#}, görüş yarı genişliği {halfW:0.#}\n");
        int bad = 0, checkedN = 0;

        foreach (ParallaxEffect p in Object.FindObjectsByType<ParallaxEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Bounds b = default;
            bool has = false;

            foreach (SpriteRenderer r in p.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }

            // Sadece geniş şeritler (tek ağaç/çalı değil) kenarı kaplamak zorunda.
            if (!has || b.size.x < halfW * 2f)
                continue;

            checkedN++;
            float refX = p.reference == ParallaxEffect.Reference.PlacedPosition ? p.transform.position.x : camStartX;

            float leftEdgeAtMin = b.min.x + (cMin - refX) * p.parallaxX;
            float rightEdgeAtMax = b.max.x + (cMax - refX) * p.parallaxX;
            float needLeft = cMin - halfW;
            float needRight = cMax + halfW;

            string msg = "";
            if (leftEdgeAtMin > needLeft) msg += $" SOLDA {leftEdgeAtMin - needLeft:0.#} birim boşluk;";
            if (rightEdgeAtMax < needRight) msg += $" SAĞDA {needRight - rightEdgeAtMax:0.#} birim boşluk;";

            if (msg != "")
            {
                bad++;
                sb.AppendLine($"  [BOŞLUK]{msg}  {Path(p.transform)}  (genişlik {b.size.x:0.#}, oran {p.parallaxX:0.##})");
            }
        }

        sb.AppendLine(bad == 0
            ? $"  {checkedN} geniş katman kontrol edildi: kenarda boşluk görünmüyor."
            : $"  {bad} katmanda kenar boşluğu var: katmanı o kadar uzat ya da daha az parallax ver.");
        Debug.Log(sb.ToString());
    }

    // ---------- Ön plan salınımı ----------

    [MenuItem("Tools/Parallax/Setup Foreground Sway (ön plan otları sallan)")]
    private static void SetupForegroundSway()
    {
        int added = 0, existing = 0, skipped = 0;
        Undo.SetCurrentGroupName("Foreground Sway");
        int group = Undo.GetCurrentGroup();

        foreach (SpriteRenderer r in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r.sortingOrder < 450 || r.sortingOrder >= 600)
                continue;

            if (r.GetComponent<ForegroundSway>() != null) { existing++; continue; }

            // Başka bir script bu objeyi zaten oynatıyorsa (ör. ön plan blur kontrolü) dokunma.
            if (r.GetComponent<ParticleSystem>() != null || r.GetComponent<Animator>() != null) { skipped++; continue; }

            Undo.AddComponent<ForegroundSway>(r.gameObject);
            added++;
        }

        Undo.CollapseUndoOperations(group);
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"Ön plan salınımı: {added} obje eklendi, {existing} zaten vardı, {skipped} atlandı. " +
                  "Ayar: her objede ForegroundSway (Angle / Speed / Gustiness). Play'de görünür. Sahneyi kaydet (Ctrl+S).");
    }

    private static int MinChildOrder(Transform t)
    {
        int best = int.MaxValue;

        foreach (SpriteRenderer r in t.GetComponentsInChildren<SpriteRenderer>(true))
            if (r.sortingOrder < best)
                best = r.sortingOrder;

        return best == int.MaxValue ? 0 : best;
    }

    private static string Path(Transform t)
    {
        string s = t.name;

        while (t.parent != null)
        {
            t = t.parent;
            s = t.name + "/" + s;
        }

        return s;
    }
}
#endif
