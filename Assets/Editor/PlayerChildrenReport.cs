#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Report Player Children
/// Sahnedeki Player'ın tüm çocuklarını ağaç olarak Console'a yazar:
///   - aktif mi, hangi bileşenler var, SpriteRenderer açık mı / hangi sprite
///   - hangi script alanı bu objeyi kullanıyor (REF), kullanan yoksa "unreferenced"
/// Çıktıyı kopyalayıp bana yapıştır; silinecekleri ona göre belirleriz.
/// Hiçbir şeyi değiştirmez.
/// </summary>
public static class PlayerChildrenReport
{
    [MenuItem("Tools/Report Player Children")]
    public static void Report()
    {
        PlayerController pc = Object.FindAnyObjectByType<PlayerController>();

        if (pc == null)
        {
            Debug.LogWarning("Sahnede PlayerController bulunamadı.");
            return;
        }

        Transform root = pc.transform;

        // Player ağacındaki tüm script alanlarını tara: hangi child'a referans var?
        Dictionary<Object, List<string>> refs = new Dictionary<Object, List<string>>();

        Component[] all = root.GetComponentsInChildren<Component>(true);

        for (int i = 0; i < all.Length; i++)
        {
            Component c = all[i];

            if (c == null || c is Transform)
                continue;

            SerializedObject so;

            try { so = new SerializedObject(c); }
            catch { continue; }

            SerializedProperty p = so.GetIterator();

            while (p.NextVisible(true))
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference)
                    continue;

                Object target = p.objectReferenceValue;

                if (target == null)
                    continue;

                GameObject tg = target is GameObject
                    ? (GameObject)target
                    : (target is Component ? ((Component)target).gameObject : null);

                if (tg == null || !tg.transform.IsChildOf(root) || tg == c.gameObject)
                    continue;

                List<string> list;

                if (!refs.TryGetValue(tg, out list))
                {
                    list = new List<string>();
                    refs[tg] = list;
                }

                list.Add(c.GetType().Name + "." + p.name);
            }
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("PLAYER ÇOCUK RAPORU (" + root.name + ")");
        Dump(root, 0, sb, refs);

        Debug.Log(sb.ToString());
        GUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log("Rapor panoya da kopyalandı (Ctrl+V ile yapıştırabilirsin).");
    }

    static void Dump(Transform t, int depth, StringBuilder sb, Dictionary<Object, List<string>> refs)
    {
        GameObject go = t.gameObject;

        List<string> comps = new List<string>();
        Component[] cs = go.GetComponents<Component>();

        for (int i = 0; i < cs.Length; i++)
        {
            Component c = cs[i];

            if (c == null)
            {
                comps.Add("MISSING_SCRIPT");
                continue;
            }

            if (c is Transform)
                continue;

            string n = c.GetType().Name;

            SpriteRenderer sr = c as SpriteRenderer;

            if (sr != null)
                n += "(" + (sr.enabled ? "on" : "OFF") + ", " + (sr.sprite != null ? sr.sprite.name : "no sprite") + ")";

            Collider2D col = c as Collider2D;

            if (col != null)
                n += "(" + (col.enabled ? "on" : "OFF") + (col.isTrigger ? ", trigger" : "") + ")";

            Behaviour b = c as Behaviour;

            if (b != null && !(c is Renderer) && !b.enabled)
                n += "(OFF)";

            comps.Add(n);
        }

        List<string> r;
        string refText = refs.TryGetValue(go, out r) ? "  REF: " + string.Join(", ", r) : "  unreferenced";

        sb.AppendLine(
            new string(' ', depth * 2) +
            (go.activeSelf ? "" : "[INACTIVE] ") +
            go.name +
            "  [" + string.Join(", ", comps) + "]" +
            (depth > 0 ? refText : "")
        );

        foreach (Transform c in t)
            Dump(c, depth + 1, sb, refs);
    }
}
#endif
