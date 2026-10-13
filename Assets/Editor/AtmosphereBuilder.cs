#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Tools > Lighting > Build Moonlit Atmosphere
/// Referans görseldeki ay ışıklı, puslu, kenarları yanan sahneyi kurar:
///   gökyüzü gradyanı, hilal ay + hale, ufuk parıltısı, ışık hüzmeleri, sis bantları,
///   yerde ışık lekesi, uçuşan toz, siluetlere kenar ışığı (rim) + atmosferik pus,
///   karakter gölgeleri, post-process (bloom, vignette, film grain, renk).
///
/// Tools > Lighting > Remove Moonlit Atmosphere  : her şeyi eski haline döndürür.
/// Ayrıca tek adımda Ctrl+Z ile de geri alınır.
/// </summary>
public static class AtmosphereBuilder
{
    const string Root = "Assets/Art/Atmosphere";
    const string TexDir = Root + "/Textures";
    const string MatDir = Root + "/Materials";
    const string ProfilePath = Root + "/MoonlitVolumeProfile.asset";
    const string AtmosName = "[ATMOSPHERE]";

    // Yeni sorting düzeni (mevcut order'lar x10 yapılır; araya katman girebilsin).
    const int OrderSky = -200, OrderMoonHalo = -181, OrderMoon = -180;
    const int OrderHorizonGlow = -60, OrderRays = -55, OrderFogFar = -35, OrderFogNear = -15;
    const int OrderGroundGlow = 12, OrderShadow = 15, OrderDust = 300;

    static StringBuilder log;

    // =========================================================
    // KUR
    // =========================================================

    [MenuItem("Tools/Lighting/Build Moonlit Atmosphere")]
    public static void Build()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Atmosphere: Play modunda çalışmaz.");
            return;
        }

        if (GameObject.Find(AtmosName) != null)
        {
            if (!EditorUtility.DisplayDialog("Atmosfer", "Atmosfer zaten kurulu. Kaldırıp yeniden kurayım mı?", "Evet", "Vazgeç"))
                return;

            Remove();
        }

        log = new StringBuilder("AY IŞIĞI ATMOSFERİ KURULDU\n");

        Shader rimShader = Shader.Find("ProjectM/SilhouetteRim");
        Shader addShader = Shader.Find("ProjectM/SpriteAdditive");
        Shader unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");

        if (rimShader == null || addShader == null || unlitShader == null)
        {
            Debug.LogError("Atmosphere: Shader bulunamadı (ProjectM/SilhouetteRim, ProjectM/SpriteAdditive). Shader derleme hatası var mı? Console'a bak.");
            return;
        }

        Camera cam = Camera.main;

        if (cam == null)
        {
            Debug.LogError("Atmosphere: Main Camera bulunamadı (tag 'MainCamera').");
            return;
        }

        // ---- dokular ----
        Sprite sky = LoadSprite("sky_gradient.png", FilterMode.Bilinear);
        Sprite moon = LoadSprite("moon_crescent.png", FilterMode.Bilinear);
        Sprite glow = LoadSprite("glow_soft.png", FilterMode.Bilinear);
        Sprite horizon = LoadSprite("horizon_glow.png", FilterMode.Bilinear);
        Sprite ray = LoadSprite("light_ray.png", FilterMode.Bilinear);
        Sprite fog = LoadSprite("fog_band.png", FilterMode.Bilinear);
        Sprite blob = LoadSprite("shadow_blob.png", FilterMode.Bilinear);
        Sprite groundLight = LoadSprite("ground_light.png", FilterMode.Bilinear);

        if (sky == null || moon == null || glow == null || horizon == null || ray == null || fog == null || blob == null)
        {
            Debug.LogError("Atmosphere: " + TexDir + " içinde dokular eksik.");
            return;
        }

        // ---- materyaller ----
        EnsureFolder(MatDir);

        Material mAdd = GetMat("M_Additive", addShader, m => m.SetFloat("_Intensity", 1f));
        Material mUnlit = GetMat("M_UnlitAlpha", unlitShader, null);

        Color warmRim = new Color(1.25f, 0.92f, 0.62f);
        Color haze = new Color(0.66f, 0.61f, 0.56f);

        Material mCloud = GetMat("M_Rim_Cloud", rimShader, m => SetRim(m, Color.white, new Color(0.93f, 0.88f, 0.82f), 0.18f, 0.25f, warmRim, 0.35f, 6f, 0.6f));
        Material mBack = GetMat("M_Rim_BackBush", rimShader, m => SetRim(m, Color.white, haze, 0.32f, 0.35f, warmRim, 0.9f, 8f, 0.5f));
        Material mTree = GetMat("M_Rim_Tree", rimShader, m => SetRim(m, Color.white, new Color(0.36f, 0.31f, 0.27f), 0.14f, 0.05f, warmRim, 1.35f, 14f, 0.4f));
        Material mGrass = GetMat("M_Rim_GroundBush", rimShader, m => SetRim(m, Color.white, new Color(0.30f, 0.27f, 0.24f), 0.08f, 0f, warmRim, 0.8f, 6f, 0.8f));
        Material mGround = GetMat("M_Rim_Ground", rimShader, m => SetRim(m, Color.white, new Color(0.22f, 0.20f, 0.18f), 0.05f, 0f, warmRim, 0.45f, 5f, 1f));
        Material mFront = GetMat("M_Rim_Foreground", rimShader, m => SetRim(m, Color.white, Color.black, 0f, 0f, Color.black, 0f, 0f, 0f)  /* ön plana ışık vurmaz */);

        // ---- kamera ölçüleri ----
        float viewH = cam.orthographic ? cam.orthographicSize * 2f : 20f;
        float viewW = viewH * Mathf.Max(1.2f, cam.aspect);

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Build Moonlit Atmosphere");
        int undoGroup = Undo.GetCurrentGroup();

        GameObject atmos = new GameObject(AtmosName);
        Undo.RegisterCreatedObjectUndo(atmos, "Atmosphere");
        AtmosphereRestoreData restore = atmos.AddComponent<AtmosphereRestoreData>();

        // ---- 1. sorting'i genişlet + siluet materyalleri ----
        HashSet<Renderer> blurred = BlurControlled();
        float horizonY;
        RemapSceneRenderers(restore, blurred, mCloud, mBack, mTree, mGrass, mGround, mFront, out horizonY, cam);

        // ---- 2. çapalar ----
        Transform skyAnchor = Child(atmos.transform, "SkyAnchor (kamerayı izler)");
        Transform horizonAnchor = Child(atmos.transform, "HorizonAnchor (ufukta)");

        // Gökyüzü
        Spr(skyAnchor, "Sky Gradient", sky, mUnlit, Color.white, OrderSky, new Vector2(0f, 0f), new Vector2(viewW * 1.8f, viewH * 1.8f));

        // Ay + hale
        Vector2 moonPos = new Vector2(viewW * 0.07f, viewH * 0.30f);
        float moonSize = viewH * 0.20f;
        Spr(skyAnchor, "Moon Halo", glow, mAdd, new Color(1f, 0.82f, 0.6f, 0.55f), OrderMoonHalo, moonPos, new Vector2(moonSize * 3.2f, moonSize * 3.2f));
        Spr(skyAnchor, "Moon", moon, mAdd, new Color(1f, 0.95f, 0.85f, 1f), OrderMoon, moonPos, new Vector2(moonSize, moonSize));

        // Ufuk parıltısı (çalıların arkasında)
        Spr(horizonAnchor, "Horizon Glow", horizon, mAdd, new Color(1f, 0.74f, 0.48f, 0.85f), OrderHorizonGlow, new Vector2(0f, 0f), new Vector2(viewW * 2.4f, viewH * 0.55f));

        // Işık hüzmeleri
        float[] rx = { -0.24f, -0.11f, 0.0f, 0.09f, 0.21f };
        float[] rw = { 0.07f, 0.05f, 0.09f, 0.045f, 0.065f };
        float[] ra = { 0.20f, 0.14f, 0.26f, 0.12f, 0.18f };
        float[] rr = { 5f, 2.5f, 0f, -2.5f, -5f };
        List<SpriteRenderer> rays = new List<SpriteRenderer>();

        for (int i = 0; i < rx.Length; i++)
        {
            SpriteRenderer r = Spr(
                horizonAnchor, "Light Ray " + (i + 1), ray, mAdd,
                new Color(1f, 0.86f, 0.66f, ra[i]), OrderRays,
                new Vector2(viewW * rx[i], viewH * 0.12f),
                new Vector2(viewW * rw[i], viewH * 0.85f)
            );

            r.transform.localRotation = Quaternion.Euler(0f, 0f, rr[i]);
            rays.Add(r);
        }

        // Sis bantları
        Spr(horizonAnchor, "Fog Far", fog, mUnlit, new Color(0.86f, 0.80f, 0.72f, 0.40f), OrderFogFar, new Vector2(0f, -viewH * 0.06f), new Vector2(viewW * 3f, viewH * 0.22f));
        Spr(horizonAnchor, "Fog Near", fog, mUnlit, new Color(0.55f, 0.50f, 0.46f, 0.30f), OrderFogNear, new Vector2(0f, -viewH * 0.17f), new Vector2(viewW * 3f, viewH * 0.18f));

        // Yerde ışık lekesi
        Spr(horizonAnchor, "Ground Light", groundLight != null ? groundLight : glow, mAdd, GroundLightColor, OrderGroundGlow, GroundLightPos(viewH), GroundLightSize(viewW, viewH));

        // Toz
        CreateDust(skyAnchor, glow, viewW, viewH);

        // ---- 3. AtmosphereLight ----
        AtmosphereLight al = atmos.AddComponent<AtmosphereLight>();
        al.skyAnchor = skyAnchor;
        al.horizonAnchor = horizonAnchor;
        al.horizonY = horizonY;
        al.lightOffset = new Vector2(0f, viewH * 0.18f);
        al.rays = rays.ToArray();

        // ---- 4. karakter gölgeleri ----
        AddShadows(blob, mUnlit, restore);
        AddTreeShadows(restore);
        AddCharacterRims(restore);

        // ---- 5. global ışık ----
        TuneGlobalLight(restore);

        // ---- 6. post-process ----
        Volume vol = CreateVolume(atmos.transform);
        EnablePostOnCameras();

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(atmos.scene);

        log.AppendLine("Ufuk Y: " + horizonY.ToString("0.00") + "  (ince ayar: [ATMOSPHERE] > AtmosphereLight > Horizon Y)");
        log.AppendLine("Post-process profili: " + ProfilePath + (vol != null ? "" : " (KURULAMADI)"));
        Debug.Log(log.ToString());
    }

    // =========================================================
    // KALDIR
    // =========================================================

    [MenuItem("Tools/Lighting/Remove Moonlit Atmosphere")]
    public static void Remove()
    {
        GameObject atmos = GameObject.Find(AtmosName);

        if (atmos == null)
        {
            Debug.Log("Atmosphere: kurulu değil.");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Remove Moonlit Atmosphere");
        int g = Undo.GetCurrentGroup();

        AtmosphereRestoreData data = atmos.GetComponent<AtmosphereRestoreData>();

        if (data != null)
        {
            foreach (AtmosphereRestoreData.Entry e in data.entries)
            {
                if (e == null || e.renderer == null)
                    continue;

                Undo.RecordObject(e.renderer, "Restore");
                e.renderer.sharedMaterial = e.material;
                e.renderer.sortingOrder = e.sortingOrder;
            }

            foreach (GameObject go in data.createdObjects)
                if (go != null)
                    Undo.DestroyObjectImmediate(go);

            foreach (Component comp in data.addedComponents)
                if (comp != null)
                    Undo.DestroyObjectImmediate(comp);

            if (data.globalLight != null)
            {
                Undo.RecordObject(data.globalLight, "Restore light");
                data.globalLight.intensity = data.globalLightIntensity;
                data.globalLight.color = data.globalLightColor;
            }
        }

        Undo.DestroyObjectImmediate(atmos);
        Undo.CollapseUndoOperations(g);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("Atmosphere kaldırıldı, materyaller ve sıralama eski haline döndü.");
    }

    // =========================================================
    // SAHNE RENDERER'LARI
    // =========================================================

    static void RemapSceneRenderers(
        AtmosphereRestoreData restore, HashSet<Renderer> blurred,
        Material mCloud, Material mBack, Material mTree, Material mGrass, Material mGround, Material mFront,
        out float horizonY, Camera cam)
    {
        PlayerController pc = Object.FindAnyObjectByType<PlayerController>();
        Renderer[] all = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);

        int remapped = 0, swapped = 0;
        float backTopSum = 0f; int backCount = 0;
        float grassTopSum = 0f; int grassCount = 0;
        int groundLayer = LayerMask.NameToLayer("Ground");

        for (int i = 0; i < all.Length; i++)
        {
            Renderer r = all[i];

            if (r == null || r is ParticleSystemRenderer || r.gameObject.scene != SceneMgr())
                continue;

            if (pc != null && r.transform.IsChildOf(pc.transform))
                continue;

            if (r.GetComponentInParent<Canvas>() != null)
                continue;

            AtmosphereRestoreData.Entry e = new AtmosphereRestoreData.Entry
            {
                renderer = r,
                material = r.sharedMaterial,
                sortingOrder = r.sortingOrder
            };

            bool changed = false;

            // Sorting'i x10 genişlet (ön plan 450+, efektler 100+ dokunulmaz).
            if (Mathf.Abs(r.sortingOrder) < 50)
            {
                Undo.RecordObject(r, "Atmosphere sorting");
                r.sortingOrder = r.sortingOrder * 10;
                remapped++;
                changed = true;
            }

            SpriteRenderer sr = r as SpriteRenderer;

            if (sr != null && !blurred.Contains(r))
            {
                string cat = Category(sr.transform, groundLayer);
                Material m = null;

                switch (cat)
                {
                    case "cloud": m = mCloud; break;
                    case "back": m = mBack; break;
                    case "tree": m = mTree; break;
                    case "grass": m = mGrass; break;
                    case "ground": m = mGround; break;
                    case "front": m = mFront; break;
                }

                if (cat == "back") { backTopSum += sr.bounds.max.y; backCount++; }
                if (cat == "grass") { grassTopSum += sr.bounds.max.y; grassCount++; }

                if (m != null)
                {
                    Undo.RecordObject(sr, "Atmosphere material");
                    sr.sharedMaterial = m;
                    swapped++;
                    changed = true;
                }
            }

            if (changed)
                restore.entries.Add(e);
        }

        if (backCount > 0)
            horizonY = backTopSum / backCount - 1.0f;
        else if (grassCount > 0)
            horizonY = grassTopSum / grassCount + 2.5f;
        else
            horizonY = (pc != null ? pc.transform.position.y : cam.transform.position.y) + 2.5f;

        log.AppendLine("Sorting genişletildi: " + remapped + " renderer (x10).  Siluet materyali: " + swapped + " sprite.");
        if (blurred.Count > 0)
            log.AppendLine("Blur'lu ön plan (" + blurred.Count + ") materyaline dokunulmadı.");
    }

    static UnityEngine.SceneManagement.Scene SceneMgr()
    {
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene();
    }

    static string Category(Transform t, int groundLayer)
    {
        if (t.gameObject.layer == groundLayer && groundLayer >= 0)
            return "ground";

        for (Transform p = t; p != null; p = p.parent)
        {
            string n = p.name.ToLowerInvariant();

            if (n.StartsWith("[atmosphere]")) return null;
            if (Regex.IsMatch(n, @"cloud|bulut")) return "cloud";
            if (Regex.IsMatch(n, @"screenbush|bush_front|^frontbush|^foreground")) return "front";
            if (Regex.IsMatch(n, @"backbush|bush_back|adsız|adsiz|katman|^back_layer|^background")) return "back";
            if (Regex.IsMatch(n, @"tree|ağaç|agac")) return "tree";
            if (Regex.IsMatch(n, @"front bush|groundbush|bush_ground|grass|çim|^bushes_ground")) return "grass";
            if (Regex.IsMatch(n, @"^ground$|^zemin")) return "ground";
            if (n.StartsWith("[")) break; // üst gruba geldik
        }

        return null;
    }

    static HashSet<Renderer> BlurControlled()
    {
        HashSet<Renderer> set = new HashSet<Renderer>();
        ForegroundBlurController[] fbs = Object.FindObjectsByType<ForegroundBlurController>(FindObjectsInactive.Include);

        foreach (ForegroundBlurController f in fbs)
            if (f.targetRenderers != null)
                foreach (SpriteRenderer s in f.targetRenderers)
                    if (s != null)
                        set.Add(s);

        return set;
    }

    // =========================================================
    // GÖLGE / IŞIK / POST
    // =========================================================

    static List<Transform> ShadowOwners()
    {
        List<Transform> owners = new List<Transform>();

        foreach (PlayerController p in Object.FindObjectsByType<PlayerController>()) owners.Add(p.transform);
        foreach (TrainingDummy d in Object.FindObjectsByType<TrainingDummy>()) owners.Add(d.transform);
        foreach (EnemyController e in Object.FindObjectsByType<EnemyController>()) owners.Add(e.transform);

        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            CollectNpc(root.transform, owners);

        return owners;
    }

    static Material ShadowMaterial()
    {
        Shader sh = Shader.Find("ProjectM/ProjectedShadow");

        if (sh == null)
        {
            Debug.LogError("Atmosphere: ProjectM/ProjectedShadow shader bulunamadı (derleme hatası?).");
            return null;
        }

        EnsureFolder(MatDir);
        return GetMat("M_ProjectedShadow", sh, null);
    }

    // Karakter şeklinde, yere yatık gölge (ProjectedShadow bileşeni).
    static void AddShadows(Sprite blob, Material unused, AtmosphereRestoreData restore)
    {
        Material mat = ShadowMaterial();

        if (mat == null)
            return;

        int groundMask = LayerMask.GetMask("Ground");
        int n = 0;

        foreach (Transform o in ShadowOwners())
        {
            if (o == null || o.GetComponent<ProjectedShadow>() != null)
                continue;

            ProjectedShadow ps = Undo.AddComponent<ProjectedShadow>(o.gameObject);
            ps.shadowMaterial = mat;
            ps.groundMask = groundMask;
            ps.sortingOrder = OrderShadow;

            restore.addedComponents.Add(ps);
            n++;
        }

        log.AppendLine("Karakter gölgesi (yere yatık): " + n + " obje.");
    }

    // Mevcut kurulumu yükselt: film grain'i kapat, oval gölgeleri karakter gölgesiyle değiştir.
    [MenuItem("Tools/Lighting/Upgrade: Real Shadows + No Grain")]
    public static void Upgrade()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Atmosphere: Play modunda çalışmaz.");
            return;
        }

        log = new StringBuilder("ATMOSFER GÜNCELLENDİ\n");

        GameObject atmos = GameObject.Find(AtmosName);
        AtmosphereRestoreData restore = atmos != null ? atmos.GetComponent<AtmosphereRestoreData>() : null;

        if (restore == null)
        {
            Debug.LogWarning("Atmosphere kurulu değil. Önce Tools > Lighting > Build Moonlit Atmosphere.");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Upgrade Atmosphere");
        int g = Undo.GetCurrentGroup();

        // 1. Film grain kapat
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        FilmGrain grain;

        if (profile != null && profile.TryGet(out grain))
        {
            grain.active = false;
            grain.intensity.Override(0f);
            EditorUtility.SetDirty(grain);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            log.AppendLine("Film grain kapatıldı.");
        }

        // 2. Oval gölgeleri sil
        int removed = 0;

        foreach (GroundShadow gs in Object.FindObjectsByType<GroundShadow>(FindObjectsInactive.Include))
        {
            restore.createdObjects.Remove(gs.gameObject);
            Undo.DestroyObjectImmediate(gs.gameObject);
            removed++;
        }

        log.AppendLine("Oval gölge silindi: " + removed);

        // 3. Karakter gölgeleri
        AddShadows(null, null, restore);

        EditorUtility.SetDirty(restore);
        Undo.CollapseUndoOperations(g);
        EditorSceneManager.MarkSceneDirty(atmos.scene);

        Debug.Log(log.ToString());
    }

    // Ağaçlar da karakterler gibi yere yatık gölge düşürür.
    static void AddTreeShadows(AtmosphereRestoreData restore)
    {
        Material mat = ShadowMaterial();

        if (mat == null)
            return;

        int groundLayer = LayerMask.NameToLayer("Ground");
        int groundMask = LayerMask.GetMask("Ground");
        int n = 0;

        foreach (SpriteRenderer sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
        {
            if (sr == null || Category(sr.transform, groundLayer) != "tree")
                continue;

            GameObject go = sr.gameObject;

            if (go.GetComponent<ProjectedShadow>() != null)
                continue;

            ProjectedShadow ps = Undo.AddComponent<ProjectedShadow>(go);
            ps.shadowMaterial = mat;
            ps.groundMask = groundMask;
            ps.sortingOrder = OrderShadow;
            ps.opacity = 0.45f;
            ps.length = 0.6f;
            ps.skewPerUnit = 0.025f;
            ps.maxSkew = 1.2f;
            ps.maxJumpHeight = 1000f; // ağaç zıplamaz; yerden yüksek pivot gölgeyi soldurmasın

            restore.addedComponents.Add(ps);
            n++;
        }

        if (log != null)
            log.AppendLine("Ağaç gölgesi: " + n + " ağaç.");
    }

    [MenuItem("Tools/Lighting/Add Tree Shadows")]
    public static void AddTreeShadowsMenu()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Atmosphere: Play modunda çalışmaz.");
            return;
        }

        GameObject atmos = GameObject.Find(AtmosName);
        AtmosphereRestoreData restore = atmos != null ? atmos.GetComponent<AtmosphereRestoreData>() : null;

        if (restore == null)
        {
            Debug.LogWarning("Atmosphere kurulu değil. Önce Tools > Lighting > Build Moonlit Atmosphere.");
            return;
        }

        log = new StringBuilder("AĞAÇ GÖLGELERİ\n");

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Add Tree Shadows");
        int g = Undo.GetCurrentGroup();

        AddTreeShadows(restore);

        EditorUtility.SetDirty(restore);
        Undo.CollapseUndoOperations(g);
        EditorSceneManager.MarkSceneDirty(atmos.scene);

        Debug.Log(log.ToString() + "Gölgeler Play'de görünür.");
    }

    // Yerdeki ışık: üstü ilk halindeki genişlikte, tabanı aşağı doğru yayılan yumuşak leke (ground_light.png).
    static readonly Color GroundLightColor = new Color(1f, 0.78f, 0.55f, 0.2f);
    static Vector2 GroundLightPos(float viewH) { return new Vector2(0f, -viewH * 0.24f); }
    static Vector2 GroundLightSize(float viewW, float viewH) { return new Vector2(viewW * 1.6f, viewH * 0.2f); }

    [MenuItem("Tools/Lighting/Soften Ground Light")]
    public static void SoftenGroundLight()
    {
        GameObject atmos = GameObject.Find(AtmosName);
        Camera cam = Camera.main;
        Transform t = atmos != null ? FindDeep(atmos.transform, "Ground Light") : null;

        if (t == null || cam == null)
        {
            Debug.LogWarning("Atmosphere: 'Ground Light' ya da Main Camera bulunamadı.");
            return;
        }

        Sprite horizon = LoadSprite("ground_light.png", FilterMode.Bilinear);
        SpriteRenderer sr = t.GetComponent<SpriteRenderer>();

        if (sr == null || horizon == null)
            return;

        float viewH = cam.orthographic ? cam.orthographicSize * 2f : 20f;
        float viewW = viewH * Mathf.Max(1.2f, cam.aspect);

        Undo.RecordObject(sr, "Soften ground light");
        Undo.RecordObject(t, "Soften ground light");

        sr.sprite = horizon;
        sr.color = GroundLightColor;

        Vector2 pos = GroundLightPos(viewH);
        Vector2 size = GroundLightSize(viewW, viewH);
        Vector2 sp = horizon.bounds.size;

        t.localPosition = new Vector3(pos.x, pos.y, 0f);
        t.localScale = new Vector3(size.x / sp.x, size.y / sp.y, 1f);

        EditorSceneManager.MarkSceneDirty(atmos.scene);
        Debug.Log("Yerdeki ışık yayıldı ve hafifletildi. Daha da azaltmak için 'Ground Light' rengindeki Alpha'yı düşür.");
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name)
            return t;

        foreach (Transform c in t)
        {
            Transform f = FindDeep(c, name);

            if (f != null)
                return f;
        }

        return null;
    }

    // Karakterlerin dış kenarına arkadan gelen sıcak ışık.
    static void AddCharacterRims(AtmosphereRestoreData restore)
    {
        Shader sh = Shader.Find("ProjectM/CharacterRim");

        if (sh == null)
        {
            Debug.LogError("Atmosphere: ProjectM/CharacterRim shader bulunamadı (derleme hatası?).");
            return;
        }

        EnsureFolder(MatDir);
        Material mat = GetMat("M_CharacterRim", sh, null);
        int n = 0;

        foreach (Transform o in ShadowOwners())
        {
            if (o == null || o.GetComponent<CharacterRim>() != null)
                continue;

            CharacterRim cr = Undo.AddComponent<CharacterRim>(o.gameObject);
            cr.rimMaterial = mat;

            restore.addedComponents.Add(cr);
            n++;
        }

        if (log != null)
            log.AppendLine("Karakter kenar ışığı: " + n + " obje.");
    }

    [MenuItem("Tools/Lighting/Add Character Rim Light")]
    public static void AddCharacterRimsMenu()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Atmosphere: Play modunda çalışmaz.");
            return;
        }

        GameObject atmos = GameObject.Find(AtmosName);
        AtmosphereRestoreData restore = atmos != null ? atmos.GetComponent<AtmosphereRestoreData>() : null;

        if (restore == null)
        {
            Debug.LogWarning("Atmosphere kurulu değil. Önce Tools > Lighting > Build Moonlit Atmosphere.");
            return;
        }

        log = new StringBuilder("KARAKTER KENAR IŞIĞI\n");

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Add Character Rim Light");
        int g = Undo.GetCurrentGroup();

        AddCharacterRims(restore);

        EditorUtility.SetDirty(restore);
        Undo.CollapseUndoOperations(g);
        EditorSceneManager.MarkSceneDirty(atmos.scene);

        Debug.Log(log.ToString() + "Kenar ışığı Play'de görünür.");
    }

    static void CollectNpc(Transform t, List<Transform> owners)
    {
        if (Regex.IsMatch(t.name.ToLowerInvariant(), @"^demirci|^npc"))
        {
            owners.Add(t);
            return;
        }

        foreach (Transform c in t)
            CollectNpc(c, owners);
    }

    static void TuneGlobalLight(AtmosphereRestoreData restore)
    {
        foreach (Light2D l in Object.FindObjectsByType<Light2D>())
        {
            if (l.lightType != Light2D.LightType.Global)
                continue;

            restore.globalLight = l;
            restore.globalLightIntensity = l.intensity;
            restore.globalLightColor = l.color;

            Undo.RecordObject(l, "Global light");
            l.intensity = 0.9f;
            l.color = new Color(1f, 0.92f, 0.84f);

            log.AppendLine("Global Light 2D: sıcak, %90.");
            return;
        }
    }

    static Volume CreateVolume(Transform parent)
    {
        EnsureFolder(Root);

        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath) != null)
            AssetDatabase.DeleteAsset(ProfilePath);

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, ProfilePath);

        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.8f);
        bloom.intensity.Override(1.2f);
        bloom.scatter.Override(0.72f);
        bloom.tint.Override(new Color(1f, 0.85f, 0.7f));

        Vignette vig = profile.Add<Vignette>(true);
        vig.intensity.Override(0.42f);
        vig.smoothness.Override(0.5f);
        vig.color.Override(new Color(0.05f, 0.04f, 0.03f));

        ColorAdjustments ca = profile.Add<ColorAdjustments>(true);
        ca.contrast.Override(12f);
        ca.saturation.Override(-14f);
        ca.colorFilter.Override(new Color(1f, 0.95f, 0.88f));

        Tonemapping tm = profile.Add<Tonemapping>(true);
        tm.mode.Override(TonemappingMode.Neutral);

        foreach (VolumeComponent vc in profile.components)
        {
            vc.name = vc.GetType().Name;
            AssetDatabase.AddObjectToAsset(vc, profile);
        }

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        GameObject go = new GameObject("Global Volume (Moonlit)");
        Undo.RegisterCreatedObjectUndo(go, "Volume");
        go.transform.SetParent(parent, false);

        Volume v = go.AddComponent<Volume>();
        v.isGlobal = true;
        v.priority = 10f;
        v.sharedProfile = profile;

        return v;
    }

    static void EnablePostOnCameras()
    {
        foreach (Camera c in Object.FindObjectsByType<Camera>())
        {
            UniversalAdditionalCameraData d = c.GetComponent<UniversalAdditionalCameraData>();

            if (d == null)
                continue;

            Undo.RecordObject(d, "Post");
            d.renderPostProcessing = true;
        }

        log.AppendLine("Kameralarda Post Processing açıldı.");
    }

    // =========================================================
    // YARDIMCILAR
    // =========================================================

    static void CreateDust(Transform parent, Sprite glow, float viewW, float viewH)
    {
        GameObject go = new GameObject("Dust Motes");
        Undo.RegisterCreatedObjectUndo(go, "Dust");
        go.transform.SetParent(parent, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(viewH * 0.004f, viewH * 0.011f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.75f, 0.35f), new Color(1f, 0.95f, 0.85f, 0.8f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;

        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 7f;

        ParticleSystem.ShapeModule sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(viewW * 1.2f, viewH * 0.9f, 0.1f);

        ParticleSystem.NoiseModule nz = ps.noise;
        nz.enabled = true;
        nz.strength = 0.25f;
        nz.frequency = 0.3f;

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;

        ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
        Material m = new Material(Shader.Find("Sprites/Default"));
        m.mainTexture = glow.texture;
        string path = MatDir + "/M_Dust.mat";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);
        pr.sharedMaterial = m;
        pr.sortingOrder = OrderDust;

        ps.Play();
    }

    static Transform Child(Transform parent, string name)
    {
        GameObject g = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(g, name);
        g.transform.SetParent(parent, false);
        return g.transform;
    }

    static SpriteRenderer Spr(Transform parent, string name, Sprite sprite, Material mat, Color color, int order, Vector2 localPos, Vector2 worldSize)
    {
        GameObject g = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(g, name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = new Vector3(localPos.x, localPos.y, 0f);

        SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sharedMaterial = mat;
        sr.color = color;
        sr.sortingOrder = order;

        Vector2 s = sprite.bounds.size;
        g.transform.localScale = new Vector3(worldSize.x / Mathf.Max(0.0001f, s.x), worldSize.y / Mathf.Max(0.0001f, s.y), 1f);

        return sr;
    }

    static void SetRim(Material m, Color tint, Color hazeColor, float hazeAmt, float hazeBottom, Color rim, float rimIntensity, float rimWidth, float rimTop)
    {
        m.SetColor("_Color", tint);
        m.SetColor("_HazeColor", hazeColor);
        m.SetFloat("_Haze", hazeAmt);
        m.SetFloat("_HazeBottom", hazeBottom);
        m.SetColor("_RimColor", rim);
        m.SetFloat("_RimIntensity", rimIntensity);
        m.SetFloat("_RimWidth", rimWidth);
        m.SetFloat("_RimTop", rimTop);
    }

    static Material GetMat(string name, Shader shader, System.Action<Material> setup)
    {
        string path = MatDir + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        else
        {
            m.shader = shader;
        }

        if (setup != null)
            setup(m);

        EditorUtility.SetDirty(m);
        return m;
    }

    static Sprite LoadSprite(string file, FilterMode filter)
    {
        string path = TexDir + "/" + file;
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;

        if (ti == null)
            return null;

        bool dirty =
            ti.textureType != TextureImporterType.Sprite ||
            ti.spriteImportMode != SpriteImportMode.Single ||
            ti.mipmapEnabled ||
            ti.wrapMode != TextureWrapMode.Clamp ||
            ti.textureCompression != TextureImporterCompression.Uncompressed;

        if (dirty)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 100f;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = filter;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
#endif
