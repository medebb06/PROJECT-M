using UnityEditor;
using UnityEngine;
using System.Linq;

/// <summary>
/// Prop yerlestirme araci: Tools > Props > Prop Placer
/// - Project'te sprite sec -> 'Sahneye koy' : sahne gorunumunun ortasina dogru materyal/order/parallax ile koyar
/// - Sahnede obje sec -> 'Ayari uygula'     : materyal, order, parallax'i standartlara cevirir
/// - 'Prefab uret'                           : Match/ ve Toned/ klasorlerindeki tum proplar icin hazir prefab (Assets/Prefabs/Props)
/// </summary>
public class PropPlacer : EditorWindow
{
    const string MatPath = "Assets/Art/Atmosphere/Materials/M_SpriteLitGray.mat";
    Material mat;
    float depth = 2.1f;
    bool autoOrder = true;
    int orderOverride = -15;
    string groupName = "";

    [MenuItem("Tools/Props/Prop Placer")]
    static void Open() { GetWindow<PropPlacer>("Prop Placer"); }

    void OnEnable() { mat = FindMat(); }

    static Material FindMat()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (m != null) return m;
        foreach (var g in AssetDatabase.FindAssets("M_SpriteLitGray t:Material"))
            return AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
        return null;
    }

    static int AutoOrder(string n)
    {
        n = n.ToLower();
        if (n.Contains("log") || n.Contains("stump")) return -16;
        if (n.Contains("fore")) return 5;
        return -15;
    }

    void OnGUI()
    {
        mat = (Material)EditorGUILayout.ObjectField("Materyal", mat, typeof(Material), false);
        depth = EditorGUILayout.FloatField("Parallax derinligi", depth);
        autoOrder = EditorGUILayout.Toggle("Order otomatik (log/stump -16, tas -15)", autoOrder);
        if (!autoOrder) orderOverride = EditorGUILayout.IntField("Sorting order", orderOverride);
        groupName = EditorGUILayout.TextField("Ust obje adi (bos = otomatik)", groupName);
        EditorGUILayout.Space();

        if (GUILayout.Button("Secili sprite'lari sahneye koy"))
            PlaceSelected();
        if (GUILayout.Button("Secili sahne objelerine ayari uygula"))
            ApplyToSelected();
        if (GUILayout.Button("Prefab uret (Match + Toned)"))
            MakePrefabs();
    }

    string GroupFor(string spriteName)
    {
        if (!string.IsNullOrEmpty(groupName)) return groupName;
        string n = spriteName.ToLower();
        if (n.Contains("log")) return "Logs";
        if (n.Contains("stump")) return "Stumps";
        if (n.Contains("rock")) return "ROCKS";
        return "Props";
    }

    Transform Group(string name)
    {
        var g = GameObject.Find(name);
        if (g == null) { g = new GameObject(name); Undo.RegisterCreatedObjectUndo(g, "Group"); }
        return g.transform;
    }

    void Setup(GameObject go, string nameHint)
    {
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) return;
        Undo.RecordObject(sr, "Prop ayari");
        if (mat != null) sr.sharedMaterial = mat;
        sr.color = Color.white;
        sr.sortingOrder = autoOrder ? AutoOrder(nameHint) : orderOverride;
        var pe = go.GetComponent<ParallaxEffect>();
        if (pe == null) pe = Undo.AddComponent<ParallaxEffect>(go);
        pe.reference = ParallaxEffect.Reference.PlacedPosition;
        pe.driveFromDepth = true;
        pe.depth = depth;
        pe.focal = 12f;
        pe.yRatio = 0.5f;
        float d = pe.depth + pe.focal;
        pe.parallaxX = Mathf.Clamp(pe.depth / d, -1f, 1f);
        pe.parallaxY = Mathf.Clamp(pe.parallaxX * pe.yRatio, -1f, 1f);
        EditorUtility.SetDirty(go);
    }

    void PlaceSelected()
    {
        var sprites = Selection.objects.OfType<Sprite>().ToList();
        foreach (var t in Selection.objects.OfType<Texture2D>())
        {
            var s = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(t)).OfType<Sprite>().FirstOrDefault();
            if (s != null) sprites.Add(s);
        }
        if (sprites.Count == 0) { ShowNotification(new GUIContent("Project'te sprite sec")); return; }

        Vector3 c = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        float x = c.x - (sprites.Count - 1) * 2f;
        GameObject last = null;
        foreach (var s in sprites)
        {
            var go = new GameObject(s.name);
            Undo.RegisterCreatedObjectUndo(go, "Prop yerlestir");
            go.AddComponent<SpriteRenderer>().sprite = s;
            go.transform.SetParent(Group(GroupFor(s.name)), true);
            go.transform.position = new Vector3(x, c.y, 0f);
            Setup(go, s.name);
            x += 4f;
            last = go;
        }
        Selection.activeGameObject = last;
    }

    void ApplyToSelected()
    {
        foreach (var go in Selection.gameObjects)
        {
            if (go.scene.IsValid() && go.GetComponent<SpriteRenderer>() != null)
                Setup(go, go.name);
        }
    }

    void MakePrefabs()
    {
        const string outDir = "Assets/Prefabs/Props";
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(outDir)) AssetDatabase.CreateFolder("Assets/Prefabs", "Props");
        int n = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Sprites/Props/Match", "Assets/Sprites/Props/Toned" }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            string nm = System.IO.Path.GetFileNameWithoutExtension(p);
            if (nm.StartsWith("Bank") || nm.StartsWith("Mound")) continue;
            var s = AssetDatabase.LoadAllAssetsAtPath(p).OfType<Sprite>().FirstOrDefault();
            if (s == null) continue;
            var go = new GameObject(nm);
            go.AddComponent<SpriteRenderer>().sprite = s;
            Setup(go, nm);
            PrefabUtility.SaveAsPrefabAsset(go, outDir + "/" + nm + ".prefab");
            DestroyImmediate(go);
            n++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Prop prefab uretildi: " + n);
        try { ShowNotification(new GUIContent(n + " prefab uretildi")); } catch { }
    }

    // Menu uzerinden dogrudan (pencere acmadan)
    [MenuItem("Tools/Props/Prefab uret (Match + Toned)")]
    static void MakePrefabsMenu() { var w = CreateInstance<PropPlacer>(); w.mat = FindMat(); w.MakePrefabs(); DestroyImmediate(w); }
}
