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
