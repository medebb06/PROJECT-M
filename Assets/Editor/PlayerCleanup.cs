#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Cleanup Player (Remove Old Spine Model)
///
///  1. Eski Spine modelinin ('Spine GameObject (ronin_samuray)') CapsuleCollider2D'sini
///     Player'ın köküne taşır (aynı dünya boyutu/konumu, aynı ayarlar).
///  2. Eski Spine objesini siler.
///
/// Ronni modeli, ModelPivot üzerindeki ESKİ Animator'ı (Attack1-4, Parry state'leri)
/// okuyarak çalışır ve PlayerController.playerSprite'ı gizler; bunlar bilerek DURUR.
/// Ctrl+Z ile geri alınır. Play modunda çalışmaz.
/// </summary>
public static class PlayerCleanup
{
    [MenuItem("Tools/Cleanup Player (Remove Old Spine Model)")]
    public static void Run()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("PlayerCleanup: Play modunda çalışmaz.");
            return;
        }

        PlayerController pc = Object.FindAnyObjectByType<PlayerController>();

        if (pc == null)
        {
            Debug.LogWarning("PlayerCleanup: Sahnede PlayerController yok.");
            return;
        }

        Transform root = pc.transform;

        // Eski Spine objesini bul: SkeletonAnimation bileşeni olan çocuk.
        Transform spine = null;

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root)
                continue;

            Component[] cs = t.GetComponents<Component>();

            for (int i = 0; i < cs.Length; i++)
            {
                if (cs[i] != null && cs[i].GetType().Name == "SkeletonAnimation")
                {
                    spine = t;
                    break;
                }
            }

            if (spine != null)
                break;
        }

        if (spine == null)
        {
            Debug.Log("PlayerCleanup: Eski Spine objesi zaten yok, yapılacak bir şey yok.");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Cleanup Player");
        int group = Undo.GetCurrentGroup();

        // ---- 1. Collider'ı köke taşı ----
        CapsuleCollider2D oldCol = spine.GetComponent<CapsuleCollider2D>();
        CapsuleCollider2D rootCol = root.GetComponent<CapsuleCollider2D>();

        if (oldCol != null && rootCol == null && root.GetComponent<Collider2D>() == null)
        {
            CapsuleCollider2D nc = Undo.AddComponent<CapsuleCollider2D>(root.gameObject);

            nc.direction = oldCol.direction;
            nc.isTrigger = oldCol.isTrigger;
            nc.sharedMaterial = oldCol.sharedMaterial;
            nc.usedByEffector = oldCol.usedByEffector;

            // Child yerel uzayından dünya uzayına, oradan Player yerel uzayına.
            Vector3 worldCenter = spine.TransformPoint(oldCol.offset);
            nc.offset = root.InverseTransformPoint(worldCenter);

            Vector3 cs = spine.lossyScale;
            Vector3 rs = root.lossyScale;

            nc.size = new Vector2(
                oldCol.size.x * Mathf.Abs(rs.x) > 0f ? oldCol.size.x * Mathf.Abs(cs.x) / Mathf.Abs(rs.x) : oldCol.size.x,
                oldCol.size.y * Mathf.Abs(rs.y) > 0f ? oldCol.size.y * Mathf.Abs(cs.y) / Mathf.Abs(rs.y) : oldCol.size.y
            );

            Debug.Log("PlayerCleanup: CapsuleCollider2D köke taşındı (size " + nc.size + ", offset " + nc.offset + ").");
        }
        else if (oldCol != null)
        {
            Debug.Log("PlayerCleanup: Köke collider eklenmedi (kökte zaten collider var).");
        }

        // ---- 2. Eski Spine objesini sil ----
        string spineName = spine.name;

        try
        {
            Undo.DestroyObjectImmediate(spine.gameObject);
            Debug.Log("PlayerCleanup: '" + spineName + "' silindi.");
        }
        catch (System.Exception e)
        {
            // Prefab örneğinin çocuğu script ile silinemiyorsa: kapat, elle silmeyi söyle.
            Undo.RecordObject(spine.gameObject, "Disable old spine");
            spine.gameObject.SetActive(false);

            Debug.LogWarning(
                "PlayerCleanup: '" + spineName + "' script ile silinemedi (" + e.Message + "). " +
                "Kapattım. Hierarchy'de seçip Delete'e basarak sil."
            );
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);

        Debug.Log("PlayerCleanup bitti. Play'e basıp player'ın zeminde durduğunu ve düşmana çarptığını kontrol et.");
    }
}
#endif
