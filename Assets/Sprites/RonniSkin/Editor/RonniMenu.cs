using UnityEditor;
using UnityEngine;

namespace RonniSkin.EditorTools
{
    public static class RonniMenu
    {
        [MenuItem("GameObject/RonniSkin/Create Ronni", false, 10)]
        [MenuItem("Tools/RonniSkin/Create Ronni")]
        public static void Create()
        {
            var go = new GameObject("Ronni");
            go.AddComponent<RonniSkinRig>();
            go.AddComponent<RonniSkinAnimator>();
            Undo.RegisterCreatedObjectUndo(go, "Create Ronni");
            Selection.activeGameObject = go;
        }

        [MenuItem("Tools/RonniSkin/Attach Ronni To Player")]
        public static void AttachToPlayer()
        {
            var pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc == null) { EditorUtility.DisplayDialog("RonniSkin", "Sahnede PlayerController bulunamadı.", "Tamam"); return; }
            Transform parent = pc.modelPivot != null ? pc.modelPivot : pc.transform;
            var old = parent.Find("Ronni");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var go = new GameObject("Ronni");
            Undo.RegisterCreatedObjectUndo(go, "Attach Ronni");
            go.transform.SetParent(parent, false);
            go.AddComponent<RonniSkinRig>();
            go.AddComponent<RonniSkinAnimator>();
            var br = go.AddComponent<RonniPlayerBridge>();
            br.player = pc;
            Selection.activeGameObject = go;
            EditorUtility.SetDirty(pc.gameObject);
        }
    }
}
