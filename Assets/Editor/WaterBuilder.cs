#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Water > Build Water  : zeminin (Ground collider'ı) üst çizgisinden aşağısını akışkan, yansıtan suyla kaplar.
/// Tools > Water > Remove Water : suyu kaldırır.
/// Görünüm ayarları: Assets/Art/Water/M_Water (renk, dalga, ay yolu, halkalar).
/// Davranış: Water objesindeki WaterSurface (yansıma çözünürlüğü, halka sıklığı, adım/iniş).
/// </summary>
public static class WaterBuilder
{
    private const string Dir = "Assets/Art/Water";

    [MenuItem("Tools/Water/Build Water")]
    private static void Build()
    {
        Shader sh = Shader.Find("ProjectM/Water");

        if (sh == null)
        {
            Debug.LogError("Water: ProjectM/Water shader bulunamadı (derleme hatası?). " + Dir + "/Water.shader var mı?");
            return;
        }

        Texture2D noise = SetupTexture(Dir + "/water_noise.png", false);
        SetupTexture(Dir + "/water_white.png", true);
        Sprite white = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + "/water_white.png");

        if (noise == null || white == null)
        {
            Debug.LogError("Water: " + Dir + " içinde water_noise.png / water_white.png bulunamadı.");
            return;
        }

        // ---- materyal ----
        string matPath = Dir + "/M_Water.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

        // Her kurulumda varsayılan ayarlarla yeniden oluştur (eski denemelerin değerleri kalmasın).
        if (mat != null)
            AssetDatabase.DeleteAsset(matPath);

        mat = new Material(sh) { name = "M_Water" };
        AssetDatabase.CreateAsset(mat, matPath);
        mat.SetTexture("_NoiseTex", noise);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        // ---- zemin ----
        Collider2D ground = FindGround();

        if (ground == null)
        {
            Debug.LogError("Water: 'Ground' isimli, collider'lı zemin bulunamadı.");
            return;
        }

        float waterY = ground.bounds.max.y;
        SpriteRenderer gsr = ground.GetComponent<SpriteRenderer>();
        Bounds gb = gsr != null ? gsr.bounds : ground.bounds;

        float left = gb.min.x - 30f;
        float right = gb.max.x + 30f;
        float bottom = Mathf.Min(gb.min.y, waterY - 30f);

        // ---- eskisini kaldır ----
        RemoveExisting();

        // ---- su objesi ----
        var go = new GameObject("Water");
        Undo.RegisterCreatedObjectUndo(go, "Build Water");

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = white;
        sr.sharedMaterial = mat;
        sr.sortingLayerID = gsr != null ? gsr.sortingLayerID : 0;
        sr.sortingOrder = (gsr != null ? gsr.sortingOrder : 10) + 1;

        Vector2 spriteSize = white.bounds.size;
        float w = right - left;
        float h = waterY - bottom;

        go.transform.position = new Vector3((left + right) * 0.5f, waterY - h * 0.5f, 0f);
        go.transform.localScale = new Vector3(w / spriteSize.x, h / spriteSize.y, 1f);

        if (ground.transform.parent != null)
            go.transform.SetParent(ground.transform.parent, true);

        var ws = go.AddComponent<WaterSurface>();
        ws.moon = FindMoon();
        ws.splashTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Atmosphere/glow_soft.png");

        Selection.activeGameObject = go;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);

        Debug.Log($"Water kuruldu: su çizgisi Y = {waterY:0.##} ('{ground.name}' collider'ının üstü), genişlik {w:0}, " +
                  $"ay = {(ws.moon != null ? ws.moon.name : "yok (ışık konumu kullanılır)")}. " +
                  "Görünüm: Assets/Art/Water/M_Water. Sahneyi kaydet (Ctrl+S).");
    }

    // Ön plan bitkileri yarı saydam olunca arkalarından su görünüyor: tam opak yap.
    [MenuItem("Tools/Water/Make Foreground Opaque")]
    private static void MakeForegroundOpaque()
    {
        int n = 0;

        foreach (SpriteRenderer r in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r.sortingOrder < 450 || r.sortingOrder >= 600 || r.color.a >= 0.999f)
                continue;

            Undo.RecordObject(r, "Make Foreground Opaque");
            Color c = r.color;
            c.a = 1f;
            r.color = c;
            EditorUtility.SetDirty(r);
            n++;
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"Water: {n} ön plan bitkisi tam opak yapıldı. Sahneyi kaydet (Ctrl+S).");
    }

    [MenuItem("Tools/Water/Remove Water")]
    private static void Remove()
    {
        RemoveExisting();
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
    }

    private static void RemoveExisting()
    {
        foreach (WaterSurface w in Object.FindObjectsByType<WaterSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Undo.DestroyObjectImmediate(w.gameObject);
    }

    private static Collider2D FindGround()
    {
        Collider2D best = null;

        foreach (Collider2D c in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
        {
            if (c.isTrigger || c.name != "Ground")
                continue;

            if (best == null || c.bounds.size.x > best.bounds.size.x)
                best = c;
        }

        return best;
    }

    private static Transform FindMoon()
    {
        Transform best = null;

        foreach (SpriteRenderer r in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
        {
            string n = r.name.ToLowerInvariant();

            if (!n.Contains("moon") && !n.Contains("ay_"))
                continue;

            if (n.Contains("glow") || n.Contains("halo"))
                continue;

            if (best == null || r.name.Length < best.name.Length)
                best = r.transform;
        }

        return best;
    }

    private static Texture2D SetupTexture(string path, bool sprite)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;

        if (imp == null)
            return null;

        bool changed = false;

        if (sprite)
        {
            if (imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; changed = true; }
            if (!Mathf.Approximately(imp.spritePixelsPerUnit, 4f)) { imp.spritePixelsPerUnit = 4f; changed = true; }
        }
        else
        {
            if (imp.textureType != TextureImporterType.Default) { imp.textureType = TextureImporterType.Default; changed = true; }
            if (imp.sRGBTexture) { imp.sRGBTexture = false; changed = true; }
            if (imp.wrapMode != TextureWrapMode.Repeat) { imp.wrapMode = TextureWrapMode.Repeat; changed = true; }
        }

        if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
        if (imp.filterMode != FilterMode.Bilinear) { imp.filterMode = FilterMode.Bilinear; changed = true; }

        if (changed)
            imp.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
#endif
