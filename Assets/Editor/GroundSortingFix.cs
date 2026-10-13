#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Lighting > Fix Ground Sorting
/// Sorun: karakterin parçaları (ayak, boyun...) order 10-12 aralığında; zemin (10), su (11), zemin ışığı (12)
/// ile AYNI order'da olduğundan eşitlikte hangisinin üstte çizileceği belirsizdi -> koşarken bir ayak zeminin arkasında kalıyordu.
/// Çözüm: zemin / su / zemin ışığı / gölge / su sisi karakterin çok ALTINA alınır (aralarındaki sıra korunur).
///   Zemin 10 -> -5, Su -> -4, Zemin ışığı -> -3, Su sisi -> 0, Gölge 15 -> 5
/// Karakter (10+), düşmanlar ve efektler dokunulmadan kalır.
/// </summary>
public static class GroundSortingFix
{
    [MenuItem("Tools/Lighting/Fix Ground Sorting (ayak zeminin arkasında kalıyor)")]
    private static void Fix()
    {
        var sb = new StringBuilder("Zemin sıralaması düzeltildi:\n");
        int n = 0;

        foreach (SpriteRenderer r in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            int to = int.MinValue;

            if (r.name == "Ground" && r.sortingOrder >= 5 && r.sortingOrder <= 19) to = -5;
            else if (r.name == "Water" && r.sortingOrder >= 5 && r.sortingOrder <= 19) to = -4;
            else if (r.name == "Ground Light" && r.sortingOrder >= 5 && r.sortingOrder <= 19) to = -3;

            if (to == int.MinValue)
                continue;

            Undo.RecordObject(r, "Fix Ground Sorting");
            sb.AppendLine($"  {r.name}: {r.sortingOrder} -> {to}");
            r.sortingOrder = to;
            EditorUtility.SetDirty(r);
            n++;
        }

        foreach (ProjectedShadow ps in Object.FindObjectsByType<ProjectedShadow>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (ps.sortingOrder < 5 || ps.sortingOrder > 19)
                continue;

            Undo.RecordObject(ps, "Fix Ground Sorting");
            sb.AppendLine($"  ProjectedShadow ({ps.name}): {ps.sortingOrder} -> 5");
            ps.sortingOrder = 5;
            EditorUtility.SetDirty(ps);
            n++;
        }

        foreach (MistLayers ml in Object.FindObjectsByType<MistLayers>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (ml.layers == null)
                continue;

            Undo.RecordObject(ml, "Fix Ground Sorting");

            foreach (MistLayers.LayerCfg c in ml.layers)
                if (c.followWater && c.sortingOrder >= 5 && c.sortingOrder <= 19)
                {
                    sb.AppendLine($"  Su sisi: {c.sortingOrder} -> 0");
                    c.sortingOrder = 0;
                    n++;
                }

            EditorUtility.SetDirty(ml);
        }

        // Karakterin katmanı: geçersiz kimlikli sorting layer'ı Default'a al.
        foreach (SpriteRenderer r in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r.sortingOrder < 0 || r.sortingOrder > 60 || SortingLayer.IsValid(r.sortingLayerID))
                continue;

            Undo.RecordObject(r, "Fix Sorting Layer");
            sb.AppendLine($"  {r.name}: geçersiz sorting layer ({r.sortingLayerID}) -> Default");
            r.sortingLayerID = 0;
            EditorUtility.SetDirty(r);
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log(sb.Append($"{n} değişiklik. Sahneyi kaydet (Ctrl+S).").ToString());
    }
}
#endif
