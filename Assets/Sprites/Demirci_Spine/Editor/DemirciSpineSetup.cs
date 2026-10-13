using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Spine.Unity;

// Menü: Tools > Spine > Demirci'yi Samuray Yap
// Sahnedeki "Demirci" objesinin altına samuray Spine karakterini ekler,
// sprite'ı gizler ve idle'ı oynatır. İkinci kez çalıştırılırsa mevcut olanı seçer.
public static class DemirciSpineSetup
{
    const string Folder = "Assets/Demirci_Spine";
    const string ChildName = "Samuray (Spine)";

    [MenuItem("Tools/Spine/Demirci'yi Samuray Yap")]
    public static void Setup()
    {
        GameObject demirci = GameObject.Find("Demirci");
        if (!demirci)
        {
            EditorUtility.DisplayDialog("Demirci", "Sahnede 'Demirci' adında bir obje bulunamadı.", "Tamam");
            return;
        }

        Transform existing = demirci.transform.Find(ChildName);
        if (existing)
        {
            Selection.activeObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);
            return;
        }

        FixImportSettings();

        SkeletonDataAsset data = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(Folder + "/samuray_SkeletonData.asset");
        if (!data)
        {
            EditorUtility.DisplayDialog("Demirci",
                "samuray_SkeletonData.asset bulunamadı. Unity dosyaları içe aktarmayı bitirince tekrar deneyin.", "Tamam");
            return;
        }

        GameObject child = new GameObject(ChildName);
        Undo.RegisterCreatedObjectUndo(child, "Demirci Spine");
        child.transform.SetParent(demirci.transform, false);
        SkeletonAnimation.AddToGameObject(child, data);

        SpineSpriteSwap swap = child.AddComponent<SpineSpriteSwap>();
        swap.sourceSprite = demirci.GetComponent<SpriteRenderer>();
        swap.skeletonAnimation = child.GetComponent<SkeletonAnimation>();
        swap.FitNow();

        EditorSceneManager.MarkSceneDirty(demirci.scene);
        Selection.activeObject = child;
        Debug.Log("Demirci: samuray Spine karakteri eklendi. Sahneyi kaydetmeyi unutmayın (Ctrl+S).");
    }

    static void FixImportSettings()
    {
        // Doku: renkler soluk görünmesin (sRGB) ve kenarlar temiz olsun.
        var ti = AssetImporter.GetAtPath(Folder + "/samuray.png") as TextureImporter;
        if (ti && (!ti.sRGBTexture || !ti.alphaIsTransparency))
        {
            ti.sRGBTexture = true;
            ti.alphaIsTransparency = true;
            ti.SaveAndReimport();
        }

        // Malzeme: doku düz alfa (straight alpha) olarak hazırlandı.
        var mat = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/samuray_Material.mat");
        if (mat && mat.HasProperty("_StraightAlphaInput"))
        {
            mat.SetFloat("_StraightAlphaInput", 1f);
            mat.EnableKeyword("_STRAIGHT_ALPHA_INPUT");
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
        }
    }
}
