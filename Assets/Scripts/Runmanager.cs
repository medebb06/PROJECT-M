using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KOŞU DÖNGÜSÜ:
///
///   [Lobi: zorluk seç] → başlangıç charm'ı →
///   PERDE 1: oda 1 (dövüş) → kapı seç → oda 2 → kapı → oda 3 → kapı → oda 4 → BOSS
///   PERDE 2: ...                                                            → BOSS
///   PERDE 3: ...                                                            → BOSS → ZAFER
///
/// Kapılar: Dövüş (charm), Elit (2 charm + bol altın), Dükkan, Dinlenme.
/// Altın: öldürme (+execute bonusu), parry, hasarsız oda, elit, boss.
/// Koşular arası: MetaProgress (charm kilitleri, zorluk kademeleri).
///
/// KURULUM: Sahnede boş bir objeye ekle, 'Enemy Prefab' ata (ve istersen
/// 'Boss Prefab'). Gerisi kendiliğinden kurulur.
///
/// DÜŞMAN TİPLERİ: Düellocu (Enemy Prefab), Çevik, Ağır ve Okçu. 'Quick /
/// Heavy / Archer Prefab' boşsa Enemy Prefab'ın renklendirilmiş kopyası kullanılır
/// (EnemyArchetype eklenir). Hangi odada hangi tipin çıkacağı perdeye göre
/// ağırlıklıdır (Archetype Weights Per Act).
/// </summary>
public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    // Bölüm (dövüş odası) olayları. int: dövüş odası sayacı (Stage).
    public static event Action<int> StageStarted;
    public static event Action<int> StageCleared;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration
    )]
    private static void ResetStatics()
    {
        Instance = null;
        StageStarted = null;
        StageCleared = null;
    }

    [Header("References")]
    public GameObject enemyPrefab;

    [Tooltip("Perde sonu boss'u. Boşsa Enemy Prefab büyütülerek kullanılır.")]
    public GameObject bossPrefab;

    [SerializeField] private PlayerController player;

    // =========================================================
    // DÜŞMAN TİPLERİ
    // =========================================================

    [Header("Düşman Tipleri")]
    [Tooltip("Kapalı: her odada sadece Enemy Prefab (eski davranış).")]
    [SerializeField] private bool useArchetypes = true;

    [Tooltip(
        "ÇEVİK düşman prefab'ı (hızlı seri kombolar). Boşsa Enemy Prefab'ın " +
        "renklendirilmiş, küçültülmüş kopyası.")]
    public GameObject quickPrefab;

    [Tooltip(
        "AĞIR düşman prefab'ı (yavaş, sert, yakalama/süpürme). Boşsa Enemy " +
        "Prefab'ın renklendirilmiş, büyütülmüş kopyası.")]
    public GameObject heavyPrefab;

    [Tooltip("Ayrı prefab yoksa kopyaya uygulanan renk (prefab varsa kullanılmaz).")]
    [SerializeField] private Color quickTint = new Color(0.75f, 1f, 0.8f);

    [SerializeField] private Color heavyTint = new Color(1f, 0.72f, 0.65f);

    [Tooltip(
        "OKÇU düşman prefab'ı (mesafeden ok, parry = yansıt). Boşsa Enemy " +
        "Prefab'ın renklendirilmiş kopyası.")]
    public GameObject archerPrefab;

    [SerializeField] private Color archerTint = new Color(0.78f, 0.85f, 1f);

    [Tooltip("KALABALIK (zayıf, çok sayıda) düşman: Enemy Prefab'ın koyu, küçük kopyası.")]
    [SerializeField] private Color swarmTint = new Color(0.55f, 0.5f, 0.62f);

    [Tooltip(
        "Perde başına tip ağırlıkları. x = Düellocu, y = Çevik, z = Ağır, " +
        "w = Okçu. Perde sayısından kısaysa son satır kullanılır. Koşunun " +
        "ilk odası her zaman Düellocu.")]
    [SerializeField]
    private Vector4[] archetypeWeightsPerAct =
    {
        new Vector4(0.5f, 0.2f, 0.15f, 0.15f),
        new Vector4(0.3f, 0.25f, 0.25f, 0.2f),
        new Vector4(0.25f, 0.25f, 0.25f, 0.25f)
    };

    [Header("Yeni düşman tipleri (40. adım)")]
    [Tooltip("Perde başına: x = Kalkanlı ihtimali, y = Uçan ihtimali (önce bunlar zarlanır).")]
    [SerializeField]
    private Vector2[] specialTypeChancePerAct =
    {
        new Vector2(0.1f, 0.1f),
        new Vector2(0.15f, 0.15f),
        new Vector2(0.2f, 0.2f)
    };

    [Tooltip("Perde başına: kalabalık doğarken PATLAYAN olma ihtimali.")]
    [SerializeField] private float[] bomberSwarmChancePerAct = { 0.15f, 0.25f, 0.35f };

    [SerializeField] private Color shieldedTint = new Color(0.72f, 0.82f, 1f);
    [SerializeField] private Color flyerTint = new Color(0.85f, 1f, 0.75f);
    [SerializeField] private Color bomberTint = new Color(1f, 0.55f, 0.4f);

    [Tooltip("UÇAN düşman doğsun mu (şimdilik KAPALI; kod duruyor).")]
    [SerializeField] private bool enableFlyers = false;

    [Tooltip("PATLAYAN düşman ve 'Patlayıcı' elit eki (şimdilik KAPALI; kod duruyor).")]
    [SerializeField] private bool enableBombers = false;

    [Tooltip("Elit düşmanlara rastgele ek: Hızlı / Zırhlı / Kalkanlı / Patlayıcı.")]
    [SerializeField] private bool eliteAffixes = true;

    // =========================================================
    // RASTGELE HARİTA
    // =========================================================

    [Header("Rastgele Harita (Dead Cells tarzı)")]
    [Tooltip(
        "Açık: her dövüş odası rastgele parçalardan kurulan bir haritada geçer " +
        "(arenaya girince kapılar kapanır, düşmanlar orada doğar, sonunda " +
        "çıkışa yürürsün). Kapalı: eski tek zemin.")]
    [SerializeField] private bool useLevelGenerator = true;

    [Tooltip("0 = her koşu rastgele. Başka sayı = her koşu aynı harita dizisi (test).")]
    [SerializeField] private int fixedSeed = 0;

    [Tooltip("Son arenadan sonra çıkışa yürümek için en uzun süre (sn), sonra otomatik geçilir.")]
    [SerializeField] private float exitWalkTimeout = 60f;

    [Tooltip(
        "Haritadaki nöbetçi düşmanların seni fark etme mesafesi (dünya birimi). " +
        "Bundan uzaktayken yerlerinde beklerler.")]
    [SerializeField] private float postAggroRange = 13f;

    [Tooltip(
        "Düşmanlar ölmeden çıkışa geldiysen, bu kadar sn sonra kalanlar sana gelir " +
        "(takılan / ulaşılamayan düşman bölümü kilitlemesin).")]
    [SerializeField] private float exitPullDelay = 8f;

    // DENGE (35. adım): alan adları bilerek yeni; sahnedeki eski (zor) değerler ezmesin.
    [Header("Harita düşman sayısı")]
    [Tooltip("İlk bölümdeki toplam düşman (haritada; eski dalga ayarlarından bağımsız).")]
    [SerializeField] private int mapEnemiesStart = 4;

    [Tooltip("Her bölümde eklenen düşman.")]
    [SerializeField] private float mapEnemiesPerStage = 0.75f;

    [SerializeField] private int mapEnemiesMax = 12;

    [Tooltip(
        "Bir nöbet noktasındaki düşman sayısı hedefi (aynı anda gelenler). " +
        "Nokta sayısı = toplam / bu.")]
    [SerializeField] private int mapEnemiesPerPost = 2;

    [SerializeField] private int mapMinPosts = 2;
    [SerializeField] private int mapMaxPosts = 6;

    [Header("Harita dengesi")]
    [Tooltip(
        "Haritadaki düşmanların hasar çarpanı PERDEYE göre (1, 2, 3). Normal ve " +
        "engellenemez vuruşa uygulanır.")]
    [SerializeField] private float[] mapDamageByAct = { 0.7f, 0.85f, 1f };

    [Tooltip(
        "Engellenemez vuruşa EK çarpan (prefab'da 75: tek vuruş canın %40'ını " +
        "götürüyordu).")]
    [SerializeField] private float mapUnblockableDamageMultiplier = 0.6f;

    [Tooltip("Engellenemez saldırı ihtimalinin bölümle artışı bu kadarla çarpılır (haritada).")]
    [SerializeField] private float mapUnblockableGrowthMultiplier = 0.5f;

    [Tooltip("Bir nöbet noktasındaki düşmanların HEPSİ ölünce iyileşme (azami canın oranı).")]
    [SerializeField] private float mapHealOnPostCleared = 0.12f;

    [Header("Kalabalık + öldürme kolaylığı")]
    // 37. adım: alan adları yeni (sahnedeki eski 1–3 değeri ezmesin).
    [Tooltip("Her nöbet noktasına ayrıca bu kadar KALABALIK (zayıf) düşman (en az / en çok).")]
    [SerializeField] private int mapSwarmMin = 2;
    [SerializeField] private int mapSwarmMax = 4;

    [Tooltip("Bir noktanın SÜRÜ olma ihtimali: 1 güçlü düşman + 5–6 kalabalık.")]
    [Range(0f, 1f)] [SerializeField] private float mapHordeChance = 0.25f;

    [Tooltip("Ara parçalardaki tek tük devriye noktası sayısı (1–2 kalabalık).")]
    [SerializeField] private int mapPatrolsBase = 1;

    [Tooltip("Her bu kadar bölümde +1 devriye (en çok 4).")]
    [SerializeField] private int mapPatrolGrowthEveryStages = 2;

    [Tooltip("Nöbetçilerin seni fark etme mesafesi (eski Post Aggro Range yerine).")]
    [SerializeField] private float mapAggroRange = 17f;

    [Tooltip("Her bu kadar bölümde nokta başına +1 kalabalık (0 = artmaz).")]
    [SerializeField] private int mapSwarmGrowthEveryStages = 3;

    [Tooltip("Haritadaki düşmanların can çarpanı (çabuk ölsünler).")]
    [SerializeField] private float mapEnemyHealthMultiplier = 0.7f;

    [Tooltip("Sersemleme süresi çarpanı (bitirmeye vakit kalsın).")]
    [SerializeField] private float mapStaggerDurationMultiplier = 1.4f;

    [Tooltip("Elit odada düşman sayısı çarpanı (daha az ama güçlü).")]
    [SerializeField] private float levelEliteCountMultiplier = 0.6f;

    [Tooltip(
        "Açık: dövüş ödülü (charm seçimi) bölüm İÇİNDE değil, çıkış kapısından " +
        "geçince (bölümler arası geçişte) verilir.")]
    [SerializeField] private bool rewardsOnTransition = true;

    public int RunSeed { get; private set; }

    private LevelGenerator level;
    private Transform[] levelSpawnPoints;
    private int levelSpawnCursor;

    private bool LevelActive =>
        useLevelGenerator && level != null && level.IsReady;

    // Odanın ana tipi (banner bunu yazar). Arayüz de okuyabilir.
    public EnemyArchetypeType RoomArchetype { get; private set; }

    private readonly GameObject[] archetypeTemplates = new GameObject[8];

    // Elit eki (oda başına bir tane).
    private int eliteAffixStage = -1;
    private int eliteAffix;
    private Transform variantRoot;

    // =========================================================
    // KOŞU YAPISI
    // =========================================================

    [Header("Koşu Yapısı")]
    [Min(1)]
    [SerializeField] private int acts = 3;

    [Tooltip("Boss'tan önceki oda sayısı (her perdede).")]
    [Min(1)]
    [SerializeField] private int roomsPerAct = 4;

    [Tooltip("Kapı seçiminde sunulan kapı sayısı.")]
    [Range(2, 3)]
    [SerializeField] private int doorCount = 3;

    [Header("Elit")]
    [SerializeField] private float eliteHealthMultiplier = 1.6f;
    [SerializeField] private float eliteDamageMultiplier = 1.2f;
    [SerializeField] private float eliteSpeedMultiplier = 1.1f;
    [SerializeField] private float eliteScale = 1.15f;

    [Header("Boss")]
    [SerializeField]
    private string[] bossNames =
    {
        "Kılıç Ustası",
        "Kızıl Düellocu",
        "Gölge Efendisi"
    };

    [Tooltip("Boss canı = normal düşman canı × bu (+ perde başına ek).")]
    [SerializeField] private float bossHealthMultiplier = 6f;
    [SerializeField] private float bossHealthPerAct = 1.5f;

    [Tooltip("Her execute boss'un MAX canının bu oranını götürür (0.25 = 4 execute).")]
    [Range(0.05f, 1f)]
    [SerializeField] private float bossExecutePercent = 0.25f;

    [SerializeField] private float bossScale = 1.35f;

    [Range(0.1f, 0.9f)]
    [SerializeField] private float bossPhase2At = 0.5f;

    [Tooltip("Boss yenilince iyileşme (max canın yüzdesi).")]
    [Range(0f, 1f)]
    [SerializeField] private float bossHealPercent = 0.5f;

    [Header("Dinlenme")]
    [Range(0f, 1f)]
    [SerializeField] private float restHealPercent = 0.4f;

    [Header("Ekonomi (altın)")]
    [SerializeField] private int goldPerKill = 8;

    [Tooltip("Perde başına öldürme altını artışı (0.25 = +%25).")]
    [SerializeField] private float goldPerKillPerAct = 0.25f;

    [SerializeField] private int executeKillBonus = 4;
    [SerializeField] private float eliteGoldMultiplier = 2.5f;
    [SerializeField] private int bossGoldPerAct = 60;
    [SerializeField] private int perfectRoomGold = 15;
    [SerializeField] private int goldPerParry = 1;

    [Header("Dükkan")]
    [Range(1, 4)]
    [SerializeField] private int shopCharmCount = 3;

    [SerializeField] private int charmPrice = 45;
    [SerializeField] private int upgradePrice = 60;
    [SerializeField] private int healPrice = 40;

    [Range(0f, 1f)]
    [SerializeField] private float shopHealPercent = 0.35f;

    [SerializeField] private int rerollPrice = 15;
    [SerializeField] private int rerollPriceIncrease = 10;
    [SerializeField] private int removePrice = 40;

    [Tooltip("Perde başına fiyat artışı (0.2 = +%20).")]
    [SerializeField] private float pricePerAct = 0.2f;

    // =========================================================
    // DOĞURMA / DÜELLO / ZORLUK (önceki ayarlar)
    // =========================================================

    [Header("Spawn")]
    [Tooltip("Boş bırakırsan düşmanlar oyuncunun iki yanında, zeminde doğar.")]
    [SerializeField] private Transform[] spawnPoints;

    [SerializeField] private float spawnDistanceMin = 8f;
    [SerializeField] private float spawnDistanceMax = 11f;

    [SerializeField] private float spawnHeightOffset = 1.5f;

    [SerializeField] private float aggroRange = 14f;

    [SerializeField] private float spawnRaycastHeight = 3f;

    [Header("Stuck Recovery (takılma kurtarma)")]
    [SerializeField] private float stuckTimeout = 15f;
    [SerializeField] private float stragglerDistance = 16f;
    [SerializeField] private float fallKillDepth = 25f;

    [SerializeField] private float spawnInterval = 0.35f;

    [Header("DÜELLO PROFİLİ")]
    [SerializeField] private bool duelMode = true;

    [Min(1)]
    [SerializeField] private int duelBaseEnemies = 1;

    [Min(0)]
    [SerializeField] private int duelExtraEnemyEveryNStages = 4;

    [Min(1)]
    [SerializeField] private int duelMaxEnemies = 3;

    [Min(1)]
    [SerializeField] private int duelMaxSimultaneous = 2;

    [Range(0f, 1f)]
    [SerializeField] private float duelScalingMultiplier = 0.4f;

    [Range(0f, 1f)]
    [SerializeField] private float duelHealBetweenStagesPercent = 0.15f;

    [Header("Execute Gelişimi")]
    [Min(0f)]
    [SerializeField] private float executeGrowthPerStage = 0.35f;

    [Header("Stage (kalabalık mod)")]
    [SerializeField] private int baseEnemyCount = 2;
    [SerializeField] private float enemiesPerStage = 0.75f;
    [SerializeField] private int maxEnemiesPerStage = 30;
    [SerializeField] private int maxEnemiesPerWave = 6;

    [Min(1f)]
    [SerializeField] private float stageLengthMultiplier = 1.5f;

    [SerializeField] private int wavesPerStage = 2;
    [SerializeField] private int extraWaveEveryNStages = 3;
    [SerializeField] private int maxWavesPerStage = 8;
    [SerializeField] private int nextWaveAliveThreshold = 1;
    [SerializeField] private float timeBetweenWaves = 1.0f;

    [Header("Difficulty per stage")]
    [SerializeField] private float speedBonusPerStage = 0.02f;
    [SerializeField] private float unblockableBonusPerStage = 0.02f;
    [SerializeField] private float unblockableChanceCap = 0.8f;
    [SerializeField] private float maxSpeedBonus = 0.6f;
    [SerializeField] private float healthBonusPerStage = 0.25f;

    [Header("Charms")]
    [SerializeField] private bool offerAtRunStart = true;
    [SerializeField] private int offerChoices = 3;
    [SerializeField] private bool includeDefaultCharms = true;

    [SerializeField]
    private List<CharmDefinition> extraCharms =
        new List<CharmDefinition>();

    [Header("Parry odaklı denge")]
    [SerializeField] private bool disablePlayerHealthRecovery = true;

    [Tooltip("Düşman saldırısı uyarının bu oranından sonra vurarak KESİLEMEZ. " +
             "0 = uyarı başladığı an zırhlı: cevap parry / block / dash. (Eski alan 0.15 idi.)")]
    [Range(0f, 1f)]
    [SerializeField] private float runAttackArmorPoint = 0f;

    [Tooltip("Oyuncunun normal vuruş (kombo + slam) denge hasarı çarpanı.")]
    [SerializeField] private float runPlayerAttackBalanceMultiplier = 0.75f;

    [Tooltip("Düşmanın parry'den aldığı denge hasarı çarpanı.")]
    [SerializeField] private float runParryBalanceMultiplier = 1.3f;

    [SerializeField] private float damageBonusPerStage = 0.06f;
    [SerializeField] private float maxDamageBonus = 1.5f;

    [Range(0f, 1f)]
    [SerializeField] private float healBetweenStagesPercent = 0.10f;

    [Header("Riposte (parry ödülü)")]
    [SerializeField] private float riposteDuration = 2f;
    [SerializeField] private int riposteMaxHits = 3;
    [SerializeField] private float riposteBalanceMultiplier = 2f;
    [SerializeField] private float riposteHealthMultiplier = 1.5f;

    [Range(0f, 1f)]
    [SerializeField] private float riposteCritChanceBonus = 0.5f;

    [SerializeField] private int postureRefundOnParry = 25;

    [Header("Akış (geçişler)")]
    [Tooltip("Oda temizlenince ödül/iyileşme öncesi bekleme (gerçek sn).")]
    [SerializeField] private float clearedPause = 0.35f;
    [Tooltip("Haritadan haritaya geçişte kararma süresi (gerçek sn).")]
    [SerializeField] private float transitionFadeOut = 0.12f;
    [SerializeField] private float transitionFadeIn = 0.25f;
    [Tooltip("Tek kapı varsa (ÇIKIŞ / BOSS) içine girince [W] beklemeden geçilir.")]
    [SerializeField] private bool autoEnterSingleDoor = true;

    [Header("Harita nesneleri + meydan okuma")]
    [SerializeField] private int chestGold = 30;
    [Range(0f, 1f)] [SerializeField] private float chestCharmChance = 0.4f;
    [Range(0f, 1f)] [SerializeField] private float chestHealChance = 0.3f;
    [Range(0f, 1f)] [SerializeField] private float chestHealPercent = 0.15f;
    [SerializeField] private int vaseGoldMin = 2;
    [SerializeField] private int vaseGoldMax = 6;

    [Tooltip("Normal dövüş odasının MEYDAN OKUMA olma ihtimali (süreli temizle → bonus charm).")]
    [Range(0f, 1f)] [SerializeField] private float challengeChance = 0.3f;
    [SerializeField] private float challengeBaseTime = 25f;
    [SerializeField] private float challengeTimePerEnemy = 5f;
    [SerializeField] private int challengeGold = 30;

    [Header("Düello karşılaşmaları (47. adım)")]
    [Tooltip("Açık: her nöbet noktası = 1 ANA düşman (parry testi) + 0-2 kalabalık. Sürü noktası yok, devriye az, harita kısa.")]
    [SerializeField] private bool duelEncounters = true;

    [Tooltip("Perde başına dövüş odasındaki nöbet (düello) sayısı.")]
    [SerializeField] private int[] duelPostsPerAct = { 2, 3, 3 };

    [Tooltip("Elit odasında düello sayısı (her biri elit ana düşman).")]
    [SerializeField] private int duelElitePosts = 2;

    [Tooltip("Bir düelloya eşlik eden kalabalık ihtimali.")]
    [Range(0f, 1f)] [SerializeField] private float duelSwarmChance = 0.6f;

    [Tooltip("Perde başına eşlik eden en fazla kalabalık.")]
    [SerializeField] private int[] duelSwarmMaxPerAct = { 1, 2, 2 };

    [Tooltip("Noktalar arası devriye (1 kalabalık).")]
    [SerializeField] private int duelPatrols = 1;

    [Tooltip("Düellonun ANA düşmanı: can çarpanı.")]
    [SerializeField] private float duelMainHealthMultiplier = 1.4f;

    [Tooltip("Düellonun ANA düşmanı: denge çarpanı (parry ile kırmak daha anlamlı).")]
    [SerializeField] private float duelMainBalanceMultiplier = 1.3f;

    [Tooltip("Düellonun ANA düşmanı: saldırı sonrası bekleme çarpanı (küçük = daha saldırgan).")]
    [SerializeField] private float duelMainRecoveryMultiplier = 0.8f;

    [Tooltip("Düello modunda arenalar arası ara parça (kısa harita).")]
    [SerializeField] private int duelMinFillers = 1;
    [SerializeField] private int duelMaxFillers = 2;

    [Header("Ana menü")]
    [Tooltip("Lobide (ana menü) arkada rastgele bir orman haritası kurulur.")]
    [SerializeField] private bool menuBackgroundLevel = true;

    [Header("Yetenek (Q)")]
    [SerializeField] private bool abilityOfferAtStart = true;

    [Tooltip("TEST: kilitli yetenekler de seçilebilir / dükkanda çıkar.")]
    [SerializeField] private bool testUnlockAllAbilities = false;
    [SerializeField] private int abilityShopPrice = 70;
    [SerializeField] private int abilityUpgradeShopPrice = 80;

    [Header("Öz (kalıcı para birimi, koşu sonu)")]
    [SerializeField] private float essencePerKill = 0.25f;
    [SerializeField] private int essencePerActReached = 20;
    [SerializeField] private int essencePerBoss = 25;
    [SerializeField] private int essenceForVictory = 50;
    [Tooltip("Isı kademesi başına öz çarpanı.")]
    [SerializeField] private float essencePerHeat = 0.2f;

    // =========================================================
    // DURUM (arayüz okur)
    // =========================================================

    // Yetenek seçimi (koşu başı)
    public IReadOnlyList<AbilityType> AbilityOffers => abilityOffers;
    public PlayerAbility Ability { get; private set; }

    // Yetenek bu koşuda kullanılabilir mi (kalıcı kilit ya da test anahtarı).
    public bool IsAbilityAvailable(AbilityType type) =>
        testUnlockAllAbilities || MetaProgress.IsAbilityUnlocked(type);

    // Öz: son koşuda kazanılan (sonuç ekranı / menü)
    public int LastEssence { get; private set; }
    public string LastEssenceBreakdown { get; private set; } = "";

    // Duraklatma menüsü (Esc)
    public bool IsPaused { get; private set; }

    // Ekran kararması (RunUI çizer). 0 = yok, 1 = siyah.
    public float FadeAlpha { get; private set; }

    // Meydan okuma (süreli oda)
    public bool ChallengeActive { get; private set; }
    public float ChallengeTimeLeft => ChallengeActive ? Mathf.Max(0f, challengeEndTime - Time.time) : 0f;
    private float challengeEndTime;

    public bool CanPause =>
        !IsPaused &&
        !pausedByMenu &&
        State == RunState.Fighting;

    public RunState State { get; private set; } = RunState.Lobby;

    public int Act { get; private set; }
    public int Acts => acts;
    public int RoomInAct { get; private set; }
    public int RoomsPerAct => roomsPerAct;
    public RoomType CurrentRoom { get; private set; }

    // Dövüş odası sayacı (zorluk bununla ölçeklenir).
    public int Stage { get; private set; }

    public int AliveEnemies { get; private set; }
    public int Wave { get; private set; }
    public int WaveCount { get; private set; }

    public string BannerText { get; private set; } = "";
    public float BannerUntil { get; private set; }

    // Eski adlar (uyumluluk).
    public string WaveBannerText => BannerText;
    public float WaveBannerUntil => BannerUntil;

    public bool IsStartOffer { get; private set; }
    public bool IsBonusOffer { get; private set; }
    public string OfferTitle { get; private set; } = "";

    public CharmInventory Inventory { get; private set; }
    public RunStats Stats { get; private set; }
    public IReadOnlyList<CharmDefinition> Offers => offers;

    // Ekonomi
    public int Gold { get; private set; }
    public int LastGoldGain { get; private set; }
    public string LastGoldReason { get; private set; } = "";
    public float LastGoldTime { get; private set; } = -99f;

    // Kapılar
    public IReadOnlyList<RoomType> DoorOptions => doorOptions;

    // Dükkan
    public IReadOnlyList<ShopItem> ShopItems => shopItems;
    public int CurrentRerollPrice { get; private set; }
    public int RemovePriceNow => Price(removePrice);

    // Dinlenme
    public bool CanUpgradeAnyCharm => UpgradableCharms().Count > 0;
    public int RestHealPercentDisplay =>
        Mathf.RoundToInt(restHealPercent * HeatHealMultiplier * 100f);

    // Zorluk / meta
    public int Heat { get; private set; }
    public bool IsVictory { get; private set; }
    public IReadOnlyList<string> NewUnlocks => newUnlocks;

    // Elit düşman mı? (arayüz)
    public bool IsEliteEnemy(EnemyController e) => eliteEnemies.Contains(e);

    // =========================================================
    // İÇ
    // =========================================================

    private List<CharmDefinition> offers = new List<CharmDefinition>();
    private int chosenIndex = -1;

    private readonly List<RoomType> doorOptions = new List<RoomType>();
    private int chosenDoor = -1;

    private readonly List<ShopItem> shopItems = new List<ShopItem>();
    private bool leaveShop;

    private int restChoice = -1; // 0 = iyileş, 1 = yükselt

    private bool startRequested;
    private bool restartRequested;
    private float restartRequestTime;
    private Coroutine loopRoutine;

    // Seçim ekranı oyunu bilerek durdurdu mu (PausedWait)?
    private bool pausedByMenu;

    // Zaman yanlışlıkla 0'da kaldıysa kurtarma.
    private float frozenSince = -1f;

    // F2: ekranda koşu durumu (teşhis).
    private bool showRunDebug;

    private int bonusOffers;

    // Bölüm temizlenince biriken, kapıdan geçince verilecek charm seçimleri.
    private struct PendingOffer
    {
        public string title;
        public int choices;
        public bool bonus;
    }

    private readonly List<PendingOffer> pendingOffers = new List<PendingOffer>();

    private readonly List<AbilityType> abilityOffers = new List<AbilityType>();
    private int chosenAbility = -1;

    private int runKills;
    private int runBossKills;
    private int playerBaseMaxHealth;
    private bool canControlBeforePause = true;

    private bool shopUsedThisAct;
    private bool restUsedThisAct;

    private bool roomDamaged;
    private int runExecutes;
    private int runParries;

    private readonly List<string> newUnlocks = new List<string>();

    private readonly List<CharmDefinition> pool =
        new List<CharmDefinition>();

    private readonly List<EnemyController> spawned =
        new List<EnemyController>();

    private readonly HashSet<EnemyController> eliteEnemies =
        new HashSet<EnemyController>();

    private EnemyController currentBoss;

    private Health playerHealth;
    private Vector3 playerStartPosition;

    private GameObject spawnTemplate;
    private GameObject bossTemplate;

    // =========================================================
    // ZORLUK (ISI) ÇARPANLARI
    // =========================================================

    // Isı 1: düşman hasarı +%25
    private float HeatDamageMultiplier => Heat >= 1 ? 1.25f : 1f;

    // Isı 2: iyileşmeler yarı yarıya
    private float HeatHealMultiplier => Heat >= 2 ? 0.5f : 1f;

    // Isı 3: düşman canı +%30
    private float HeatHealthMultiplier => Heat >= 3 ? 1.3f : 1f;

    // Isı 4: elitler ve boss daha güçlü, faz 2 daha erken
    private float HeatEliteMultiplier => Heat >= 4 ? 1.25f : 1f;

    // Isı 5: fiyatlar +%50, altın -%25
    private float HeatPriceMultiplier => Heat >= 5 ? 1.5f : 1f;
    private float HeatGoldMultiplier => Heat >= 5 ? 0.75f : 1f;

    public static string DescribeHeat(int level)
    {
        switch (level)
        {
            case 0: return "Normal";
            case 1: return "Düşman hasarı +%25";
            case 2: return "+ İyileşmeler yarı yarıya";
            case 3: return "+ Düşman canı +%30";
            case 4: return "+ Elit/boss güçlü, faz 2 erken";
            case 5: return "+ Fiyatlar +%50, altın −%25";
            default: return "";
        }
    }

    // =========================================================
    // KURULUM
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        CombatEvents.EnemyKilled += OnEnemyKilled;
        CombatEvents.ParrySucceeded += OnParry;
        CombatEvents.PlayerDamaged += OnPlayerDamaged;
    }

    private void OnDisable()
    {
        CombatEvents.EnemyKilled -= OnEnemyKilled;
        CombatEvents.ParrySucceeded -= OnParry;
        CombatEvents.PlayerDamaged -= OnPlayerDamaged;
    }

    private void Start()
    {
        if (player == null)
            player = FindFirstObjectByType<PlayerController>();

        if (player == null)
        {
            Debug.LogError("RunManager: PlayerController bulunamadı!");
            enabled = false;
            return;
        }

        if (enemyPrefab == null)
        {
            Debug.LogError("RunManager: Enemy Prefab atanmamış!");
            enabled = false;
            return;
        }

        playerHealth = player.GetComponent<Health>();
        playerStartPosition = player.transform.position;

        spawnTemplate = PrepareTemplate(enemyPrefab);

        bossTemplate =
            bossPrefab != null
                ? PrepareTemplate(bossPrefab)
                : spawnTemplate;

        BuildArchetypeTemplates();

        if (useLevelGenerator)
        {
            level = FindFirstObjectByType<LevelGenerator>();

            if (level == null)
                level = gameObject.AddComponent<LevelGenerator>();

            level.Init(player);
        }

        EnemyController misplaced = GetComponentInParent<EnemyController>();

        if (misplaced != null)
        {
            Debug.LogWarning(
                "RunManager: '" + misplaced.name + "' objesinde EnemyController var " +
                "(RunManager'ın kendisi ya da üst objesi). Bu bir düşman değil; " +
                "EnemyController / EnemyArchetype bileşenlerini oradan kaldır.",
                misplaced
            );
        }

        Inventory = new CharmInventory(player.gameObject);

        Stats = GetComponent<RunStats>();

        if (Stats == null)
            Stats = gameObject.AddComponent<RunStats>();

        Stats.Init(player, playerHealth);

        if (disablePlayerHealthRecovery)
            playerHealth.SetRecoveryEnabled(false);

        ParryRiposte riposte = player.GetComponent<ParryRiposte>();

        if (riposte == null)
            riposte = player.gameObject.AddComponent<ParryRiposte>();

        riposte.Configure(
            riposteDuration,
            riposteMaxHits,
            riposteBalanceMultiplier,
            riposteHealthMultiplier,
            riposteCritChanceBonus,
            postureRefundOnParry
        );

        Ability = PlayerAbility.Ensure(player.gameObject);

        PlayerDamage.AttackBalanceMultiplier = runPlayerAttackBalanceMultiplier;

        playerBaseMaxHealth = playerHealth.MaxHealth;

        if (includeDefaultCharms)
            pool.AddRange(CharmCatalog.CreateDefaults());

        pool.AddRange(extraCharms);

        if (GetComponent<RunUI>() == null)
            gameObject.AddComponent<RunUI>();

        loopRoutine = StartCoroutine(RunLoop());
    }

    // =========================================================
    // YEDEK GİRİŞ + BEKÇİ
    // Sonuç ekranında (ölüm / ZAFER) Enter, Space ya da R yeni koşu ister.
    // Ana döngü bir hata yüzünden durmuşsa istek karşılanmaz: 2.5 sn
    // sonra döngü zorla yeniden başlatılır (oyun kilitli kalmaz).
    // =========================================================

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F2))
            showRunDebug = !showRunDebug;

        // Duraklatma menüsü açıkken zaman donuk kalsın (hit-stop geri açmasın).
        if (IsPaused)
        {
            if (Time.timeScale != 0f)
                Time.timeScale = 0f;

            // Koşu bu arada bittiyse (ör. zehir) menüyü kapat.
            if (State != RunState.Fighting && State != RunState.Cleared)
                SetPaused(false);
        }

        GuardFrozenTime();

        if (State != RunState.Dead && State != RunState.Victory)
            return;

        if (
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.R)
        )
        {
            RequestRestart();
        }

        if (restartRequested && Time.unscaledTime - restartRequestTime > 2.5f)
            HardRestart();
    }

    // Menü açık değilken zaman 1 sn'den uzun 0'da kaldıysa (ör. bir
    // hit-stop yanlış "taban zaman" hatırladıysa) oyunu çöz.
    private void GuardFrozenTime()
    {
        bool menuPause =
            pausedByMenu ||
            IsPaused ||
            State == RunState.AbilityOffer ||
            State == RunState.Offer ||
            State == RunState.ChoosingRoom ||
            State == RunState.Shop ||
            State == RunState.Rest;

        if (menuPause || Time.timeScale > 0.001f)
        {
            frozenSince = -1f;
            return;
        }

        if (frozenSince < 0f)
        {
            frozenSince = Time.unscaledTime;
            return;
        }

        if (Time.unscaledTime - frozenSince < 1f)
            return;

        Debug.LogWarning(
            "RunManager: zaman " + State + " durumunda 1 sn'den uzun DONUK kaldı " +
            "(timeScale 0). Düzeltildi."
        );

        HitStop.ClearAll();
        EnemyTime.Clear();

        Time.timeScale = 1f;

        frozenSince = -1f;
    }

    private void OnGUI()
    {
        if (!showRunDebug || player == null)
            return;

        string playerState =
            player.stateMachine != null && player.stateMachine.CurrentState != null
                ? player.stateMachine.CurrentState.GetType().Name
                : "-";

        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(8, 200, 560, 130), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(
            new Rect(14, 204, 550, 126),
            "KOŞU [F2]  durum: " + State +
            "  perde " + Act + " oda " + RoomInAct + "  dövüş " + Stage +
            "\ntimeScale: " + Time.timeScale.ToString("0.00") +
            "  düşman zamanı: " + EnemyTime.Scale.ToString("0.00") +
            "  menü duraklatması: " + pausedByMenu +
            "\ncanlı düşman: " + AliveEnemies + " / doğan: " + spawned.Count +
            "  dalga " + Wave + "/" + WaveCount +
            "\noyuncu: " + playerState +
            "  canControl: " + player.canControl +
            "  inputLocked: " + player.inputLocked +
            "  ölü: " + (playerHealth != null && playerHealth.IsDead) +
            (level != null && level.HasLevel
                ? "\nharita tohum " + level.Seed + ": " + level.LastLayout
                : "")
        );
    }

    private void HardRestart()
    {
        Debug.LogWarning(
            "RunManager: koşu döngüsü yeni koşu isteğine cevap vermedi " +
            "(büyük ihtimalle önceki bir hata). Döngü yeniden başlatılıyor."
        );

        if (loopRoutine != null)
            StopCoroutine(loopRoutine);

        restartRequested = false;

        try
        {
            ResetRun();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        loopRoutine = StartCoroutine(RunLoop());
    }

    // =========================================================
    // ARAYÜZ İÇİN API
    // =========================================================

    public void Choose(int index)
    {
        if (State != RunState.Offer)
            return;

        if (index < 0 || index >= offers.Count)
            return;

        chosenIndex = index;
    }

    public void ChooseDoor(int index)
    {
        if (State != RunState.ChoosingRoom)
            return;

        if (index < 0 || index >= doorOptions.Count)
            return;

        chosenDoor = index;
    }

    public void RequestStart()
    {
        if (State == RunState.Lobby)
            startRequested = true;
    }

    public void ChooseAbility(int index)
    {
        if (State != RunState.AbilityOffer)
            return;

        if (index < 0 || index >= abilityOffers.Count)
            return;

        // Kilitli kart seçilemez.
        if (!IsAbilityAvailable(abilityOffers[index]))
            return;

        chosenAbility = index;
    }

    // ---------------- DURAKLATMA (Esc) ----------------

    public void SetPaused(bool paused)
    {
        if (paused == IsPaused)
            return;

        if (paused)
        {
            if (!CanPause)
                return;

            HitStop.ClearAll();

            canControlBeforePause = player.canControl;
            player.canControl = false;

            IsPaused = true;
            Time.timeScale = 0f;
        }
        else
        {
            IsPaused = false;

            HitStop.ClearAll();
            Time.timeScale = 1f;

            if (State == RunState.Fighting || State == RunState.Cleared)
                player.canControl = canControlBeforePause;
        }
    }

    /// <summary>
    /// Duraklatma menüsünden "ANA MENÜ": koşu biter (istatistik + öz kaydedilir),
    /// döngü lobiye döner.
    /// </summary>
    public void AbandonRun()
    {
        if (
            State == RunState.Lobby ||
            State == RunState.Dead ||
            State == RunState.Victory
        )
        {
            return;
        }

        if (loopRoutine != null)
            StopCoroutine(loopRoutine);

        IsPaused = false;
        pausedByMenu = false;
        Time.timeScale = 1f;

        IsVictory = false;
        Act = Mathf.Clamp(Act, 1, acts);

        FinishRunSafely();

        restartRequested = false;

        try
        {
            ResetRun();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        loopRoutine = StartCoroutine(RunLoop());
    }

    public void ChangeHeat(int delta)
    {
        if (State != RunState.Lobby)
            return;

        MetaProgress.SelectedHeat = MetaProgress.SelectedHeat + delta;
        Heat = MetaProgress.SelectedHeat;
    }

    public void RequestRestart()
    {
        if (State != RunState.Dead && State != RunState.Victory)
            return;

        if (!restartRequested)
            restartRequestTime = Time.unscaledTime;

        restartRequested = true;
    }

    public void QueueBonusOffer()
    {
        bonusOffers++;
    }

    public void ShowBanner(string text, float seconds)
    {
        BannerText = text;
        BannerUntil = Time.unscaledTime + seconds;
    }

    // ---------------- DÜKKAN ----------------

    public bool CanAfford(int price) => Gold >= price;

    public bool BuyShopItem(int index)
    {
        if (State != RunState.Shop)
            return false;

        if (index < 0 || index >= shopItems.Count)
            return false;

        ShopItem item = shopItems[index];

        if (item.sold || Gold < item.price)
            return false;

        if (item.kind == ShopItemKind.Charm)
        {
            if (item.charm == null || !Inventory.CanAdd(item.charm))
                return false;

            Inventory.Add(item.charm);
        }
        else if (item.kind == ShopItemKind.Heal)
        {
            if (playerHealth.CurrentHealth >= playerHealth.MaxHealth)
                return false;

            HealPercent(shopHealPercent);
        }
        else if (item.kind == ShopItemKind.Ability)
        {
            if (Ability == null)
                return false;

            if (item.abilityUpgrade)
            {
                if (!Ability.HasAbility || Ability.Type != item.ability || !Ability.CanLevelUp)
                    return false;

                Ability.LevelUp();
            }
            else
            {
                // Değiştir: seviye korunur.
                int keepLevel = Ability.HasAbility ? Ability.Level : 1;

                Ability.Equip(item.ability, keepLevel);

                // Eski yeteneğin yükseltme kartı artık geçersiz.
                for (int i = 0; i < shopItems.Count; i++)
                {
                    if (shopItems[i].kind == ShopItemKind.Ability && shopItems[i].abilityUpgrade)
                        shopItems[i].sold = true;
                }
            }
        }

        Gold -= item.price;
        item.sold = true;

        // Aynı charm'ın diğer kartlarında fiyat (yeni/yükseltme) güncellensin.
        RefreshShopPrices();

        return true;
    }

    public bool RerollShop()
    {
        if (State != RunState.Shop || Gold < CurrentRerollPrice)
            return false;

        Gold -= CurrentRerollPrice;
        CurrentRerollPrice += rerollPriceIncrease;

        RollShopCharms();

        return true;
    }

    public bool RemoveCharm(CharmDefinition definition)
    {
        if (State != RunState.Shop || Gold < RemovePriceNow)
            return false;

        if (!Inventory.Remove(definition))
            return false;

        Gold -= RemovePriceNow;

        RefreshShopPrices();

        return true;
    }

    public void LeaveShop()
    {
        if (State == RunState.Shop)
            leaveShop = true;
    }

    // ---------------- DİNLENME ----------------

    public void ChooseRest(bool heal)
    {
        if (State != RunState.Rest)
            return;

        if (!heal && !CanUpgradeAnyCharm)
            return;

        restChoice = heal ? 0 : 1;
    }

    // =========================================================
    // ANA DÖNGÜ
    // =========================================================

    private IEnumerator RunLoop()
    {
        while (true)
        {
            // ---------------- LOBİ ----------------

            State = RunState.Lobby;
            Heat = MetaProgress.SelectedHeat;
            startRequested = false;

            player.canControl = false;

            BuildMenuLevel();

            while (!startRequested)
                yield return null;

            player.canControl = true;

            BeginRun();

            if (abilityOfferAtStart)
                yield return AbilityOfferRoutine();

            if (offerAtRunStart)
                yield return OfferRoutine(true, "BAŞLANGIÇ CHARM'INI SEÇ", null, offerChoices);

            bool won = false;

            // ---------------- PERDELER ----------------

            for (Act = 1; Act <= acts && !playerHealth.IsDead; Act++)
            {
                shopUsedThisAct = false;
                restUsedThisAct = false;

                ShowBanner("PERDE " + Act, 1.8f);

                for (
                    RoomInAct = 1;
                    RoomInAct <= roomsPerAct && !playerHealth.IsDead;
                    RoomInAct++
                )
                {
                    RoomType type;

                    // Koşunun ilk odası her zaman dövüş.
                    if (Act == 1 && RoomInAct == 1)
                    {
                        type = RoomType.Fight;
                    }
                    else
                    {
                        yield return ChooseRoomRoutine();
                        type = doorOptions[Mathf.Clamp(chosenDoor, 0, doorOptions.Count - 1)];
                    }

                    // Kapı akışı atlandıysa (harita yok vb.) ödül kaybolmasın.
                    yield return GrantPendingOffers();

                    yield return RunRoom(type);
                }

                if (playerHealth.IsDead)
                    break;

                // ---------------- BOSS ----------------

                RoomInAct = roomsPerAct + 1;

                // Haritada: çıkışta tek kapı, BOSS.
                if (LevelActive && level.HasLevel)
                {
                    yield return PhysicalDoors(new List<RoomType> { RoomType.Boss });

                    yield return GrantPendingOffers();
                }

                if (playerHealth.IsDead)
                    break;

                yield return GrantPendingOffers();

                yield return RunRoom(RoomType.Boss);

                if (playerHealth.IsDead)
                    break;

                if (Act == acts)
                    won = true;
            }

            Act = Mathf.Clamp(Act, 1, acts);

            // ---------------- SONUÇ ----------------

            IsVictory = won && !playerHealth.IsDead;

            Wave = Mathf.Clamp(Wave, 0, WaveCount);

            // İstatistik / meta kaydı bir hata verse bile sonuç ekranı
            // ve yeni koşu çalışsın.
            FinishRunSafely();

            State = IsVictory ? RunState.Victory : RunState.Dead;

            if (IsVictory)
            {
                // Zafer: oyuncu dövüşten çıksın.
                player.canControl = false;
            }

            restartRequested = false;

            yield return new WaitForSecondsRealtime(1.0f);

            while (
                !restartRequested &&
                (IsVictory || playerHealth.IsDead)
            )
            {
                yield return null;
            }

            ResetRun();
        }
    }

    private void FinishRunSafely()
    {
        try
        {
            if (!IsVictory)
                Stats.EndStage(false);

            Stats.EndRun(this, IsVictory);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        try
        {
            RecordMeta();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        try
        {
            GrantEssence();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    // Koşu sonu ÖZ: öldürme + ulaşılan perde + boss + zafer, ısıyla artar.
    private void GrantEssence()
    {
        int actReached = IsVictory ? acts : Mathf.Clamp(Act, 1, acts);

        float kills = runKills * essencePerKill;
        int acts_ = (actReached - 1) * essencePerActReached;
        int bosses = runBossKills * essencePerBoss;
        int win = IsVictory ? essenceForVictory : 0;

        float multiplier =
            (1f + essencePerHeat * Heat) *
            MetaProgress.EssenceMultiplier;

        int total = Mathf.RoundToInt((kills + acts_ + bosses + win) * multiplier);

        LastEssence = total;

        LastEssenceBreakdown =
            "öldürme " + Mathf.RoundToInt(kills) +
            "  •  perde " + acts_ +
            "  •  boss " + bosses +
            (win > 0 ? "  •  zafer " + win : "") +
            (multiplier > 1.001f ? "  •  ×" + multiplier.ToString("0.0#") : "");

        MetaProgress.AddEssence(total);
    }

    // Ana menü arkası: küçük bir orman haritası (demirci başta selam verir).
    private void BuildMenuLevel()
    {
        if (!menuBackgroundLevel || !LevelActive)
            return;

        try
        {
            level.Generate(UnityEngine.Random.Range(1, int.MaxValue), 1, false);

            if (level.HasLevel)
                level.TeleportPlayer(level.PlayerStart);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            level.Clear();
        }
    }

    private void BeginRun()
    {
        Stage = 0;
        Act = 1;
        RoomInAct = 0;
        Gold = 0;
        LastGoldTime = -99f;
        bonusOffers = 0;
        pendingOffers.Clear();
        runExecutes = 0;
        runParries = 0;
        IsVictory = false;
        newUnlocks.Clear();

        Heat = MetaProgress.SelectedHeat;

        // ---------------- KALICI GELİŞİM ----------------

        runKills = 0;
        runBossKills = 0;
        LastEssence = 0;
        LastEssenceBreakdown = "";

        Gold = MetaProgress.StartGold;

        if (playerHealth != null && playerBaseMaxHealth > 0)
            playerHealth.SetMaxHealth(playerBaseMaxHealth + MetaProgress.BonusMaxHealth, true);

        if (Ability != null)
            Ability.Clear();

        RunSeed =
            fixedSeed != 0
                ? fixedSeed
                : UnityEngine.Random.Range(1, int.MaxValue);

        State = RunState.Starting;

        ExecuteMeter.ResetForRun();
        KillStreak.ResetForRun();

        Stats.BeginRun();
    }

    private void RecordMeta()
    {
        int actReached =
            IsVictory ? acts : Mathf.Clamp(Act, 1, acts);

        int heatBefore = MetaProgress.HeatUnlocked;

        List<string> ids =
            MetaProgress.RecordRun(
                actReached,
                IsVictory,
                Heat,
                runExecutes,
                runParries
            );

        newUnlocks.Clear();

        for (int i = 0; i < pool.Count; i++)
        {
            CharmDefinition d = pool[i];

            if (d != null && !string.IsNullOrEmpty(d.unlockId) && ids.Contains(d.unlockId))
                newUnlocks.Add("Yeni charm: " + d.displayName);
        }

        if (MetaProgress.HeatUnlocked > heatBefore)
            newUnlocks.Add("Yeni zorluk: Isı " + MetaProgress.HeatUnlocked);
    }

    // =========================================================
    // KAPI SEÇİMİ
    // =========================================================

    private IEnumerator ChooseRoomRoutine()
    {
        GenerateDoors();

        chosenDoor = -1;

        // Haritada: kapılar çıkış alanında, fiziksel.
        if (LevelActive && level.HasLevel)
        {
            yield return PhysicalDoors(doorOptions);

            // Kapıdan geçildi: bölümler arası geçiş → biriken ödüller.
            yield return GrantPendingOffers();
            yield break;
        }

        State = RunState.ChoosingRoom;

        yield return PausedWait(() => chosenDoor >= 0);
    }

    private void GenerateDoors()
    {
        doorOptions.Clear();

        bool lastBeforeBoss = RoomInAct == roomsPerAct;

        // Aday ağırlıkları.
        List<RoomType> candidates = new List<RoomType>();
        List<float> weights = new List<float>();

        void Add(RoomType t, float w)
        {
            candidates.Add(t);
            weights.Add(w);
        }

        Add(RoomType.Fight, 3f);

        if (Act >= 2 || RoomInAct >= 2)
            Add(RoomType.Elite, 1.6f);

        if (!shopUsedThisAct)
            Add(RoomType.Shop, lastBeforeBoss ? 3f : 1.2f);

        if (!restUsedThisAct && RoomInAct >= 2)
            Add(RoomType.Rest, lastBeforeBoss ? 3f : 1f);

        // Tekrarsız ağırlıklı seçim.
        while (doorOptions.Count < doorCount && candidates.Count > 0)
        {
            float total = 0f;

            for (int i = 0; i < weights.Count; i++)
                total += weights[i];

            float roll = UnityEngine.Random.value * total;
            int pick = candidates.Count - 1;

            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= weights[i];

                if (roll <= 0f)
                {
                    pick = i;
                    break;
                }
            }

            doorOptions.Add(candidates[pick]);
            candidates.RemoveAt(pick);
            weights.RemoveAt(pick);
        }

        // En az bir dövüş kapısı olsun (ilerleme garantisi).
        if (!doorOptions.Contains(RoomType.Fight) && !doorOptions.Contains(RoomType.Elite))
            doorOptions[doorOptions.Count - 1] = RoomType.Fight;

        // Boss'tan önce: dükkan ya da dinlenme şansı garanti (kullanılmadıysa).
        if (
            lastBeforeBoss &&
            !doorOptions.Contains(RoomType.Shop) &&
            !doorOptions.Contains(RoomType.Rest)
        )
        {
            RoomType support =
                !restUsedThisAct ? RoomType.Rest
                : !shopUsedThisAct ? RoomType.Shop
                : RoomType.Fight;

            if (support != RoomType.Fight)
            {
                // Elit varsa onun yerine koy (normal dövüş kapısı kalsın).
                int replace =
                    CountCombatDoors() > 1
                        ? doorOptions.IndexOf(RoomType.Elite)
                        : -1;

                if (replace >= 0)
                    doorOptions[replace] = support;
                else if (doorOptions.Count < 3)
                    doorOptions.Add(support);
            }
        }
    }

    private int CountCombatDoors()
    {
        int n = 0;

        for (int i = 0; i < doorOptions.Count; i++)
        {
            if (doorOptions[i] == RoomType.Fight || doorOptions[i] == RoomType.Elite)
                n++;
        }

        return n;
    }

    // =========================================================
    // ODALAR
    // =========================================================

    private IEnumerator RunRoom(RoomType type)
    {
        // Haritadan yeni haritaya: kısa kararma, ışınlanma görünmesin.
        if (LevelActive && level.HasLevel)
        {
            yield return FadeTo(1f, transitionFadeOut);
            StartCoroutine(FadeInSoon());
        }

        CurrentRoom = type;

        switch (type)
        {
            case RoomType.Shop:
                shopUsedThisAct = true;

                if (LevelActive)
                    yield return TransitionRoom(RoomType.Shop);
                else
                    yield return ShopRoutine();

                break;

            case RoomType.Rest:
                restUsedThisAct = true;

                if (LevelActive)
                    yield return TransitionRoom(RoomType.Rest);
                else
                    yield return RestRoutine();

                break;

            default:
                yield return CombatRoom(type);
                break;
        }
    }

    private IEnumerator CombatRoom(RoomType type)
    {
        Stage++;

        Stats.BeginStage(Stage);

        RaiseStage(StageStarted, Stage);

        roomDamaged = false;

        spawned.Clear();
        eliteEnemies.Clear();
        currentBoss = null;

        bool useLevel = PrepareLevel(type);

        if (type == RoomType.Boss)
        {
            WaveCount = 1;
            Wave = 1;

            State = RunState.Fighting;

            string bossName = BossNameForAct();

            if (useLevel)
            {
                ShowBanner("BOSS ARENASI  →", 1.6f);

                yield return EnterArena(0);
            }

            ShowBanner(bossName.ToUpperInvariant(), 2f);

            SpawnBoss(bossName);

            yield return WaitUntilCleared(0);

            if (useLevel)
            {
                ReleaseArena(0);
                level.ClearDoors();
            }
        }
        else if (useLevel)
        {
            // Haritada: düşmanlar noktalarında bekler, hepsi ölünce çıkış açılır.
            yield return LevelCombat(type);
        }
        else
        {
            RoomArchetype = PickArchetype(true);

            WaveCount = WavesForStage();

            for (Wave = 1; Wave <= WaveCount; Wave++)
            {
                State = RunState.Fighting;

                if (Wave == 1)
                {
                    string typeName =
                        useArchetypes
                            ? "  •  " + EnemyArchetype.NameOf(RoomArchetype).ToUpperInvariant()
                            : "";

                    ShowBanner(
                        type == RoomType.Elite
                            ? "ELİT" + typeName
                            : "PERDE " + Act + "  •  ODA " + RoomInAct + typeName,
                        1.4f
                    );
                }
                else
                {
                    ShowBanner("DALGA " + Wave + " / " + WaveCount, 1.2f);
                }

                // Haritada: dalga = arena. Oyuncu arenaya girince kapanır.
                if (useLevel)
                    yield return EnterArena(Wave - 1);

                if (playerHealth.IsDead)
                    yield break;

                int spawnedBefore = spawned.Count;

                yield return SpawnWave(
                    EnemiesForWave(Wave),
                    type == RoomType.Elite
                );

                if (spawned.Count == spawnedBefore && !playerHealth.IsDead)
                {
                    Debug.LogError(
                        "RunManager: düşman doğurulamadı! " +
                        "'Enemy Prefab' alanını kontrol et."
                    );

                    yield break;
                }

                int target =
                    Wave < WaveCount && !duelMode
                        ? Mathf.Max(0, nextWaveAliveThreshold)
                        : 0;

                yield return WaitUntilCleared(target);

                if (useLevel)
                    ReleaseArena(Wave - 1);

                if (playerHealth.IsDead)
                    yield break;

                if (Wave < WaveCount && !useLevel)
                    yield return new WaitForSeconds(timeBetweenWaves);
            }
        }

        if (playerHealth.IsDead)
            yield break;

        // Haritada: çıkış kapıları (oda seçimi) ödüllerden sonra çıkışta açılır.

        // ---------------- TEMİZLENDİ ----------------

        State = RunState.Cleared;

        Stats.EndStage(true);

        RaiseStage(StageCleared, Stage);

        if (!roomDamaged)
            AddGold(perfectRoomGold, "Hasarsız oda");

        yield return new WaitForSecondsRealtime(clearedPause);

        float heal =
            type == RoomType.Boss
                ? bossHealPercent
                : (duelMode ? duelHealBetweenStagesPercent : healBetweenStagesPercent);

        HealPercent(heal);

        // ---------------- ÖDÜL ----------------
        // Haritada: ödüller biriktirilir, çıkış kapısından geçince verilir
        // (bölüm sırasında menü açılmaz).

        if (type == RoomType.Elite)
        {
            QueueOffer("ELİT ÖDÜLÜ  (1/2)", offerChoices, false);
            QueueOffer("ELİT ÖDÜLÜ  (2/2)", offerChoices, false);
        }
        else if (type == RoomType.Boss)
        {
            if (Act < acts)
                QueueOffer("BOSS ÖDÜLÜ", offerChoices + 1, false);
        }
        else
        {
            QueueOffer("BİR CHARM SEÇ", offerChoices, false);
        }

        while (bonusOffers > 0)
        {
            bonusOffers--;

            QueueOffer("KUSURSUZ ODA: BONUS CHARM", offerChoices, true);
        }

        bool deferRewards = rewardsOnTransition && useLevel;

        if (!deferRewards)
            yield return GrantPendingOffers();
        else if (pendingOffers.Count > 0)
            ShowBanner("ÖDÜL ÇIKIŞTA  →  " + pendingOffers.Count + " CHARM", 2f);

        yield return new WaitForSecondsRealtime(0.3f);
    }

    // =========================================================
    // RASTGELE HARİTA AKIŞI
    // =========================================================

    // Odanın haritasını kurar ve oyuncuyu başlangıca ışınlar.
    private bool PrepareLevel(RoomType type)
    {
        levelSpawnPoints = null;
        levelSpawnCursor = 0;

        if (!LevelActive)
            return false;

        bool boss = type == RoomType.Boss;

        int arenaCount = boss ? 1 : LevelPostCount(type);

        // Düello modu: arenalar arası kısa yol.
        if (duelEncounters && level != null)
        {
            level.minFillers = Mathf.Max(0, duelMinFillers);
            level.maxFillers = Mathf.Max(level.minFillers, duelMaxFillers);
        }

        try
        {
            level.Generate(RunSeed + Stage * 7919, arenaCount, boss);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            level.Clear();
            return false;
        }

        if (!level.HasLevel)
            return false;

        level.TeleportPlayer(level.PlayerStart);

        // Düşmanlar ölmeden çıkış kilitli.
        level.SetExitDoors(
            new[] { "ÇIKIŞ" },
            new[] { new Color(0.9f, 0.85f, 0.7f) },
            true
        );

        level.SetLockedText("KİLİTLİ");

        return true;
    }

    // Haritada dövüş odası: her arena bir NÖBET noktası. Düşmanlar orada
    // bekler (yakına gelince saldırır). Hepsi ölünce çıkış açılır.
    private IEnumerator LevelCombat(RoomType type)
    {
        RoomArchetype = PickArchetype(true);

        EnemyArchetypeType roomType = RoomArchetype;

        WaveCount = Mathf.Max(1, level.ArenaCount);

        State = RunState.Fighting;

        string typeName =
            useArchetypes
                ? "  •  " + EnemyArchetype.NameOf(roomType).ToUpperInvariant()
                : "";

        ShowBanner(
            type == RoomType.Elite
                ? "ELİT" + typeName
                : "PERDE " + Act + "  •  ODA " + RoomInAct + typeName,
            1.6f
        );

        postGroups.Clear();

        int totalEnemies = LevelEnemyTotal(type);
        int posts = Mathf.Max(1, level.ArenaCount);

        for (int i = 0; i < level.ArenaCount; i++)
        {
            Wave = i + 1;

            // İlk nöbet odanın tipi; sonrakiler karışık.
            RoomArchetype = i == 0 ? roomType : PickArchetype(false);

            levelSpawnPoints = Shuffled(level.ArenaSpawnPoints(i));
            levelSpawnCursor = 0;

            int before = spawned.Count;

            // Toplamı noktalara eşit dağıt (ilk noktalar +1 alır).
            int count = totalEnemies / posts + (i < totalEnemies % posts ? 1 : 0);

            // SÜRÜ noktası: 1 güçlü düşman + bol kalabalık (ilk nokta hariç).
            bool horde =
                !duelEncounters &&
                useArchetypes && i > 0 && UnityEngine.Random.value < mapHordeChance;

            // Düello: tek ANA düşman.
            if (horde || duelEncounters)
                count = 1;

            yield return SpawnWave(Mathf.Max(1, count), type == RoomType.Elite, 0f);

            // Düello: ana düşman daha dayanıklı ve saldırgan.
            if (duelEncounters)
            {
                for (int k = before; k < spawned.Count; k++)
                    BoostDuelMain(spawned[k]);
            }

            // Kalabalık: noktayı doldurur (zayıf, çabuk ölür, infaz barını doldurur).
            if (useArchetypes)
            {
                int extra =
                    mapSwarmGrowthEveryStages > 0
                        ? (Stage - 1) / mapSwarmGrowthEveryStages
                        : 0;

                int swarm =
                    horde
                        ? UnityEngine.Random.Range(5, 7) + extra
                        : UnityEngine.Random.Range(
                            Mathf.Min(mapSwarmMin, mapSwarmMax),
                            Mathf.Max(mapSwarmMin, mapSwarmMax) + 1
                        ) + extra;

                // Düello: eşlik eden az kalabalık (ana düşmanı gölgelemesin).
                if (duelEncounters)
                {
                    int max = Mathf.Max(0, ActValue(duelSwarmMaxPerAct, 1));

                    swarm =
                        max > 0 && UnityEngine.Random.value < duelSwarmChance
                            ? UnityEngine.Random.Range(1, max + 1)
                            : 0;
                }

                if (swarm > 0)
                    yield return SpawnWave(swarm, false, 0f, EnemyArchetypeType.Swarm);
            }

            // Nöbet noktası grubu (hepsi ölünce iyileşme).
            List<EnemyController> group = new List<EnemyController>();

            for (int k = before; k < spawned.Count; k++)
                group.Add(spawned[k]);

            postGroups.Add(group);

            for (int k = before; k < spawned.Count; k++)
                MakeGuard(spawned[k]);

            if (playerHealth.IsDead)
                yield break;
        }

        // DEVRİYELER: noktalar arasındaki yürüme boş geçmesin.
        if (useArchetypes)
        {
            int patrols =
                Mathf.Min(
                    4,
                    mapPatrolsBase +
                    (mapPatrolGrowthEveryStages > 0 ? (Stage - 1) / mapPatrolGrowthEveryStages : 0)
                );

            if (duelEncounters)
                patrols = Mathf.Max(0, duelPatrols);

            Transform[] patrolPoints = level.CreatePatrolPoints(patrols);

            for (int p = 0; p < patrolPoints.Length; p++)
            {
                levelSpawnPoints = new[] { patrolPoints[p] };
                levelSpawnCursor = 0;

                int before = spawned.Count;

                yield return SpawnWave(duelEncounters ? 1 : UnityEngine.Random.Range(1, 3), false, 0f, EnemyArchetypeType.Swarm);

                List<EnemyController> group = new List<EnemyController>();

                for (int k = before; k < spawned.Count; k++)
                {
                    group.Add(spawned[k]);
                    MakeGuard(spawned[k]);
                }

                postGroups.Add(group);
            }
        }

        RoomArchetype = roomType;
        levelSpawnPoints = null;

        if (spawned.Count == 0)
        {
            Debug.LogError("RunManager: haritaya düşman doğurulamadı! 'Enemy Prefab' alanını kontrol et.");
            level.ClearDoors();
            yield break;
        }

        // MEYDAN OKUMA: süre içinde hepsini temizle → bonus charm + altın.
        bool challenge =
            type == RoomType.Fight &&
            !(Act == 1 && RoomInAct == 1) &&
            UnityEngine.Random.value < challengeChance;

        if (challenge)
        {
            float limit = Mathf.Round(challengeBaseTime + challengeTimePerEnemy * spawned.Count);

            challengeEndTime = Time.time + limit;
            ChallengeActive = true;

            ShowBanner("MEYDAN OKUMA  •  " + Mathf.RoundToInt(limit) + " SN İÇİNDE TEMİZLE", 2.4f);
        }

        yield return WaitLevelCleared();

        if (challenge)
        {
            bool success = Time.time <= challengeEndTime && !playerHealth.IsDead;

            ChallengeActive = false;

            if (success)
            {
                QueueOffer("MEYDAN OKUMA ÖDÜLÜ", offerChoices, true);
                AddGold(challengeGold, "Meydan okuma");

                CombatCallout.Popup(player.transform.position + Vector3.up * 2.4f, "MEYDAN OKUMA BAŞARILI!", new Color(1f, 0.85f, 0.3f), 1.1f);
            }
            else if (!playerHealth.IsDead)
            {
                CombatCallout.Popup(player.transform.position + Vector3.up * 2.4f, "SÜRE DOLDU", new Color(0.75f, 0.75f, 0.8f), 0.9f);
            }
        }

        level.ClearDoors();
    }

    private void MakeGuard(EnemyController enemy)
    {
        if (enemy == null)
            return;

        enemy.alwaysHunt = false;
        enemy.chaseRange = mapAggroRange;
    }

    // Haritadaki nöbet noktası grupları (LevelCombat doldurur).
    private readonly List<List<EnemyController>> postGroups = new List<List<EnemyController>>();

    private static bool GroupDead(List<EnemyController> group)
    {
        for (int i = 0; i < group.Count; i++)
        {
            EnemyController e = group[i];

            if (e == null || e.IsDead || !e.gameObject.activeInHierarchy)
                continue;

            Health h = e.GetComponent<Health>();

            if (h != null && (h.IsDead || h.CurrentHealth <= 0))
                continue;

            return false;
        }

        return true;
    }

    // Temizlenen nöbet noktası → iyileş.
    private void CheckPostsCleared()
    {
        for (int i = postGroups.Count - 1; i >= 0; i--)
        {
            List<EnemyController> group = postGroups[i];

            if (group.Count == 0 || !GroupDead(group))
                continue;

            postGroups.RemoveAt(i);

            float postHeal = mapHealOnPostCleared + MetaProgress.BonusPostHeal;

            if (postHeal > 0f && playerHealth != null && !playerHealth.IsDead)
            {
                HealPercent(postHeal);

                CombatCallout.Popup(
                    player.transform.position + Vector3.up * 2.2f,
                    "+" + Mathf.RoundToInt(postHeal * HeatHealMultiplier * 100f) + "% CAN",
                    new Color(0.5f, 1f, 0.55f),
                    1f
                );
            }
        }
    }

    private IEnumerator WaitLevelCleared()
    {
        float nearExitSince = -1f;
        float nextInfo = 0f;
        int lastShown = -1;

        while (!playerHealth.IsDead)
        {
            RefreshAlive();

            CheckPostsCleared();

            if (AliveEnemies <= 0)
                break;

            if (AliveEnemies != lastShown)
            {
                lastShown = AliveEnemies;
                level.SetLockedText("KİLİTLİ  •  " + AliveEnemies + " düşman");
            }

            if (level.NearExit(player.transform.position))
            {
                if (nearExitSince < 0f)
                    nearExitSince = Time.time;

                // Kilitli kapıda [W]: kalanları HEMEN getir + kim kaldığını yaz.
                if (InteractPressed())
                {
                    Debug.Log(
                        "RunManager: çıkış kilitli, kalan " + AliveEnemies + " düşman:\n" +
                        DescribeAliveEnemies()
                    );

                    PullRemainingEnemies();
                    nearExitSince = Time.time;
                }

                if (Time.time >= nextInfo)
                {
                    ShowBanner("KALAN DÜŞMAN: " + AliveEnemies, 1.4f);
                    nextInfo = Time.time + 3f;
                }

                if (exitPullDelay > 0f && Time.time - nearExitSince > exitPullDelay)
                {
                    PullRemainingEnemies();
                    nearExitSince = Time.time;
                }
            }
            else
            {
                nearExitSince = -1f;
            }

            yield return null;
        }
    }

    // Çıkışta bekleyen oyuncuya kalan (uzaktaki) düşmanları getir.
    private void PullRemainingEnemies()
    {
        int pulled = 0;

        for (int i = 0; i < spawned.Count; i++)
        {
            EnemyController e = spawned[i];

            if (e == null || e.IsDead)
                continue;

            e.alwaysHunt = true;

            if (Vector2.Distance(e.transform.position, player.transform.position) > postAggroRange)
            {
                e.transform.position = RecoverPosition(-1);

                Rigidbody2D rb = e.GetComponent<Rigidbody2D>();

                if (rb != null)
                    rb.linearVelocity = Vector2.zero;

                pulled++;
            }
        }

        if (pulled > 0)
            ShowBanner("KALANLAR GELİYOR!", 1.4f);
    }

    // =========================================================
    // FİZİKSEL KAPILAR / GEÇİŞ ALANLARI
    // =========================================================

    private static bool InteractPressed()
    {
        // Duraklatma menüsü açıkken kapı / tezgah seçilmesin.
        if (Instance != null && Instance.IsPaused)
            return false;

        return
            Input.GetKeyDown(KeyCode.W) ||
            Input.GetKeyDown(KeyCode.UpArrow) ||
            Input.GetKeyDown(KeyCode.F);
    }

    private static string DoorLabel(RoomType type)
    {
        switch (type)
        {
            case RoomType.Elite: return "ELİT";
            case RoomType.Shop: return "DÜKKAN";
            case RoomType.Rest: return "DİNLENME";
            case RoomType.Boss: return "BOSS";
            default: return "DÖVÜŞ";
        }
    }

    private static Color DoorColor(RoomType type)
    {
        switch (type)
        {
            case RoomType.Elite: return new Color(0.75f, 0.45f, 1f);
            case RoomType.Shop: return new Color(1f, 0.82f, 0.3f);
            case RoomType.Rest: return new Color(0.45f, 0.95f, 0.55f);
            case RoomType.Boss: return new Color(1f, 0.3f, 0.25f);
            default: return new Color(1f, 0.55f, 0.45f);
        }
    }

    // Çıkış alanında her seçenek için bir kapı; içine girip [W] ile seç.
    private IEnumerator PhysicalDoors(List<RoomType> options)
    {
        chosenDoor = -1;

        State = RunState.Fighting;

        string[] labels = new string[options.Count];
        Color[] colors = new Color[options.Count];

        for (int i = 0; i < options.Count; i++)
        {
            labels[i] = DoorLabel(options[i]);
            colors[i] = DoorColor(options[i]);
        }

        level.SetExitDoors(labels, colors, false);

        ShowBanner(options.Count > 1 ? "ÇIKIŞTA BİR KAPI SEÇ  →" : "ÇIKIŞA İLERLE  →", 2f);

        while (chosenDoor < 0 && !playerHealth.IsDead)
        {
            int door = level.DoorAt(player.transform.position);

            // Tek kapı: içine girmek yeter (akış). Birden fazla: [W] ile seç.
            if (door >= 0 && (InteractPressed() || (autoEnterSingleDoor && options.Count == 1 && !IsPaused)))
                chosenDoor = door;

            yield return null;
        }

        level.ClearDoors();
    }

    // Dükkan / dinlenme: küçük bir geçiş alanı. Tezgaha / ateşe gelip [W]
    // ile açılır (bir kez). Çıkışta sonraki odanın kapıları çıkar.
    private IEnumerator TransitionRoom(RoomType type)
    {
        bool shop = type == RoomType.Shop;

        bool built = false;

        try
        {
            level.Generate(
                RunSeed + Stage * 7919 + Act * 977 + RoomInAct * 31 + (shop ? 101 : 202),
                0,
                false,
                shop ? ChunkKind.Shop : ChunkKind.Rest
            );

            built = level.HasLevel;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        if (!built)
        {
            yield return shop ? ShopRoutine() : RestRoutine();
            yield break;
        }

        level.TeleportPlayer(level.PlayerStart);
        level.CreateStand(shop ? "DÜKKAN" : "DİNLENME", DoorColor(type));

        State = RunState.Fighting;

        ShowBanner(shop ? "DÜKKAN" : "DİNLENME ALANI", 1.6f);

        bool used = false;

        while (!playerHealth.IsDead)
        {
            if (!used && level.PlayerAtStand(player.transform.position) && InteractPressed())
            {
                used = true;
                level.SetStandUsed();

                yield return shop ? ShopRoutine() : RestRoutine();

                State = RunState.Fighting;
                ShowBanner("ÇIKIŞA İLERLE  →", 1.6f);
            }

            if (level.NearExit(player.transform.position))
                break;

            yield return null;
        }
    }

    // Oyuncu arenaya girene kadar bekler, sonra kapıları kapatır ve
    // düşmanların arenada doğmasını sağlar.
    private IEnumerator EnterArena(int index)
    {
        State = RunState.Fighting;

        if (index > 0 || !level.PlayerInArena(index, player.transform.position))
            ShowBanner("İLERLE  →", 1.4f);

        while (
            !playerHealth.IsDead &&
            !level.PlayerInArena(index, player.transform.position)
        )
        {
            yield return null;
        }

        if (playerHealth.IsDead)
            yield break;

        level.SetArenaLocked(index, true);

        levelSpawnPoints = level.ArenaSpawnPoints(index);
        levelSpawnCursor = 0;
    }

    private void ReleaseArena(int index)
    {
        if (level != null)
            level.SetArenaLocked(index, false);

        levelSpawnPoints = null;
    }

    private IEnumerator WalkToExit()
    {
        ShowBanner("ÇIKIŞA İLERLE  →", 2f);

        float start = Time.time;

        while (
            !playerHealth.IsDead &&
            !level.PlayerAtExit(player.transform.position) &&
            Time.time - start < exitWalkTimeout
        )
        {
            yield return null;
        }
    }

    private IEnumerator WaitUntilCleared(int target)
    {
        int lastAlive = AliveEnemies;
        float lastProgressTime = Time.time;

        // Üst üste ilerlemesiz kurtarma sayısı. 2'yi geçerse oda zorla
        // temizlenir: oyun hiçbir zaman kilitli kalmaz.
        int stuckStrikes = 0;

        while (!playerHealth.IsDead)
        {
            RefreshAlive();

            if (AliveEnemies <= target)
                break;

            if (AliveEnemies != lastAlive)
            {
                lastAlive = AliveEnemies;
                lastProgressTime = Time.time;
                stuckStrikes = 0;
            }
            else if (
                stuckTimeout > 0f &&
                Time.time - lastProgressTime > stuckTimeout
            )
            {
                stuckStrikes++;

                if (stuckStrikes >= 3)
                {
                    ForceClearRoom();
                    stuckStrikes = 0;
                }
                else
                {
                    RecoverStragglers();
                }

                lastProgressTime = Time.time;
            }

            yield return null;
        }
    }

    private string BossNameForAct()
    {
        if (bossNames == null || bossNames.Length == 0)
            return "Boss";

        return bossNames[Mathf.Clamp(Act - 1, 0, bossNames.Length - 1)];
    }

    // ---------------- DÜKKAN ----------------

    private IEnumerator ShopRoutine()
    {
        CurrentRerollPrice = Price(rerollPrice);

        shopItems.Clear();

        RollShopCharms();

        shopItems.Add(
            new ShopItem
            {
                kind = ShopItemKind.Heal,
                price = Price(healPrice)
            }
        );

        AddAbilityShopItems();

        leaveShop = false;

        State = RunState.Shop;

        yield return PausedWait(() => leaveShop);
    }

    // Yetenek kartları: mevcut yeteneği yükselt + (açık başka yetenek varsa)
    // değiştir. Yeteneği olmayan oyuncuya satın alma kartı.
    private void AddAbilityShopItems()
    {
        if (Ability == null)
            return;

        if (Ability.HasAbility && Ability.CanLevelUp)
        {
            shopItems.Add(
                new ShopItem
                {
                    kind = ShopItemKind.Ability,
                    ability = Ability.Type,
                    abilityUpgrade = true,
                    price = Price(abilityUpgradeShopPrice)
                }
            );
        }

        List<AbilityType> others = new List<AbilityType>();

        for (int i = 0; i < AbilityInfo.All.Length; i++)
        {
            AbilityType t = AbilityInfo.All[i];

            if (!IsAbilityAvailable(t))
                continue;

            if (Ability.HasAbility && t == Ability.Type)
                continue;

            others.Add(t);
        }

        if (others.Count == 0)
            return;

        shopItems.Add(
            new ShopItem
            {
                kind = ShopItemKind.Ability,
                ability = others[UnityEngine.Random.Range(0, others.Count)],
                abilityUpgrade = false,
                price = Price(abilityShopPrice)
            }
        );
    }

    private void RollShopCharms()
    {
        // Var olan charm kartlarını çıkar (iyileşme kalır).
        shopItems.RemoveAll(i => i.kind == ShopItemKind.Charm);

        List<CharmDefinition> rolled =
            CharmCatalog.Roll(pool, Inventory, shopCharmCount);

        for (int i = 0; i < rolled.Count; i++)
        {
            shopItems.Insert(
                i,
                new ShopItem
                {
                    kind = ShopItemKind.Charm,
                    charm = rolled[i]
                }
            );
        }

        RefreshShopPrices();
    }

    private void RefreshShopPrices()
    {
        for (int i = 0; i < shopItems.Count; i++)
        {
            ShopItem item = shopItems[i];

            if (item.kind != ShopItemKind.Charm || item.charm == null)
                continue;

            item.price =
                Inventory.GetStacks(item.charm) > 0
                    ? Price(upgradePrice)
                    : Price(charmPrice);
        }
    }

    private int Price(int basePrice)
    {
        return Mathf.Max(
            1,
            Mathf.RoundToInt(
                basePrice *
                (1f + pricePerAct * (Mathf.Max(1, Act) - 1)) *
                HeatPriceMultiplier *
                MetaProgress.ShopPriceMultiplier
            )
        );
    }

    // ---------------- DİNLENME ----------------

    private IEnumerator RestRoutine()
    {
        restChoice = -1;

        State = RunState.Rest;

        yield return PausedWait(() => restChoice >= 0);

        if (restChoice == 0)
        {
            HealPercent(restHealPercent);
            yield break;
        }

        List<CharmDefinition> upgradable = UpgradableCharms();

        if (upgradable.Count > 0)
        {
            yield return OfferRoutine(
                false,
                "BİR CHARM'I GÜÇLENDİR",
                upgradable,
                upgradable.Count
            );
        }
    }

    private List<CharmDefinition> UpgradableCharms()
    {
        List<CharmDefinition> list = new List<CharmDefinition>();

        if (Inventory == null)
            return list;

        IReadOnlyList<CharmInventory.Entry> entries = Inventory.Entries;

        for (int i = 0; i < entries.Count; i++)
        {
            if (Inventory.CanAdd(entries[i].definition))
                list.Add(entries[i].definition);
        }

        return list;
    }

    // ---------------- YETENEK SEÇİMİ (koşu başı) ----------------

    private IEnumerator AbilityOfferRoutine()
    {
        if (Ability == null)
            yield break;

        // Ekranda TÜM yetenekler görünür; kilitliler gri (Demirci'den açılır).
        abilityOffers.Clear();
        abilityOffers.AddRange(AbilityInfo.All);

        int available = 0;

        for (int i = 0; i < abilityOffers.Count; i++)
        {
            if (IsAbilityAvailable(abilityOffers[i]))
                available++;
        }

        if (available == 0)
            yield break;

        chosenAbility = -1;

        State = RunState.AbilityOffer;

        yield return PausedWait(() => chosenAbility >= 0);

        AbilityType picked = abilityOffers[Mathf.Clamp(chosenAbility, 0, abilityOffers.Count - 1)];

        MetaProgress.SelectedAbility = (int)picked;

        Ability.Equip(picked);

        State = RunState.Starting;
    }

    // ---------------- CHARM TEKLİFİ ----------------

    private IEnumerator OfferRoutine(
        bool isStartOffer,
        string title,
        List<CharmDefinition> custom,
        int choices,
        bool isBonus = false
    )
    {
        List<CharmDefinition> rolled =
            custom ?? CharmCatalog.Roll(pool, Inventory, choices);

        if (rolled.Count == 0)
            yield break;

        offers = rolled;
        chosenIndex = -1;
        IsStartOffer = isStartOffer;
        IsBonusOffer = isBonus;
        OfferTitle = title;

        State = RunState.Offer;

        yield return PausedWait(() => chosenIndex >= 0);

        CharmDefinition picked = offers[chosenIndex];

        offers = new List<CharmDefinition>();

        Inventory.Add(picked);
    }

    private void QueueOffer(string title, int choices, bool bonus)
    {
        pendingOffers.Add(new PendingOffer { title = title, choices = choices, bonus = bonus });
    }

    // Biriken charm seçimlerini sırayla gösterir (bölümler arası geçiş).
    private IEnumerator GrantPendingOffers()
    {
        while (pendingOffers.Count > 0 && !playerHealth.IsDead)
        {
            PendingOffer o = pendingOffers[0];
            pendingOffers.RemoveAt(0);

            yield return OfferRoutine(false, o.title, null, o.choices, o.bonus);
        }
    }

    // Oyunu durdurup bir karar bekler (seçim ekranları).
    private IEnumerator PausedWait(Func<bool> done)
    {
        HitStop.ClearAll();
        EnemyTime.Clear();

        player.canControl = false;

        pausedByMenu = true;

        Time.timeScale = 0f;

        while (!done())
        {
            if (Time.timeScale != 0f)
                Time.timeScale = 0f;

            yield return null;
        }

        // Duraklama sırasında gelmiş hit-stop istekleri "taban zaman = 0"
        // hatırlamasın: temizle, sonra zamanı aç.
        HitStop.ClearAll();

        Time.timeScale = 1f;

        pausedByMenu = false;

        // Seçim tıklamasının saldırı tamponu sönsün, sonra kontrolü ver.
        PlayerCombatController combat = player.GetComponent<PlayerCombatController>();

        if (combat != null)
            combat.CancelAttack();

        yield return new WaitForSecondsRealtime(0.1f);

        player.canControl = true;
    }

    // ---------------- HARİTA NESNELERİ (LevelProps) ----------------

    public void OnVaseBroken(Vector3 at)
    {
        int gold = UnityEngine.Random.Range(Mathf.Min(vaseGoldMin, vaseGoldMax), Mathf.Max(vaseGoldMin, vaseGoldMax) + 1);

        AddGold(gold, "Vazo");
    }

    public void OnChestOpened(Vector3 at)
    {
        AddGold(Mathf.RoundToInt(chestGold * (1f + 0.25f * (Mathf.Max(1, Act) - 1))), "Sandık");

        float roll = UnityEngine.Random.value;

        if (roll < chestCharmChance)
        {
            QueueOffer("SANDIK: CHARM", offerChoices, true);
            CombatCallout.Popup(at + Vector3.up, "CHARM  (çıkışta)", new Color(0.75f, 0.6f, 1f), 1f);
        }
        else if (roll < chestCharmChance + chestHealChance)
        {
            HealPercent(chestHealPercent);
            CombatCallout.Popup(at + Vector3.up, "+CAN", new Color(0.5f, 1f, 0.55f), 1f);
        }
        else
        {
            CombatCallout.Popup(at + Vector3.up, "ALTIN!", new Color(1f, 0.85f, 0.3f), 1f);
        }
    }

    // ---------------- KARARMA ----------------

    private IEnumerator FadeTo(float target, float duration)
    {
        float from = FadeAlpha;
        float start = Time.unscaledTime;

        if (duration <= 0f)
        {
            FadeAlpha = target;
            yield break;
        }

        while (Time.unscaledTime - start < duration)
        {
            FadeAlpha = Mathf.Lerp(from, target, (Time.unscaledTime - start) / duration);
            yield return null;
        }

        FadeAlpha = target;
    }

    // Yeni harita aynı karede kurulur; iki kare sonra açıl.
    private IEnumerator FadeInSoon()
    {
        yield return null;
        yield return null;
        yield return FadeTo(0f, transitionFadeIn);
    }

    // =========================================================
    // EKONOMİ
    // =========================================================

    private void AddGold(int amount, string reason)
    {
        amount = Mathf.RoundToInt(amount * HeatGoldMultiplier);

        if (amount <= 0)
            return;

        Gold += amount;

        LastGoldGain = amount;
        LastGoldReason = reason;
        LastGoldTime = Time.unscaledTime;

        if (Stats != null)
            Stats.AddGold(amount);
    }

    private void OnEnemyKilled(EnemyController enemy)
    {
        if (enemy == null || !spawned.Contains(enemy))
            return;

        bool executed = enemy.CurrentState is EnemyExecuteState;

        if (executed)
            runExecutes++;

        runKills++;

        if (enemy == currentBoss)
        {
            runBossKills++;

            AddGold(bossGoldPerAct * Act, "Boss");
            return;
        }

        float amount =
            goldPerKill * (1f + goldPerKillPerAct * (Act - 1));

        if (executed)
            amount += executeKillBonus;

        string reason = executed ? "Execute" : "Öldürme";

        if (eliteEnemies.Contains(enemy))
        {
            amount *= eliteGoldMultiplier;
            reason = "Elit";
        }

        AddGold(Mathf.RoundToInt(amount), reason);
    }

    private void OnParry(EnemyController enemy, bool brokeBalance)
    {
        if (State != RunState.Fighting)
            return;

        runParries++;

        if (goldPerParry > 0)
            AddGold(goldPerParry, "Parry");
    }

    private void OnPlayerDamaged(PlayerDamageReport report)
    {
        if (report.kind == PlayerHitKind.BlockCost)
            return;

        roomDamaged = true;
    }

    private void HealPercent(float percent)
    {
        percent *= HeatHealMultiplier;

        if (percent <= 0f || playerHealth == null)
            return;

        int heal =
            Mathf.Max(1, Mathf.RoundToInt(playerHealth.MaxHealth * percent));

        playerHealth.Heal(heal);
    }

    private static void RaiseStage(Action<int> handlers, int stage)
    {
        if (handlers == null)
            return;

        Delegate[] list = handlers.GetInvocationList();

        for (int i = 0; i < list.Length; i++)
        {
            try
            {
                ((Action<int>)list[i])(stage);
            }
            catch (Exception e)
            {
                Debug.LogError("RunManager: bölüm olayı abone hatası → " + e);
            }
        }
    }

    // =========================================================
    // DOĞURMA
    // =========================================================

    // HARİTA: bölümdeki toplam düşman.
    private int LevelEnemyTotal(RoomType type)
    {
        float count =
            mapEnemiesStart + Mathf.Max(0, Stage - 1) * mapEnemiesPerStage;

        if (type == RoomType.Elite)
            count *= levelEliteCountMultiplier;

        return Mathf.Clamp(Mathf.RoundToInt(count), 1, Mathf.Max(1, mapEnemiesMax));
    }

    // HARİTA: nöbet noktası (arena) sayısı.
    private int LevelPostCount(RoomType type)
    {
        if (duelEncounters)
        {
            if (type == RoomType.Elite)
                return Mathf.Max(1, duelElitePosts);

            return Mathf.Max(1, ActValue(duelPostsPerAct, 2));
        }

        int total = LevelEnemyTotal(type);

        int posts =
            Mathf.CeilToInt(total / (float)Mathf.Max(1, mapEnemiesPerPost));

        return Mathf.Clamp(posts, Mathf.Max(1, mapMinPosts), Mathf.Max(mapMinPosts, mapMaxPosts));
    }

    private void BoostDuelMain(EnemyController enemy)
    {
        if (enemy == null)
            return;

        Health h = enemy.GetComponent<Health>();

        if (h != null && !Mathf.Approximately(duelMainHealthMultiplier, 1f))
            h.SetMaxHealth(Mathf.Max(1, Mathf.RoundToInt(h.MaxHealth * duelMainHealthMultiplier)), true);

        EnemyBalance b = enemy.GetComponent<EnemyBalance>();

        if (b != null && !Mathf.Approximately(duelMainBalanceMultiplier, 1f))
            b.SetMaxBalance(Mathf.Max(1, Mathf.RoundToInt(b.MaxBalance * duelMainBalanceMultiplier)));

        enemy.attackRecoveryTime *= Mathf.Max(0.2f, duelMainRecoveryMultiplier);
    }

    private int ActValue(int[] values, int fallback)
    {
        if (values == null || values.Length == 0)
            return fallback;

        return values[Mathf.Clamp(Act - 1, 0, values.Length - 1)];
    }

    private static Transform[] Shuffled(Transform[] points)
    {
        if (points == null)
            return null;

        Transform[] copy = (Transform[])points.Clone();

        for (int i = copy.Length - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            Transform t = copy[i];
            copy[i] = copy[j];
            copy[j] = t;
        }

        return copy;
    }

    private int WavesForStage()
    {
        if (duelMode)
        {
            return Mathf.Max(
                1,
                Mathf.CeilToInt(
                    TotalEnemiesForStage() /
                    (float)Mathf.Max(1, duelMaxSimultaneous)
                )
            );
        }

        int extra =
            extraWaveEveryNStages > 0
                ? (Stage - 1) / extraWaveEveryNStages
                : 0;

        return Mathf.Clamp(
            wavesPerStage + extra,
            1,
            Mathf.Max(1, maxWavesPerStage)
        );
    }

    private int TotalEnemiesForStage()
    {
        if (duelMode)
        {
            int extra =
                duelExtraEnemyEveryNStages > 0
                    ? (Stage - 1) / duelExtraEnemyEveryNStages
                    : 0;

            return Mathf.Clamp(
                duelBaseEnemies + extra,
                1,
                Mathf.Max(1, duelMaxEnemies)
            );
        }

        float baseCount =
            baseEnemyCount +
            Mathf.FloorToInt((Stage - 1) * enemiesPerStage);

        int total =
            Mathf.RoundToInt(baseCount * stageLengthMultiplier);

        return Mathf.Clamp(
            total,
            1,
            Mathf.Max(1, maxEnemiesPerStage)
        );
    }

    private int EnemiesForWave(int wave)
    {
        int total = TotalEnemiesForStage();

        int waves = Mathf.Max(1, WaveCount);

        int count = total / waves + (wave <= total % waves ? 1 : 0);

        int cap =
            duelMode
                ? duelMaxSimultaneous
                : maxEnemiesPerWave;

        return Mathf.Clamp(count, 1, Mathf.Max(1, cap));
    }

    private IEnumerator SpawnWave(int count, bool elite, float interval = -1f, EnemyArchetypeType? forceType = null)
    {
        float wait = interval < 0f ? spawnInterval : interval;

        int firstSide = UnityEngine.Random.value < 0.5f ? -1 : 1;

        for (int i = 0; i < count; i++)
        {
            if (playerHealth.IsDead)
                yield break;

            int side = (i % 2 == 0) ? firstSide : -firstSide;

            // İlk düşman odanın tipi; sonrakiler (2+ düşmanlı odada) karışık.
            EnemyArchetypeType archetype =
                forceType.HasValue
                    ? forceType.Value
                    : (i == 0 ? RoomArchetype : PickArchetype(false));

            // Kalabalığın bir kısmı PATLAYAN.
            if (
                useArchetypes &&
                enableBombers &&
                archetype == EnemyArchetypeType.Swarm &&
                bomberSwarmChancePerAct != null &&
                bomberSwarmChancePerAct.Length > 0 &&
                UnityEngine.Random.value < bomberSwarmChancePerAct[Mathf.Clamp(Act - 1, 0, bomberSwarmChancePerAct.Length - 1)]
            )
            {
                archetype = EnemyArchetypeType.Bomber;
            }

            EnemyController enemy = SpawnOne(side, TemplateFor(archetype));

            if (enemy != null)
            {
                // Tip çarpanları ÖNCE (taban değerler), zorluk ölçeği SONRA.
                EnemyArchetype archetypeComponent =
                    enemy.GetComponent<EnemyArchetype>();

                if (archetypeComponent != null)
                    archetypeComponent.Apply();

                ConfigureEnemy(enemy);

                if (elite)
                    MakeElite(enemy);

                spawned.Add(enemy);
            }

            RefreshAlive();

            if (wait > 0f)
                yield return new WaitForSeconds(wait);
        }
    }

    private void SpawnBoss(string bossName)
    {
        int side = UnityEngine.Random.value < 0.5f ? -1 : 1;

        EnemyController boss = SpawnOne(side, bossTemplate);

        if (boss == null)
            return;

        ConfigureEnemy(boss);

        Health health = boss.GetComponent<Health>();

        if (health != null)
        {
            float mult =
                (bossHealthMultiplier + bossHealthPerAct * (Act - 1)) *
                HeatEliteMultiplier;

            health.SetMaxHealth(
                Mathf.RoundToInt(health.MaxHealth * mult),
                true
            );

            // Boss'u bir execute bitirmez: her biri canın bir parçasını alır.
            boss.executeDamage =
                Mathf.Max(
                    1,
                    Mathf.RoundToInt(health.MaxHealth * bossExecutePercent)
                );
        }

        boss.attackDamage =
            Mathf.Max(1, Mathf.RoundToInt(boss.attackDamage * HeatEliteMultiplier));

        boss.transform.localScale *= bossScale;

        BossController controller = boss.GetComponent<BossController>();

        if (controller == null)
            controller = boss.gameObject.AddComponent<BossController>();

        float phaseAt =
            Heat >= 4
                ? Mathf.Min(0.7f, bossPhase2At + 0.15f)
                : bossPhase2At;

        controller.Setup(bossName, phaseAt, Act);

        currentBoss = boss;

        spawned.Add(boss);

        RefreshAlive();
    }

    private void MakeElite(EnemyController enemy)
    {
        eliteEnemies.Add(enemy);

        float m = HeatEliteMultiplier;

        Health health = enemy.GetComponent<Health>();

        if (health != null)
        {
            health.SetMaxHealth(
                Mathf.RoundToInt(health.MaxHealth * eliteHealthMultiplier * m),
                true
            );
        }

        enemy.attackDamage =
            Mathf.Max(1, Mathf.RoundToInt(enemy.attackDamage * eliteDamageMultiplier * m));

        enemy.chaseSpeed *= eliteSpeedMultiplier;

        enemy.transform.localScale *= eliteScale;

        if (eliteAffixes)
            ApplyEliteAffix(enemy);
    }

    private static readonly string[] EliteAffixNames = { "HIZLI", "ZIRHLI", "KALKANLI", "PATLAYICI" };

    // Odadaki elitler aynı eki alır; ilk elitte banner'da duyurulur.
    private void ApplyEliteAffix(EnemyController enemy)
    {
        if (eliteAffixStage != Stage)
        {
            eliteAffixStage = Stage;
            // Patlayıcı (3) sadece Patlayan açıkken.
            eliteAffix = UnityEngine.Random.Range(0, enableBombers ? EliteAffixNames.Length : EliteAffixNames.Length - 1);

            ShowBanner("ELİT  •  " + EliteAffixNames[eliteAffix], 2f);
        }

        switch (eliteAffix)
        {
            case 0: // HIZLI: hızlı koşar, kısa uyarı.
                enemy.chaseSpeed *= 1.3f;
                enemy.attackWarningTime *= 0.8f;
                enemy.attackRecoveryTime *= 0.8f;
                break;

            case 1: // ZIRHLI: denge çok, az savrulur.
                EnemyBalance balance = enemy.GetComponent<EnemyBalance>();

                if (balance != null)
                    balance.SetMaxBalance(Mathf.RoundToInt(balance.MaxBalance * 1.6f));

                enemy.balanceHitKnockbackForce *= 0.4f;
                enemy.healthKnockbackForce *= 0.4f;
                break;

            case 2: // KALKANLI
                if (enemy.GetComponent<EnemyShield>() == null)
                    enemy.gameObject.AddComponent<EnemyShield>();
                break;

            case 3: // PATLAYICI: ölünce patlar (oyuncuya da vurur).
                EnemyBomber bomber = enemy.GetComponent<EnemyBomber>();

                if (bomber == null)
                    bomber = enemy.gameObject.AddComponent<EnemyBomber>();

                bomber.onlyOnDeath = true;
                bomber.radius = 3f;
                break;
        }
    }

    private EnemyController SpawnOne(int side, GameObject template)
    {
        Vector3 playerPosition = player.transform.position;

        Vector3 position;

        if (levelSpawnPoints != null && levelSpawnPoints.Length > 0)
        {
            // Arena noktaları sırayla (iki düşman üst üste doğmasın).
            Transform point =
                levelSpawnPoints[levelSpawnCursor % levelSpawnPoints.Length];

            // Noktalar bittiyse aynı noktanın yanına (üst üste binmesin).
            int lap = levelSpawnCursor / levelSpawnPoints.Length;

            levelSpawnCursor++;

            position = point.position;

            if (lap > 0)
            {
                float dir = lap % 2 == 1 ? 1f : -1f;

                position += new Vector3(dir * 1.6f * ((lap + 1) / 2), 0.5f, 0f);
            }
        }
        else if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform point =
                spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];

            position = point.position;
        }
        else
        {
            float distance =
                UnityEngine.Random.Range(spawnDistanceMin, spawnDistanceMax);

            float x = playerPosition.x + side * distance;
            float y = playerPosition.y;

            RaycastHit2D hit =
                Physics2D.Raycast(
                    new Vector2(x, playerPosition.y + spawnRaycastHeight),
                    Vector2.down,
                    30f,
                    player.Movement.groundMask
                );

            if (hit.collider != null)
                y = hit.point.y + spawnHeightOffset;

            position = new Vector3(x, y, playerPosition.z);
        }

        if (template == null)
        {
            Debug.LogError(
                "RunManager: düşman şablonu yok edilmiş! 'Enemy Prefab' " +
                "alanına Project penceresinden PREFAB ata (sahne nesnesi değil)."
            );

            return null;
        }

        GameObject obj =
            Instantiate(template, position, Quaternion.identity);

        if (!obj.activeSelf)
            obj.SetActive(true);

        return obj.GetComponent<EnemyController>();
    }

    // Bölüm zorluğu + doğan düşman oyuncuyu hemen fark etsin.
    private void ConfigureEnemy(EnemyController enemy)
    {
        float t =
            (Stage - 1) *
            (duelMode ? duelScalingMultiplier : 1f);

        enemy.chaseRange = aggroRange;
        enemy.alwaysHunt = true;

        enemy.attackCommitPoint =
            Mathf.Min(enemy.attackCommitPoint, runAttackArmorPoint);

        enemy.parryBalanceDamage =
            Mathf.Max(1, Mathf.RoundToInt(enemy.parryBalanceDamage * runParryBalanceMultiplier));

        float damageMultiplier =
            (1f + Mathf.Min(maxDamageBonus, damageBonusPerStage * t)) *
            HeatDamageMultiplier;

        enemy.attackDamage =
            Mathf.Max(
                1,
                Mathf.RoundToInt(enemy.attackDamage * damageMultiplier)
            );

        enemy.unblockableDamage =
            Mathf.Max(
                1,
                Mathf.RoundToInt(enemy.unblockableDamage * HeatDamageMultiplier)
            );

        enemy.chaseSpeed *=
            1f + Mathf.Min(maxSpeedBonus, speedBonusPerStage * t);

        bool onMap = LevelActive && level != null && level.HasLevel;

        enemy.unblockableChance =
            Mathf.Min(
                unblockableChanceCap,
                enemy.unblockableChance +
                unblockableBonusPerStage * t * (onMap ? mapUnblockableGrowthMultiplier : 1f)
            );

        // Harita dengesi: perdeye göre hasar, engellenemez vuruş ayrıca hafif.
        if (onMap)
        {
            float actMul =
                mapDamageByAct != null && mapDamageByAct.Length > 0
                    ? mapDamageByAct[Mathf.Clamp(Act - 1, 0, mapDamageByAct.Length - 1)]
                    : 1f;

            enemy.attackDamage =
                Mathf.Max(1, Mathf.RoundToInt(enemy.attackDamage * actMul));

            enemy.unblockableDamage =
                Mathf.Max(
                    1,
                    Mathf.RoundToInt(enemy.unblockableDamage * actMul * mapUnblockableDamageMultiplier)
                );

            enemy.staggerDuration *= Mathf.Max(0.1f, mapStaggerDurationMultiplier);
        }

        Health health = enemy.GetComponent<Health>();

        if (health != null)
        {
            int scaled =
                Mathf.RoundToInt(
                    health.MaxHealth *
                    (1f + healthBonusPerStage * t) *
                    HeatHealthMultiplier
                );

            if (onMap && !Mathf.Approximately(mapEnemyHealthMultiplier, 1f))
                scaled = Mathf.Max(1, Mathf.RoundToInt(scaled * mapEnemyHealthMultiplier));

            health.SetMaxHealth(scaled, true);
        }

        if (executeGrowthPerStage > 0f)
        {
            enemy.executeDamage =
                Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        enemy.executeDamage *
                        (1f + executeGrowthPerStage * (Stage - 1)) *
                        HeatHealthMultiplier
                    )
                );
        }
    }

    // =========================================================
    // TAKILMA KURTARMA
    // =========================================================

    private void RecoverStragglers()
    {
        Vector3 playerPosition = player.transform.position;

        int moved = 0;
        int removed = 0;

        for (int i = 0; i < spawned.Count; i++)
        {
            EnemyController e = spawned[i];

            if (e == null || e.IsDead)
                continue;

            Vector3 ep = e.transform.position;

            if (ep.y < playerPosition.y - fallKillDepth)
            {
                Destroy(e.gameObject);
                removed++;
                continue;
            }

            float distance = Vector2.Distance(ep, playerPosition);

            if (
                distance > stragglerDistance ||
                e.CurrentState is EnemyIdleState
            )
            {
                int side = (i % 2 == 0) ? -1 : 1;

                e.transform.position = RecoverPosition(side);

                Rigidbody2D rb = e.GetComponent<Rigidbody2D>();

                if (rb != null)
                    rb.linearVelocity = Vector2.zero;

                e.alwaysHunt = true;

                moved++;
            }
        }

        Debug.LogWarning(
            "RunManager: " + stuckTimeout.ToString("0") +
            " sn'dir ilerleme yok → kurtarma. Taşınan: " + moved +
            ", silinen: " + removed +
            "\nHâlâ canlı sayılan düşmanlar:\n" + DescribeAliveEnemies()
        );
    }

    // Takılmanın teşhisi: canlı sayılan her düşmanın adı, durumu, uzaklığı,
    // görünür / aktif olup olmadığı.
    private string DescribeAliveEnemies()
    {
        Vector3 playerPosition = player.transform.position;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        for (int i = 0; i < spawned.Count; i++)
        {
            EnemyController e = spawned[i];

            if (e == null || e.IsDead)
                continue;

            Health h = e.GetComponent<Health>();

            sb.Append("  - ")
              .Append(e.name)
              .Append(" | durum: ")
              .Append(e.CurrentState != null ? e.CurrentState.GetType().Name : "yok")
              .Append(" | uzaklık: ")
              .Append(Vector2.Distance(e.transform.position, playerPosition).ToString("0.0"))
              .Append(" | aktif: ")
              .Append(e.gameObject.activeInHierarchy && e.enabled)
              .Append(" | görünür: ")
              .Append(IsVisible(e))
              .Append(" | can: ")
              .Append(h != null ? h.CurrentHealth + "/" + h.MaxHealth : "?")
              .Append(" | konum: ")
              .Append(e.transform.position.ToString("0.0"))
              .Append('\n');
        }

        return sb.Length > 0 ? sb.ToString() : "  (yok)";
    }

    private static bool IsVisible(EnemyController e)
    {
        SpriteRenderer[] renderers = e.GetComponentsInChildren<SpriteRenderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];

            if (sr.enabled && sr.sprite != null && sr.color.a > 0.05f && sr.isVisible)
                return true;
        }

        return false;
    }

    // Son çare: kurtarma 2 kez işe yaramadıysa kalan düşmanları kaldır,
    // oda temizlenmiş sayılır. (Teşhis için kim olduklarını yazar.)
    private void ForceClearRoom()
    {
        Debug.LogWarning(
            "RunManager: oda ilerlemiyor → ZORLA TEMİZLENDİ. Kaldırılan düşmanlar:\n" +
            DescribeAliveEnemies()
        );

        for (int i = 0; i < spawned.Count; i++)
        {
            EnemyController e = spawned[i];

            if (e != null && !e.IsDead)
                Destroy(e.gameObject);
        }

        spawned.Clear();

        RefreshAlive();
    }

    private Vector3 RecoverPosition(int side)
    {
        Vector3 playerPosition = player.transform.position;

        float x =
            playerPosition.x +
            side * UnityEngine.Random.Range(6f, 9f);

        float y = playerPosition.y;

        RaycastHit2D hit =
            Physics2D.Raycast(
                new Vector2(x, playerPosition.y + 1.5f),
                Vector2.down,
                10f,
                player.Movement.groundMask
            );

        if (hit.collider != null)
            y = hit.point.y + spawnHeightOffset;

        return new Vector3(x, y, playerPosition.z);
    }

    // =========================================================
    // DÜŞMAN TİPLERİ
    // =========================================================

    private void BuildArchetypeTemplates()
    {
        archetypeTemplates[(int)EnemyArchetypeType.Duelist] = spawnTemplate;

        archetypeTemplates[(int)EnemyArchetypeType.Quick] =
            MakeVariantTemplate(
                quickPrefab != null ? quickPrefab : enemyPrefab,
                EnemyArchetypeType.Quick,
                quickPrefab == null,
                quickTint
            );

        archetypeTemplates[(int)EnemyArchetypeType.Heavy] =
            MakeVariantTemplate(
                heavyPrefab != null ? heavyPrefab : enemyPrefab,
                EnemyArchetypeType.Heavy,
                heavyPrefab == null,
                heavyTint
            );

        archetypeTemplates[(int)EnemyArchetypeType.Archer] =
            MakeVariantTemplate(
                archerPrefab != null ? archerPrefab : enemyPrefab,
                EnemyArchetypeType.Archer,
                archerPrefab == null,
                archerTint
            );

        archetypeTemplates[(int)EnemyArchetypeType.Swarm] =
            MakeVariantTemplate(
                enemyPrefab,
                EnemyArchetypeType.Swarm,
                true,
                swarmTint
            );

        archetypeTemplates[(int)EnemyArchetypeType.Shielded] =
            MakeVariantTemplate(enemyPrefab, EnemyArchetypeType.Shielded, true, shieldedTint);

        archetypeTemplates[(int)EnemyArchetypeType.Flyer] =
            MakeVariantTemplate(enemyPrefab, EnemyArchetypeType.Flyer, true, flyerTint);

        archetypeTemplates[(int)EnemyArchetypeType.Bomber] =
            MakeVariantTemplate(enemyPrefab, EnemyArchetypeType.Bomber, true, bomberTint);
    }

    // Kapalı (inaktif) bir kök altında kopya: Awake/OnEnable çalışmaz,
    // sahnede görünmez. Doğan her düşman bu kopyadan üretilir ve rengi
    // EnemyController.Awake'te "orijinal renk" olarak alınır (flaş bozmaz).
    private GameObject MakeVariantTemplate(
        GameObject source,
        EnemyArchetypeType type,
        bool tint,
        Color tintColor
    )
    {
        if (source == null)
            return spawnTemplate;

        if (variantRoot == null)
        {
            GameObject root = new GameObject("Düşman Tipi Şablonları");

            root.SetActive(false);
            root.transform.SetParent(transform, false);

            variantRoot = root.transform;
        }

        GameObject template = Instantiate(source, variantRoot);

        template.name =
            source.name + " (" + EnemyArchetype.NameOf(type) + " şablon)";

        if (!template.activeSelf)
            template.SetActive(true);   // kök kapalı: yine görünmez

        EnemyArchetype archetype = template.GetComponent<EnemyArchetype>();

        if (archetype == null)
            archetype = template.AddComponent<EnemyArchetype>();

        // Ayrı prefab'da tip zaten seçiliyse ona dokunma; yoksa ata.
        if (tint || archetype.type == EnemyArchetypeType.Duelist)
            archetype.type = type;

        // Kendi sprite'lı prefab: boyutu prefab belirler (tip büyütmesin).
        archetype.applyScale = tint;

        if (tint)
        {
            SpriteRenderer[] renderers =
                template.GetComponentsInChildren<SpriteRenderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                Color c = renderers[i].color;

                renderers[i].color =
                    new Color(
                        c.r * tintColor.r,
                        c.g * tintColor.g,
                        c.b * tintColor.b,
                        c.a
                    );
            }
        }

        return template;
    }

    private GameObject TemplateFor(EnemyArchetypeType type)
    {
        if (!useArchetypes)
            return spawnTemplate;

        GameObject template = archetypeTemplates[(int)type];

        return template != null ? template : spawnTemplate;
    }

    // Perdeye göre ağırlıklı tip. Koşunun ilk odası her zaman Düellocu.
    private EnemyArchetypeType PickArchetype(bool forRoom)
    {
        if (!useArchetypes)
            return EnemyArchetypeType.Duelist;

        if (forRoom && Stage <= 1)
            return EnemyArchetypeType.Duelist;

        // Yeni tipler (Kalkanlı / Uçan) önce zarlanır.
        if (specialTypeChancePerAct != null && specialTypeChancePerAct.Length > 0)
        {
            Vector2 sp = specialTypeChancePerAct[Mathf.Clamp(Act - 1, 0, specialTypeChancePerAct.Length - 1)];

            float r = UnityEngine.Random.value;

            if (r < sp.x)
                return EnemyArchetypeType.Shielded;

            if (enableFlyers && r < sp.x + sp.y)
                return EnemyArchetypeType.Flyer;
        }

        if (archetypeWeightsPerAct == null || archetypeWeightsPerAct.Length == 0)
            return EnemyArchetypeType.Duelist;

        int index =
            Mathf.Clamp(Act - 1, 0, archetypeWeightsPerAct.Length - 1);

        Vector4 w = archetypeWeightsPerAct[index];

        float d = Mathf.Max(0f, w.x);
        float q = Mathf.Max(0f, w.y);
        float h = Mathf.Max(0f, w.z);
        float a = Mathf.Max(0f, w.w);

        float total = d + q + h + a;

        if (total <= 0f)
            return EnemyArchetypeType.Duelist;

        float roll = UnityEngine.Random.value * total;

        if (roll < d)
            return EnemyArchetypeType.Duelist;

        if (roll < d + q)
            return EnemyArchetypeType.Quick;

        if (roll < d + q + h)
            return EnemyArchetypeType.Heavy;

        return EnemyArchetypeType.Archer;
    }

    private GameObject PrepareTemplate(GameObject source)
    {
        if (!source.scene.IsValid())
            return source;

        Debug.LogWarning(
            "RunManager: '" + source.name + "' SAHNEDEKİ bir nesne. Gizli " +
            "şablon kopyası oluşturuluyor. Doğrusu: Project penceresinden prefab."
        );

        GameObject template =
            Instantiate(source, transform);

        template.name = source.name + " (Şablon)";

        template.SetActive(false);

        return template;
    }

    private void RefreshAlive()
    {
        int alive = 0;

        for (int i = 0; i < spawned.Count; i++)
        {
            EnemyController e = spawned[i];

            if (e == null || e.IsDead || !e.gameObject.activeInHierarchy)
                continue;

            // Canı bitmiş ama ölüm state'ine geçememiş düşman da ölü sayılır.
            Health h = e.GetComponent<Health>();

            if (h != null && (h.IsDead || h.CurrentHealth <= 0))
                continue;

            alive++;
        }

        AliveEnemies = Mathf.Max(alive, 0);
    }

    // =========================================================
    // YENİDEN BAŞLAT
    // =========================================================

    private void ResetRun()
    {
        for (int i = EnemyController.All.Count - 1; i >= 0; i--)
        {
            EnemyController e = EnemyController.All[i];

            if (e == null)
                continue;

            // GÜVENLİK: RunManager'ın kendisini ya da üst objesini (ör.
            // 'Managers') ASLA silme. Yanlışlıkla oraya EnemyController
            // eklenmişse koşu yöneticisi de silinip oyun kilitleniyordu.
            if (transform.IsChildOf(e.transform))
            {
                Debug.LogWarning(
                    "RunManager: '" + e.name + "' üzerinde EnemyController var ama " +
                    "RunManager'ı içeriyor; SİLİNMEDİ. Bu objeden EnemyController / " +
                    "EnemyArchetype gibi düşman bileşenlerini kaldır.",
                    e
                );

                continue;
            }

            Destroy(e.gameObject);
        }

        spawned.Clear();
        eliteEnemies.Clear();
        currentBoss = null;
        shopItems.Clear();
        doorOptions.Clear();

        bonusOffers = 0;
        pendingOffers.Clear();
        AliveEnemies = 0;
        Wave = 0;
        WaveCount = 0;
        BannerUntil = 0f;
        Gold = 0;

        Inventory.Clear();

        if (Ability != null)
            Ability.Clear();

        IsPaused = false;
        FadeAlpha = 0f;
        ChallengeActive = false;

        HitStop.ClearAll();
        EnemyTime.Clear();

        Time.timeScale = 1f;

        playerHealth.Revive();

        PlayerPosture posture = player.GetComponent<PlayerPosture>();

        if (posture != null)
            posture.ResetPosture();

        player.hitInvincibilityTimer = 0f;

        if (level != null)
            level.Clear();

        levelSpawnPoints = null;

        player.transform.position = playerStartPosition;

        if (player.rb != null)
            player.rb.linearVelocity = Vector2.zero;

        player.canControl = true;
        player.inputLocked = false;
        player.isInvincible = false;
        player.isAttackLocked = false;

        pausedByMenu = false;

        player.stateMachine.ChangeState(
            new GroundedState(player, player.stateMachine)
        );

        // Ölüm durumundan çıkışta fizik / saldırı geri açılsın (yedek).
        if (player.rb != null)
            player.rb.simulated = true;

        PlayerCombatController combat = player.GetComponent<PlayerCombatController>();

        if (combat != null)
            combat.enabled = true;

        Time.timeScale = 1f;
    }
}