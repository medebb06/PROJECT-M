#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Tools > Lighting > Generate Rounded Light (Normal Maps)
///
/// Ağaç / çalı sprite'larının alfa şeklinden "kubbe" normal map'i üretir:
/// kenarlar yuvarlak bir topun kenarı gibi dışa bakar, iç kısım düz kalır.
/// Normal map, sprite'a '_NormalMap' ikincil dokusu olarak bağlanır (Sprite Editor > Secondary Textures).
/// SilhouetteRim shader'ı bununla ışığa bakan kenarları YUMUŞAKÇA, hacimliymiş gibi parlatır.
///
/// Üretilen dosyalar: kaynak dokunun yanında  <ad>_N.png  (yarı çözünürlük).
/// Tekrar çalıştırmak güvenlidir (üzerine yazar).
/// </summary>
public static class NormalMapGenerator
{
    // Kategori başına kubbe yarıçapı (dokunun kısa kenarına oranla).
    static readonly Dictionary<string, float> RadiusFrac = new Dictionary<string, float>
    {
        { "tree", 0.09f },
        { "back", 0.11f },
        { "grass", 0.05f },
    };

    [MenuItem("Tools/Lighting/Generate Rounded Light (Normal Maps)")]
    public static void Generate()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("NormalMapGenerator: Play modunda çalışmaz.");
            return;
        }

        // ---- hangi dokular, hangi kategori ----
        Dictionary<string, string> texCat = new Dictionary<string, string>();
        int groundLayer = LayerMask.NameToLayer("Ground");

        foreach (SpriteRenderer sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
        {
            if (sr == null || sr.sprite == null)
                continue;

            string cat = Category(sr.transform, groundLayer);

            if (cat == null || !RadiusFrac.ContainsKey(cat))
                continue;

            string path = AssetDatabase.GetAssetPath(sr.sprite.texture);

            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/") || path.EndsWith("_N.png"))
                continue;

            if (!texCat.ContainsKey(path))
                texCat[path] = cat;
        }

        if (texCat.Count == 0)
        {
            Debug.LogWarning("NormalMapGenerator: Ağaç/çalı sprite'ı bulunamadı.");
            return;
        }

        StringBuilder log = new StringBuilder("YUVARLAK IŞIK: normal map üretildi\n");
        int done = 0, i = 0;

        try
        {
            foreach (KeyValuePair<string, string> kv in texCat)
            {
                i++;
                EditorUtility.DisplayProgressBar("Normal map üretiliyor", Path.GetFileName(kv.Key), i / (float)texCat.Count);

                string result = Process(kv.Key, RadiusFrac[kv.Value]);
                log.AppendLine("  " + Path.GetFileName(kv.Key) + " (" + kv.Value + "): " + result);

                if (result == "OK")
                    done++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        ApplyMaterialSettings();
        log.AppendLine(done + "/" + texCat.Count + " doku. Materyaller güncellendi (M_Rim_Tree / BackBush / GroundBush).");
        Debug.Log(log.ToString());
    }

    // Yuvarlak ışığın şiddeti (materyallerden de elle ayarlanabilir).
    public static void ApplyMaterialSettings()
    {
        Set("M_Rim_Tree", 1.25f, 0.12f, 0.15f);
        Set("M_Rim_BackBush", 0.8f, 0.15f, 0.25f);
        Set("M_Rim_GroundBush", 0.6f, 0.3f, 0.15f);
    }

    static void Set(string mat, float normalIntensity, float rimIntensity, float coreShadow)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Atmosphere/Materials/" + mat + ".mat");

        if (m == null)
            return;

        Undo.RecordObject(m, "Rounded light");
        m.SetFloat("_NormalIntensity", normalIntensity);
        m.SetFloat("_RimIntensity", rimIntensity);
        m.SetFloat("_CoreShadow", coreShadow);
        m.SetFloat("_NormalPower", 1.3f);
        m.SetFloat("_LightBack", 0.35f);
        EditorUtility.SetDirty(m);
    }

    // =========================================================

    static string Process(string texPath, float radiusFrac)
    {
        string full = Path.Combine(Directory.GetCurrentDirectory(), texPath);

        if (!File.Exists(full))
            return "dosya yok";

        string ext = Path.GetExtension(texPath).ToLowerInvariant();

        if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
            return "atlandı (" + ext + " desteklenmiyor)";

        Texture2D src = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        if (!src.LoadImage(File.ReadAllBytes(full)))
            return "okunamadı";

        // Yarı çözünürlükte çalış (normal map yumuşak; hız + bellek).
        int W = Mathf.Max(8, src.width / 2);
        int H = Mathf.Max(8, src.height / 2);

        float[] alpha = Downsample(src, W, H);
        Object.DestroyImmediate(src);

        float R = Mathf.Clamp(Mathf.Min(W, H) * radiusFrac, 4f, 120f);

        // Kenara uzaklık (piksel).
        float[] dist = DistanceToEdge(alpha, W, H);

        // Kubbe yüksekliği: çeyrek daire profili (kenarda dik, içeride düz).
        float[] h = new float[W * H];

        for (int k = 0; k < h.Length; k++)
        {
            float t = Mathf.Min(dist[k] / R, 1f);
            float u = 1f - t;
            h[k] = R * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u));
        }

        // Yumuşak kubbe: kademelenme / halka izleri kalmasın.
        h = GaussianBlur(h, W, H, Mathf.Max(1.5f, R * 0.15f));

        Texture2D outTex = new Texture2D(W, H, TextureFormat.RGB24, false);
        Color32[] px = new Color32[W * H];

        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                int k = y * W + x;

                if (alpha[k] < 0.02f)
                {
                    px[k] = new Color32(128, 128, 255, 255);
                    continue;
                }

                float dx = (h[y * W + Mathf.Min(x + 1, W - 1)] - h[y * W + Mathf.Max(x - 1, 0)]) * 0.5f;
                float dy = (h[Mathf.Min(y + 1, H - 1) * W + x] - h[Mathf.Max(y - 1, 0) * W + x]) * 0.5f;

                Vector3 n = new Vector3(-dx, -dy, 1f).normalized;

                // Titreşimli (dither) yuvarlama: 8 bitte bant/basamak oluşmasın.
                float j = Hash(x, y) - 0.5f;

                px[k] = new Color32(
                    Q(n.x, j),
                    Q(n.y, -j),
                    Q(n.z, j * 0.5f),
                    255);
            }
        }

        outTex.SetPixels32(px);
        outTex.Apply();

        string nPath = Path.Combine(Path.GetDirectoryName(texPath), Path.GetFileNameWithoutExtension(texPath) + "_N.png").Replace('\\', '/');
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), nPath), outTex.EncodeToPNG());
        Object.DestroyImmediate(outTex);

        AssetDatabase.ImportAsset(nPath, ImportAssetOptions.ForceUpdate);

        TextureImporter nImp = AssetImporter.GetAtPath(nPath) as TextureImporter;

        if (nImp != null)
        {
            nImp.textureType = TextureImporterType.Default;
            nImp.sRGBTexture = false;
            nImp.mipmapEnabled = true;
            nImp.wrapMode = TextureWrapMode.Clamp;
            nImp.textureCompression = TextureImporterCompression.Uncompressed; // sıkıştırma bant yapar
            nImp.filterMode = FilterMode.Bilinear;
            nImp.SaveAndReimport();
        }

        Texture2D nTex = AssetDatabase.LoadAssetAtPath<Texture2D>(nPath);

        if (nTex == null)
            return "normal map yüklenemedi";

        // ---- ikincil doku olarak bağla ----
        TextureImporter srcImp = AssetImporter.GetAtPath(texPath) as TextureImporter;

        if (srcImp == null)
            return "importer yok";

        SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
        factories.Init();

        ISpriteEditorDataProvider dp = factories.GetSpriteEditorDataProviderFromObject(srcImp);

        if (dp == null)
            return "sprite data provider yok";

        dp.InitSpriteEditorDataProvider();
        ISecondaryTextureDataProvider sec = dp.GetDataProvider<ISecondaryTextureDataProvider>();

        if (sec == null)
            return "secondary texture desteklenmiyor";

        List<SecondarySpriteTexture> list = new List<SecondarySpriteTexture>();

        if (sec.textures != null)
            foreach (SecondarySpriteTexture t in sec.textures)
                if (t.name != "_NormalMap")
                    list.Add(t);

        list.Add(new SecondarySpriteTexture { name = "_NormalMap", texture = nTex });
        sec.textures = list.ToArray();

        dp.Apply();
        srcImp.SaveAndReimport();

        return "OK";
    }

    static byte Q(float v, float jitter)
    {
        return (byte)Mathf.Clamp(Mathf.FloorToInt((v * 0.5f + 0.5f) * 255f + 0.5f + jitter), 0, 255);
    }

    static float Hash(int x, int y)
    {
        uint h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
    }

    // Ayrılabilir Gauss bulanıklığı.
    static float[] GaussianBlur(float[] src, int W, int H, float sigma)
    {
        int r = Mathf.CeilToInt(sigma * 2.5f);
        float[] k = new float[r * 2 + 1];
        float sum = 0f;

        for (int i = -r; i <= r; i++)
        {
            k[i + r] = Mathf.Exp(-(i * i) / (2f * sigma * sigma));
            sum += k[i + r];
        }

        for (int i = 0; i < k.Length; i++)
            k[i] /= sum;

        float[] tmp = new float[src.Length];
        float[] dst = new float[src.Length];

        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float a = 0f;
                for (int i = -r; i <= r; i++)
                    a += src[y * W + Mathf.Clamp(x + i, 0, W - 1)] * k[i + r];
                tmp[y * W + x] = a;
            }

        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float a = 0f;
                for (int i = -r; i <= r; i++)
                    a += tmp[Mathf.Clamp(y + i, 0, H - 1) * W + x] * k[i + r];
                dst[y * W + x] = a;
            }

        return dst;
    }

    // Kutu ortalamasıyla küçült (kenarlar merdiven gibi olmasın).
    static float[] Downsample(Texture2D src, int W, int H)
    {
        Color32[] s = src.GetPixels32();
        int sw = src.width, sh = src.height;
        float[] a = new float[W * H];

        for (int y = 0; y < H; y++)
        {
            int y0 = y * sh / H, y1 = Mathf.Max(y0 + 1, (y + 1) * sh / H);

            for (int x = 0; x < W; x++)
            {
                int x0 = x * sw / W, x1 = Mathf.Max(x0 + 1, (x + 1) * sw / W);
                float sum = 0f;
                int c = 0;

                for (int yy = y0; yy < y1 && yy < sh; yy++)
                    for (int xx = x0; xx < x1 && xx < sw; xx++)
                    {
                        sum += s[yy * sw + xx].a;
                        c++;
                    }

                a[y * W + x] = c > 0 ? sum / (255f * c) : 0f;
            }
        }

        return a;
    }

    // Felzenszwalb kesin Öklid uzaklık dönüşümü (opak pikselin en yakın saydam piksele uzaklığı).
    static float[] DistanceToEdge(float[] alpha, int W, int H)
    {
        const float INF = 1e20f;
        float[] f = new float[W * H];

        for (int k = 0; k < f.Length; k++)
            f[k] = alpha[k] < 0.5f ? 0f : INF;

        // Doku kenarı da "dış" sayılsın.
        for (int x = 0; x < W; x++) { f[x] = 0f; f[(H - 1) * W + x] = 0f; }
        for (int y = 0; y < H; y++) { f[y * W] = 0f; f[y * W + W - 1] = 0f; }

        int n = Mathf.Max(W, H);
        float[] tmpF = new float[n];
        float[] tmpD = new float[n];
        int[] v = new int[n];
        float[] z = new float[n + 1];

        for (int x = 0; x < W; x++)
        {
            for (int y = 0; y < H; y++) tmpF[y] = f[y * W + x];
            Edt1D(tmpF, H, tmpD, v, z);
            for (int y = 0; y < H; y++) f[y * W + x] = tmpD[y];
        }

        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++) tmpF[x] = f[y * W + x];
            Edt1D(tmpF, W, tmpD, v, z);
            for (int x = 0; x < W; x++) f[y * W + x] = Mathf.Sqrt(tmpD[x]);
        }

        return f;
    }

    static void Edt1D(float[] f, int n, float[] d, int[] v, float[] z)
    {
        const float INF = 1e20f;
        int k = 0;
        v[0] = 0;
        z[0] = -INF;
        z[1] = INF;

        for (int q = 1; q < n; q++)
        {
            float s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2f * q - 2f * v[k]);

            while (s <= z[k])
            {
                k--;
                s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2f * q - 2f * v[k]);
            }

            k++;
            v[k] = q;
            z[k] = s;
            z[k + 1] = INF;
        }

        k = 0;

        for (int q = 0; q < n; q++)
        {
            while (z[k + 1] < q)
                k++;

            float dq = q - v[k];
            d[q] = dq * dq + f[v[k]];
        }
    }

    static float[] Blur(float[] src, int W, int H)
    {
        float[] dst = new float[src.Length];

        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float sum = 0f;
                int c = 0;

                for (int oy = -1; oy <= 1; oy++)
                {
                    int yy = y + oy;
                    if (yy < 0 || yy >= H) continue;

                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int xx = x + ox;
                        if (xx < 0 || xx >= W) continue;

                        sum += src[yy * W + xx];
                        c++;
                    }
                }

                dst[y * W + x] = sum / c;
            }
        }

        return dst;
    }

    // AtmosphereBuilder ile aynı sınıflandırma.
    static string Category(Transform t, int groundLayer)
    {
        if (groundLayer >= 0 && t.gameObject.layer == groundLayer)
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
            if (n.StartsWith("[")) break;
        }

        return null;
    }
}
#endif
