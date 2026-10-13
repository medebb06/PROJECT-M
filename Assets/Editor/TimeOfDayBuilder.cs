#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Lighting > Build Time Of Day
///   - [ATMOSPHERE] objesine TimeOfDay (saat sistemi) ekler
///   - Gökyüzünü saat rengine göre boyayan ProjectM/Sky materyaline geçirir
///   - Güneş + güneş halesi oluşturur
///   - Sis katmanları (MistLayers) ve ışık tozu (AtmosphereDust) ekler, eski "Dust Motes"u kapatır
/// Tekrar çalıştırmak güvenli: olanı yeniden bağlar, ayarları bozmaz.
/// Kullanım: Hierarchy'de [ATMOSPHERE] > TimeOfDay > Hour kaydırıcısı ya da hazır saat butonları.
/// </summary>
public static class TimeOfDayBuilder
{
    [MenuItem("Tools/Lighting/Build Time Of Day")]
    private static void Build()
    {
        AtmosphereLight al = Object.FindFirstObjectByType<AtmosphereLight>(FindObjectsInactive.Include);

        if (al == null)
        {
            Debug.LogError("Time Of Day: AtmosphereLight bulunamadı. Önce Tools > Lighting > Build Moonlit Atmosphere.");
            return;
        }

        Shader sky = Shader.Find("ProjectM/Sky");
        Shader mist = Shader.Find("ProjectM/Mist");
        Shader pg = Shader.Find("ProjectM/ParticleGlow");
        Shader add = Shader.Find("ProjectM/SpriteAdditive");

        if (sky == null || mist == null || pg == null || add == null)
        {
            Debug.LogError("Time Of Day: Shader bulunamadı (Sky/Mist/ParticleGlow/SpriteAdditive). Console'daki derleme hatalarına bak.");
            return;
        }

        Transform root = al.transform;
        Undo.SetCurrentGroupName("Build Time Of Day");
        int group = Undo.GetCurrentGroup();

        // ---- dokular ----
        Sprite sunSprite = LoadSprite("sun_disc");
        Sprite glowSprite = LoadSprite("glow_soft");
        Sprite whiteSprite = LoadSprite("star_white");
        Texture2D glowTex = glowSprite != null ? glowSprite.texture : null;
        Texture2D noise = LoadTex("water_noise");

        if (sunSprite == null || glowSprite == null || whiteSprite == null || noise == null)
        {
            Debug.LogError("Time Of Day: dokular eksik (sun_disc / glow_soft / star_white / water_noise). Dosyaları Assets/Art altına koyduğundan emin ol.");
            return;
        }

        // ---- gökyüzü ----
        SpriteRenderer skyR = Find<SpriteRenderer>(root, "Sky Gradient");

        if (skyR != null)
        {
            Material ms = GetMat("M_Sky", sky, null);
            Undo.RecordObject(skyR, "Sky");
            skyR.sharedMaterial = ms;
            skyR.color = Color.white;
            EditorUtility.SetDirty(skyR);
        }
        else
            Debug.LogWarning("Time Of Day: 'Sky Gradient' bulunamadı; gökyüzü rengi değişmez.");

        // ---- güneş ----
        SpriteRenderer moon = Find<SpriteRenderer>(root, "Moon");
        Transform parent = moon != null ? moon.transform.parent : root;
        Material mAdd = GetMat("M_Additive", add, m => m.SetFloat("_Intensity", 1f));

        float viewH = 18f;
        Camera cam = Camera.main;
        if (cam != null && cam.orthographic) viewH = cam.orthographicSize * 2f;

        MakeBody(parent, "Sun", sunSprite, mAdd, -182, viewH * 0.12f, moon);
        MakeBody(parent, "Sun Halo", glowSprite, mAdd, -183, viewH * 0.12f * 7f, moon);

        // ---- sis ----
        MistLayers ml = Find<MistLayers>(root, null);

        if (ml == null)
        {
            var go = new GameObject("Mist");
            Undo.RegisterCreatedObjectUndo(go, "Mist");
            go.transform.SetParent(root, false);
            ml = go.AddComponent<MistLayers>();
        }

        Undo.RecordObject(ml, "Mist");
        ml.shader = mist;
        ml.noise = noise;
        ml.quad = whiteSprite;
        if (ml.layers == null || ml.layers.Count == 0) ml.layers = MistLayers.DefaultLayers();
        EditorUtility.SetDirty(ml);

        // ---- ışık tozu ----
        AtmosphereDust ad = Find<AtmosphereDust>(root, null);

        if (ad == null)
        {
            var go = new GameObject("Light Dust");
            Undo.RegisterCreatedObjectUndo(go, "Dust");
            go.transform.SetParent(root, false);
            ad = go.AddComponent<AtmosphereDust>();
        }

        Undo.RecordObject(ad, "Dust");
        ad.shader = pg;
        ad.glow = glowTex;
        EditorUtility.SetDirty(ad);

        // Eski toz parçacıkları kapansın (yenisi var).
        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            if (ps.name == "Dust Motes" && ps.gameObject.activeSelf)
            {
                Undo.RecordObject(ps.gameObject, "Old Dust");
                ps.gameObject.SetActive(false);
            }

        // ---- TimeOfDay ----
        TimeOfDay tod = root.GetComponent<TimeOfDay>();

        if (tod == null)
        {
            tod = Undo.AddComponent<TimeOfDay>(root.gameObject);
            tod.ResetKeys();
        }

        EditorUtility.SetDirty(tod);
        Undo.CollapseUndoOperations(group);
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Selection.activeGameObject = root.gameObject;

        Debug.Log("Time Of Day kuruldu. [" + root.name + "] > TimeOfDay: Hour kaydırıcısı / hazır saat butonları. " +
                  "Play'de , . (hazır saat) ve [ ] (saat) tuşları. Sis: Mist objesi, toz: Light Dust objesi. Sahneyi kaydet (Ctrl+S).");
    }

    private static void MakeBody(Transform parent, string name, Sprite sprite, Material mat, int order, float size, SpriteRenderer like)
    {
        SpriteRenderer sr = Find<SpriteRenderer>(parent, name);

        if (sr == null)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.SetParent(parent, false);
            go.transform.position = like != null ? like.transform.position : parent.position;
            sr = go.AddComponent<SpriteRenderer>();
            float s = size / Mathf.Max(0.01f, sprite.bounds.size.x);
            go.transform.localScale = Vector3.one * s;
        }

        Undo.RecordObject(sr, name);
        sr.sprite = sprite;
        sr.sharedMaterial = mat;
        sr.sortingOrder = order;
        if (like != null) sr.sortingLayerID = like.sortingLayerID;
        sr.color = new Color(1f, 1f, 1f, 0f);
        EditorUtility.SetDirty(sr);
    }

    private static T Find<T>(Transform root, string name) where T : Component
    {
        foreach (T c in root.GetComponentsInChildren<T>(true))
            if (name == null || c.name == name)
                return c;

        return null;
    }

    private static Material GetMat(string name, Shader sh, System.Action<Material> setup)
    {
        string[] guids = AssetDatabase.FindAssets(name + " t:Material");
        Material m = null;

        foreach (string g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (System.IO.Path.GetFileNameWithoutExtension(p) == name) { m = AssetDatabase.LoadAssetAtPath<Material>(p); break; }
        }

        if (m == null)
        {
            m = new Material(sh) { name = name };
            AssetDatabase.CreateAsset(m, "Assets/Art/Atmosphere/Materials/" + name + ".mat");
        }

        m.shader = sh;
        if (setup != null) setup(m);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static string PathOf(string file)
    {
        foreach (string g in AssetDatabase.FindAssets(file + " t:Texture2D"))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (System.IO.Path.GetFileNameWithoutExtension(p) == file) return p;
        }

        return null;
    }

    private static Sprite LoadSprite(string file)
    {
        string p = PathOf(file);
        if (p == null) return null;

        var imp = AssetImporter.GetAtPath(p) as TextureImporter;

        if (imp != null && imp.textureType != TextureImporterType.Sprite)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.alphaIsTransparency = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(p);
    }

    private static Texture2D LoadTex(string file)
    {
        string p = PathOf(file);
        return p == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(p);
    }
}

[CustomEditor(typeof(TimeOfDay))]
public class TimeOfDayEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var t = (TimeOfDay)target;

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Mavi Gece")) Set(t, 23f);
        if (GUILayout.Button("Şafak")) Set(t, 6f);
        if (GUILayout.Button("Gündüz")) Set(t, 12f);
        if (GUILayout.Button("Gün Batımı")) Set(t, 18f);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4);

        DrawDefaultInspector();

        EditorGUILayout.Space(4);
        if (GUILayout.Button("Anahtarları varsayılana sıfırla"))
        {
            Undo.RecordObject(t, "Reset Keys");
            t.ResetKeys();
            EditorUtility.SetDirty(t);
        }
    }

    private static void Set(TimeOfDay t, float h)
    {
        Undo.RecordObject(t, "Time Of Day");
        t.SetHour(h, true);
        EditorUtility.SetDirty(t);
    }
}
#endif
