#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;
using RonniSkin;

/// <summary>
/// Tenka (chiburi + noto) klibini edit modunda, yan yana birkaç fazda gösterir. Geçici (kaydedilmez) nesneler kurar;
/// Clear() ile temizlenir. Sahnede/projede iz bırakmaz. Menü: Tools > Ronni > Clear Tenka Preview
/// </summary>
public static class TenkaPreview
{
    public const string RootName = "TenkaPreviewRoot";
    public const string CamName = "TenkaPreviewCam";
    static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly MethodInfo playerStart = typeof(RonniClipPlayer).GetMethod("Start", BF);
    static readonly MethodInfo playerUpdate = typeof(RonniClipPlayer).GetMethod("Update", BF);
    static readonly MethodInfo partLate = typeof(RonniSoftPart).GetMethod("LateUpdate", BF);
    static readonly MethodInfo tenkaLate = typeof(RonniTenka).GetMethod("LateUpdate", BF);

    [MenuItem("Tools/Ronni/Clear Tenka Preview")]
    public static void Clear()
    {
        var root = GameObject.Find(RootName);
        if (root != null) Object.DestroyImmediate(root);
        var cam = GameObject.Find(CamName);
        if (cam != null) Object.DestroyImmediate(cam);
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            if (sr != null && (sr.name.StartsWith("Tenka_Drop") || sr.name.StartsWith("Tenka_BloodSplat"))) Object.DestroyImmediate(sr.gameObject);
    }

    /// phases: klip fazları (0..1). Her biri ayrı bir rig olarak yan yana dizilir.
    public static string Show(string folder, string clip, float[] phases, float spacing, float camHalfHeight)
    {
        Clear();
        var root = new GameObject(RootName);
        root.hideFlags = HideFlags.DontSaveInEditor;
        root.transform.position = new Vector3(2000f, 2000f, 0f);
        int n = phases.Length;
        for (int i = 0; i < n; i++)
        {
            var go = new GameObject("Rig" + i);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(i * spacing, 0f, 0f);
            var rig = go.AddComponent<RonniSkinRig>();
            rig.unitsPerPixel = 0.001f; rig.sortingLayerName = "Default"; rig.sortingOrderOffset = 0; rig.resourceFolder = folder;
            rig.Build();
            var pl = go.AddComponent<RonniClipPlayer>();
            pl.clip = clip; pl.autoBlink = false; pl.autoGlance = false; pl.fadeTime = 0f; pl.manual01 = phases[i];
            playerStart.Invoke(pl, null);
            var tk = go.AddComponent<RonniTenka>();
            tk.blood = false; tk.sfx = false; tk.sheathGlint = false; tk.groundSplats = false;
            var parts = go.GetComponentsInChildren<RonniSoftPart>(true);
            for (int k = 0; k < 4; k++)
            {
                pl.manual01 = phases[i];
                playerUpdate.Invoke(pl, null);
                foreach (var p in parts) if (p != null && partLate != null) partLate.Invoke(p, null);
                tk.Drive(rig);
                if (tenkaLate != null) tenkaLate.Invoke(tk, null);
            }
        }
        var camGo = new GameObject(CamName);
        camGo.hideFlags = HideFlags.DontSaveInEditor;
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.6f, 0.66f, 1f);
        cam.nearClipPlane = -50f; cam.farClipPlane = 50f; cam.orthographicSize = camHalfHeight;
        float aspect = 16f / 9f;
        float width = spacing * n;
        // yatayda sığması için gerekirse büyüt
        float needHalfH = width * 0.5f / aspect;
        if (cam.orthographicSize < needHalfH) cam.orthographicSize = needHalfH;
        cam.transform.position = new Vector3(2000f + (n - 1) * spacing * 0.5f - spacing * 0.28f, 2000f + 1.55f, -10f);
        return "ok n=" + n + " ortho=" + cam.orthographicSize;
    }
}
#endif
