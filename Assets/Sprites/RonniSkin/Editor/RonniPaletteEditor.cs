using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RonniSkin
{
    [CustomEditor(typeof(RonniPalette))]
    public class RonniPaletteEditor : Editor
    {
        const string PendingKey = "RonniPalette.PendingJson";
        static readonly Dictionary<int, bool> fold = new Dictionary<int, bool>();
        static bool showFamilies;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var pal = (RonniPalette)target;

            EditorGUILayout.HelpBox(
                "Her kategori için rengi seç; bütün animasyonlardaki (idle, kılıç çekik idle, koşu, saldırı, zıplama, ön ve yan profil) ilgili parçalar birlikte değişir.\n" +
                "Varsayılan renkler yeni idle'ın renkleridir. Değişiklikler Play modunda anında görünür (rig'ler Play'de kurulur).",
                MessageType.Info);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("paletteOn"), new GUIContent("Palet açık"));
            EditorGUILayout.Space(4);

            var cats = serializedObject.FindProperty("categories");
            for (int i = 0; i < cats.arraySize; i++)
            {
                var c = cats.GetArrayElementAtIndex(i);
                var name = c.FindPropertyRelative("name").stringValue;
                var color = c.FindPropertyRelative("color");
                var def = c.FindPropertyRelative("defaultColor");

                using (new EditorGUILayout.HorizontalScope())
                {
                    var en = c.FindPropertyRelative("enabled");
                    en.boolValue = EditorGUILayout.Toggle(en.boolValue, GUILayout.Width(16));
                    EditorGUILayout.LabelField(name, GUILayout.Width(140));
                    using (new EditorGUI.DisabledScope(!en.boolValue))
                        color.colorValue = EditorGUILayout.ColorField(GUIContent.none, color.colorValue, true, false, false);
                    if (GUILayout.Button(new GUIContent("↺", "Varsayılan (yeni idle) rengine dön"), GUILayout.Width(24))) color.colorValue = def.colorValue;
                }
                if (c.FindPropertyRelative("hasSecondary").boolValue)
                {
                    var sc = c.FindPropertyRelative("secondaryColor");
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(20);
                        EditorGUILayout.LabelField("   " + c.FindPropertyRelative("secondaryName").stringValue, GUILayout.Width(140));
                        sc.colorValue = EditorGUILayout.ColorField(GUIContent.none, sc.colorValue, true, false, false);
                        if (GUILayout.Button(new GUIContent("↺", "Varsayılana dön"), GUILayout.Width(24))) sc.colorValue = c.FindPropertyRelative("secondaryDefault").colorValue;
                    }
                }
                fold.TryGetValue(i, out bool open);
                EditorGUI.indentLevel++;
                open = EditorGUILayout.Foldout(open, "İnce ayar", true);
                fold[i] = open;
                if (open)
                {
                    EditorGUILayout.PropertyField(c.FindPropertyRelative("hueOffset"), new GUIContent("Ton kaydır"));
                    EditorGUILayout.PropertyField(c.FindPropertyRelative("satMul"), new GUIContent("Doygunluk"));
                    EditorGUILayout.PropertyField(c.FindPropertyRelative("valMul"), new GUIContent("Parlaklık"));
                    EditorGUILayout.PropertyField(c.FindPropertyRelative("tint"), new GUIContent("Çarpma rengi"));
                    EditorGUILayout.PropertyField(c.FindPropertyRelative("keywords"), new GUIContent("Parça adı içerir"), true);
                    EditorGUILayout.PropertyField(c.FindPropertyRelative("exclude"), new GUIContent("Hariç tut"), true);
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.ColorField(new GUIContent("Varsayılan renk"), def.colorValue, true, false, false);
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Renkleri varsayılana döndür"))
                {
                    serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(pal, "Ronni palet sıfırla"); pal.ResetColors(); EditorUtility.SetDirty(pal);
                    serializedObject.Update();
                }
            }
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button(new GUIContent("Play'deki renkleri Play'den çıkınca koru", "Play modunda yaptığın ayarlar normalde Play bitince kaybolur. Buna basarsan Play'den çıkınca sahnedeki palete geri yazılır.")))
                {
                    serializedObject.ApplyModifiedProperties();
                    SessionState.SetString(PendingKey, EditorJsonUtility.ToJson(pal));
                    Debug.Log("[RonniPalette] Renkler kaydedildi; Play'den çıkınca palete yazılacak.");
                }
            }

            EditorGUILayout.Space(6);
            showFamilies = EditorGUILayout.Foldout(showFamilies, "Taban dönüşümleri (gelişmiş: eski çizim → yeni idle renkleri)", true);
            if (showFamilies)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("families"), true);

            if (serializedObject.ApplyModifiedProperties()) pal.MarkDirty();
        }

        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        static void OnPlayMode(PlayModeStateChange s)
        {
            if (s != PlayModeStateChange.EnteredEditMode) return;
            string json = SessionState.GetString(PendingKey, "");
            if (string.IsNullOrEmpty(json)) return;
            SessionState.EraseString(PendingKey);
            foreach (var p in Object.FindObjectsOfType<RonniPalette>(true))
            {
                Undo.RecordObject(p, "Ronni palet (Play renkleri)");
                EditorJsonUtility.FromJsonOverwrite(json, p);
                EditorUtility.SetDirty(p);
                if (PrefabUtility.IsPartOfPrefabInstance(p)) PrefabUtility.RecordPrefabInstancePropertyModifications(p);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(p.gameObject.scene);
            }
            Debug.Log("[RonniPalette] Play'deki renkler palete yazıldı. Sahneyi kaydetmeyi unutma.");
        }
    }
}
