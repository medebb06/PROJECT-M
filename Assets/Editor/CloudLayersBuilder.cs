#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Lighting > Build Cloud Layers
///   İlk çalıştırma: bulutları boyutlarına göre 3 derinlik katmanına böler (Uzak / Orta / Yakın),
///   yükseklik ve boyutlarını katmana göre dizer, her katmana kendi parallax'ını, materyalini
///   (ay ışığıyla aydınlanma) ve tek yönlü akış hızını verir.
///   Sonraki çalıştırmalar: konumlara dokunmaz, sadece materyal / parallax / hız ayarlarını yeniden uygular.
///
/// Katman materyalleri: Assets/Art/Atmosphere/Materials/M_Cloud_Far / Mid / Near
/// Hız ve yön: her bulutun CloudDrift bileşeninde. Parallax: Clouds_Far / Mid / Near objelerinde.
/// </summary>
public static class CloudLayersBuilder
{
    private const string MatDir = "Assets/Art/Atmosphere/Materials";

    private class Layer
    {
        public string name;
        public int order;
        public float scaleMul;
        public float parX, parY;
        public float speed;
        public float bob;
        public float[] xs;
        public float[] ys;
        public Material mat;
    }

    private static Layer[] MakeLayers()
    {
        return new[]
        {
            new Layer { name = "Clouds_Far",  order = -53, scaleMul = 0.65f, parX = 0.96f, parY = 0.90f, speed = 0.10f, bob = 0.05f,
                        xs = new[] { -45f, -12f, 28f, 62f },  ys = new[] { 3.4f, 4.7f, 3.8f, 5.0f } },
            new Layer { name = "Clouds_Mid",  order = -50, scaleMul = 1.00f, parX = 0.90f, parY = 0.80f, speed = 0.22f, bob = 0.09f,
                        xs = new[] { -50f, 22f, 60f },        ys = new[] { 6.0f, 7.6f, 6.6f } },
            new Layer { name = "Clouds_Near", order = -47, scaleMul = 1.20f, parX = 0.82f, parY = 0.70f, speed = 0.42f, bob = 0.14f,
                        xs = new[] { -38f, 40f, 75f },        ys = new[] { 9.2f, 8.5f, 10.0f } },
        };
    }

    [MenuItem("Tools/Lighting/Build Cloud Layers")]
    private static void Build()
    {
        Transform container = FindContainer();

        if (container == null)
        {
            Debug.LogError("Cloud Layers: 'Clouds' objesi bulunamadı ([WORLD]/Clouds).");
            return;
        }

        Shader rim = Shader.Find("ProjectM/SilhouetteRim");

        if (rim == null)
        {
            Debug.LogError("Cloud Layers: ProjectM/SilhouetteRim shader bulunamadı (derleme hatası?).");
            return;
        }

        Undo.SetCurrentGroupName("Build Cloud Layers");
        int undoGroup = Undo.GetCurrentGroup();

        Layer[] layers = MakeLayers();

        layers[0].mat = MakeMat("M_Cloud_Far", rim, m =>
        {
            Set(m, new Color(0.70f, 0.68f, 0.68f, 0.92f), new Color(0.66f, 0.59f, 0.52f), 0.55f, 0.30f,
                new Color(1.10f, 1.00f, 0.85f), 0.6f, 5f, 0f, 0.15f, 0.8f, 0.5f, 0.35f, 4f, 14f);
        });
        layers[1].mat = MakeMat("M_Cloud_Mid", rim, m =>
        {
            Set(m, new Color(0.60f, 0.58f, 0.58f, 1f), new Color(0.66f, 0.60f, 0.54f), 0.25f, 0.25f,
                new Color(1.30f, 1.15f, 0.90f), 1.8f, 7f, 0f, 0.5f, 1.4f, 0.25f, 0.5f, 6f, 18f);
        });
        layers[2].mat = MakeMat("M_Cloud_Near", rim, m =>
        {
            Set(m, new Color(0.30f, 0.29f, 0.30f, 1f), new Color(0.55f, 0.50f, 0.46f), 0f, 0.12f,
                new Color(1.40f, 1.25f, 1.00f), 3.2f, 9f, 0f, 1.0f, 1.3f, 0.2f, 0.6f, 7f, 22f);
        });

        // Bulutları topla (daha önce katmanlanmışsa katmanlardan da).
        List<SpriteRenderer> clouds = new List<SpriteRenderer>();

        foreach (SpriteRenderer sr in container.GetComponentsInChildren<SpriteRenderer>(true))
            if (sr.name.StartsWith("Cloud"))
                clouds.Add(sr);

        if (clouds.Count == 0)
        {
            Debug.LogError("Cloud Layers: 'Clouds' altında bulut sprite'ı bulunamadı.");
            return;
        }

        bool alreadyLayered = container.Find(layers[0].name) != null;

        // Eski tek-parça parallax'ı kaldır (katmanlar kendi parallax'ını taşıyor; ikisi birden çift kayma yapar).
        ParallaxEffect old = container.GetComponent<ParallaxEffect>();

        if (old != null)
            Undo.DestroyObjectImmediate(old);

        // Katman objeleri
        Transform[] groups = new Transform[3];

        for (int i = 0; i < 3; i++)
        {
            Transform g = container.Find(layers[i].name);

            if (g == null)
            {
                var go = new GameObject(layers[i].name);
                Undo.RegisterCreatedObjectUndo(go, "Build Cloud Layers");
                go.transform.SetParent(container, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localScale = Vector3.one;
                g = go.transform;
            }

            ParallaxEffect pe = g.GetComponent<ParallaxEffect>();

            if (pe == null)
                pe = Undo.AddComponent<ParallaxEffect>(g.gameObject);
            else
                Undo.RecordObject(pe, "Cloud Parallax");

            pe.parallaxX = layers[i].parX;
            pe.parallaxY = layers[i].parY;
            pe.reference = ParallaxEffect.Reference.CameraStart;
            EditorUtility.SetDirty(pe);
            groups[i] = g;
        }

        // Katman ataması: ilk çalıştırmada boyuta göre (küçük = uzak). Sonrasında bulunduğu katmanda kalır.
        int[] layerOf = new int[clouds.Count];

        if (!alreadyLayered)
        {
            var idx = new List<int>();
            for (int i = 0; i < clouds.Count; i++) idx.Add(i);
            idx.Sort((a, b) => Size(clouds[a]).CompareTo(Size(clouds[b])));

            int far = layers[0].xs.Length, mid = layers[1].xs.Length;

            for (int k = 0; k < idx.Count; k++)
                layerOf[idx[k]] = k < far ? 0 : (k < far + mid ? 1 : 2);
        }
        else
        {
            for (int i = 0; i < clouds.Count; i++)
            {
                Transform p = clouds[i].transform.parent;
                layerOf[i] = p == groups[0] ? 0 : (p == groups[1] ? 1 : 2);
            }
        }

        var counters = new int[3];
        var log = new StringBuilder("Bulut katmanları:\n");

        for (int i = 0; i < clouds.Count; i++)
        {
            SpriteRenderer sr = clouds[i];
            int li = layerOf[i];
            Layer L = layers[li];
            int n = counters[li]++;

            Undo.RecordObject(sr.transform, "Cloud Layer");
            Undo.RecordObject(sr, "Cloud Layer");

            if (!alreadyLayered)
            {
                float jx = Jit(sr.name, 1) * 5f;
                float jy = Jit(sr.name, 2) * 0.4f;
                float x = L.xs[Mathf.Min(n, L.xs.Length - 1)] + jx;
                float y = L.ys[Mathf.Min(n, L.ys.Length - 1)] + jy;

                sr.transform.SetParent(groups[li], true);
                sr.transform.position = new Vector3(x, y, sr.transform.position.z);
                sr.transform.localScale = sr.transform.localScale * L.scaleMul;
            }

            sr.sortingOrder = L.order;
            sr.sharedMaterial = L.mat;
            sr.color = Color.white;     // renk artık materyalde (katmana göre)

            CloudDrift cd = sr.GetComponent<CloudDrift>();

            if (cd == null)
                cd = Undo.AddComponent<CloudDrift>(sr.gameObject);
            else
                Undo.RecordObject(cd, "Cloud Drift");

            cd.speed = L.speed * Mathf.Lerp(0.8f, 1.25f, Mathf.Abs(Jit(sr.name, 3)));
            cd.direction = 1f;
            cd.bobAmplitude = L.bob;
            cd.bobPeriod = 55f;
            cd.wrapHalfSpan = 80f;

            EditorUtility.SetDirty(sr);
            EditorUtility.SetDirty(cd);
            log.AppendLine($"  {sr.name,-10} -> {L.name}  x {sr.transform.position.x:0.#}  y {sr.transform.position.y:0.#}  hız {cd.speed:0.00}");
        }

        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

        log.AppendLine("Parallax: Clouds_Far/Mid/Near objeleri. Materyal: Assets/Art/Atmosphere/Materials/M_Cloud_*. Sahneyi kaydet (Ctrl+S).");
        Debug.Log(log.ToString());
    }

    private static void Set(Material m, Color tint, Color hazeColor, float haze, float hazeBottom,
                            Color rim, float rimIntensity, float rimWidth, float rimTop,
                            float normalIntensity, float normalPower, float moonRimMin,
                            float moonFade, float moonFadeRadius, float moonRimRadius)
    {
        m.SetColor("_Color", tint);
        m.SetColor("_HazeColor", hazeColor);
        m.SetFloat("_Haze", haze);
        m.SetFloat("_HazeBottom", hazeBottom);
        m.SetColor("_RimColor", rim);
        m.SetFloat("_RimIntensity", rimIntensity);
        m.SetFloat("_RimWidth", rimWidth);
        m.SetFloat("_RimTop", rimTop);
        m.SetFloat("_NormalIntensity", normalIntensity);
        m.SetFloat("_NormalPower", normalPower);
        m.SetFloat("_LightBack", 0.35f);
        m.SetFloat("_CoreShadow", Mathf.Lerp(0.15f, 0.55f, Mathf.Clamp01(normalIntensity)));
        m.SetFloat("_RimIntensity", 0f);      // düz bulut: kenar parlaması yok
        m.SetFloat("_NormalIntensity", 0f);
        m.SetFloat("_CoreShadow", 0f);
        m.SetFloat("_WindAmount", 0f);
        m.SetFloat("_FlutterAmount", 0f);

        // Ay: ışık kaynağı olarak ay, yakınında yarı saydam.
        m.SetFloat("_UseMoon", 1f);
        m.SetFloat("_MoonRimMin", moonRimMin);
        m.SetFloat("_MoonRimRadius", moonRimRadius);
        m.SetFloat("_MoonFade", moonFade);
        m.SetFloat("_MoonFadeRadius", moonFadeRadius);
    }

    private static Material MakeMat(string name, Shader sh, System.Action<Material> setup)
    {
        string path = MatDir + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (m == null)
        {
            m = new Material(sh) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }

        m.shader = sh;
        setup(m);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static float Size(SpriteRenderer sr)
    {
        Vector3 s = sr.transform.lossyScale;
        return Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y));
    }

    // İsimden kararlı, -1..1 arası sahte rastgele.
    private static float Jit(string s, int salt)
    {
        int h = salt * 7919;

        foreach (char c in s)
            h = h * 31 + c;

        return (Mathf.Abs(Mathf.Sin(h * 12.9898f) * 43758.5453f) % 1f) * 2f - 1f;
    }

    private static Transform FindContainer()
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == "Clouds" && t.GetComponentInChildren<SpriteRenderer>(true) != null)
                return t;

        return null;
    }
}
#endif
