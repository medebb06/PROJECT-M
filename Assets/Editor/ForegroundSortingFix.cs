#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Sorting > Fix Foreground Sorting
///
/// Neden: Ronni'nin parçaları (offset + 0..290) ve etkileşim efektleri aynı 'Default'
/// katmanında çiziliyor. Ön plandaki çalı/ot sprite'larının order'ı (≈90-100) bunun
/// altında kaldığı için player hep önde görünüyor.
///
/// Ne yapar:
///   - Ön plan sprite'larını bulur: 'Foreground' grubunun altındakiler, adı
///     screenbush / Bush_Front / frontbush olanlar ve o an SEÇİLİ objeler.
///   - Hepsini 'Default' katmanına alır (silinmiş/geçersiz katman ID'si varsa düzeltir).
///   - Order'ı, player'ın EN ÜST parçasının üstüne taşır; kendi aralarındaki sırayı korur.
///     (Hasar sayıları / efektler 600+ olduğu için ön plan çalılarının üstünde kalır.)
///
/// Ctrl+Z ile geri alınır. Play modunda çalışmaz.
/// </summary>
public static class ForegroundSortingFix
{
    // Ronni parçaları offset + 0..~300 aralığında. Güvenli pay bırak.
    const int PlayerSpan = 330;
    const int MinBase = 450;
    const int MaxBase = 560; // 600+ olan callout/HUD'un altında kal

    [MenuItem("Tools/Sorting/Fix Foreground Sorting")]
    public static void Fix()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("ForegroundSortingFix: Play modunda çalışmaz.");
            return;
        }

        // Player'ın taban order'ı (Ronni'nin offset'i bundan gelir).
        int playerOffset = 0;
        PlayerController pc = Object.FindAnyObjectByType<PlayerController>();

        if (pc != null && pc.playerSprite != null)
            playerOffset = pc.playerSprite.sortingOrder;

        int baseOrder = Mathf.Clamp(playerOffset + PlayerSpan, MinBase, MaxBase);

        // ---- ön plan sprite'larını topla ----
        HashSet<SpriteRenderer> set = new HashSet<SpriteRenderer>();
        SpriteRenderer[] all = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            SpriteRenderer sr = all[i];

            if (sr == null || (pc != null && sr.transform.IsChildOf(pc.transform)))
                continue;

            if (IsForeground(sr.transform))
                set.Add(sr);
        }

        foreach (GameObject go in Selection.gameObjects)
            foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>(true))
                if (pc == null || !sr.transform.IsChildOf(pc.transform))
                    set.Add(sr);

        if (set.Count == 0)
        {
            Debug.LogWarning("ForegroundSortingFix: Ön plan sprite'ı bulunamadı. Çalıyı Hierarchy'de seçip tekrar çalıştır.");
            return;
        }

        List<SpriteRenderer> list = new List<SpriteRenderer>(set);
        list.Sort((a, b) => a.sortingOrder.CompareTo(b.sortingOrder));

        int minOld = list[0].sortingOrder;
        int defaultId = SortingLayer.NameToID("Default");

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Fix Foreground Sorting");
        int group = Undo.GetCurrentGroup();

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Ön plan sorting düzeltildi (taban order " + baseOrder + ", player offset " + playerOffset + "):");

        for (int i = 0; i < list.Count; i++)
        {
            SpriteRenderer sr = list[i];
            int newOrder = baseOrder + (sr.sortingOrder - minOld);

            Undo.RecordObject(sr, "Foreground sorting");

            string was = SortingLayer.IsValid(sr.sortingLayerID)
                ? SortingLayer.IDToName(sr.sortingLayerID)
                : "GEÇERSİZ(" + sr.sortingLayerID + ")";

            sb.AppendLine("  " + sr.gameObject.name + ":  " + was + "/" + sr.sortingOrder + "  →  Default/" + newOrder);

            sr.sortingLayerID = defaultId;
            sr.sortingOrder = newOrder;
            EditorUtility.SetDirty(sr);
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(list[0].gameObject.scene);

        Debug.Log(sb.ToString());
    }

    static bool IsForeground(Transform t)
    {
        // Adı veya üst gruplarından biri ön plan gibi mi?
        for (Transform p = t; p != null; p = p.parent)
        {
            string n = p.name.ToLowerInvariant();

            if (Regex.IsMatch(n, @"^foreground|screenbush|bush_front|^frontbush"))
                return true;
        }

        return false;
    }
}
#endif
