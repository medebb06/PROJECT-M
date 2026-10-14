#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// CharacterGrounding (ayak gölgesi + ayak koyulaştırma) ve SmearLightSpill (smear ışığı) bileşenlerini
/// Player'a KALICI olarak ekler; böylece Inspector'da görünür ve ayarlanabilir.
/// Menü: Tools > Ronni > Add Grounding + Smear Light To Player
/// </summary>
public static class AddGroundingToPlayer
{
    [MenuItem("Tools/Ronni/Add Grounding + Smear Light To Player")]
    public static void Add()
    {
        PlayerController pc = Object.FindFirstObjectByType<PlayerController>();

        if (pc == null)
        {
            Debug.LogWarning("Player bulunamadı.");
            return;
        }

        int added = 0;

        if (pc.GetComponent<CharacterGrounding>() == null) { Undo.AddComponent<CharacterGrounding>(pc.gameObject); added++; }
        if (pc.GetComponent<SmearLightSpill>() == null) { Undo.AddComponent<SmearLightSpill>(pc.gameObject); added++; }

        EditorSceneManager.MarkSceneDirty(pc.gameObject.scene);
        Selection.activeGameObject = pc.gameObject;
        Debug.Log("Player'a " + added + " bileşen eklendi (CharacterGrounding, SmearLightSpill). Sahneyi kaydetmeyi unutma.");
    }
}
#endif
