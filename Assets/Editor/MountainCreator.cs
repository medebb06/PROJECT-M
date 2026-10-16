#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Background > Add Mountains : sahneye MountainBackdrop ekler (sprite'lar otomatik atanir).
public static class MountainCreator
{
    [MenuItem("Tools/Background/Add Mountains")]
    public static void Add()
    {
        MountainBackdrop existing = Object.FindAnyObjectByType<MountainBackdrop>();
        if (existing != null) { Selection.activeGameObject = existing.gameObject; Debug.Log("Sahnede zaten MountainBackdrop var."); return; }

        GameObject go = new GameObject("Mountains");
        Undo.RegisterCreatedObjectUndo(go, "Add Mountains");
        MountainBackdrop mb = go.AddComponent<MountainBackdrop>();

        if (mb.baseSprite == null)
            Debug.LogWarning("Mountain sprite'lari atanamadi: Assets/Sprites/background/Mountains/ altindaki PNG'lerin Sprite olarak import edildiginden emin ol.");

        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
}
#endif
