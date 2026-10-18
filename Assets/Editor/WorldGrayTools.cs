#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > World Gray
///   Apply To Open Scene : oyuncu ve NPC disindaki varsayilan sprite materyallerini (Sprite-Lit/Unlit-Default) gri materyalle degistirir.
///   Revert Open Scene   : geri alir.
/// Ozel shader'li materyallere (SilhouetteRim, Water, ...) dokunmaz; onlar zaten _WorldGray'i okur.
/// </summary>
public static class WorldGrayTools
{
    private const string MatDir = "Assets/Art/Atmosphere/Materials";
    private const string LitName = "Universal Render Pipeline/2D/Sprite-Lit-Default";
    private const string UnlitName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";

    private static Material GetOrCreate(string file, string shaderName)
    {
        string path = MatDir + "/" + file;
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (m != null)
            return m;

        Shader s = Shader.Find(shaderName);

        if (s == null)
            return null;

        m = new Material(s) { name = System.IO.Path.GetFileNameWithoutExtension(file) };
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    private static bool IsCharacter(Renderer r)
    {
        return r.GetComponentInParent<PlayerMovement>(true) != null || r.transform.root.name == "[NPC]";
    }

    private static IEnumerable<SpriteRenderer> SceneSprites()
    {
        foreach (SpriteRenderer sr in Resources.FindObjectsOfTypeAll<SpriteRenderer>())
        {
            if (sr == null || !sr.gameObject.scene.IsValid() || EditorUtility.IsPersistent(sr))
                continue;

            yield return sr;
        }
    }

    [MenuItem("Tools/World Gray/Apply To Open Scene")]
    private static void Apply()
    {
        Material lit = GetOrCreate("M_SpriteLitGray.mat", "ProjectM/SpriteLitGray");
        Material unlit = GetOrCreate("M_SpriteUnlitGray.mat", "ProjectM/SpriteUnlitGray");

        if (lit == null || unlit == null)
        {
            Debug.LogError("World Gray: ProjectM/SpriteLitGray veya SpriteUnlitGray shader'i bulunamadi.");
            return;
        }

        int n = 0, skipped = 0;
        Undo.SetCurrentGroupName("World Gray Apply");
        int group = Undo.GetCurrentGroup();

        foreach (SpriteRenderer sr in SceneSprites())
        {
            Material m = sr.sharedMaterial;

            if (m == null || (m.shader.name != LitName && m.shader.name != UnlitName))
                continue;

            if (IsCharacter(sr)) { skipped++; continue; }

            Undo.RecordObject(sr, "World Gray");
            sr.sharedMaterial = m.shader.name == LitName ? lit : unlit;
            EditorUtility.SetDirty(sr);
            n++;
        }

        Undo.CollapseUndoOperations(group);
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"World Gray: {n} sprite gri materyale gecti, {skipped} karakter sprite'i atlandi. Sahneyi kaydet (Ctrl+S).");
    }

    [MenuItem("Tools/World Gray/Revert Open Scene")]
    private static void Revert()
    {
        Material lit = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/M_SpriteLitGray.mat");
        Material unlit = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/M_SpriteUnlitGray.mat");
        Shader litS = Shader.Find(LitName), unlitS = Shader.Find(UnlitName);
        Material litDef = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
        Material unlitDef = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
        int n = 0;

        foreach (SpriteRenderer sr in SceneSprites())
        {
            if (sr.sharedMaterial == lit && litDef != null) { Undo.RecordObject(sr, "World Gray Revert"); sr.sharedMaterial = litDef; n++; }
            else if (sr.sharedMaterial == unlit && unlitDef != null) { Undo.RecordObject(sr, "World Gray Revert"); sr.sharedMaterial = unlitDef; n++; }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"World Gray: {n} sprite varsayilan materyale dondu.");
    }
}
#endif
