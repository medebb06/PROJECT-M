#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RonniSkin;

/// <summary>
/// Ronni modelini EDIT MODUNDA sahnede gösterir (oyun başlamadan).
///
/// Ronni, Play'de kodla kurulduğu için sahnede normalde görünmez. Bu araç, gerçek
/// 'Ronni' objesinin yerine geçici bir ÖNİZLEME kopyası kurar:
///   - HideFlags.DontSave: sahneye KAYDEDİLMEZ, build'e girmez, hiyerarşide görünmez.
///   - Play'e basarken yok edilir, Play bitince yeniden kurulur.
///   - Player'ı sürükleyince önizleme de takip eder.
///
/// Menü: Tools > Ronni > Edit Preview (aç/kapa)   |   Tools > Ronni > Refresh Preview
/// </summary>
[InitializeOnLoad]
public static class RonniEditPreview
{
    const string PrefKey = "RonniEditPreview.Enabled";
    const string PreviewName = "RonniEditPreview (not saved)";

    static GameObject preview;

    static RonniEditPreview()
    {
        EditorApplication.delayCall += Refresh;
        EditorApplication.update += Sync;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorSceneManager.sceneOpened += (s, m) => EditorApplication.delayCall += Refresh;
        AssemblyReloadEvents.beforeAssemblyReload += Destroy;
    }

    static bool Enabled
    {
        get { return EditorPrefs.GetBool(PrefKey, true); }
        set { EditorPrefs.SetBool(PrefKey, value); }
    }

    [MenuItem("Tools/Ronni/Edit Preview (aç/kapa)")]
    static void Toggle()
    {
        Enabled = !Enabled;
        Refresh();
        Debug.Log("Ronni Edit Preview: " + (Enabled ? "AÇIK" : "KAPALI"));
    }

    [MenuItem("Tools/Ronni/Edit Preview (aç/kapa)", true)]
    static bool ToggleValidate()
    {
        Menu.SetChecked("Tools/Ronni/Edit Preview (aç/kapa)", Enabled);
        return true;
    }

    [MenuItem("Tools/Ronni/Refresh Preview")]
    public static void RefreshMenu() { Refresh(); }

    static void OnPlayModeChanged(PlayModeStateChange s)
    {
        if (s == PlayModeStateChange.ExitingEditMode)
            Destroy();
        else if (s == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += Refresh;
    }

    // =========================================================

    static RonniSkinRig FindRealRig()
    {
        PlayerController pc = Object.FindAnyObjectByType<PlayerController>();

        if (pc == null)
            return null;

        RonniSkinRig[] rigs = pc.GetComponentsInChildren<RonniSkinRig>(true);

        for (int i = 0; i < rigs.Length; i++)
            if (rigs[i] != null && (preview == null || rigs[i].gameObject != preview))
                return rigs[i];

        return null;
    }

    public static void Refresh()
    {
        Destroy();

        if (!Enabled || Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        RonniSkinRig real = FindRealRig();

        if (real == null)
            return;

        preview = new GameObject(PreviewName);
        preview.hideFlags = HideFlags.DontSaveInEditor | HideFlags.NotEditable | HideFlags.HideInHierarchy;

        RonniSkinRig rig = preview.AddComponent<RonniSkinRig>();

        // Ayarları gerçek Ronni'den kopyala (klasör, ölçek, sorting).
        EditorJsonCopy(real, rig);

        Sync();

        try
        {
            rig.Build();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Ronni Edit Preview kurulamadı: " + e.Message);
            Destroy();
            return;
        }

        // Her şey kaydedilmesin; materyal/mesh sızıntısı olmasın.
        foreach (Transform t in preview.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.NotEditable | HideFlags.HideInHierarchy;

            MeshRenderer mr = t.GetComponent<MeshRenderer>();

            if (mr != null && mr.sharedMaterial != null)
                mr.sharedMaterial.hideFlags = HideFlags.DontSave;

            MeshFilter mf = t.GetComponent<MeshFilter>();

            if (mf != null && mf.sharedMesh != null)
                mf.sharedMesh.hideFlags = HideFlags.DontSave;
        }

        SceneView.RepaintAll();
    }

    static void EditorJsonCopy(RonniSkinRig from, RonniSkinRig to)
    {
        to.unitsPerPixel = from.unitsPerPixel;
        to.sortingLayerName = from.sortingLayerName;
        to.sortingOrderOffset = from.sortingOrderOffset;
        to.skinGrid = from.skinGrid;
        to.resourceFolder = from.resourceFolder;
    }

    // Önizleme, gerçek Ronni'nin konumunu/ölçeğini izler.
    static void Sync()
    {
        if (preview == null)
            return;

        if (Application.isPlaying)
        {
            Destroy();
            return;
        }

        RonniSkinRig real = FindRealRig();

        if (real == null)
            return;

        Transform r = real.transform;
        Transform p = preview.transform;

        if (p.position != r.position)
            p.position = r.position;

        if (p.rotation != r.rotation)
            p.rotation = r.rotation;

        if (p.localScale != r.lossyScale)
            p.localScale = r.lossyScale;
    }

    static void Destroy()
    {
        if (preview != null)
            Object.DestroyImmediate(preview);

        preview = null;

        // Eski oturumdan kalmış önizleme varsa temizle.
        RonniSkinRig[] all = Resources.FindObjectsOfTypeAll<RonniSkinRig>();

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].gameObject.name == PreviewName)
                Object.DestroyImmediate(all[i].gameObject);
        }
    }
}
#endif
