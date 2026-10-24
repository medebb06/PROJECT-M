using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// PLAY MODE SAVER
// Unity, play modunda yapılan değişiklikleri oyunu durdurunca siler. Bu araç şöyle çalışır:
//   1) Play modundayken Hierarchy'den platform/nesne seç, yerleştir/taşı/ölçekle.
//   2) Tools > Play Mode Saver > Remember Selected  (menüden; kısayol YOK)  -> seçili nesneler "hatırlanır".
//   3) Oyunu durdur: hatırlanan nesnelerin son halleri sahneye otomatik uygulanır (sahne "dirty" olur, Ctrl+S ile kaydet).
// Ctrl+Z ile geri alınabilir.
//
// Notlar:
//  - Sadece sahnede zaten var olan nesneler hatırlanır (play modunda yeni yaratılanlar için bkz. aşağıdaki uyarı).
//  - "Transform" modu sadece konum/dönüş/ölçeği alır (platformlar için yeter).
//  - "All components" modu nesnenin tüm bileşenlerinin sayı/renk/bool/enum alanlarını alır (nesne referanslarına dokunmaz).
//  - Kod ile sürekli oynatılan nesneleri (dönen yıldız, parallax, oyuncu) seçme: anlık değerleri kaydedilir.
[InitializeOnLoad]
public static class PlayModeSaver
{
    private const string KeyList = "PlayModeSaver.list";
    private const string KeyData = "PlayModeSaver.data";

    [Serializable] private class Entry { public string id; public bool allComponents; }
    [Serializable] private class EntryList { public List<Entry> entries = new List<Entry>(); }
    [Serializable] private class PropVal { public string path; public string val; }
    [Serializable] private class CompData { public string type; public int index; public List<PropVal> props = new List<PropVal>(); }
    [Serializable] private class ObjData { public string id; public string name; public List<CompData> comps = new List<CompData>(); }
    [Serializable] private class Store { public List<ObjData> objs = new List<ObjData>(); }

    static PlayModeSaver()
    {
        EditorApplication.playModeStateChanged -= OnPlayMode;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    // ------------------------------------------------------------ menü
    [MenuItem("Tools/Play Mode Saver/Remember Selected (Transform)")]
    private static void RememberTransform() { Remember(false); }

    [MenuItem("Tools/Play Mode Saver/Remember Selected (All components)")]
    private static void RememberAll() { Remember(true); }

    [MenuItem("Tools/Play Mode Saver/Forget All")]
    private static void ForgetAll()
    {
        SessionState.EraseString(KeyList);
        SessionState.EraseString(KeyData);
        Debug.Log("[PlayModeSaver] Hatırlanan her şey silindi.");
    }

    [MenuItem("Tools/Play Mode Saver/Remember Selected (Transform)", true)]
    [MenuItem("Tools/Play Mode Saver/Remember Selected (All components)", true)]
    private static bool RememberValidate() { return Application.isPlaying && Selection.gameObjects.Length > 0; }

    private static void Remember(bool all)
    {
        EntryList list = LoadList();
        int added = 0, skipped = 0;
        foreach (GameObject go in Selection.gameObjects)
        {
            if (EditorUtility.IsPersistent(go)) { skipped++; continue; }   // prefab asset
            GlobalObjectId gid = GlobalObjectId.GetGlobalObjectIdSlow(go);
            if (gid.targetObjectId == 0UL) { skipped++; Debug.LogWarning("[PlayModeSaver] '" + go.name + "' play modunda yaratılmış, sahnede karşılığı yok: atlandı."); continue; }
            string id = gid.ToString();
            list.entries.RemoveAll(e => e.id == id);
            list.entries.Add(new Entry { id = id, allComponents = all });
            added++;
        }
        SaveList(list);
        Debug.Log("[PlayModeSaver] " + added + " nesne hatırlanıyor (" + (all ? "tüm bileşenler" : "Transform") + ")" + (skipped > 0 ? ", " + skipped + " atlandı" : "") + ". Toplam: " + list.entries.Count);
    }

    // ------------------------------------------------------------ play modu olayları
    private static void OnPlayMode(PlayModeStateChange s)
    {
        if (s == PlayModeStateChange.ExitingPlayMode) Capture();
        else if (s == PlayModeStateChange.EnteredEditMode) Apply();
    }

    private static void Capture()
    {
        EntryList list = LoadList();
        if (list.entries.Count == 0) return;
        Store store = new Store();
        foreach (Entry e in list.entries)
        {
            GameObject go = Resolve(e.id);
            if (go == null) continue;
            ObjData od = new ObjData { id = e.id, name = go.name };
            Component[] comps = go.GetComponents<Component>();
            Dictionary<Type, int> counts = new Dictionary<Type, int>();
            foreach (Component c in comps)
            {
                if (c == null) continue;
                Type t = c.GetType();
                int idx; counts.TryGetValue(t, out idx); counts[t] = idx + 1;
                if (!e.allComponents && !(c is Transform)) continue;
                CompData cd = new CompData { type = t.FullName, index = idx };
                CaptureProps(c, cd);
                if (cd.props.Count > 0) od.comps.Add(cd);
            }
            store.objs.Add(od);
        }
        SessionState.SetString(KeyData, JsonUtility.ToJson(store));
    }

    private static void Apply()
    {
        string json = SessionState.GetString(KeyData, "");
        if (string.IsNullOrEmpty(json)) return;
        SessionState.EraseString(KeyData);
        Store store = JsonUtility.FromJson<Store>(json);
        if (store == null) return;

        int applied = 0, missing = 0;
        Undo.IncrementCurrentGroup();
        foreach (ObjData od in store.objs)
        {
            GameObject go = Resolve(od.id);
            if (go == null) { missing++; Debug.LogWarning("[PlayModeSaver] '" + od.name + "' sahnede bulunamadı, uygulanamadı."); continue; }
            Component[] comps = go.GetComponents<Component>();
            foreach (CompData cd in od.comps)
            {
                Component target = null; int seen = 0;
                foreach (Component c in comps)
                {
                    if (c == null || c.GetType().FullName != cd.type) continue;
                    if (seen == cd.index) { target = c; break; }
                    seen++;
                }
                if (target == null) continue;
                Undo.RecordObject(target, "Play Mode Saver");
                SerializedObject so = new SerializedObject(target);
                foreach (PropVal pv in cd.props)
                {
                    SerializedProperty sp = so.FindProperty(pv.path);
                    if (sp != null) TrySet(sp, pv.val);
                }
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            }
            EditorSceneManager.MarkSceneDirty(go.scene);
            applied++;
        }
        Undo.SetCurrentGroupName("Play Mode Saver");
        Debug.Log("[PlayModeSaver] " + applied + " nesnenin play modundaki hali sahneye uygulandı" + (missing > 0 ? " (" + missing + " bulunamadı)" : "") + ". Kaydetmek için Ctrl+S. (Ctrl+Z ile geri alınır)");
    }

    // ------------------------------------------------------------ yardımcılar
    private static GameObject Resolve(string id)
    {
        GlobalObjectId gid;
        if (!GlobalObjectId.TryParse(id, out gid)) return null;
        UnityEngine.Object o = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
        return o as GameObject;
    }

    private static EntryList LoadList()
    {
        string j = SessionState.GetString(KeyList, "");
        if (string.IsNullOrEmpty(j)) return new EntryList();
        EntryList l = JsonUtility.FromJson<EntryList>(j);
        return l ?? new EntryList();
    }

    private static void SaveList(EntryList l) { SessionState.SetString(KeyList, JsonUtility.ToJson(l)); }

    private static void CaptureProps(Component c, CompData cd)
    {
        SerializedObject so = new SerializedObject(c);
        SerializedProperty it = so.GetIterator();
        bool enter = true;
        while (it.Next(enter))
        {
            enter = true;
            if (it.propertyPath == "m_Script") continue;
            if (it.isArray && it.propertyType == SerializedPropertyType.Generic) { enter = false; continue; }
            string v;
            if (TryGet(it, out v)) cd.props.Add(new PropVal { path = it.propertyPath, val = v });
        }
    }

    private static string F(float f) { return f.ToString("R", CultureInfo.InvariantCulture); }
    private static float P(string s) { return float.Parse(s, CultureInfo.InvariantCulture); }

    private static bool TryGet(SerializedProperty p, out string v)
    {
        v = null;
        switch (p.propertyType)
        {
            case SerializedPropertyType.Integer: v = p.longValue.ToString(CultureInfo.InvariantCulture); return true;
            case SerializedPropertyType.Boolean: v = p.boolValue ? "1" : "0"; return true;
            case SerializedPropertyType.Float: v = F(p.floatValue); return true;
            case SerializedPropertyType.String: v = p.stringValue; return true;
            case SerializedPropertyType.Enum: v = p.enumValueIndex.ToString(CultureInfo.InvariantCulture); return true;
            case SerializedPropertyType.LayerMask: v = p.intValue.ToString(CultureInfo.InvariantCulture); return true;
            case SerializedPropertyType.Color: { Color c = p.colorValue; v = F(c.r) + "," + F(c.g) + "," + F(c.b) + "," + F(c.a); return true; }
            case SerializedPropertyType.Vector2: { Vector2 a = p.vector2Value; v = F(a.x) + "," + F(a.y); return true; }
            case SerializedPropertyType.Vector3: { Vector3 a = p.vector3Value; v = F(a.x) + "," + F(a.y) + "," + F(a.z); return true; }
            case SerializedPropertyType.Vector4: { Vector4 a = p.vector4Value; v = F(a.x) + "," + F(a.y) + "," + F(a.z) + "," + F(a.w); return true; }
            case SerializedPropertyType.Quaternion: { Quaternion q = p.quaternionValue; v = F(q.x) + "," + F(q.y) + "," + F(q.z) + "," + F(q.w); return true; }
            case SerializedPropertyType.Rect: { Rect r = p.rectValue; v = F(r.x) + "," + F(r.y) + "," + F(r.width) + "," + F(r.height); return true; }
            default: return false;   // nesne referansları vb. atlanır
        }
    }

    private static void TrySet(SerializedProperty p, string v)
    {
        try
        {
            string[] s = v.Split(',');
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer: p.longValue = long.Parse(v, CultureInfo.InvariantCulture); break;
                case SerializedPropertyType.Boolean: p.boolValue = v == "1"; break;
                case SerializedPropertyType.Float: p.floatValue = P(v); break;
                case SerializedPropertyType.String: p.stringValue = v; break;
                case SerializedPropertyType.Enum: p.enumValueIndex = int.Parse(v, CultureInfo.InvariantCulture); break;
                case SerializedPropertyType.LayerMask: p.intValue = int.Parse(v, CultureInfo.InvariantCulture); break;
                case SerializedPropertyType.Color: p.colorValue = new Color(P(s[0]), P(s[1]), P(s[2]), P(s[3])); break;
                case SerializedPropertyType.Vector2: p.vector2Value = new Vector2(P(s[0]), P(s[1])); break;
                case SerializedPropertyType.Vector3: p.vector3Value = new Vector3(P(s[0]), P(s[1]), P(s[2])); break;
                case SerializedPropertyType.Vector4: p.vector4Value = new Vector4(P(s[0]), P(s[1]), P(s[2]), P(s[3])); break;
                case SerializedPropertyType.Quaternion: p.quaternionValue = new Quaternion(P(s[0]), P(s[1]), P(s[2]), P(s[3])); break;
                case SerializedPropertyType.Rect: p.rectValue = new Rect(P(s[0]), P(s[1]), P(s[2]), P(s[3])); break;
            }
        }
        catch (Exception) { /* bozuk değer: atla */ }
    }
}
