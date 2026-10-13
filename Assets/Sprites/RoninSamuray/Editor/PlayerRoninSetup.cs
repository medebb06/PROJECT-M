using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Spine.Unity;

// Menü: Tools > Spine > Player'a Yeni Ronin'i Uygula
// Player'daki RoninSpineVisual objesinin Spine verisini yeni ronin (ronin_samuray) ile değiştirir.
public static class PlayerRoninSetup
{
    const string Folder = "Assets/RoninSamuray";
    const float SkeletonScale = 0.0025f;
    const float ArtHeightPixels = 1581f;   // at kuyruğu ucundan ayak altına

    [MenuItem("Tools/Spine/Player'a Yeni Ronin'i Uygula")]
    public static void Apply()
    {
        FixImportSettings();

        SkeletonDataAsset data = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(Folder + "/ronin_samuray_SkeletonData.asset");
        if (!data)
        {
            EditorUtility.DisplayDialog("Ronin", "ronin_samuray_SkeletonData.asset bulunamadı. Unity içe aktarmayı bitirince tekrar deneyin.", "Tamam");
            return;
        }
        if (Mathf.Abs(data.scale - SkeletonScale) > 0.00001f)
        {
            data.scale = SkeletonScale;
            data.Clear();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }

        RoninSpineVisual visual = Object.FindFirstObjectByType<RoninSpineVisual>(FindObjectsInactive.Include);
        if (!visual)
        {
            EditorUtility.DisplayDialog("Ronin", "Sahnede RoninSpineVisual bulunamadı (Player > ModelPivot altındaki Spine objesi).", "Tamam");
            return;
        }

        Undo.RecordObject(visual, "Yeni Ronin");
        SkeletonAnimation anim = visual.GetComponent<SkeletonAnimation>();
        SkeletonRenderer rend = visual.GetComponent<SkeletonRenderer>();
        if (anim) Undo.RecordObject(anim, "Yeni Ronin");
        if (rend) Undo.RecordObject(rend, "Yeni Ronin");

        if (anim) anim.SkeletonDataAsset = data;
        if (rend) rend.Initialize(true);
        if (anim) anim.Initialize(true);

        visual.idleAnimation = "idle";
        visual.runAnimation = "idle";        // bu karakterde şimdilik sadece idle var
        visual.fallbackAnimation = "idle";
        visual.roninArtHeight = ArtHeightPixels * SkeletonScale;
        visual.artFacesLeft = true;
        visual.gameObject.name = "Spine GameObject (ronin_samuray)";
        EditorUtility.SetDirty(visual);
        if (anim) EditorUtility.SetDirty(anim);
        if (rend) EditorUtility.SetDirty(rend);

        EditorSceneManager.MarkSceneDirty(visual.gameObject.scene);
        Selection.activeObject = visual.gameObject;
        Debug.Log("Player: yeni ronin uygulandı. Sahneyi kaydetmeyi unutmayın (Ctrl+S).");
    }

    static void FixImportSettings()
    {
        var ti = AssetImporter.GetAtPath(Folder + "/ronin_samuray.png") as TextureImporter;
        if (ti && (!ti.sRGBTexture || !ti.alphaIsTransparency))
        {
            ti.sRGBTexture = true;
            ti.alphaIsTransparency = true;
            ti.SaveAndReimport();
        }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/ronin_samuray_Material.mat");
        if (mat && mat.HasProperty("_StraightAlphaInput"))
        {
            mat.SetFloat("_StraightAlphaInput", 1f);
            mat.EnableKeyword("_STRAIGHT_ALPHA_INPUT");
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
        }
    }
}
