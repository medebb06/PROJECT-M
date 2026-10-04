using System.Collections.Generic;

/// <summary>
/// HARİTA PARÇALARI. Her parça satır satır yazılmış küçük bir harita
/// (ÜSTTEN aşağı). Üretici bunları rastgele sırayla yan yana dizer.
///
/// İşaretler:
///   #  zemin (yüzeyde çim, altında toprak tile'ı otomatik seçilir)
///   .  boşluk
///   E  düşman doğma noktası (arenada)
///   P  oyuncu başlangıcı (başlangıç parçasında)
///   X  çıkış (çıkış parçasında; oda seçimi kapıları burada çıkar)
///   S  dükkan tezgahı (geçiş parçası)
///   R  kamp ateşi / dinlenme (geçiş parçası)
///   =  tek yönlü platform (alttan geçilir, üstüne basılır). İki yanında
///      zemin varsa ip köprü, yoksa uçan tahta olarak çizilir.
///
/// KURALLAR (oynanabilirlik için):
///  - İlk ve son sütunun en alt satırı zemin olmalı (bağlantı yüksekliği
///    buradan hesaplanır; parçalar yüzey yükseklikleri eşleşecek şekilde
///    birleştirilir).
///  - Basamaklar en fazla 2 kare, çukurlar en fazla 3 kare.
///  - Yüzeyin üstünde en az 6 boş satır (tavan yok, zıplama rahat).
///  - Zeminin altı üretici tarafından doldurulur; sütunda hiç zemin
///    yoksa orası ÇUKUR olur (düşen oyuncu son güvenli yere döner).
///
/// Yeni parça eklemek için aşağıdaki listelere bir tane daha yaz.
/// </summary>
public enum ChunkKind
{
    Start,
    Filler,
    Arena,
    BossArena,
    Exit,
    Shop,
    Rest
}

public class LevelChunk
{
    public string name;
    public ChunkKind kind;
    public string[] rows;   // üstten aşağı

    public float weight = 1f;

    public LevelChunk(string name, ChunkKind kind, float weight, params string[] rows)
    {
        this.name = name;
        this.kind = kind;
        this.weight = weight;
        this.rows = rows;
    }

    public int Width => rows[0].Length;
    public int Height => rows.Length;

    // (x, y) y = 0 en alt satır.
    public char At(int x, int y)
    {
        return rows[Height - 1 - y][x];
    }

    public bool Solid(int x, int y)
    {
        return At(x, y) == '#';
    }

    // Sütundaki en üst zeminin bir üstü (yüzey). Zemin yoksa -1.
    public int SurfaceAt(int x)
    {
        for (int y = Height - 1; y >= 0; y--)
        {
            if (Solid(x, y))
                return y + 1;
        }

        return -1;
    }

    public int LeftSurface => SurfaceAt(0);
    public int RightSurface => SurfaceAt(Width - 1);

    // Sağ yüzey − sol yüzey (yokuş yukarı +).
    public int Delta => RightSurface - LeftSurface;
}

public static class LevelChunkLibrary
{
    private static List<LevelChunk> all;

    public static List<LevelChunk> All
    {
        get
        {
            if (all == null)
                all = Build();

            return all;
        }
    }

    public static List<LevelChunk> OfKind(ChunkKind kind)
    {
        List<LevelChunk> list = new List<LevelChunk>();

        for (int i = 0; i < All.Count; i++)
        {
            if (All[i].kind == kind)
                list.Add(All[i]);
        }

        return list;
    }

    private static List<LevelChunk> Build()
    {
        return new List<LevelChunk>
        {
            // =================================================
            // BAŞLANGIÇ
            // =================================================

            new LevelChunk("Başlangıç", ChunkKind.Start, 1f,
                "..............",
                "..............",
                "..............",
                "..............",
                "..............",
                "..............",
                "...P..........",
                "##############",
                "##############"),

            // =================================================
            // ARA PARÇALAR (yürüme / zıplama)
            // =================================================

            new LevelChunk("Düz", ChunkKind.Filler, 1f,
                "..........",
                "..........",
                "..........",
                "..........",
                "..........",
                "..........",
                "..........",
                "##########",
                "##########"),

            new LevelChunk("Basamak Yukarı", ChunkKind.Filler, 1f,
                "............",
                "............",
                "............",
                "............",
                "............",
                "............",
                "............",
                "......######",
                "############",
                "############"),

            new LevelChunk("Basamak Aşağı", ChunkKind.Filler, 1f,
                "............",
                "............",
                "............",
                "............",
                "............",
                "............",
                "............",
                "######......",
                "############",
                "############"),

            new LevelChunk("Merdiven Yukarı", ChunkKind.Filler, 0.8f,
                "..............",
                "..............",
                "..............",
                "..............",
                "..............",
                "..............",
                "..............",
                "..........####",
                ".....#########",
                "##############",
                "##############"),

            new LevelChunk("Merdiven Aşağı", ChunkKind.Filler, 0.8f,
                "..............",
                "..............",
                "..............",
                "..............",
                "..............",
                "..............",
                "..............",
                "####..........",
                "#########.....",
                "##############",
                "##############"),

            new LevelChunk("Çukur", ChunkKind.Filler, 0.9f,
                "............",
                "............",
                "............",
                "............",
                "............",
                "............",
                "............",
                "#####...####",
                "#####...####"),

            new LevelChunk("Basamak Taşları", ChunkKind.Filler, 0.7f,
                "................",
                "................",
                "................",
                "................",
                "................",
                "................",
                "................",
                "####..##..######",
                "####..##..######"),

            new LevelChunk("Tepe", ChunkKind.Filler, 0.9f,
                "..............",
                "..............",
                "..............",
                "..............",
                "..............",
                "..............",
                "..............",
                "....######....",
                "##############",
                "##############"),

            new LevelChunk("Kaya", ChunkKind.Filler, 0.7f,
                "..............",
                "..............",
                "..............",
                "..............",
                "..............",
                "..............",
                "..............",
                "......##......",
                "......##......",
                "##############",
                "##############"),

            new LevelChunk("Çukur + Basamak", ChunkKind.Filler, 0.6f,
                "..............",
                "..............",
                "..............",
                "..............",
                "..............",
                "..............",
                "..............",
                ".......#######",
                "####...#######",
                "####...#######"),

            // '=' : tek yönlü platform. İki yanı zemin = ip köprü.
            new LevelChunk("Köprü", ChunkKind.Filler, 0.8f,
                "..............",
                "..............",
                "..............",
                "..............",
                "..............",
                "..............",
                "..............",
                "#####====#####",
                "#####....#####"),

            // Geniş çukur, üstünde iki uçan tahta (2 kare yukarıda).
            new LevelChunk("Platformlu Çukur", ChunkKind.Filler, 0.6f,
                "................",
                "................",
                "................",
                "................",
                "................",
                "....===..===....",
                "................",
                "####........####",
                "####........####"),

            // =================================================
            // ARENALAR (dövüş: girince iki yan kapanır)
            // =================================================

            new LevelChunk("Arena: Düz", ChunkKind.Arena, 1f,
                "..........................",
                "..........................",
                "..........................",
                "..........................",
                "..........................",
                "..........................",
                "....E...............E.....",
                "##########################",
                "##########################"),

            new LevelChunk("Arena: Tümsekler", ChunkKind.Arena, 0.8f,
                "............................",
                "............................",
                "............................",
                "............................",
                "............................",
                "............................",
                ".....E................E.....",
                ".........##......##.........",
                "############################",
                "############################"),

            new LevelChunk("Arena: Basamaklı", ChunkKind.Arena, 0.7f,
                "............................",
                "............................",
                "............................",
                "............................",
                "............................",
                "............................",
                "...E..................E.....",
                "#####..................#####",
                "############################",
                "############################"),

            // =================================================
            // BOSS ARENASI
            // =================================================

            new LevelChunk("Boss Arenası", ChunkKind.BossArena, 1f,
                "..................................",
                "..................................",
                "..................................",
                "..................................",
                "..................................",
                "..................................",
                "..........................E.......",
                "##################################",
                "##################################"),

            // =================================================
            // GEÇİŞ ALANLARI (dükkan / dinlenme)
            // =================================================

            new LevelChunk("Dükkan", ChunkKind.Shop, 1f,
                "................",
                "................",
                "................",
                "................",
                "................",
                "................",
                "........S.......",
                "################",
                "################"),

            new LevelChunk("Kamp", ChunkKind.Rest, 1f,
                "................",
                "................",
                "................",
                "................",
                "................",
                "................",
                "........R.......",
                "################",
                "################"),

            // =================================================
            // ÇIKIŞ (oda seçimi kapıları burada)
            // =================================================

            new LevelChunk("Çıkış", ChunkKind.Exit, 1f,
                "................",
                "................",
                "................",
                "................",
                "................",
                "................",
                "............X...",
                "################",
                "################")
        };
    }
}
