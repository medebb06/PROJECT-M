#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Açık sahnenin hiyerarşisini düzenler: kökteki objeleri isimlerine/bileşenlerine göre
/// gruplara toplar, dağınık çalı/ağaç/bulut gruplarını birleştirip düzgün isimlendirir.
///
///   Tools > Organize Hierarchy (Preview)   : sadece ne yapacağını Console'a yazar
///   Tools > Organize Hierarchy             : uygular (Ctrl+Z ile geri alınır)
///
/// Konumlar korunur (worldPositionStays). Gruplar (0,0,0)'dadır.
/// Tanıyamadığı objelere dokunmaz, Console'da listeler.
/// </summary>
public static class HierarchyOrganizer
{
    const string SYS = "[SYSTEMS]";
    const string CAM = "[CAMERAS]";
    const string LIGHT = "[LIGHTING]";
    const string PLAYER = "[PLAYER]";
    const string ENEMY = "[ENEMIES]";
    const string NPC = "[NPC]";
    const string WORLD = "[WORLD]";

    static readonly string[] TopOrder = { SYS, CAM, LIGHT, PLAYER, ENEMY, NPC, WORLD };

    // WORLD içindeki alt grupların sırası (arkadan öne).
    static readonly string[] WorldOrder =
    {
        "Sky", "Clouds", "Background", "Trees", "Bushes_Back",
        "Ground", "Bushes_Ground", "Props", "Foreground"
    };

    // Alt grup -> çocukların isim öneki. (null = çocukları yeniden isimlendirme)
    static readonly Dictionary<string, string> ChildPrefix = new Dictionary<string, string>
    {
        { "Clouds", "Cloud" },
        { "Background", "Back_Layer" },
        { "Trees", "Tree" },
        { "Bushes_Back", "Bush_Back" },
        { "Bushes_Ground", "Bush_Ground" },
        { "Foreground", "Bush_Front" },
    };

    static StringBuilder log;
    static bool apply;

    [MenuItem("Tools/Organize Hierarchy (Preview)")]
    public static void Preview() { Run(false); }

    [MenuItem("Tools/Organize Hierarchy")]
    public static void Apply() { Run(true); }

    // =========================================================

    static void Run(bool doApply)
    {
        apply = doApply;
        log = new StringBuilder();

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
            return;

        if (Application.isPlaying)
        {
            Debug.LogWarning("HierarchyOrganizer: Play modunda çalışmaz. Önce Play'i durdur.");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Organize Hierarchy");
        int undoGroup = Undo.GetCurrentGroup();

        List<GameObject> roots = new List<GameObject>(scene.GetRootGameObjects());
        List<string> unknown = new List<string>();

        // Konteyner (içinde sprite olmayan, çocuklu) objeleri iki geçişte işle:
        // önce script'li olanlar alt grup olarak benimsenir, sonra düz olanlar birleştirilir.
        List<GameObject> plainContainers = new List<GameObject>();

        // ---- 1. geçiş: sınıflandır ----
        List<KeyValuePair<GameObject, string>> plan = new List<KeyValuePair<GameObject, string>>();

        for (int i = 0; i < roots.Count; i++)
        {
            GameObject go = roots[i];

            if (IsGroupRoot(go.name))
                continue;

            string cat = Classify(go);

            if (cat == null)
            {
                unknown.Add(go.name);
                continue;
            }

            plan.Add(new KeyValuePair<GameObject, string>(go, cat));
        }

        // Script'li konteynerler önce (alt grup olarak benimsenir).
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < plan.Count; i++)
            {
                GameObject go = plan[i].Key;
                string cat = plan[i].Value;

                if (go == null)
                    continue;

                bool world = cat.StartsWith(WORLD + "/");
                bool container = world && IsContainer(go);
                bool plain = container && IsPlain(go);

                if (pass == 0 && !(container && !plain))
                    continue;

                if (pass == 1 && (container && !plain))
                    continue;

                MoveInto(go, cat, container, plain);
                plan[i] = new KeyValuePair<GameObject, string>(null, cat);
            }
        }

        // ---- alt grupların çocuklarını sırala ve adlandır ----
        Transform world2 = FindRoot(scene, WORLD);

        if (world2 != null)
        {
            foreach (KeyValuePair<string, string> kv in ChildPrefix)
            {
                Transform sub = world2.Find(kv.Key);

                if (sub != null)
                    RenameChildren(sub, kv.Value);
            }

            OrderChildren(world2, WorldOrder);
        }

        // ---- üst grupları sıraya diz ----
        for (int i = 0; i < TopOrder.Length; i++)
        {
            Transform g = FindRoot(scene, TopOrder[i]);

            if (g != null && apply)
                g.SetSiblingIndex(i);
        }

        if (apply)
        {
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        if (unknown.Count > 0)
            log.AppendLine("\nTanınmayan (dokunulmadı): " + string.Join(", ", unknown));

        Debug.Log((apply ? "HİYERARŞİ DÜZENLENDİ" : "ÖNİZLEME (değişiklik yok)") + "\n" + log);
    }

    // =========================================================
    // SINIFLANDIRMA
    // =========================================================

    static bool IsGroupRoot(string name)
    {
        for (int i = 0; i < TopOrder.Length; i++)
            if (TopOrder[i] == name)
                return true;

        return false;
    }

    static string Classify(GameObject go)
    {
        string n = go.name.ToLowerInvariant();

        HashSet<string> types = new HashSet<string>();
        Component[] comps = go.GetComponents<Component>();

        for (int i = 0; i < comps.Length; i++)
            if (comps[i] != null)
                types.Add(comps[i].GetType().Name);

        bool hasSprite = types.Contains("SpriteRenderer");

        if (types.Contains("EventSystem") || Regex.IsMatch(n, @"manager|^theme |audio|music"))
            return SYS;

        if (types.Contains("Camera") || HasTypeContaining(types, "Cinemachine") || n.Contains("camera"))
            return CAM;

        if (types.Contains("Light2D") || types.Contains("Volume") || (!hasSprite && Regex.IsMatch(n, @"light|volume")))
            return LIGHT;

        if (types.Contains("PlayerController") || SafeTag(go, "Player"))
            return PLAYER;

        if (types.Contains("TrainingDummy") || types.Contains("EnemyController") || go.layer == LayerMask.NameToLayer("Enemy"))
            return ENEMY;

        if (Regex.IsMatch(n, @"demirci|npc|blacksmith"))
            return NPC;

        // ---- dünya ----
        if (types.Contains("SkyBackdrop") || Regex.IsMatch(n, @"\bsky|gökyüz|gokyuz"))
            return WORLD + "/Sky";

        if (Regex.IsMatch(n, @"cloud|bulut"))
            return WORLD + "/Clouds";

        if (go.layer == LayerMask.NameToLayer("Ground") || go.layer == LayerMask.NameToLayer("Wall") ||
            Regex.IsMatch(n, @"^ground$|^zemin|platform"))
            return WORLD + "/Ground";

        if (Regex.IsMatch(n, @"tree|ağaç|agac"))
            return WORLD + "/Trees";

        if (Regex.IsMatch(n, @"screenbush|^frontbush|foreground|^fg"))
            return WORLD + "/Foreground";

        if (Regex.IsMatch(n, @"backbush|^back"))
            return WORLD + "/Bushes_Back";

        if (Regex.IsMatch(n, @"bush|grass|çim|cim|çalı|otlar"))
            return WORLD + "/Bushes_Ground";

        if (Regex.IsMatch(n, @"adsız|adsiz|katman|^background|^bg"))
            return WORLD + "/Background";

        return null;
    }

    static bool HasTypeContaining(HashSet<string> types, string part)
    {
        foreach (string t in types)
            if (t.Contains(part))
                return true;

        return false;
    }

    static bool SafeTag(GameObject go, string tag)
    {
        try { return go.CompareTag(tag); }
        catch { return false; }
    }

    static bool IsContainer(GameObject go)
    {
        return go.transform.childCount > 0 && go.GetComponent<SpriteRenderer>() == null;
    }

    // Transform dışında hiç bileşeni yok.
    static bool IsPlain(GameObject go)
    {
        return go.GetComponents<Component>().Length <= 1;
    }

    // =========================================================
    // TAŞIMA
    // =========================================================

    static void MoveInto(GameObject go, string cat, bool container, bool plain)
    {
        string[] parts = cat.Split('/');
        Transform top = GetOrCreateRoot(parts[0]);

        if (parts.Length == 1)
        {
            Reparent(go.transform, top, "→ " + parts[0]);
            return;
        }

        string subName = parts[1];
        Transform sub = top != null ? top.Find(subName) : null;

        // Script'li konteyner: alt grubun kendisi olur (parallax vb. bozulmasın).
        if (container && !plain && sub == null)
        {
            Log(go.name + "  →  " + parts[0] + "/" + subName + "  (yeniden adlandırıldı)");

            if (apply)
            {
                Undo.RecordObject(go, "Rename");
                go.name = subName;
                Undo.SetTransformParent(go.transform, top, "Reparent");
            }

            return;
        }

        if (sub == null)
            sub = CreateChild(top, subName);

        if (container && plain)
        {
            // Düz konteyner: çocukları alt gruba taşı, boş konteyneri sil.
            Log(go.name + "  →  birleştirildi: " + parts[0] + "/" + subName + "  (" + go.transform.childCount + " obje)");

            if (!apply)
                return;

            List<Transform> kids = new List<Transform>();

            foreach (Transform c in go.transform)
                kids.Add(c);

            for (int i = 0; i < kids.Count; i++)
                Undo.SetTransformParent(kids[i], sub, "Reparent");

            if (go.transform.childCount == 0)
                Undo.DestroyObjectImmediate(go);

            return;
        }

        Reparent(go.transform, sub, "→ " + parts[0] + "/" + subName);
    }

    static void Reparent(Transform t, Transform parent, string what)
    {
        Log(t.name + "  " + what);

        if (apply && parent != null)
            Undo.SetTransformParent(t, parent, "Reparent");
    }

    static Transform GetOrCreateRoot(string name)
    {
        Transform existing = FindRoot(SceneManager.GetActiveScene(), name);

        if (existing != null)
            return existing;

        Log("+ grup oluşturuldu: " + name);

        if (!apply)
            return null;

        GameObject g = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(g, "Create group");
        g.transform.position = Vector3.zero;

        return g.transform;
    }

    static Transform CreateChild(Transform parent, string name)
    {
        Log("+ alt grup: " + (parent != null ? parent.name : "?") + "/" + name);

        if (!apply || parent == null)
            return null;

        GameObject g = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(g, "Create group");
        g.transform.SetParent(parent, false);

        return g.transform;
    }

    static Transform FindRoot(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
            if (roots[i].name == name)
                return roots[i].transform;

        return null;
    }

    // =========================================================
    // ADLANDIRMA / SIRALAMA
    // =========================================================

    static void RenameChildren(Transform group, string prefix)
    {
        if (!apply)
            return;

        List<Transform> kids = new List<Transform>();

        foreach (Transform c in group)
            kids.Add(c);

        // Soldan sağa (dünya x'ine göre).
        kids.Sort((a, b) => a.position.x.CompareTo(b.position.x));

        int n = 1;

        for (int i = 0; i < kids.Count; i++)
        {
            Transform k = kids[i];

            // İç içe prefab parçalarının adı değiştirilemez.
            if (PrefabUtility.IsPartOfPrefabInstance(k.gameObject) && !PrefabUtility.IsOutermostPrefabInstanceRoot(k.gameObject))
            {
                n++;
                continue;
            }

            string newName = Regex.IsMatch(k.name, "^square", RegexOptions.IgnoreCase)
                ? "Filler_Square"
                : prefix + "_" + n.ToString("00");

            if (k.name != newName)
            {
                Undo.RecordObject(k.gameObject, "Rename");
                k.name = newName;
            }

            if (newName != "Filler_Square")
                n++;

            k.SetSiblingIndex(i);
        }
    }

    static void OrderChildren(Transform parent, string[] order)
    {
        if (!apply)
            return;

        int idx = 0;

        for (int i = 0; i < order.Length; i++)
        {
            Transform c = parent.Find(order[i]);

            if (c != null)
                c.SetSiblingIndex(idx++);
        }
    }

    static void Log(string s)
    {
        log.AppendLine(s);
    }
}
#endif
