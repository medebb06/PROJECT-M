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

        if (topTiles == null || topTiles.Length == 0)
        {
            Debug.LogWarning(
                "LevelGenerator: tile bulunamadı. Sahnede tile'lı bir Tilemap olmalı " +
                "ya da 'Top Tiles' / 'Fill Tiles' alanlarını doldur."
            );
        }
    }

    public bool IsReady =>
        topTiles != null && topTiles.Length > 0;

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
        Clear();

        if (!IsReady)
            return;

        EnsureRoot();

        Seed = seed;

        System.Random rng = new System.Random(seed);

        HashSet<Vector2Int> solids = new HashSet<Vector2Int>();
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
                        else if (r == 0 && (c == 'E' || c == 'P' || c == 'X'))
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

        // ---------------- ARENALAR ----------------

        int total = boss ? 1 : Mathf.Max(1, arenaCount);

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

        Place(Pick(rng, LevelChunkLibrary.OfKind(ChunkKind.Exit)), -1);

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

        int bottom = minLowest - groundDepth;

        for (int x = 0; x < width; x++)
        {
            // Hiç zemini olmayan sütun = çukur: boş kalır.
            if (lowest[x] == int.MaxValue)
                continue;

            for (int y = bottom; y < lowest[x]; y++)
                solids.Add(new Vector2Int(x, y));
        }

        // ---------------- UÇ DUVARLARI ----------------

        int wallTop = maxHighest + wallHeight;

        for (int y = bottom; y <= wallTop; y++)
        {
            for (int k = 1; k <= 2; k++)
            {
                solids.Add(new Vector2Int(-k, y));
                solids.Add(new Vector2Int(width - 1 + k, y));
            }
        }

        // ---------------- BOYA ----------------

        Paint(solids);

        // ---------------- İŞARETLER ----------------

        Vector3 cellSize = CellWorldSize();

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

        foreach (Vector2Int c in solids)
        {
            bool up = solids.Contains(c + Vector2Int.up);
            bool left = solids.Contains(c + Vector2Int.left);
            bool right = solids.Contains(c + Vector2Int.right);

            TileBase[] set = TilesFor(up, left, right);

            // Konuma bağlı (tohumla tutarlı) varyasyon.
            int hash = Mathf.Abs(c.x * 73856093 ^ c.y * 19349663);

            positions[i] = new Vector3Int(origin.x + c.x, origin.y + c.y, 0);
            tiles[i] = set[hash % set.Length];

            i++;
        }

        tilemap.SetTiles(positions, tiles);
        tilemap.CompressBounds();
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
        for (int i = EnemyController.All.Count - 1; i >= 0; i--)
        {
            EnemyController e = EnemyController.All[i];

            if (e == null || e.IsDead || e.transform.position.y >= fallLine)
                continue;

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
        arenas.Clear();

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