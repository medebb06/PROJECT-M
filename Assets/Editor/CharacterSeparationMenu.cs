#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Lighting > Character Separation
///   Apply  : rim'i ay ışığı rengine çevirir, arka kenara soğuk kontur açar, karakterin arkasına yumuşak aydınlık leke (CharacterBackdrop) ekler.
///   Remove : leke bileşenini kaldırır (rim değerleri el ile geri alınır).
/// </summary>
public static class CharacterSeparationMenu
{
    const string GlowPath = "Assets/Art/Atmosphere/Textures/glow_soft.png";
    const string AddMatPath = "Assets/Art/Atmosphere/Materials/M_Additive.mat";

    [MenuItem("Tools/Lighting/Character Separation/Apply")]
    public static void Apply()
    {
        int rimCount = 0;

        foreach (CharacterRim rim in Object.FindObjectsByType<CharacterRim>(FindObjectsSortMode.None))
        {
            Undo.RecordObject(rim, "Character Separation");
            // Sıcak turuncu yerine soğuk ay ışığı: kırmızı haori sahnenin tek sıcak rengi olarak kalır.
            rim.color = new Color(0.62f, 0.76f, 1f, 1f);
            rim.innerWidth = 0.06f;
            rim.innerIntensity = 1.2f;
            rim.outerWidth = 0.13f;
            rim.outerIntensity = 0.4f;
            rim.upBias = 0.8f;
            rim.moonRim = true;
            rim.moonColor = new Color(0.55f, 0.72f, 1f, 1f);
            rim.moonWidth = 0.05f;
            rim.moonIntensity = 1.0f;
            EditorUtility.SetDirty(rim);
            rimCount++;
        }

        Sprite glow = AssetDatabase.LoadAssetAtPath<Sprite>(GlowPath);
        Material add = AssetDatabase.LoadAssetAtPath<Material>(AddMatPath);

        if (glow == null || add == null)
        {
            Debug.LogWarning("Character Separation: glow_soft sprite'ı veya M_Additive bulunamadı (" + GlowPath + " / " + AddMatPath + ").");
            return;
        }

        PlayerController pc = Object.FindFirstObjectByType<PlayerController>();

        if (pc == null)
        {
            Debug.LogWarning("Character Separation: sahnede PlayerController yok.");
            return;
        }

        CharacterBackdrop bd = pc.GetComponent<CharacterBackdrop>();

        if (bd == null)
            bd = Undo.AddComponent<CharacterBackdrop>(pc.gameObject);

        Undo.RecordObject(bd, "Character Separation");
        bd.glowSprite = glow;
        bd.additiveMaterial = add;
        bd.color = new Color(0.45f, 0.58f, 0.85f, 1f);
        bd.intensity = 0.6f;
        bd.size = new Vector2(7.5f, 8.5f);
        EditorUtility.SetDirty(bd);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pc.gameObject.scene);
        Debug.Log("Character Separation: " + rimCount + " rim güncellendi, arka plan lekesi " + pc.name + " üzerine eklendi. Play'e basıp bak; ince ayar: CharacterBackdrop > intensity / size.");
    }

    [MenuItem("Tools/Lighting/Character Separation/Remove Backdrop")]
    public static void Remove()
    {
        foreach (CharacterBackdrop bd in Object.FindObjectsByType<CharacterBackdrop>(FindObjectsSortMode.None))
            Undo.DestroyObjectImmediate(bd);
    }
}
#endif
