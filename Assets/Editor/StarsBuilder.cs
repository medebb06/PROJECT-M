#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Lighting > Add Stars    : gökyüzüne (SkyAnchor altına) yıldız katmanı ekler.
/// Tools > Lighting > Remove Stars : kaldırır.
/// Görünüm: Assets/Art/Atmosphere/M_Stars (sıklık, boyut, parlaklık, pırıldama, ufuk sönmesi).
/// </summary>
public static class StarsBuilder
{
    private const string Dir = "Assets/Art/Atmosphere";

    [MenuItem("Tools/Lighting/Add Stars")]
    private static void Add()
    {
        Shader sh = Shader.Find("ProjectM/Stars");

        if (sh == null)
        {
            Debug.LogError("Stars: ProjectM/Stars shader bulunamadı (derleme hatası?). Assets/Art/Atmosphere/Stars.shader var mı?");
            return;
        }

        string texPath = Dir + "/star_white.png";
        var imp = AssetImporter.GetAtPath(texPath) as TextureImporter;

        if (imp == null)
        {
            Debug.LogError("Stars: " + texPath + " bulunamadı.");
            return;
        }

        if (imp.textureType != TextureImporterType.Sprite || !Mathf.Approximately(imp.spritePixelsPerUnit, 4f))
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spritePixelsPerUnit = 4f;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }

        Sprite white = AssetDatabase.LoadAssetAtPath<Sprite>(texPath);

        Transform anchor = null;
        Transform skyGradient = null;

        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name.StartsWith("SkyAnchor")) anchor = t;
            if (t.name == "Sky Gradient") skyGradient = t;
        }

        if (anchor == null)
        {
            Debug.LogError("Stars: SkyAnchor bulunamadı. Önce Tools > Lighting > Build Moonlit Atmosphere.");
            return;
        }

        Remove();

        string matPath = Dir + "/M_Stars.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

        if (mat == null)
        {
            mat = new Material(sh) { name = "M_Stars" };
            AssetDatabase.CreateAsset(mat, matPath);
        }

        mat.shader = sh;
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        var go = new GameObject("Stars");
        Undo.RegisterCreatedObjectUndo(go, "Add Stars");
        go.transform.SetParent(anchor, false);
        go.transform.localPosition = Vector3.zero;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = white;
        sr.sharedMaterial = mat;

        SpriteRenderer sky = skyGradient != null ? skyGradient.GetComponent<SpriteRenderer>() : null;
        sr.sortingLayerID = sky != null ? sky.sortingLayerID : 0;
        // Gökyüzü gradyanının hemen üstü; ay hâlesi, bulutlar ve hepsi yıldızların önünde kalır.
        sr.sortingOrder = (sky != null ? sky.sortingOrder : -200) + 5;

        Vector2 s = white.bounds.size;
        go.transform.localScale = new Vector3(70f / s.x, 45f / s.y, 1f);

        Selection.activeGameObject = go;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log("Stars eklendi (SkyAnchor/Stars). Ayarlar: Assets/Art/Atmosphere/M_Stars. Sahneyi kaydet (Ctrl+S).");
    }

    [MenuItem("Tools/Lighting/Remove Stars")]
    private static void Remove()
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t != null && t.name == "Stars" && t.parent != null && t.parent.name.StartsWith("SkyAnchor"))
                Undo.DestroyObjectImmediate(t.gameObject);
    }
}
#endif
