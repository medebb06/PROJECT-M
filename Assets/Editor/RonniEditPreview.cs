#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RonniSkin;

/// <summary>
/// Ronni modelini EDIT MODUNDA sahnede gösterir (oyun başlamadan).
///
/// Oyundaki başlangıç görünümüyle aynı: kılıç çekikse RonniAtk 'idle_drawn' klibi,
/// değilse RonniIdle 'idle' klibi; klibin ilk karesinde SABİT durur (sahnede animasyon yok). Boy ve yön de oyundaki
/// gibi (ana rig boyuna göre ölçek × idleScale, sağa bakarken aynalı).
///   - HideFlags.DontSave: sahneye KAYDEDİLMEZ, build'e girmez, hiyerarşide görünmez.
///   - Play'e basarken yok edilir, Play bitince yeniden kurulur.
///   - Player'ı sürükleyince önizleme de takip eder.
///
/// Menü: Tools > Ronni > Toggle Edit Preview   |   Tools > Ronni > Refresh Preview
/// </summary>
[InitializeOnLoad]
public static class RonniEditPreview
{
    const string PrefKey = "RonniEditPreview.Enabled";
    const string PreviewName = "RonniEditPreview (not saved)";
    const float MainRigHeightPx = 3350f;   // RonniPlayerBridge.RonniHeightPx (ana rig, şapka ucu → taban)

    static GameObject preview;
    static RonniSkinRig previewRig;
    static RonniClipPlayer previewPlayer;
    static RonniSoftPart[] previewParts;
    static float previewK = 1f;
    static float clipPeriod = 3.2f;
    static double nextFrame;
    static int posedFrames;   // sabit poz: ilk birkaç güncellemede pozla, sonra dur

    static readonly MethodInfo playerStart = typeof(RonniClipPlayer).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly MethodInfo playerUpdate = typeof(RonniClipPlayer).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly MethodInfo partLate = typeof(RonniSoftPart).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

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

    [MenuItem("Tools/Ronni/Toggle Edit Preview")]
    static void Toggle()
    {
        Enabled = !Enabled;
        Refresh();
        Debug.Log("Ronni Edit Preview: " + (Enabled ? "AÇIK" : "KAPALI"));
    }

    [MenuItem("Tools/Ronni/Toggle Edit Preview", true)]
    static bool ToggleValidate()
    {
        Menu.SetChecked("Tools/Ronni/Toggle Edit Preview", Enabled);
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
            if (rigs[i] != null && (preview == null || !rigs[i].transform.IsChildOf(preview.transform)))
                return rigs[i];

        return null;
    }

    static bool HasClip(string folder, string clip)
    {
        var ta = Resources.Load<TextAsset>(folder + "/ronni_anims");
        return ta != null && ta.text.Contains("\"name\":\"" + clip + "\"") || ta != null && ta.text.Contains("\"name\": \"" + clip + "\"");
    }

    public static void Refresh()
    {
        Destroy();

        if (!Enabled || Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        RonniSkinRig real = FindRealRig();

        if (real == null)
            return;

        RonniPlayerBridge br = real.GetComponent<RonniPlayerBridge>();

        // Oyundaki başlangıç görünümünü seç: kılıç çekik idle → yoksa nefes alan idle → yoksa eski ön görünüm.
        string folder = real.resourceFolder, clip = null;
        float scaleMul = 1f;
        clipPeriod = 3.2f;

        if (br != null)
        {
            scaleMul = br.idleScale;

            if (br.startDrawn && !string.IsNullOrEmpty(br.attackFolder) && HasClip(br.attackFolder, "idle_drawn"))
            {
                folder = br.attackFolder; clip = "idle_drawn"; clipPeriod = Mathf.Max(0.1f, br.idleDrawnPeriod2);
            }
            else if (!string.IsNullOrEmpty(br.idleFolder) && HasClip(br.idleFolder, "idle"))
            {
                folder = br.idleFolder; clip = "idle";
            }
            else scaleMul = 1f;
        }

        preview = new GameObject(PreviewName);
        preview.hideFlags = HideFlags.DontSaveInEditor | HideFlags.NotEditable | HideFlags.HideInHierarchy;

        RonniSkinRig rig = preview.AddComponent<RonniSkinRig>();

        rig.unitsPerPixel = real.unitsPerPixel;
        rig.sortingLayerName = real.sortingLayerName;
        rig.sortingOrderOffset = real.sortingOrderOffset;
        rig.skinGrid = real.skinGrid;
        rig.resourceFolder = folder;

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

        // Bridge.MakeProfile ile aynı ölçek: ana rig boyuna eşitle, sonra idleScale
        previewK = (rig.HeightPx > 0f ? MainRigHeightPx / rig.HeightPx : 1f) * scaleMul;

        // Saldırı rig'inin smear şeridi idle'da gizli (Bridge de böyle yapıyor)
        rig.SetPartWindow("atk_smear", 0f, 0f, Color.clear, Color.clear);

        previewRig = rig;
        previewParts = preview.GetComponentsInChildren<RonniSoftPart>(true);

        if (clip != null && playerStart != null && playerUpdate != null)
        {
            previewPlayer = preview.AddComponent<RonniClipPlayer>();
            previewPlayer.clip = clip;
            previewPlayer.autoBlink = false;
            previewPlayer.autoGlance = false;
            previewPlayer.fadeTime = 0f;
            previewPlayer.manual01 = 0f;
            playerStart.Invoke(previewPlayer, null);
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

        Sync();
        SceneView.RepaintAll();
    }

    // Önizleme gerçek Ronni'yi izler ve idle klibini oynatır (~30 fps).
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

        // oyunda başlangıçta sağa bakar → Bridge kökü aynalar (idle çizimleri sola bakıyor)
        Vector3 ls = r.lossyScale;
        Vector3 want = new Vector3(-Mathf.Abs(ls.x) * previewK, Mathf.Abs(ls.y) * previewK, 1f);

        if (p.position != r.position) p.position = r.position;
        if (p.rotation != r.rotation) p.rotation = r.rotation;
        if (p.localScale != want) p.localScale = want;

        // Sahnede animasyon yok: klibin ilk karesinde sabit durur (birkaç kez uygulanır ki yumuşak parçalar da otursun).
        if (previewPlayer == null || posedFrames >= 3 || EditorApplication.timeSinceStartup < nextFrame)
            return;

        nextFrame = EditorApplication.timeSinceStartup + 1.0 / 30.0;
        posedFrames++;

        previewPlayer.manual01 = 0f;

        try
        {
            playerUpdate.Invoke(previewPlayer, null);

            if (partLate != null && previewParts != null)
                for (int i = 0; i < previewParts.Length; i++)
                    if (previewParts[i] != null) partLate.Invoke(previewParts[i], null);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Ronni Edit Preview animasyonu durdu: " + e.Message);
            previewPlayer = null;
        }

        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();   // Game görünümü de güncellensin
    }

    static void Destroy()
    {
        if (preview != null)
            Object.DestroyImmediate(preview);

        preview = null;
        posedFrames = 0;
        previewRig = null;
        previewPlayer = null;
        previewParts = null;

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
