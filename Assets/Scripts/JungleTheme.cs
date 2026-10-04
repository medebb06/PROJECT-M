using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ORMAN TEMASI (Jungle/Tiles.png). LevelGenerator bu bileşeni bulursa
/// rastgele haritayı bu tileset ile boyar ve süsler.
///
/// KURULUM (bir kez):
///   1) LevelGenerator'ın olduğu objeye (Managers) 'Jungle Theme' ekle.
///      (LevelGenerator sahnede yoksa onu da aynı objeye ekle.)
///   2) 'Sheet' alanına Assets/Sprites/Tile/Jungle/Tiles.png'yi sürükle.
///      Sprite'lar otomatik dolar (Sprite Editor'da 16x16 'Grid By Cell
///      Size' ile dilimlenmiş olmalı).
///
/// Tile'lar sayfadaki KONUMLARINA göre tanınır (sütun, satır; sol üst 0,0):
///   Sol üst çim seti (0-4, 0-4):
///     satır 0 = çimin üste taşan uçları (süs, çarpışmasız)
///     satır 1 = yüzey (sol köşe, 3 orta, sağ köşe)
///     satır 2-3 = toprak (sol kenar, 6 orta varyasyon, sağ kenar)
///     satır 4 = alt kenar
///   Kaya seti (0-4, 5-9) aynı düzende ('Ground Style = Rock').
///   Tahta platform (5-7, 6), ip köprü (5-9, 7-9), ağaç (9-13, 0-9),
///   arka çalılar (17-24, 0-14), su (6-9, 19-20), kayalar (0-14, 21-22),
///   mantar / çiçek / saz / bitki (15-22, 15-23).
/// </summary>
public class JungleTheme : MonoBehaviour
{
    public enum GroundStyle
    {
        Grass,
        Rock
    }

    [Header("Tileset")]
    [Tooltip("Tiles.png. Sürükleyince 'Sprites' otomatik dolar (yalnızca Editor'de).")]
    public Texture2D sheet;

    [Tooltip("Sayfanın tüm sprite'ları (elle de sürüklenebilir).")]
    public Sprite[] sprites;

    [Tooltip("Bir tile'ın piksel boyu.")]
    [Min(1)] public int cellPixels = 16;

    // Her sprite'ın sayfadaki (sütun, satır) yeri. Editor'de hesaplanıp kaydedilir.
    [HideInInspector] public Vector2Int[] spriteCells;
    [HideInInspector] public int cellsForCount = -1;

    [Header("Zemin")]
    public GroundStyle groundStyle = GroundStyle.Grass;

    [Tooltip("Yüzeyin üstüne çimin taşan uçlarını koy.")]
    public bool grassOverhang = true;

    [Header("Süsler (çarpışmasız)")]
    [Tooltip("Yüzeydeki her karede küçük süs (çim tutamı, mantar, çiçek) olasılığı.")]
    [Range(0f, 1f)] public float propChance = 0.22f;

    [Tooltip("İki kat boylu süs (saz, lavanta, mavi çiçek) olasılığı.")]
    [Range(0f, 1f)] public float tallPropChance = 0.1f;

    [Tooltip("2x2 büyük süs (dev mantar, yapraklı bitki) olasılığı.")]
    [Range(0f, 1f)] public float bigPropChance = 0.05f;

    [Header("Arka plan")]
    public bool trees = true;

    [Min(4)] public int treeSpacingMin = 9;
    [Min(4)] public int treeSpacingMax = 16;

    [Range(0f, 1f)] public float beehiveChance = 0.3f;

    public bool bushes = true;

    [Min(0)] public int bushGapMin = 5;
    [Min(0)] public int bushGapMax = 9;

    public bool rocks = true;

    [Range(0f, 1f)] public float rockChance = 0.2f;

    public Color treeTint = new Color(0.45f, 0.5f, 0.45f, 1f);
    public Color bushTint = new Color(0.6f, 0.68f, 0.6f, 1f);
    public Color rockTint = new Color(0.55f, 0.55f, 0.55f, 1f);

    [Header("Su (çukurların dibinde)")]
    public bool water = true;

    [Tooltip("Su yüzeyi, çukur kenarının bu kadar kare altında.")]
    [Min(1)] public int waterDrop = 3;

    public Color waterColor = new Color(1f, 1f, 1f, 0.9f);

    [Header("Çizim sırası (zemin Tilemap'ine göre)")]
    [Tooltip("Negatif = karakterlerin arkasında.")]
    public int treeOrder = -12;
    public int bushOrder = -10;
    public int rockOrder = -9;
    public int propOrder = -2;

    [Tooltip("Çim uçları. Oyuncunun ayağının ÖNÜNDE görünsün istersen büyük bir sayı yap (ör. 50).")]
    public int overhangOrder = -1;

    [Tooltip("Su, düşen oyuncunun önünde kalsın.")]
    public int waterOrder = 30;

    // =========================================================
    // SPRITE → TILE
    // =========================================================

    private Dictionary<int, Sprite> lookup;
    private readonly Dictionary<int, Tile> tileCache = new Dictionary<int, Tile>();
    private bool warnedMissing;

    private static int Key(int col, int row)
    {
        return col * 1000 + row;
    }

    public bool Ready
    {
        get
        {
            BuildLookup();

            return lookup != null && lookup.Count > 0 && lookup.ContainsKey(Key(1, 1));
        }
    }

    private void BuildLookup()
    {
        if (lookup != null && lookup.Count > 0)
            return;

        lookup = new Dictionary<int, Sprite>();

        if (sprites == null)
            return;

        bool cellsValid =
            spriteCells != null &&
            spriteCells.Length == sprites.Length &&
            cellsForCount == sprites.Length;

        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite s = sprites[i];

            if (s == null)
                continue;

            Vector2Int c = cellsValid ? spriteCells[i] : CellOf(s);

            int k = Key(c.x, c.y);

            if (!lookup.ContainsKey(k))
                lookup.Add(k, s);
        }
    }

    // Sprite'ın sayfadaki hücresi (sol üst 0,0).
    private Vector2Int CellOf(Sprite s)
    {
        Rect r = s.rect;
        int texH = s.texture != null ? s.texture.height : 0;

        int col = Mathf.FloorToInt(r.center.x / cellPixels);
        int row = Mathf.FloorToInt((texH - r.center.y) / cellPixels);

        return new Vector2Int(col, row);
    }

    /// <summary>(sütun, satır) hücresinin tile'ı; yoksa null.</summary>
    public TileBase Get(int col, int row, bool solid)
    {
        BuildLookup();

        int k = Key(col, row);
        int cacheKey = solid ? k : -k - 1;

        if (tileCache.TryGetValue(cacheKey, out Tile cached) && cached != null)
            return cached;

        if (lookup == null || !lookup.TryGetValue(k, out Sprite s) || s == null)
        {
            if (!warnedMissing)
            {
                warnedMissing = true;

                Debug.LogWarning(
                    "JungleTheme: (" + col + "," + row + ") hücresinde sprite yok. Tiles.png " +
                    "Sprite Editor'da 'Grid By Cell Size' 16x16 ile dilimlenmiş olmalı."
                );
            }

            return null;
        }

        Tile t = ScriptableObject.CreateInstance<Tile>();
        t.name = "Orman " + col + "," + row;
        t.sprite = s;
        t.colliderType = solid ? Tile.ColliderType.Grid : Tile.ColliderType.None;

        tileCache[cacheKey] = t;

        return t;
    }

    private static int Hash(int x, int y)
    {
        return ((x * 73856093) ^ (y * 19349663)) & 0x7fffffff;
    }

    private int BaseRow => groundStyle == GroundStyle.Rock ? 5 : 0;

    private static int GroundCol(bool left, bool right, int h)
    {
        if (!left && right)
            return 0;

        if (left && !right)
            return 4;

        if (!left && !right)
            return 0;

        return 1 + h % 3;
    }

    /// <summary>Zemin hücresinin tile'ı (komşulara göre köşe/kenar/iç).</summary>
    public TileBase GroundTile(bool up, bool down, bool left, bool right, int x, int y)
    {
        int h = Hash(x, y);
        int col = GroundCol(left, right, h);

        int row =
            !up
                ? BaseRow + 1
                : (!down ? BaseRow + 4 : BaseRow + 2 + (h / 3) % 2);

        TileBase t = Get(col, row, true);

        if (t == null)
            t = Get(1, BaseRow + (up ? 2 : 1), true);

        return t;
    }

    // =========================================================
    // PLATFORM ('=' hücreleri)
    // =========================================================

    public struct Run
    {
        public int y;
        public int x0;
        public int x1;   // dahil
        public bool bridge;
    }

    public static List<Run> FindRuns(HashSet<Vector2Int> platforms, HashSet<Vector2Int> solids)
    {
        List<Vector2Int> cells = new List<Vector2Int>(platforms);

        cells.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));

        List<Run> runs = new List<Run>();
        HashSet<Vector2Int> done = new HashSet<Vector2Int>();

        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int c = cells[i];

            if (done.Contains(c))
                continue;

            int x = c.x;

            while (platforms.Contains(new Vector2Int(x + 1, c.y)))
                x++;

            for (int k = c.x; k <= x; k++)
                done.Add(new Vector2Int(k, c.y));

            runs.Add(
                new Run
                {
                    y = c.y,
                    x0 = c.x,
                    x1 = x,
                    bridge =
                        solids.Contains(new Vector2Int(c.x - 1, c.y)) &&
                        solids.Contains(new Vector2Int(x + 1, c.y))
                }
            );
        }

        return runs;
    }

    /// <summary>Köprü: ip köprü tahtası; değilse uçan tahta platform.</summary>
    public TileBase PlatformTile(Run run, int x)
    {
        int col;

        if (run.bridge)
        {
            col = x == run.x0 ? 6 : (x == run.x1 ? 8 : 7);

            if (run.x0 == run.x1)
                col = 7;

            return Get(col, 9, true) ?? Get(6, 6, true);
        }

        col = x == run.x0 ? 5 : (x == run.x1 ? 7 : 6);

        if (run.x0 == run.x1)
            col = 6;

        return Get(col, 6, true);
    }

    // =========================================================
    // SÜSLEME
    // =========================================================

    public struct BuildInfo
    {
        public HashSet<Vector2Int> solids;
        public HashSet<Vector2Int> platforms;
        public int width;
        public int bottom;
        public int top;
        public Vector2Int origin;
        public bool[] reserved;     // bu sütunlara süs konmaz (kapı, tezgah …)
        public int seed;
    }

    private struct Prop
    {
        public Vector2Int[] cells;   // (sütun, satır) sayfada, ALT satır sonda değil:
        public int w;                // cells[j * w + i], j = 0 üst satır
        public int h;
    }

    private static Prop P(int w, int h, params int[] colRow)
    {
        Vector2Int[] cells = new Vector2Int[w * h];

        for (int i = 0; i < cells.Length; i++)
            cells[i] = new Vector2Int(colRow[i * 2], colRow[i * 2 + 1]);

        return new Prop { cells = cells, w = w, h = h };
    }

    private static readonly Prop[] smallProps =
    {
        P(1, 1, 22, 17),   // çim tutamı
        P(1, 1, 22, 17),
        P(1, 1, 22, 17),
        P(1, 1, 15, 15),   // mantarlar
        P(1, 1, 15, 16),
        P(1, 1, 20, 16),
        P(1, 1, 16, 17),   // mavi çiçek
        P(1, 1, 21, 16)    // kazık
    };

    private static readonly Prop[] tallProps =
    {
        P(1, 2, 15, 17, 15, 18),   // mavi çiçekli sarmaşık
        P(1, 2, 17, 17, 17, 18),   // lavanta
        P(1, 2, 16, 18, 16, 19),   // saz
        P(1, 2, 16, 20, 16, 21),   // çift saz
        P(1, 2, 22, 15, 22, 16)    // uzun kazık
    };

    private static readonly Prop[] bigProps =
    {
        P(2, 2, 16, 15, 17, 15, 16, 16, 17, 16),   // dev mantar
        P(2, 2, 18, 15, 19, 15, 18, 16, 19, 16),   // sivri dev mantar
        P(2, 2, 20, 22, 21, 22, 20, 23, 21, 23)    // yapraklı bitki
    };

    private Transform layerRoot;
    private TilemapRenderer groundRenderer;
    private Tilemap groundMap;

    private readonly Dictionary<string, Tilemap> layers = new Dictionary<string, Tilemap>();

    /// <summary>
    /// Haritayı süsler. Katmanlar 'root' altına kurulur (harita silinince gider).
    /// </summary>
    public void Decorate(BuildInfo info, Transform root, Tilemap ground)
    {
        layers.Clear();

        layerRoot = root;
        groundMap = ground;
        groundRenderer = ground != null ? ground.GetComponent<TilemapRenderer>() : null;

        System.Random rng = new System.Random(info.seed ^ 0x5bd1e99);

        // Sütun başına yüzey (en üst zemin + 1); çukur = int.MinValue.
        int min = -2;
        int max = info.width + 2;

        int[] surface = new int[max - min];

        for (int i = 0; i < surface.Length; i++)
            surface[i] = int.MinValue;

        foreach (Vector2Int c in info.solids)
        {
            if (c.x < min || c.x >= max)
                continue;

            int idx = c.x - min;

            if (c.y + 1 > surface[idx])
                surface[idx] = c.y + 1;
        }

        int Surf(int x)
        {
            return x < min || x >= max ? int.MinValue : surface[x - min];
        }

        bool Reserved(int x)
        {
            return info.reserved != null && x >= 0 && x < info.reserved.Length && info.reserved[x];
        }

        HashSet<Vector2Int> used = new HashSet<Vector2Int>();

        // ---------------- ÇİM UÇLARI ----------------

        if (grassOverhang)
        {
            Tilemap over = Layer("Çim Uçları", overhangOrder, Color.white);

            foreach (Vector2Int c in info.solids)
            {
                Vector2Int above = c + Vector2Int.up;

                if (info.solids.Contains(above) || info.platforms.Contains(above))
                    continue;

                bool left = info.solids.Contains(c + Vector2Int.left);
                bool right = info.solids.Contains(c + Vector2Int.right);

                int col = GroundCol(left, right, Hash(c.x, c.y));

                Set(over, info, above, col, BaseRow);
            }
        }

        // ---------------- KÖPRÜ İPLERİ / DİREKLERİ ----------------

        Tilemap props = Layer("Süsler", propOrder, Color.white);

        List<Run> runs = FindRuns(info.platforms, info.solids);

        for (int i = 0; i < runs.Count; i++)
        {
            Run r = runs[i];

            if (!r.bridge)
                continue;

            for (int x = r.x0; x <= r.x1; x++)
            {
                int col = x == r.x0 ? 6 : (x == r.x1 ? 8 : 7);
                Vector2Int cell = new Vector2Int(x, r.y + 1);

                Set(props, info, cell, col, 8);
                used.Add(cell);
            }

            Vector2Int l1 = new Vector2Int(r.x0 - 1, r.y + 1);
            Vector2Int l2 = new Vector2Int(r.x0 - 1, r.y + 2);
            Vector2Int r1 = new Vector2Int(r.x1 + 1, r.y + 1);
            Vector2Int r2 = new Vector2Int(r.x1 + 1, r.y + 2);

            Set(props, info, l1, 5, 8);
            Set(props, info, l2, 5, 7);
            Set(props, info, r1, 9, 8);
            Set(props, info, r2, 9, 7);

            used.Add(l1);
            used.Add(l2);
            used.Add(r1);
            used.Add(r2);
        }

        // ---------------- SU ----------------

        if (water)
        {
            Tilemap waterMap = Layer("Su", waterOrder, waterColor);

            int x = 0;

            while (x < info.width)
            {
                if (Surf(x) != int.MinValue)
                {
                    x++;
                    continue;
                }

                int x0 = x;

                while (x < info.width && Surf(x) == int.MinValue)
                    x++;

                int x1 = x - 1;

                int sl = Surf(x0 - 1);
                int sr = Surf(x1 + 1);

                int edge =
                    sl == int.MinValue
                        ? sr
                        : (sr == int.MinValue ? sl : Mathf.Min(sl, sr));

                if (edge == int.MinValue)
                    continue;

                int line = edge - 1 - waterDrop;

                for (int px = x0; px <= x1; px++)
                {
                    int col = 6 + ((px % 4) + 4) % 4;

                    Set(waterMap, info, new Vector2Int(px, line), col, 19);

                    for (int y = info.bottom - 12; y < line; y++)
                        Set(waterMap, info, new Vector2Int(px, y), col, 20);
                }
            }
        }

        // Düz zemin aralığı: [x, x+w) hepsi zeminli ve aynı yükseklikte mi?
        bool Flat(int x, int w, out int s)
        {
            s = Surf(x);

            if (s == int.MinValue)
                return false;

            for (int i = 1; i < w; i++)
            {
                if (Surf(x + i) != s)
                    return false;
            }

            return true;
        }

        // Aralıktaki en alçak yüzey (çukur varsa false).
        bool Solid(int x, int w, out int lowest)
        {
            lowest = int.MaxValue;

            for (int i = 0; i < w; i++)
            {
                int s = Surf(x + i);

                if (s == int.MinValue)
                    return false;

                lowest = Mathf.Min(lowest, s);
            }

            return true;
        }

        // ---------------- AĞAÇLAR ----------------

        if (trees)
        {
            Tilemap treeMap = Layer("Arka Ağaçlar", treeOrder, treeTint);

            int x = rng.Next(2, 6);

            while (x < info.width - 3)
            {
                if (!Flat(x, 2, out int baseY) || Surf(x - 1) == int.MinValue || Surf(x + 2) == int.MinValue)
                {
                    x++;
                    continue;
                }

                PlaceTree(treeMap, info, rng, x, baseY, info.top);

                x += rng.Next(Mathf.Min(treeSpacingMin, treeSpacingMax), Mathf.Max(treeSpacingMin, treeSpacingMax) + 1);
            }
        }

        // ---------------- ÇALILAR ----------------

        if (bushes)
        {
            Tilemap bushMap = Layer("Arka Çalılar", bushOrder, bushTint);

            int x = rng.Next(0, 4);

            while (x <= info.width - 8)
            {
                if (!Solid(x, 8, out int baseY))
                {
                    x++;
                    continue;
                }

                int variant = rng.Next(0, 5) * 3;

                for (int i = 0; i < 8; i++)
                {
                    for (int j = 0; j < 3; j++)
                        Set(bushMap, info, new Vector2Int(x + i, baseY + 2 - j), 17 + i, variant + j);
                }

                x += 8 + rng.Next(Mathf.Min(bushGapMin, bushGapMax), Mathf.Max(bushGapMin, bushGapMax) + 1) - 4;
            }
        }

        // ---------------- KAYALAR ----------------

        if (rocks)
        {
            Tilemap rockMap = Layer("Arka Kayalar", rockOrder, rockTint);

            int x = rng.Next(0, 6);

            while (x <= info.width - 5)
            {
                if (rng.NextDouble() > rockChance || !Solid(x, 5, out int baseY))
                {
                    x += 3;
                    continue;
                }

                int startCol = rng.Next(0, 3) * 5;

                for (int i = 0; i < 5; i++)
                {
                    Set(rockMap, info, new Vector2Int(x + i, baseY + 1), startCol + i, 21);
                    Set(rockMap, info, new Vector2Int(x + i, baseY), startCol + i, 22);
                }

                x += 5 + rng.Next(6, 14);
            }
        }

        // ---------------- KÜÇÜK SÜSLER ----------------

        bool Free(int x, int y, int w, int h)
        {
            for (int i = 0; i < w; i++)
            {
                if (Reserved(x + i))
                    return false;

                for (int j = 0; j < h; j++)
                {
                    Vector2Int c = new Vector2Int(x + i, y + j);

                    if (used.Contains(c) || info.platforms.Contains(c) || info.solids.Contains(c))
                        return false;
                }
            }

            return true;
        }

        for (int x = 0; x < info.width; x++)
        {
            int s = Surf(x);

            if (s == int.MinValue)
                continue;

            double roll = rng.NextDouble();

            Prop p;

            if (roll < bigPropChance)
                p = bigProps[rng.Next(bigProps.Length)];
            else if (roll < bigPropChance + tallPropChance)
                p = tallProps[rng.Next(tallProps.Length)];
            else if (roll < bigPropChance + tallPropChance + propChance)
                p = smallProps[rng.Next(smallProps.Length)];
            else
                continue;

            if (!Flat(x, p.w, out _) || !Free(x, s, p.w, p.h))
                continue;

            for (int j = 0; j < p.h; j++)
            {
                for (int i = 0; i < p.w; i++)
                {
                    Vector2Int src = p.cells[j * p.w + i];
                    Vector2Int cell = new Vector2Int(x + i, s + p.h - 1 - j);

                    Set(props, info, cell, src.x, src.y);
                    used.Add(cell);
                }
            }

            x += p.w - 1;
        }
    }

    // Ağaç: gövde (10-11), tepede yapraklar (9 ve 12), dallar, isteğe bağlı arı kovanı.
    private void PlaceTree(Tilemap map, BuildInfo info, System.Random rng, int x, int baseY, int top)
    {
        int height = Mathf.Max(8, top - baseY + 1);

        int branchK = 5 + rng.Next(0, Mathf.Max(1, height - 9));
        bool hive = rng.NextDouble() < beehiveChance;
        bool smallHive = !hive && rng.NextDouble() < beehiveChance;

        for (int k = 0; k < height; k++)
        {
            int y = top - k;

            int row;

            if (k < 4)
                row = k;
            else if (k == height - 1)
                row = 9;
            else
                row = 4 + (k - 4) % 5;

            Set(map, info, new Vector2Int(x, y), 10, row);
            Set(map, info, new Vector2Int(x + 1, y), 11, row);

            if (k <= 3)
            {
                // Yapraklar ve sol dal.
                Set(map, info, new Vector2Int(x - 1, y), 9, k);

                if (k <= 1)
                    Set(map, info, new Vector2Int(x + 2, y), 12, k);
                else if (smallHive)
                    Set(map, info, new Vector2Int(x + 2, y), 12, k);
            }

            if (k == branchK)
            {
                if (hive)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        Set(map, info, new Vector2Int(x + 2, y - j), 12, 6 + j);
                        Set(map, info, new Vector2Int(x + 3, y - j), 13, 6 + j);
                    }
                }
                else
                {
                    Set(map, info, new Vector2Int(x + 2, y), 12, 5);
                }
            }
        }
    }

    private void Set(Tilemap map, BuildInfo info, Vector2Int cell, int col, int row)
    {
        TileBase t = Get(col, row, false);

        if (t == null || map == null)
            return;

        map.SetTile(new Vector3Int(info.origin.x + cell.x, info.origin.y + cell.y, 0), t);
    }

    private Tilemap Layer(string layerName, int orderOffset, Color tint)
    {
        if (layers.TryGetValue(layerName, out Tilemap existing) && existing != null)
            return existing;

        GameObject obj = new GameObject(layerName);

        obj.transform.SetParent(layerRoot, false);

        if (groundMap != null)
            obj.transform.localPosition = groundMap.transform.localPosition;

        Tilemap map = obj.AddComponent<Tilemap>();
        TilemapRenderer r = obj.AddComponent<TilemapRenderer>();

        map.color = tint;

        if (groundMap != null)
            map.tileAnchor = groundMap.tileAnchor;

        if (groundRenderer != null)
        {
            r.sortingLayerID = groundRenderer.sortingLayerID;
            r.sortingOrder = groundRenderer.sortingOrder + orderOffset;
            r.sharedMaterial = groundRenderer.sharedMaterial;
        }
        else
        {
            r.sortingOrder = orderOffset;
        }

        layers[layerName] = map;

        return map;
    }

    // =========================================================
    // EDITOR: sprite'ları otomatik doldur
    // =========================================================

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        if (sheet != null)
        {
            string path = AssetDatabase.GetAssetPath(sheet);

            if (!string.IsNullOrEmpty(path))
            {
                Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
                List<Sprite> list = new List<Sprite>();

                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] is Sprite s)
                        list.Add(s);
                }

                if (list.Count > 0 && (sprites == null || sprites.Length != list.Count))
                    sprites = list.ToArray();
            }
        }

        if (sprites != null)
        {
            spriteCells = new Vector2Int[sprites.Length];

            for (int i = 0; i < sprites.Length; i++)
                spriteCells[i] = sprites[i] != null ? CellOf(sprites[i]) : new Vector2Int(-1, -1);

            cellsForCount = sprites.Length;
        }

        lookup = null;
        tileCache.Clear();
        warnedMissing = false;
    }

    [ContextMenu("Kontrol: gerekli tile'lar var mı?")]
    private void CheckTiles()
    {
        lookup = null;
        BuildLookup();

        int[,] needed =
        {
            { 0, 0 }, { 1, 0 }, { 4, 0 }, { 0, 1 }, { 1, 1 }, { 4, 1 }, { 0, 2 }, { 1, 2 }, { 4, 2 }, { 1, 4 },
            { 5, 6 }, { 6, 6 }, { 7, 6 }, { 6, 9 }, { 7, 9 }, { 5, 7 }, { 10, 4 }, { 17, 0 }, { 6, 19 }, { 22, 17 }
        };

        List<string> missing = new List<string>();

        for (int i = 0; i < needed.GetLength(0); i++)
        {
            if (!lookup.ContainsKey(Key(needed[i, 0], needed[i, 1])))
                missing.Add("(" + needed[i, 0] + "," + needed[i, 1] + ")");
        }

        if (missing.Count == 0)
            Debug.Log("JungleTheme: " + lookup.Count + " sprite tanındı, gerekli tile'ların hepsi var.");
        else
            Debug.LogWarning("JungleTheme: eksik hücreler: " + string.Join(", ", missing) +
                             " → Tiles.png'yi Sprite Editor'da Grid By Cell Size 16x16 ile dilimle.");
    }
#endif
}
