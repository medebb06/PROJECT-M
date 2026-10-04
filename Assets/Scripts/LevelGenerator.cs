using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// RASTGELE HARİTA (Dead Cells tarzı parça birleştirme).
///
/// Her dövüş odasında LevelChunkLibrary'deki elle tasarlanmış parçalardan
/// rastgele bir dizi kurar:
///   [Başlangıç] → ara parçalar → [ARENA] → ara parçalar → [ARENA] … → [Çıkış]
/// Parçalar yüzey yükseklikleri eşleşecek şekilde birleştirilir, zeminin
/// altı doldurulur, iki uca duvar konur. Aynı tohum (seed) = aynı harita.
///
/// ARENA: oyuncu arenaya girince iki yanı kapanır (kırmızı yarı saydam
/// duvar), düşmanlar arenanın 'E' noktalarında doğar; temizlenince açılır.
/// ÇUKUR: düşen oyuncu son güvenli yere döner ve canının %10'unu kaybeder;
/// düşen düşman ölür.
///
/// KURULUM YOK: RunManager kendisi ekler. Tile'lar, katman (layer),
/// çizim sırası ve collider ayarı sahnedeki mevcut Tilemap'ten kopyalanır
/// (çimli üst tile'lar ve toprak tile'ları otomatik ayrılır). İstersen
/// 'Top Tiles' / 'Fill Tiles' alanlarını elle doldur.
/// Harita, sahnedeki zeminin uzağında ('Origin' hücresinde) kurulur;
/// lobi için senin elle yaptığın zemin olduğu gibi kalır.
/// </summary>
public class LevelGenerator : MonoBehaviour
{
    [Header("Tema")]
    [Tooltip(
        "Orman teması (JungleTheme). Boşsa aynı objede / sahnede aranır. " +
        "Varsa zemin onunla boyanır ve harita süslenir; yoksa aşağıdaki tile'lar kullanılır.")]
    public JungleTheme theme;

    [Header("Tile'lar (boşsa sahnedeki Tilemap'ten otomatik)")]
    [Tooltip("Ayarların kopyalanacağı Tilemap. Boşsa sahnedeki en dolu Tilemap.")]
    public Tilemap templateTilemap;

    [Tooltip("Yüzey (çimli) ORTA tile'lar.")]
    public TileBase[] topTiles;

    [Tooltip("İç (toprak) ORTA tile'lar.")]
    public TileBase[] fillTiles;

    [Tooltip("Köşe/kenar tile'ları (boşsa şablondan otomatik; o da yoksa orta tile).")]
    public TileBase[] topLeftTiles;
    public TileBase[] topRightTiles;
    public TileBase[] fillLeftTiles;
    public TileBase[] fillRightTiles;

    [Header("Yerleşim")]
    [Tooltip("Haritanın kurulduğu hücre (sahnedeki zeminden uzak olsun).")]
    public Vector2Int origin = new Vector2Int(0, 300);

    [Tooltip("İki arena arasındaki ara parça sayısı (en az / en çok).")]
    [Min(0)] public int minFillers = 2;
    [Min(0)] public int maxFillers = 4;

    [Tooltip("Yüzey başlangıçtan en fazla bu kadar kare yukarı/aşağı kayar.")]
    [Min(1)] public int maxDrift = 5;

    [Tooltip("En alçak yüzeyin altındaki toprak kalınlığı (kare).")]
    [Min(1)] public int groundDepth = 6;

    [Tooltip(
        "Zemin GÖRSEL olarak bunun kadar daha aşağı uzar (kamera altını görmesin). " +
        "Çukur düşme çizgisi değişmez.")]
    [Min(0)] public int visualDepth = 30;

    [Tooltip(
        "Haritanın iki ucunda zemin bu kadar kare daha devam eder; geçişi görünmez " +
        "bir duvar engeller (düz duvar yok).")]
    [Min(0)] public int edgeExtension = 24;

    [Tooltip("Haritanın iki ucundaki duvarın yüksekliği (kare).")]
    [Min(4)] public int wallHeight = 16;

    [Tooltip(
        "Parçaları yatayda bu kadar kat GENİŞLET (zeminli sütunlar; çukurlar " +
        "aynı kalır, zıplanabilir olsun). 2 = haritalar ve arenalar iki kat uzun.")]
    [Range(1, 3)] public int horizontalStretch = 2;

    [Tooltip("Son arenadan sonra çıkıştan önceki ara parça sayısı (en az / en çok).")]
    [Min(0)] public int minFillersBeforeExit = 1;
    [Min(0)] public int maxFillersBeforeExit = 2;

    [Header("Arena")]
    [Tooltip("Oyuncu arenanın sol kenarından bu kadar kare içeri girince kapanır.")]
    public float arenaEnterMargin = 3f;

    public Color lockColor = new Color(1f, 0.35f, 0.25f, 0.35f);

    [Header("Çukur")]
    [Tooltip("Çukura düşen oyuncunun kaybettiği can (azami canın oranı).")]
    [Range(0f, 1f)] public float pitDamagePercent = 0.1f;

    [Tooltip("Haritanın dibinden bu kadar kare aşağı düşen 'çukura düştü' sayılır.")]
    public float pitFallDepth = 3f;

    // =========================================================
    // DURUM
    // =========================================================

    private class ArenaInfo
    {
        public float leftX;
        public float rightX;
        public float floorY;
        public readonly List<Transform> spawns = new List<Transform>();
        public GameObject leftWall;
        public GameObject rightWall;
    }

    public bool HasLevel { get; private set; }

    // Geçiş alanındaki dükkan tezgahı / kamp ateşi.
    public bool HasSpecial { get; private set; }
    public Vector3 SpecialPosition { get; private set; }

    // Çıkış parçasının dünya x aralığı (kapılar buraya dizilir).
    private float exitZoneLeft;
    private float exitZoneRight;
    private float exitFloorY;
    public int Seed { get; private set; }
    public Vector3 PlayerStart { get; private set; }
    public Vector3 ExitPosition { get; private set; }
    public int ArenaCount => arenas.Count;
    public string LastLayout { get; private set; } = "";

    private readonly List<ArenaInfo> arenas = new List<ArenaInfo>();

    private GameObject root;
    private Tilemap tilemap;

    private PlayerController player;
    private Health playerHealth;

    private Vector3 lastSafe;
    private float nextSafeSample;
    private float bottomWorldY;

    private static Sprite whiteSprite;

    // Haritada (düşme çizgisinin üstünde) görülmüş düşmanlar.
    private readonly HashSet<EnemyController> seenOnMap = new HashSet<EnemyController>();

    // =========================================================
    // KURULUM
    // =========================================================

    public void Init(PlayerController playerController)
    {
        player = playerController;
        playerHealth = player != null ? player.GetComponent<Health>() : null;

        if (templateTilemap == null)
            templateTilemap = FindTemplateTilemap();

        if ((topTiles == null || topTiles.Length == 0) &&
            templateTilemap != null)
        {
            DetectTiles(templateTilemap);
        }

        if (theme == null)
            theme = GetComponent<JungleTheme>();

        if (theme == null)
            theme = FindFirstObjectByType<JungleTheme>();

        if (ThemeOn)
        {
            Debug.Log("LevelGenerator: ORMAN teması aktif (" + theme.groundStyle + ").");
        }
        else if (theme != null)
        {
            Debug.LogWarning(
                "LevelGenerator: JungleTheme kapalı/hazır değil → eski tile'lar kullanılıyor. " +
                "'Sheet' alanına Tiles.png'yi sürükle."
            );
        }

        if ((topTiles == null || topTiles.Length == 0) && !ThemeOn)
        {
            Debug.LogWarning(
                "LevelGenerator: tile bulunamadı. Sahnede tile'lı bir Tilemap olmalı " +
                "ya da 'Top Tiles' / 'Fill Tiles' alanlarını doldur."
            );
        }
    }

    public bool IsReady =>
        ThemeOn || (topTiles != null && topTiles.Length > 0);

    private bool ThemeOn =>
        theme != null && theme.enabled && theme.Ready;

    private Tilemap FindTemplateTilemap()
    {
        Tilemap[] maps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);

        Tilemap best = null;
        int bestCount = 0;

        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap m = maps[i];

            if (m == null || (root != null && m.transform.IsChildOf(root.transform)))
                continue;

            m.CompressBounds();

            int count = m.GetUsedTilesCount();

            if (count > bestCount)
            {
                best = m;
                bestCount = count;
            }
        }

        return best;
    }

    // Şablondaki her tile'ın ROLÜNÜ komşularına bakarak bulur:
    //   üstü boş            → yüzey (çim): sol boş = sol köşe, sağ boş = sağ köşe
    //   üstü dolu           → iç (toprak): sol boş = sol kenar, sağ boş = sağ kenar
    // Böylece köşe sprite'ları düz zeminin ortasına konmaz (boşluk/çizgi olmaz).
    private void DetectTiles(Tilemap map)
    {
        List<TileBase> topMid = new List<TileBase>();
        List<TileBase> topL = new List<TileBase>();
        List<TileBase> topR = new List<TileBase>();
        List<TileBase> fill = new List<TileBase>();
        List<TileBase> fillL = new List<TileBase>();
        List<TileBase> fillR = new List<TileBase>();
        List<TileBase> bottomRow = new List<TileBase>();

        BoundsInt b = map.cellBounds;

        foreach (Vector3Int pos in b.allPositionsWithin)
        {
            TileBase t = map.GetTile(pos);

            if (t == null)
                continue;

            bool up = map.GetTile(pos + Vector3Int.up) != null;
            bool left = map.GetTile(pos + Vector3Int.left) != null;
            bool right = map.GetTile(pos + Vector3Int.right) != null;
            bool down = map.GetTile(pos + Vector3Int.down) != null;

            // En alt sıra (alt kenar çizgili olabilir): iç dolguya karışmasın.
            if (up && !down)
            {
                if (!bottomRow.Contains(t))
                    bottomRow.Add(t);

                continue;
            }

            List<TileBase> target;

            if (!up)
                target = !left && right ? topL : (left && !right ? topR : (left && right ? topMid : topMid));
            else
                target = !left && right ? fillL : (left && !right ? fillR : fill);

            if (!target.Contains(t))
                target.Add(t);
        }

        // Ortada da kullanılan bir tile köşe listesinden çıkmasın ama
        // köşede kullanılan bir tile ortaya karışmasın.
        RemoveShared(topMid, topL);
        RemoveShared(topMid, topR);
        RemoveShared(fill, fillL);
        RemoveShared(fill, fillR);

        if (topMid.Count == 0)
            topMid.AddRange(topL.Count > 0 ? topL : topR);

        if (fill.Count == 0)
            fill.AddRange(bottomRow.Count > 0 ? bottomRow : topMid);

        topTiles = topMid.ToArray();
        fillTiles = fill.ToArray();

        if (topLeftTiles == null || topLeftTiles.Length == 0)
            topLeftTiles = topL.ToArray();

        if (topRightTiles == null || topRightTiles.Length == 0)
            topRightTiles = topR.ToArray();

        if (fillLeftTiles == null || fillLeftTiles.Length == 0)
            fillLeftTiles = fillL.ToArray();

        if (fillRightTiles == null || fillRightTiles.Length == 0)
            fillRightTiles = fillR.ToArray();

        Debug.Log(
            "LevelGenerator: '" + map.name + "' Tilemap'inden tile rolleri → " +
            "yüzey " + topTiles.Length + " (sol köşe " + topLeftTiles.Length +
            ", sağ köşe " + topRightTiles.Length + "), iç " + fillTiles.Length +
            " (sol kenar " + fillLeftTiles.Length + ", sağ kenar " + fillRightTiles.Length + ")"
        );
    }

    // 'mid' listesinde olan tile'ları köşe listesinden çıkar (köşe gerçekten
    // köşe tile'ı olsun). Liste boşalırsa orta tile'a düşülür.
    private static void RemoveShared(List<TileBase> mid, List<TileBase> corner)
    {
        for (int i = corner.Count - 1; i >= 0; i--)
        {
            if (mid.Contains(corner[i]))
                corner.RemoveAt(i);
        }
    }

    private void EnsureRoot()
    {
        if (root != null)
            return;

        root = new GameObject("Rastgele Harita");

        Grid grid = root.AddComponent<Grid>();

        Grid templateGrid =
            templateTilemap != null ? templateTilemap.layoutGrid : null;

        if (templateGrid != null)
        {
            grid.cellSize = templateGrid.cellSize;
            grid.cellGap = templateGrid.cellGap;
            grid.cellLayout = templateGrid.cellLayout;
            grid.cellSwizzle = templateGrid.cellSwizzle;
            root.transform.localScale = templateGrid.transform.lossyScale;
        }

        GameObject ground = new GameObject("Zemin");

        ground.transform.SetParent(root.transform, false);

        tilemap = ground.AddComponent<Tilemap>();

        TilemapRenderer renderer = ground.AddComponent<TilemapRenderer>();

        TilemapCollider2D col = ground.AddComponent<TilemapCollider2D>();

        if (templateTilemap != null)
        {
            GameObject src = templateTilemap.gameObject;

            ground.layer = src.layer;

            try
            {
                ground.tag = src.tag;
            }
            catch
            {
                // Etiket tanımlı değilse geç.
            }

            ground.transform.localPosition = templateTilemap.transform.localPosition;

            TilemapRenderer srcRenderer = src.GetComponent<TilemapRenderer>();

            if (srcRenderer != null)
            {
                renderer.sortingLayerID = srcRenderer.sortingLayerID;
                renderer.sortingOrder = srcRenderer.sortingOrder;
                renderer.sharedMaterial = srcRenderer.sharedMaterial;
            }

            tilemap.color = templateTilemap.color;
            tilemap.tileAnchor = templateTilemap.tileAnchor;

            // Kaynakta birleşik collider varsa aynısını kur (kenar takılması olmasın).
            if (src.GetComponent<CompositeCollider2D>() != null)
            {
                Rigidbody2D rb = ground.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Static;

                ground.AddComponent<CompositeCollider2D>();

                col.compositeOperation = Collider2D.CompositeOperation.Merge;
            }
        }
    }

    // =========================================================
    // ÜRETİM
    // =========================================================

    private struct Marker
    {
        public char kind;
        public Vector2Int cell;
        public int arenaIndex;
    }

    /// <summary>
    /// Haritayı kurar. arenaCount: dövüş sayısı (dalga başına bir arena).
    /// boss: tek bir büyük boss arenası.
    /// </summary>
    public void Generate(int seed, int arenaCount, bool boss)
    {
        Generate(seed, arenaCount, boss, ChunkKind.Filler);
    }

    /// <summary>
    /// special = Shop / Rest: arenasız GEÇİŞ alanı (ortada tezgah / kamp ateşi).
    /// </summary>
    public void Generate(int seed, int arenaCount, bool boss, ChunkKind special)
    {
        Clear();

        if (!IsReady)
            return;

        // Düşman çubukları yöneticisi bir şekilde silindiyse yeniden kur.
        if (FindFirstObjectByType<EnemyOverheadBars>() == null)
            new GameObject("EnemyOverheadBars").AddComponent<EnemyOverheadBars>();

        EnsureRoot();

        Seed = seed;

        System.Random rng = new System.Random(seed);

        HashSet<Vector2Int> solids = new HashSet<Vector2Int>();
        HashSet<Vector2Int> platforms = new HashSet<Vector2Int>();   // '=' tek yönlü
        List<Marker> markers = new List<Marker>();
        List<Vector2Int> arenaSpans = new List<Vector2Int>();   // x başı, x sonu
        List<int> arenaFloors = new List<int>();

        System.Text.StringBuilder layout = new System.Text.StringBuilder();

        int cursorX = 0;
        int surface = 0;
        LevelChunk previous = null;

        void Place(LevelChunk chunk, int arenaIndex)
        {
            int offsetY = surface - chunk.LeftSurface;
            int outX = 0;

            for (int x = 0; x < chunk.Width; x++)
            {
                // Zeminli sütunlar genişler; çukur sütunları aynı kalır.
                int repeat =
                    chunk.SurfaceAt(x) > 0
                        ? Mathf.Max(1, horizontalStretch)
                        : 1;

                for (int r = 0; r < repeat; r++)
                {
                    for (int y = 0; y < chunk.Height; y++)
                    {
                        char c = chunk.At(x, y);

                        Vector2Int cell = new Vector2Int(cursorX + outX, offsetY + y);

                        if (c == '#')
                            solids.Add(cell);
                        else if (c == '=')
                            platforms.Add(cell);
                        else if (r == 0 && (c == 'E' || c == 'P' || c == 'X' || c == 'S' || c == 'R'))
                            markers.Add(new Marker { kind = c, cell = cell, arenaIndex = arenaIndex });
                    }

                    outX++;
                }
            }

            if (layout.Length > 0)
                layout.Append(" → ");

            layout.Append(chunk.name);

            cursorX += outX;
            surface = offsetY + chunk.RightSurface;
            previous = chunk;
        }

        void PlaceFillers(int count)
        {
            for (int i = 0; i < count; i++)
                Place(PickFiller(rng, surface, previous), -1);
        }

        // ---------------- BAŞLANGIÇ ----------------

        Place(Pick(rng, LevelChunkLibrary.OfKind(ChunkKind.Start)), -1);

        // ---------------- GEÇİŞ ALANI ----------------

        bool transition = special == ChunkKind.Shop || special == ChunkKind.Rest;

        if (transition)
        {
            PlaceFillers(1);
            Place(Pick(rng, LevelChunkLibrary.OfKind(special)), -1);
        }

        // ---------------- ARENALAR ----------------

        int total = transition ? 0 : (boss ? 1 : Mathf.Max(1, arenaCount));

        for (int a = 0; a < total; a++)
        {
            int fillers =
                boss
                    ? 1
                    : rng.Next(Mathf.Min(minFillers, maxFillers), Mathf.Max(minFillers, maxFillers) + 1);

            PlaceFillers(fillers);

            LevelChunk arena =
                Pick(
                    rng,
                    LevelChunkLibrary.OfKind(boss ? ChunkKind.BossArena : ChunkKind.Arena)
                );

            int start = cursorX;

            arenaFloors.Add(surface);   // arenanın sol yüzey yüksekliği

            Place(arena, a);

            arenaSpans.Add(new Vector2Int(start, cursorX));
        }

        // ---------------- ÇIKIŞ ----------------

        PlaceFillers(
            boss
                ? 1
                : rng.Next(
                    Mathf.Min(minFillersBeforeExit, maxFillersBeforeExit),
                    Mathf.Max(minFillersBeforeExit, maxFillersBeforeExit) + 1
                )
        );

        int exitStartCell = cursorX;
        int exitFloorCell = surface;

        Place(Pick(rng, LevelChunkLibrary.OfKind(ChunkKind.Exit)), -1);

        int exitEndCell = cursorX;

        int width = cursorX;

        // ---------------- ALT DOLGU ----------------

        int[] lowest = new int[width];
        int[] highest = new int[width];

        for (int x = 0; x < width; x++)
        {
            lowest[x] = int.MaxValue;
            highest[x] = int.MinValue;
        }

        foreach (Vector2Int c in solids)
        {
            if (c.x < 0 || c.x >= width)
                continue;

            lowest[c.x] = Mathf.Min(lowest[c.x], c.y);
            highest[c.x] = Mathf.Max(highest[c.x], c.y);
        }

        int minLowest = int.MaxValue;
        int maxHighest = int.MinValue;

        for (int x = 0; x < width; x++)
        {
            if (lowest[x] == int.MaxValue)
                continue;

            minLowest = Mathf.Min(minLowest, lowest[x]);
            maxHighest = Mathf.Max(maxHighest, highest[x]);
        }

        // 'bottom' = çukur düşme hesabının tabanı; zemin görselde daha da aşağı iner.
        int bottom = minLowest - groundDepth;
        int paintBottom = bottom - visualDepth;

        for (int x = 0; x < width; x++)
        {
            // Hiç zemini olmayan sütun = çukur: boş kalır.
            if (lowest[x] == int.MaxValue)
                continue;

            for (int y = paintBottom; y < lowest[x]; y++)
                solids.Add(new Vector2Int(x, y));
        }

        // ---------------- UÇLAR ----------------
        // Duvar yerine zemin iki yanda düz devam eder; görünmez duvar keser.

        int wallTop = maxHighest + wallHeight;

        int leftTop = highest[0] != int.MinValue ? highest[0] : surface - 1;
        int rightTop = highest[width - 1] != int.MinValue ? highest[width - 1] : surface - 1;

        for (int k = 1; k <= Mathf.Max(1, edgeExtension); k++)
        {
            for (int y = paintBottom; y <= leftTop; y++)
                solids.Add(new Vector2Int(-k, y));

            for (int y = paintBottom; y <= rightTop; y++)
                solids.Add(new Vector2Int(width - 1 + k, y));
        }

        // ---------------- BOYA ----------------

        Paint(solids);

        PaintPlatforms(platforms, solids);

        // ---------------- İŞARETLER ----------------

        Vector3 cellSize = CellWorldSize();

        CreateEdgeWall("Uç Engeli (sol)", -1, paintBottom, wallTop + 40);
        CreateEdgeWall("Uç Engeli (sağ)", width, paintBottom, wallTop + 40);

        bottomWorldY = CellToWorld(new Vector2Int(0, bottom)).y;

        PlayerStart = CellToWorld(new Vector2Int(2, surface)) + Vector3.up * 0.1f;
        ExitPosition = CellToWorld(new Vector2Int(width - 3, surface));

        for (int i = 0; i < arenaSpans.Count; i++)
        {
            ArenaInfo info = new ArenaInfo
            {
                leftX = CellLeftWorld(arenaSpans[i].x),
                rightX = CellLeftWorld(arenaSpans[i].y),
                floorY = CellToWorld(new Vector2Int(arenaSpans[i].x, arenaFloors[i])).y
            };

            arenas.Add(info);
        }

        for (int i = 0; i < markers.Count; i++)
        {
            Marker m = markers[i];
            Vector3 world = CellToWorld(m.cell) + Vector3.up * 0.1f;

            if (m.kind == 'P')
            {
                PlayerStart = world;
            }
            else if (m.kind == 'S' || m.kind == 'R')
            {
                SpecialPosition = world;
                HasSpecial = true;
            }
            else if (m.kind == 'X')
            {
                ExitPosition = world;
            }
            else if (m.kind == 'E' && m.arenaIndex >= 0 && m.arenaIndex < arenas.Count)
            {
                GameObject point = new GameObject("Doğma Noktası " + (m.arenaIndex + 1));

                point.transform.SetParent(root.transform, true);
                point.transform.position = world;

                arenas[m.arenaIndex].spawns.Add(point.transform);
            }
        }

        // Arena duvarları (kapalı başlar).
        for (int i = 0; i < arenas.Count; i++)
        {
            ArenaInfo info = arenas[i];

            float h = wallHeight * cellSize.y;

            info.leftWall = CreateWall("Arena Kapısı (sol)", info.leftX, info.floorY, h, cellSize.x);
            info.rightWall = CreateWall("Arena Kapısı (sağ)", info.rightX, info.floorY, h, cellSize.x);

            // Doğma noktası yoksa arenanın 3/4'üne bir tane koy.
            if (info.spawns.Count == 0)
            {
                GameObject point = new GameObject("Doğma Noktası " + (i + 1));

                point.transform.SetParent(root.transform, true);
                point.transform.position =
                    new Vector3(Mathf.Lerp(info.leftX, info.rightX, 0.75f), info.floorY + 0.1f, 0f);

                info.spawns.Add(point.transform);
            }
        }

        // Demirci: dövüş bölümlerinin başında (selam verir).
        if (!transition && blacksmithAtLevelStart)
        {
            Vector3 at =
                PlayerStart + new Vector3(blacksmithStartOffset * cellSize.x, -0.1f, 0f);

            greeter = SpawnBlacksmith(at);

            if (greeter != null)
            {
                greeterText =
                    CreateLabel(
                        greeter.transform.parent,
                        PickGreeting(seed),
                        new Color(1f, 0.9f, 0.7f),
                        0f,
                        0.7f
                    );

                PlaceAbove(greeterText.transform.parent, greeter, 0.6f);
                greeterText.transform.parent.gameObject.SetActive(false);
            }
        }

        exitZoneLeft = CellLeftWorld(exitStartCell);
        exitZoneRight = CellLeftWorld(exitEndCell);
        exitFloorY = CellToWorld(new Vector2Int(exitStartCell, exitFloorCell)).y;

        // ---------------- ORMAN SÜSLERİ ----------------

        if (ThemeOn)
        {
            bool[] reserved = new bool[width];

            // Çıkış (kapılar) alanı boş kalsın.
            for (int x = Mathf.Max(0, exitStartCell); x < Mathf.Min(width, exitEndCell); x++)
                reserved[x] = true;

            for (int i = 0; i < markers.Count; i++)
            {
                Marker m = markers[i];

                int pad = m.kind == 'S' || m.kind == 'R' ? 3 : (m.kind == 'P' ? 1 : -1);

                for (int x = m.cell.x - pad; x <= m.cell.x + pad; x++)
                {
                    if (x >= 0 && x < width)
                        reserved[x] = true;
                }
            }

            try
            {
                theme.Decorate(
                    new JungleTheme.BuildInfo
                    {
                        solids = solids,
                        platforms = platforms,
                        width = width,
                        bottom = paintBottom,
                        top = wallTop,
                        origin = origin,
                        reserved = reserved,
                        seed = seed
                    },
                    root.transform,
                    tilemap
                );
            }
            catch (System.Exception ex)
            {
                // Süs hatası haritayı/koşuyu bozmasın.
                Debug.LogException(ex);
            }
        }

        lastSafe = PlayerStart;
        LastLayout = layout.ToString();
        HasLevel = true;

        Debug.Log(
            "HARİTA (tohum " + seed + ", " + width + " kare): " + LastLayout
        );
    }

    private LevelChunk Pick(System.Random rng, List<LevelChunk> list)
    {
        float total = 0f;

        for (int i = 0; i < list.Count; i++)
            total += Mathf.Max(0f, list[i].weight);

        double roll = rng.NextDouble() * total;

        for (int i = 0; i < list.Count; i++)
        {
            roll -= Mathf.Max(0f, list[i].weight);

            if (roll <= 0)
                return list[i];
        }

        return list[list.Count - 1];
    }

    // Yüzey çok yukarı çıktıysa yukarı giden parçalar seçilmez (ve tersi);
    // az önce kullanılan parça daha az seçilir.
    private LevelChunk PickFiller(System.Random rng, int surface, LevelChunk previous)
    {
        List<LevelChunk> fillers = LevelChunkLibrary.OfKind(ChunkKind.Filler);

        List<LevelChunk> pool = new List<LevelChunk>();

        for (int i = 0; i < fillers.Count; i++)
        {
            LevelChunk c = fillers[i];

            int after = surface + c.Delta;

            if (after > maxDrift || after < -maxDrift)
                continue;

            pool.Add(c);
        }

        if (pool.Count == 0)
            pool = fillers;

        float total = 0f;

        for (int i = 0; i < pool.Count; i++)
            total += WeightOf(pool[i], previous);

        double roll = rng.NextDouble() * total;

        for (int i = 0; i < pool.Count; i++)
        {
            roll -= WeightOf(pool[i], previous);

            if (roll <= 0)
                return pool[i];
        }

        return pool[pool.Count - 1];
    }

    private static float WeightOf(LevelChunk c, LevelChunk previous)
    {
        float w = Mathf.Max(0f, c.weight);

        return c == previous ? w * 0.2f : w;
    }

    private void Paint(HashSet<Vector2Int> solids)
    {
        Vector3Int[] positions = new Vector3Int[solids.Count];
        TileBase[] tiles = new TileBase[solids.Count];

        int i = 0;

        bool themed = ThemeOn;

        foreach (Vector2Int c in solids)
        {
            bool up = solids.Contains(c + Vector2Int.up);
            bool down = solids.Contains(c + Vector2Int.down);
            bool left = solids.Contains(c + Vector2Int.left);
            bool right = solids.Contains(c + Vector2Int.right);

            positions[i] = new Vector3Int(origin.x + c.x, origin.y + c.y, 0);

            if (themed)
            {
                tiles[i] = theme.GroundTile(up, down, left, right, c.x, c.y);
            }
            else
            {
                TileBase[] set = TilesFor(up, left, right);

                // Konuma bağlı (tohumla tutarlı) varyasyon.
                int hash = (c.x * 73856093 ^ c.y * 19349663) & 0x7fffffff;

                tiles[i] = set[hash % set.Length];
            }

            i++;
        }

        tilemap.SetTiles(positions, tiles);
        tilemap.CompressBounds();
    }

    // '=' hücreleri: TEK YÖNLÜ platform (alttan zıplanıp üstüne çıkılır).
    // İki yanı zemin olan dizi = ip köprü; havada olan = tahta platform.
    private void PaintPlatforms(HashSet<Vector2Int> platforms, HashSet<Vector2Int> solids)
    {
        if (platforms.Count == 0)
            return;

        Tilemap map = CreatePlatformLayer();

        bool themed = ThemeOn;

        List<JungleTheme.Run> runs = JungleTheme.FindRuns(platforms, solids);

        for (int r = 0; r < runs.Count; r++)
        {
            JungleTheme.Run run = runs[r];

            for (int x = run.x0; x <= run.x1; x++)
            {
                TileBase t =
                    themed
                        ? theme.PlatformTile(run, x)
                        : (Has(topTiles) ? topTiles[0] : null);

                if (t != null)
                    map.SetTile(new Vector3Int(origin.x + x, origin.y + run.y, 0), t);
            }
        }
    }

    private Tilemap CreatePlatformLayer()
    {
        GameObject obj = new GameObject("Platformlar (tek yönlü)");

        obj.transform.SetParent(root.transform, false);
        obj.transform.localPosition = tilemap.transform.localPosition;
        obj.layer = tilemap.gameObject.layer;

        try
        {
            obj.tag = tilemap.gameObject.tag;
        }
        catch
        {
            // Etiket tanımlı değilse geç.
        }

        Tilemap map = obj.AddComponent<Tilemap>();
        TilemapRenderer r = obj.AddComponent<TilemapRenderer>();

        map.tileAnchor = tilemap.tileAnchor;
        map.color = tilemap.color;

        TilemapRenderer src = tilemap.GetComponent<TilemapRenderer>();

        if (src != null)
        {
            r.sortingLayerID = src.sortingLayerID;
            r.sortingOrder = src.sortingOrder;
            r.sharedMaterial = src.sharedMaterial;
        }

        TilemapCollider2D col = obj.AddComponent<TilemapCollider2D>();

        Rigidbody2D rb = obj.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;

        CompositeCollider2D composite = obj.AddComponent<CompositeCollider2D>();

        col.compositeOperation = Collider2D.CompositeOperation.Merge;
        col.usedByEffector = true;
        composite.usedByEffector = true;

        PlatformEffector2D effector = obj.AddComponent<PlatformEffector2D>();
        effector.useOneWay = true;
        effector.surfaceArc = 170f;
        effector.useSideFriction = false;
        effector.useSideBounce = false;

        return map;
    }

    // Komşulara göre doğru rol; o rolde tile yoksa ortaya düşer.
    private TileBase[] TilesFor(bool up, bool left, bool right)
    {
        TileBase[] mid = topTiles;
        TileBase[] fillMid = Has(fillTiles) ? fillTiles : topTiles;

        if (!up)
        {
            if (!left && right && Has(topLeftTiles))
                return topLeftTiles;

            if (left && !right && Has(topRightTiles))
                return topRightTiles;

            return mid;
        }

        if (!left && right && Has(fillLeftTiles))
            return fillLeftTiles;

        if (left && !right && Has(fillRightTiles))
            return fillRightTiles;

        return fillMid;
    }

    private static bool Has(TileBase[] set)
    {
        return set != null && set.Length > 0;
    }

    // =========================================================
    // ÇIKIŞ KAPILARI (fiziksel oda seçimi)
    // =========================================================

    private class Door
    {
        public GameObject root;
        public SpriteRenderer frame;
        public TextMesh label;
        public TextMesh prompt;
        public float x;
        public bool locked;
        public Color color;
    }

    private readonly List<Door> doors = new List<Door>();

    private class Stand
    {
        public GameObject root;
        public SpriteRenderer frame;
        public TextMesh prompt;
        public bool used;
        public Color color;
    }

    private Stand stand;

    // =========================================================
    // DEMİRCİ (NPC)
    // =========================================================

    [Header("Demirci (NPC)")]
    [Tooltip(
        "Şablon. Boşsa sahnede adı 'Blacksmith Name' olan obje kullanılır " +
        "(kopyası doğar; fizik bileşenleri kaldırılır).")]
    public GameObject blacksmith;

    public string blacksmithName = "Demirci";

    [Tooltip("Dükkan geçişinde tezgah yerine demirci durur.")]
    public bool blacksmithAtShop = true;

    [Tooltip("Her dövüş bölümünün başında demirci durur ve selam verir.")]
    public bool blacksmithAtLevelStart = true;

    [Tooltip("Bölüm başında oyuncunun kaç kare sağında durur.")]
    public float blacksmithStartOffset = 7f;

    [Tooltip("Sprite'ı varsayılan olarak SAĞA mı bakıyor? (oyuncuya dönmesi için)")]
    public bool blacksmithFacesRight = true;

    public string[] blacksmithGreetings =
    {
        "Kılıcın keskin olsun!",
        "Ormanda dikkatli ol, savaşçı.",
        "Dönüşte uğra, malım bol.",
        "Parry'yi unutma!",
        "Yine mi sen? Hâlâ hayattasın demek."
    };

    private GameObject greeter;
    private TextMesh greeterText;

    private GameObject FindBlacksmithTemplate()
    {
        if (blacksmith != null)
            return blacksmith;

        if (string.IsNullOrEmpty(blacksmithName))
            return null;

        Transform[] all =
            FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];

            if (t == null || t.name != blacksmithName)
                continue;

            if (root != null && t.IsChildOf(root.transform))
                continue;

            blacksmith = t.gameObject;

            return blacksmith;
        }

        return null;
    }

    // Zemine (ayakları 'groundPoint'e) oturan bir demirci kopyası.
    private GameObject SpawnBlacksmith(Vector3 groundPoint)
    {
        GameObject template = FindBlacksmithTemplate();

        if (template == null)
            return null;

        GameObject holder = new GameObject("Demirci (NPC)");
        holder.transform.SetParent(root.transform, true);
        holder.transform.position = groundPoint;
        holder.transform.localScale = Vector3.one;

        GameObject npc = Instantiate(template, groundPoint, Quaternion.identity);

        npc.name = "Demirci";
        npc.SetActive(true);

        // Fizik yok: oyuncuyu itmesin, düşmesin, vurulmasın.
        foreach (Rigidbody2D rb in npc.GetComponentsInChildren<Rigidbody2D>(true))
            Destroy(rb);

        foreach (Collider2D c in npc.GetComponentsInChildren<Collider2D>(true))
            Destroy(c);

        npc.transform.SetParent(holder.transform, true);

        // Ayakları zemine oturt.
        Bounds b;

        if (TryRendererBounds(npc, out b))
            npc.transform.position += new Vector3(0f, groundPoint.y - b.min.y, 0f);

        NpcFacePlayer face = npc.AddComponent<NpcFacePlayer>();
        face.Bind(player != null ? player.transform : null, blacksmithFacesRight);

        return npc;
    }

    private static bool TryRendererBounds(GameObject obj, out Bounds bounds)
    {
        bounds = new Bounds(obj.transform.position, Vector3.zero);

        bool any = false;

        foreach (SpriteRenderer sr in obj.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!sr.enabled || sr.sprite == null)
                continue;

            if (!any)
                bounds = sr.bounds;
            else
                bounds.Encapsulate(sr.bounds);

            any = true;
        }

        return any;
    }

    // Yazı tutucusunu NPC'nin başının üstüne taşır.
    private static void PlaceAbove(Transform label, GameObject npc, float extra)
    {
        if (label == null || npc == null)
            return;

        Bounds b;

        float top =
            TryRendererBounds(npc, out b)
                ? b.max.y
                : npc.transform.position.y + 2f;

        label.position = new Vector3(npc.transform.position.x, top + extra, label.position.z);
    }

    private string PickGreeting(int seed)
    {
        if (blacksmithGreetings == null || blacksmithGreetings.Length == 0)
            return "";

        return blacksmithGreetings[Mathf.Abs(seed) % blacksmithGreetings.Length];
    }

    [Header("Kapılar")]
    [Tooltip("Kapı yazılarının boyutu.")]
    public float labelScale = 1.4f;

    public Color lockedDoorColor = new Color(0.45f, 0.45f, 0.5f, 0.6f);

    /// <summary>Çıkış alanına kapıları dizer (varsa eskileri siler).</summary>
    public void SetExitDoors(string[] labels, Color[] colors, bool locked)
    {
        ClearDoors();

        if (!HasLevel || labels == null || labels.Length == 0)
            return;

        Vector3 cell = CellWorldSize();

        int n = labels.Length;

        for (int i = 0; i < n; i++)
        {
            float x = Mathf.Lerp(exitZoneLeft, exitZoneRight, (i + 1f) / (n + 1f));

            Color color = colors != null && i < colors.Length ? colors[i] : Color.white;

            doors.Add(CreateDoor(labels[i], color, x, cell));
        }

        SetDoorsLocked(locked, null);
    }

    public void SetDoorsLocked(bool locked, string lockedLabel)
    {
        for (int i = 0; i < doors.Count; i++)
        {
            Door d = doors[i];

            d.locked = locked;

            if (d.frame != null)
                d.frame.color = locked ? lockedDoorColor : WithAlpha(d.color, 0.55f);

            if (locked && lockedLabel != null && d.prompt != null)
                d.prompt.text = lockedLabel;
        }
    }

    public void SetLockedText(string text)
    {
        for (int i = 0; i < doors.Count; i++)
        {
            if (doors[i].locked && doors[i].prompt != null)
                doors[i].prompt.text = text;
        }
    }

    // Oyuncunun içinde durduğu (kilitsiz) kapı; yoksa -1.
    public int DoorAt(Vector3 position)
    {
        float half = CellWorldSize().x * 1.3f;

        for (int i = 0; i < doors.Count; i++)
        {
            if (!doors[i].locked && Mathf.Abs(position.x - doors[i].x) <= half)
                return i;
        }

        return -1;
    }

    public bool NearExit(Vector3 position)
    {
        return HasLevel && position.x >= exitZoneLeft;
    }

    public void ClearDoors()
    {
        for (int i = 0; i < doors.Count; i++)
        {
            if (doors[i].root != null)
                Destroy(doors[i].root);
        }

        doors.Clear();
    }

    private Door CreateDoor(string text, Color color, float x, Vector3 cell)
    {
        Door d = new Door { x = x, color = color };

        d.root = new GameObject("Kapı: " + text);
        d.root.transform.SetParent(root.transform, true);
        d.root.transform.position = new Vector3(x, exitFloorY, 0f);

        float w = cell.x * 2.2f;
        float h = cell.y * 4f;

        d.frame = CreateRect(d.root.transform, w, h, WithAlpha(color, 0.55f), 0);

        d.label = CreateLabel(d.root.transform, text, color, h + cell.y * 0.8f, 1f);
        d.prompt = CreateLabel(d.root.transform, "[W] GİR", Color.white, h * 0.5f, 0.75f);

        d.prompt.gameObject.SetActive(false);

        return d;
    }

    // =========================================================
    // TEZGAH / KAMP ATEŞİ (geçiş alanı)
    // =========================================================

    public void CreateStand(string text, Color color)
    {
        if (!HasLevel || !HasSpecial)
            return;

        Vector3 cell = CellWorldSize();

        stand = new Stand { color = color };

        stand.root = new GameObject("Tezgah: " + text);
        stand.root.transform.SetParent(root.transform, true);
        stand.root.transform.position = SpecialPosition;

        float w = cell.x * 3f;
        float h = cell.y * 2.5f;

        GameObject npc =
            blacksmithAtShop && text == "DÜKKAN"
                ? SpawnBlacksmith(SpecialPosition)
                : null;

        if (npc != null)
        {
            // Tezgah yerine demirci: yazılar başının üstünde.
            Bounds nb;

            if (TryRendererBounds(npc, out nb))
                h = Mathf.Max(h, nb.max.y - SpecialPosition.y);
        }
        else
        {
            stand.frame = CreateRect(stand.root.transform, w, h, WithAlpha(color, 0.6f), 0);
        }

        CreateLabel(stand.root.transform, text, color, h + cell.y * 0.8f, 1f);

        stand.prompt = CreateLabel(stand.root.transform, "[W] " + text, Color.white, h + cell.y * 2f, 0.75f);
        stand.prompt.gameObject.SetActive(false);
    }

    public bool PlayerAtStand(Vector3 position)
    {
        if (stand == null || stand.used)
            return false;

        return Mathf.Abs(position.x - SpecialPosition.x) <= CellWorldSize().x * 2.5f;
    }

    public void SetStandUsed()
    {
        if (stand == null)
            return;

        stand.used = true;

        if (stand.frame != null)
            stand.frame.color = lockedDoorColor;

        if (stand.prompt != null)
            stand.prompt.gameObject.SetActive(false);
    }

    // Kapı / tezgah yanındayken "[W]" ipucu.
    private void UpdatePrompts()
    {
        if (player == null)
            return;

        Vector3 p = player.transform.position;
        float half = CellWorldSize().x * 1.3f;

        for (int i = 0; i < doors.Count; i++)
        {
            Door d = doors[i];

            bool inside = Mathf.Abs(p.x - d.x) <= half;

            if (d.prompt != null)
                d.prompt.gameObject.SetActive(inside || d.locked);

            if (d.frame != null && !d.locked)
                d.frame.color = WithAlpha(d.color, inside ? 0.85f : 0.55f);
        }

        // Demirci selamı: yakındayken.
        if (greeter != null && greeterText != null)
        {
            bool near = Mathf.Abs(p.x - greeter.transform.position.x) <= CellWorldSize().x * 4f;

            GameObject holder = greeterText.transform.parent.gameObject;

            if (holder.activeSelf != near)
                holder.SetActive(near);
        }

        if (stand != null && stand.prompt != null)
            stand.prompt.gameObject.SetActive(PlayerAtStand(p));
    }

    private SpriteRenderer CreateRect(Transform parent, float w, float h, Color color, int orderOffset)
    {
        GameObject obj = new GameObject("Çerçeve");

        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = GetWhiteSprite();
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = new Vector2(w, h);
        sr.color = color;

        TilemapRenderer tr = tilemap != null ? tilemap.GetComponent<TilemapRenderer>() : null;

        if (tr != null)
        {
            sr.sortingLayerID = tr.sortingLayerID;
            sr.sortingOrder = tr.sortingOrder + orderOffset;
        }

        return sr;
    }

    private TextMesh CreateLabel(Transform parent, string text, Color color, float height, float scale)
    {
        TilemapRenderer tr = tilemap != null ? tilemap.GetComponent<TilemapRenderer>() : null;

        int layer = tr != null ? tr.sortingLayerID : 0;
        int order = (tr != null ? tr.sortingOrder : 0) + 20;

        GameObject holder = new GameObject("Yazı");
        holder.transform.SetParent(parent, false);
        holder.transform.localPosition = new Vector3(0f, height, 0f);
        holder.transform.localScale = Vector3.one * labelScale * scale;

        TextMesh shadow = CombatCallout.CreateText(holder.transform, text, new Color(0f, 0f, 0f, 0.8f), 0.05f, layer, order);
        shadow.transform.localPosition = new Vector3(0.04f, -0.04f, 0f);

        TextMesh label = CombatCallout.CreateText(holder.transform, text, color, 0.05f, layer, order + 1);

        // Yazı değişince gölge de değişsin.
        holder.AddComponent<TextShadowSync>().Bind(label, shadow);

        return label;
    }

    private static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }

    // =========================================================
    // ARENA API (RunManager kullanır)
    // =========================================================

    public bool PlayerInArena(int index, Vector3 position)
    {
        if (index < 0 || index >= arenas.Count)
            return true;

        ArenaInfo a = arenas[index];

        return
            position.x > a.leftX + arenaEnterMargin * CellWorldSize().x &&
            position.x < a.rightX;
    }

    public void SetArenaLocked(int index, bool locked)
    {
        if (index < 0 || index >= arenas.Count)
            return;

        ArenaInfo a = arenas[index];

        if (a.leftWall != null)
            a.leftWall.SetActive(locked);

        if (a.rightWall != null)
            a.rightWall.SetActive(locked);
    }

    public Transform[] ArenaSpawnPoints(int index)
    {
        if (index < 0 || index >= arenas.Count)
            return null;

        return arenas[index].spawns.ToArray();
    }

    public Vector3 ArenaCenter(int index)
    {
        if (index < 0 || index >= arenas.Count)
            return PlayerStart;

        ArenaInfo a = arenas[index];

        return new Vector3((a.leftX + a.rightX) * 0.5f, a.floorY, 0f);
    }

    public bool PlayerAtExit(Vector3 position)
    {
        return position.x >= ExitPosition.x - CellWorldSize().x;
    }

    // =========================================================
    // OYUNCU
    // =========================================================

    public void TeleportPlayer(Vector3 position)
    {
        if (player == null)
            return;

        player.transform.position = position;

        if (player.rb != null)
        {
            player.rb.position = position;
            player.rb.linearVelocity = Vector2.zero;
        }

        lastSafe = position;

        SnapCameras();
    }

    // Kamera ışınlanmaya kaymadan yetişsin.
    private static void SnapCameras()
    {
        CinemachineVirtualCameraBase[] cams =
            FindObjectsByType<CinemachineVirtualCameraBase>(FindObjectsSortMode.None);

        for (int i = 0; i < cams.Length; i++)
        {
            if (cams[i] != null)
                cams[i].PreviousStateIsValid = false;
        }
    }

    // Çukur koruması + düşen düşmanlar.
    private void Update()
    {
        if (!HasLevel || player == null)
            return;

        UpdatePrompts();

        if (playerHealth != null && playerHealth.IsDead)
            return;

        float fallLine = bottomWorldY - pitFallDepth * CellWorldSize().y;

        // Son güvenli nokta: yerdeyken düzenli kaydet.
        if (player.isGrounded && Time.time >= nextSafeSample)
        {
            nextSafeSample = Time.time + 0.25f;

            if (player.transform.position.y > fallLine + 1f)
                lastSafe = player.transform.position;
        }

        if (player.transform.position.y < fallLine)
            OnPlayerFell();

        // Düşen düşmanlar ölür (oda kilitlenmesin).
        // Sadece daha önce HARİTADA (düşme çizgisinin üstünde) görülmüş
        // düşmanlar sayılır: lobideki / sahnedeki başka objeler (ör. yanlışlıkla
        // EnemyController eklenmiş 'Managers') "düştü" sanılıp yok edilmesin.
        for (int i = EnemyController.All.Count - 1; i >= 0; i--)
        {
            EnemyController e = EnemyController.All[i];

            if (e == null || e.IsDead)
                continue;

            // Bu üreticiyi (ve RunManager'ı) taşıyan obje asla düşman değildir.
            if (transform.IsChildOf(e.transform))
                continue;

            if (e.transform.position.y >= fallLine)
            {
                seenOnMap.Add(e);
                continue;
            }

            if (!seenOnMap.Contains(e))
                continue;

            seenOnMap.Remove(e);

            Health h = e.GetComponent<Health>();

            if (h != null)
                h.TakeDamage(Mathf.Max(1, h.CurrentHealth));
            else
                Destroy(e.gameObject);
        }
    }

    private void OnPlayerFell()
    {
        TeleportPlayer(lastSafe + Vector3.up * 0.5f);

        if (playerHealth == null || pitDamagePercent <= 0f)
            return;

        int damage =
            Mathf.Max(1, Mathf.CeilToInt(playerHealth.MaxHealth * pitDamagePercent));

        PlayerDamageReceiver receiver = player.GetComponent<PlayerDamageReceiver>();

        if (receiver != null)
        {
            receiver.TakeDamage(
                damage,
                Vector2.up,
                0f,
                0f,
                0.05f,
                -1f,
                null,
                PlayerHitKind.Other
            );
        }
        else
        {
            playerHealth.TakeDamage(damage);
        }

        CombatCallout.Popup(
            player.transform.position + Vector3.up * 2f,
            "ÇUKUR!",
            new Color(1f, 0.45f, 0.35f),
            1f
        );
    }

    // =========================================================
    // TEMİZLE
    // =========================================================

    public void Clear()
    {
        seenOnMap.Clear();
        greeter = null;
        greeterText = null;
        arenas.Clear();
        doors.Clear();
        stand = null;
        HasSpecial = false;

        if (root != null)
        {
            Destroy(root);
            root = null;
            tilemap = null;
        }

        HasLevel = false;
        LastLayout = "";
    }

    // =========================================================
    // YARDIMCILAR
    // =========================================================

    private Vector3 CellWorldSize()
    {
        if (tilemap == null || tilemap.layoutGrid == null)
            return Vector3.one;

        Vector3 size = tilemap.layoutGrid.cellSize;
        Vector3 scale = tilemap.transform.lossyScale;

        return new Vector3(size.x * scale.x, size.y * scale.y, 1f);
    }

    // Hücrenin alt-orta noktası (dünya).
    private Vector3 CellToWorld(Vector2Int cell)
    {
        Vector3Int c = new Vector3Int(origin.x + cell.x, origin.y + cell.y, 0);

        Vector3 corner = tilemap.CellToWorld(c);

        return corner + new Vector3(CellWorldSize().x * 0.5f, 0f, 0f);
    }

    private float CellLeftWorld(int cellX)
    {
        return tilemap.CellToWorld(new Vector3Int(origin.x + cellX, origin.y, 0)).x;
    }

    // Görünmez engel: bir hücre genişliğinde, zemin katmanında.
    private void CreateEdgeWall(string wallName, int cellX, int fromCellY, int toCellY)
    {
        Vector3 cell = CellWorldSize();

        float x = CellLeftWorld(cellX) + cell.x * 0.5f;
        float y0 = CellToWorld(new Vector2Int(cellX, fromCellY)).y;
        float y1 = CellToWorld(new Vector2Int(cellX, toCellY)).y;

        GameObject wall = new GameObject(wallName);

        wall.transform.SetParent(root.transform, true);
        wall.transform.position = new Vector3(x, (y0 + y1) * 0.5f, 0f);
        wall.transform.localScale = Vector3.one;

        if (tilemap != null)
            wall.layer = tilemap.gameObject.layer;

        BoxCollider2D box = wall.AddComponent<BoxCollider2D>();
        box.size = new Vector2(cell.x, Mathf.Abs(y1 - y0));
    }

    private GameObject CreateWall(string wallName, float x, float floorY, float height, float cellWidth)
    {
        GameObject wall = new GameObject(wallName);

        wall.transform.SetParent(root.transform, true);
        wall.transform.position = new Vector3(x, floorY + height * 0.5f, 0f);
        wall.transform.localScale = Vector3.one;

        if (tilemap != null)
            wall.layer = tilemap.gameObject.layer;

        BoxCollider2D box = wall.AddComponent<BoxCollider2D>();
        box.size = new Vector2(cellWidth * 0.5f, height);

        SpriteRenderer sr = wall.AddComponent<SpriteRenderer>();
        sr.sprite = GetWhiteSprite();
        sr.color = lockColor;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = new Vector2(cellWidth * 0.5f, height);

        TilemapRenderer tr = tilemap != null ? tilemap.GetComponent<TilemapRenderer>() : null;

        if (tr != null)
        {
            sr.sortingLayerID = tr.sortingLayerID;
            sr.sortingOrder = tr.sortingOrder + 10;
        }

        wall.SetActive(false);

        return wall;
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null)
            return whiteSprite;

        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point
        };

        Color[] px = new Color[16];

        for (int i = 0; i < px.Length; i++)
            px[i] = Color.white;

        tex.SetPixels(px);
        tex.Apply();

        whiteSprite =
            Sprite.Create(
                tex,
                new Rect(0, 0, 4, 4),
                new Vector2(0.5f, 0.5f),
                4f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(1, 1, 1, 1)
            );

        return whiteSprite;
    }
}

// Kapı yazısının gölgesi, yazı değişince onu takip eder.
public class TextShadowSync : MonoBehaviour
{
    private TextMesh label;
    private TextMesh shadow;

    public void Bind(TextMesh label, TextMesh shadow)
    {
        this.label = label;
        this.shadow = shadow;
    }

    private void LateUpdate()
    {
        if (label != null && shadow != null && shadow.text != label.text)
            shadow.text = label.text;
    }
}

// NPC oyuncuya döner (kökün x ölçeğini çevirerek).
public class NpcFacePlayer : MonoBehaviour
{
    private Transform target;
    private bool facesRight = true;
    private float baseScaleX = 1f;

    public void Bind(Transform target, bool facesRight)
    {
        this.target = target;
        this.facesRight = facesRight;
        baseScaleX = Mathf.Abs(transform.localScale.x);
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        float dx = target.position.x - transform.position.x;

        if (Mathf.Abs(dx) < 0.2f)
            return;

        bool wantRight = dx > 0f;
        float sign = wantRight == facesRight ? 1f : -1f;

        Vector3 s = transform.localScale;
        s.x = baseScaleX * sign;
        transform.localScale = s;
    }
}
