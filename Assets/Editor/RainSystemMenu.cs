#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Rain > Add Rain System : sahneye kalıcı RainSystem ekler (ayarları Inspector'da düzenlenebilir ve KAYDEDİLİR).
public static class RainSystemMenu
{
    [MenuItem("Tools/Rain/Add Rain System To Scene")]
    public static void Add()
    {
        RainSystem existing = Object.FindFirstObjectByType<RainSystem>();

        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("Sahnede zaten RainSystem var, seçtim.");
            return;
        }

        GameObject go = new GameObject("RainSystem");
        Undo.RegisterCreatedObjectUndo(go, "Add Rain System");
        go.AddComponent<RainSystem>();
        Selection.activeGameObject = go;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("RainSystem eklendi. Inspector'dan her şeyi açıp kapatabilirsin (Play sırasında da canlı çalışır). Play dışında düzenleyip sahneyi kaydet.");
    }
}
#endif
