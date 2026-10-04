using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// ORMAN TEMASI (Jungle/Tiles.png). LevelGenerator bu bileşeni bulursa
/// rastgele haritayı bu tileset ile boyar ve süsler.
///
/// KURULUM (bir kez):
///   1) LevelGenerator'ın olduğu objeye (Managers) 'Jungle Theme' ekle.
///   2) 'Sheet' alanına Assets/Sprites/Tile/Jungle/Tiles.png'yi sürükle.
/// Sprite Editor'daki dilimleme önemli değil: sayfa oyun başında 16x16 kesilir.
///
/// NE ÇİZER:
///   - Zemin: sol üst çim seti (0-4, 0-4) komşuya göre (köşe/kenar/iç),
///     üstte çimin taşan uçları (ayrı, çarpışmasız katman).
///   - '=' platformlar: ip köprü / tahta.
///   - Çukurların dibinde su.
///   - PARALLAX arka plan: 3 çalı bandı (uzak / orta / yakın; sayfadaki
///     17-24 × 0-14 çalı tepeleri) + aralarında ağaç gövdeleri. Bantlar
///     kamerayla farklı hızda kayar (Factor: 0 = zeminle aynı, 1 = kamerayla
///     sabit). Her bant iki üst üste binen sıra çalı + altında dolgu: zemin
///     alçalsa da / çukurda da boşluk görünmez.
///   - Çiçek / mantar / sandık gibi küçük süsler KULLANILMAZ.
///
/// Sayfa haritası (sütun, satır; sol üst 0,0): çim seti 0-4 × 0-4, kaya seti
/// 0-4 × 5-9, tahta 5-7 × 6, ip köprü 5-9 × 7-9, ağaç 9-13 × 0-9, çalılar
/// 17-24 × 0-14 (5 renk: 0 parlak yeşil … 4 en koyu), su 6-9 × 19-20.
/// </summary>
public class JungleTheme : MonoBehaviour
{
    public enum GroundStyle
    {
        Grass,
        Rock
    }

    [System.Serializable]
    public class ParallaxBand
    {
        public string name = "Bant";

        [Tooltip("0 = zeminle aynı hızda (yakın), 1 = kamerayla sabit (sonsuz uzak).")]
        [Range(0f, 1f)] public float factor = 0.5f;

        [Tooltip("Çalı rengi: 0 parlak yeşil, 1 zeytin, 2 koyu zeytin, 3 koyu, 4 en koyu.")]
        [Range(0, 4)] public int bushColor = 3;

        [Tooltip("Bandın tabanı: ortalama zemin yüksekliğinin bu kadar kare üstü.")]
        public int heightAboveGround = 3;

        [Tooltip("Açık: en YÜKSEK zemine göre (uzak bant tepelerin üstünden görünsün).")]
        public bool fromHighestGround;

        public Color tint = Color.white;

        [Tooltip("Çizim sırası (zemin Tilemap'ine göre, negatif = arkada).")]
        public int order = -20;

        public ParallaxBand()
        {
        }

        public ParallaxBand(string name, float factor, int bushColor, int height, bool fromHighest, Color tint, int order)
        {
            this.name = name;
            this.factor = factor;
            this.bushColor = bushColor;
            this.heightAboveGround = height;
            this.fromHighestGround = fromHighest;
            this.tint = tint;
            this.order = order;
        }
    }

    [Header("Tileset")]
    [Tooltip("Tiles.png (Project penceresinden sürükle).")]
    public Texture2D sheet;

    [Tooltip("Bir tile'ın piksel boyu.")]
    [Min(1)] public int cellPixels = 16;

    [Tooltip(
        "1 tile = kaç piksel / birim. Tiles.png'nin 'Pixels Per Unit' değeriyle aynı olmalı " +
        "(16 → bir tile bir Tilemap hücresini tam doldurur).")]
    [Min(1f)] public float pixelsPerUnit = 16f;

    [Header("Zemin")]
    public GroundStyle groundStyle = GroundStyle.Grass;

    [Tooltip("Yüzeyin üstüne çimin taşan uçlarını koy.")]
    public bool grassOverhang = true;

    [Tooltip("Çim uçları. Oyuncunun ayağının ÖNÜNDE görünsün istersen büyük bir sayı yap (ör. 50).")]
    public int overhangOrder = -1;

    [Tooltip("Köprü ipleri / direkleri.")]
    public int bridgeDetailOrder = -2;

    [Header("Parallax arka plan")]
    public bool parallax = true;

    [Tooltip("Uzaktan yakına. Sıra / renk / hız buradan ayarlanır.")]
    public ParallaxBand[] bands =
    {
        new ParallaxBand("Uzak çalılar", 0.75f, 4, 6, true, new Color(0.55f, 0.68f, 0.68f, 1f), -30),
        new ParallaxBand("Orta çalılar", 0.5f, 3, 3, false, new Color(0.72f, 0.82f, 0.78f, 1f), -20),
        new ParallaxBand("Yakın çalılar", 0.25f, 2, 1, false, new Color(0.88f, 0.95f, 0.88f, 1f), -12)
    };

    [Tooltip("Komşu bant renginin araya karışma olasılığı (çeşitlilik).")]
    [Range(0f, 1f)] public float bushColorMix = 0.3f;

    [Tooltip("Dikey parallax (0 = bantlar dikeyde sabit; zeminle hizalı kalır).")]
    [Range(0f, 0.5f)] public float verticalFactor = 0f;

    [Header("Ağaçlar (parallax)")]
    public bool trees = true;

    [Range(0f, 1f)] public float treeFactor = 0.6f;

    [Tooltip("Ağaç dibi: ortalama zeminin bu kadar kare üstü (orta bantın arkasında kalsın).")]
    public int treeBaseAboveGround = 2;

    [Min(4)] public int treeSpacingMin = 8;
    [Min(4)] public int treeSpacingMax = 14;

    [Range(0f, 1f)] public float beehiveChance = 0.2f;

    public Color treeTint = new Color(0.4f, 0.48f, 0.45f, 1f);
    public int treeLayerOrder = -25;

    [Header("Su (çukurların dibinde)")]
    public bool water = true;

    [Tooltip("Su yüzeyi, çukur kenarının bu kadar kare altında.")]
    [Min(1)] public int waterDrop = 3;

    public Color waterColor = new Color(1f, 1f, 1f, 0.9f);

    [Tooltip("Su, düşen oyuncunun önünde kalsın.")]
    public int waterOrder = 30;
    // =========================================================
    // SPRITE → TILE (sayfa çalışma anında 16x16 kesilir)
    // =========================================================

    private readonly Dictionary<int, Sprite> cutCache = new Dictionary<int, Sprite>();
    private readonly Dictionary<int, Tile> tileCache = new Dictionary<int, Tile>();
    private bool warnedMissing;

    private static int Key(int col, int row)
    {
        return col * 1000 + row;
    }

    public int Columns => sheet != null ? sheet.width / Mathf.Max(1, cellPixels) : 0;
    public int Rows => sheet != null ? sheet.height / Mathf.Max(1, cellPixels) : 0;

    // Çim seti en az 5x5 olmalı.
    public bool Ready => sheet != null && Columns >= 5 && Rows >= 5;

    /// <summary>Hazır değilse nedenini söyler (log için).</summary>
    public string Problem
    {
        get
        {
            if (sheet == null)
                return "'Sheet' alanı boş → Tiles.png'yi sürükle.";

            if (Columns < 5 || Rows < 5)
                return "Sayfa çok küçük (" + sheet.width + "x" + sheet.height + "), 'Cell Pixels' doğru mu?";

            return "";
        }
    }

    private Sprite SpriteAt(int col, int row)
    {
        if (sheet == null || col < 0 || row < 0 || col >= Columns || row >= Rows)
            return null;

        int k = Key(col, row);

        if (cutCache.TryGetValue(k, out Sprite cached) && cached != null)
            return cached;

        Rect r =
            new Rect(
                col * cellPixels,
                sheet.height - (row + 1) * cellPixels,   // doku y'si aşağıdan yukarı
                cellPixels,
                cellPixels
            );

        Sprite s =
            Sprite.Create(
                sheet,
                r,
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect
            );

        s.name = "Orman " + col + "," + row;

        cutCache[k] = s;

        return s;
    }

    /// <summary>(sütun, satır) hücresinin tile'ı; yoksa null.</summary>
    public TileBase Get(int col, int row, bool solid)
    {
        int k = Key(col, row);
        int cacheKey = solid ? k : -k - 1;

        if (tileCache.TryGetValue(cacheKey, out Tile cachedTile) && cachedTile != null)
            return cachedTile;

        Sprite s = SpriteAt(col, row);

        if (s == null)
        {
            if (!warnedMissing)
            {
                warnedMissing = true;
                Debug.LogWarning("JungleTheme: (" + col + "," + row + ") hücresi sayfanın dışında. " + Problem);
            }

            return null;
        }

        Tile t = ScriptableObject.CreateInstance<Tile>();
        t.name = s.name;
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
        public bool[] reserved;     // (artık süs yok; uyumluluk için duruyor)
        public int seed;
    }

    // Parallax katmanlarının sola/sağa taşma payı (kare).
    private const int BandMargin = 48;

    private Transform layerRoot;
    private TilemapRenderer groundRenderer;
    private Tilemap groundMap;

    private readonly Dictionary<string, Tilemap> layers = new Dictionary<string, Tilemap>();

    /// <summary>
    /// Haritayı süsler. Katmanlar 'root' altına kurulur (harita silinince gider).
    /// </summary>
    public void Decorate(BuildInfo info, Transform root, Tilemap ground)
    {
        pending.Clear();

        try
        {
            DecorateInner(info, root, ground);
        }
        finally
        {
            Flush();
        }
    }

    // Tile'lar toplu yazılır (binlerce tek tek SetTile yavaş olur).
    private readonly Dictionary<Tilemap, List<Vector3Int>> pending = new Dictionary<Tilemap, List<Vector3Int>>();
    private readonly Dictionary<Tilemap, List<TileBase>> pendingTiles = new Dictionary<Tilemap, List<TileBase>>();

    private void Flush()
    {
        foreach (KeyValuePair<Tilemap, List<Vector3Int>> pair in pending)
        {
            if (pair.Key == null)
                continue;

            pair.Key.SetTiles(pair.Value.ToArray(), pendingTiles[pair.Key].ToArray());
        }

        pending.Clear();
        pendingTiles.Clear();
    }

    private void DecorateInner(BuildInfo info, Transform root, Tilemap ground)
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

        // Ortalama ve en yüksek zemin (parallax bantları buna göre).
        long sum = 0;
        int n = 0;
        int highest = int.MinValue;

        for (int x = 0; x < info.width; x++)
        {
            int s = Surf(x);

            if (s == int.MinValue)
                continue;

            sum += s;
            n++;
            highest = Mathf.Max(highest, s);
        }

        int average = n > 0 ? Mathf.RoundToInt(sum / (float)n) : 0;

        if (highest == int.MinValue)
            highest = average;

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

        List<Run> runs = FindRuns(info.platforms, info.solids);

        Tilemap bridgeMap = null;

        for (int i = 0; i < runs.Count; i++)
        {
            Run r = runs[i];

            if (!r.bridge)
                continue;

            if (bridgeMap == null)
                bridgeMap = Layer("Köprü İpleri", bridgeDetailOrder, Color.white);

            for (int x = r.x0; x <= r.x1; x++)
            {
                int col = x == r.x0 ? 6 : (x == r.x1 ? 8 : 7);

                Set(bridgeMap, info, new Vector2Int(x, r.y + 1), col, 8);
            }

            Set(bridgeMap, info, new Vector2Int(r.x0 - 1, r.y + 1), 5, 8);
            Set(bridgeMap, info, new Vector2Int(r.x0 - 1, r.y + 2), 5, 7);
            Set(bridgeMap, info, new Vector2Int(r.x1 + 1, r.y + 1), 9, 8);
            Set(bridgeMap, info, new Vector2Int(r.x1 + 1, r.y + 2), 9, 7);
        }

        // ---------------- SU ----------------

        if (water)
        {
            Tilemap waterMap = null;

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

                if (waterMap == null)
                    waterMap = Layer("Su", waterOrder, waterColor);

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

        if (!parallax)
            return;

        // Parallax çapası: başlangıç noktası (kamera oradan başlar).
        Vector3 anchor =
            ground != null
                ? ground.CellToWorld(new Vector3Int(info.origin.x + 2, info.origin.y + average, 0))
                : Vector3.zero;

        // ---------------- ÇALI BANTLARI ----------------

        if (bands != null)
        {
            for (int b = 0; b < bands.Length; b++)
            {
                ParallaxBand band = bands[b];

                if (band == null)
                    continue;

                int baseY =
                    (band.fromHighestGround ? highest : average) + band.heightAboveGround;

                BuildBand(info, rng, band, baseY, anchor);
            }
        }

        // ---------------- AĞAÇLAR ----------------

        if (trees)
        {
            Tilemap treeMap = Layer("Arka Ağaçlar", treeLayerOrder, treeTint);

            AddParallax(treeMap, treeFactor, anchor);

            int baseY = average + treeBaseAboveGround;
            int top = Mathf.Max(info.top, baseY + 10);

            int x = -BandMargin + rng.Next(0, 6);

            while (x < info.width + BandMargin)
            {
                PlaceTree(treeMap, info, rng, x, baseY, top);

                x += rng.Next(Mathf.Min(treeSpacingMin, treeSpacingMax), Mathf.Max(treeSpacingMin, treeSpacingMax) + 1);
            }
        }
    }

    // Bir çalı bandı: iki sıra (A/B) 8 genişlikte çalı tepesi, 6'şar kare
    // aralıkla üst üste binerek; A sırasında tepelerin altı aynı rengin
    // alt satırıyla dolu (zemin alçalınca boşluk görünmesin).
    private void BuildBand(BuildInfo info, System.Random rng, ParallaxBand band, int baseY, Vector3 anchor)
    {
        Tilemap a = Layer(band.name + " A", band.order, band.tint);
        Tilemap b = Layer(band.name + " B", band.order + 1, band.tint);

        AddParallax(a, band.factor, anchor);
        AddParallax(b, band.factor, anchor);

        int color = Mathf.Clamp(band.bushColor, 0, 4);

        int x = -BandMargin + rng.Next(0, 6);
        int i = 0;

        while (x < info.width + BandMargin)
        {
            int c = color;

            if (rng.NextDouble() < bushColorMix)
                c = Mathf.Clamp(color + (rng.Next(0, 2) == 0 ? -1 : 1), 0, 4);

            Tilemap target = i % 2 == 0 ? a : b;

            // Tepeler biraz inip çıksın.
            int lift = rng.Next(0, 3) == 0 ? 1 : 0;

            for (int col = 0; col < 8; col++)
            {
                for (int row = 0; row < 3; row++)
                    Set(target, info, new Vector2Int(x + col, baseY + lift + 2 - row), 17 + col, c * 3 + row);

                // Kaldırılan tepenin altı boş kalmasın.
                if (lift > 0)
                    Set(target, info, new Vector2Int(x + col, baseY), 19 + (col % 4), c * 3 + 2);
            }

            x += 6;
            i++;
        }

        // Dolgu: bandın altından haritanın dibine kadar.
        int fillRow = color * 3 + 2;

        for (int fx = -BandMargin; fx < info.width + BandMargin + 8; fx++)
        {
            for (int y = info.bottom - 12; y < baseY; y++)
                Set(a, info, new Vector2Int(fx, y), 19 + ((fx * 7 + y * 3) & 3), fillRow);
        }
    }

    private void AddParallax(Tilemap map, float factor, Vector3 anchor)
    {
        if (map == null)
            return;

        JungleParallax p = map.GetComponent<JungleParallax>();

        if (p == null)
            p = map.gameObject.AddComponent<JungleParallax>();

        p.Init(factor, verticalFactor, anchor);
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
                Set(map, info, new Vector2Int(x - 1, y), 9, k);

                if (k <= 1 || smallHive)
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

        if (!pending.TryGetValue(map, out List<Vector3Int> cells))
        {
            cells = new List<Vector3Int>();
            pending[map] = cells;
            pendingTiles[map] = new List<TileBase>();
        }

        cells.Add(new Vector3Int(info.origin.x + cell.x, info.origin.y + cell.y, 0));
        pendingTiles[map].Add(t);
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

    private void OnValidate()
    {
        // Ayar değişince kesilmiş sprite'lar yeniden üretilsin.
        cutCache.Clear();
        tileCache.Clear();
        warnedMissing = false;
    }
}

/// <summary>
/// Parallax katmanı: kamera çapadan ne kadar uzaklaştıysa katman onun
/// 'factor' katı kadar kameranın peşinden gelir (uzak = daha yavaş görünür).
/// Cinemachine kamerayı LateUpdate'te taşıdığı için en sonda çalışır.
/// </summary>
[DefaultExecutionOrder(10000)]
public class JungleParallax : MonoBehaviour
{
    private float factorX;
    private float factorY;
    private Vector3 anchor;
    private Vector3 basePosition;
    private bool ready;

    public void Init(float factorX, float factorY, Vector3 anchor)
    {
        this.factorX = factorX;
        this.factorY = factorY;
        this.anchor = anchor;

        basePosition = transform.position;
        ready = true;

        LateUpdate();
    }

    private void LateUpdate()
    {
        if (!ready)
            return;

        Camera cam = Camera.main;

        if (cam == null)
            return;

        Vector3 c = cam.transform.position;

        transform.position =
            basePosition +
            new Vector3((c.x - anchor.x) * factorX, (c.y - anchor.y) * factorY, 0f);
    }
}
