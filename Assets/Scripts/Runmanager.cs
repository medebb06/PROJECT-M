using NUnit.Framework.Interfaces;
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

    [Range(0f, 1f)]
    [SerializeField] private float runAttackCommitPoint = 0.15f;

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

    // =========================================================
    // DURUM (arayüz okur)
    // =========================================================

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

    private int bonusOffers;

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

        if (includeDefaultCharms)
            pool.AddRange(CharmCatalog.CreateDefaults());

        pool.AddRange(extraCharms);

        if (GetComponent<RunUI>() == null)
            gameObject.AddComponent<RunUI>();

        StartCoroutine(RunLoop());
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

    public void ChangeHeat(int delta)
    {
        if (State != RunState.Lobby)
            return;

        MetaProgress.SelectedHeat = MetaProgress.SelectedHeat + delta;
        Heat = MetaProgress.SelectedHeat;
    }

    public void RequestRestart()
    {
        if (State == RunState.Dead || State == RunState.Victory)
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

            while (!startRequested)
                yield return null;

            player.canControl = true;

            BeginRun();

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

                    yield return RunRoom(type);
                }

                if (playerHealth.IsDead)
                    break;

                // ---------------- BOSS ----------------

                RoomInAct = roomsPerAct + 1;

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

            if (!IsVictory)
                Stats.EndStage(false);

            Stats.EndRun(this, IsVictory);

            RecordMeta();

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

    private void BeginRun()
    {
        Stage = 0;
        Act = 1;
        RoomInAct = 0;
        Gold = 0;
        LastGoldTime = -99f;
        bonusOffers = 0;
        runExecutes = 0;
        runParries = 0;
        IsVictory = false;
        newUnlocks.Clear();

        Heat = MetaProgress.SelectedHeat;

        State = RunState.Starting;

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
        CurrentRoom = type;

        switch (type)
        {
            case RoomType.Shop:
                shopUsedThisAct = true;
                yield return ShopRoutine();
                break;

            case RoomType.Rest:
                restUsedThisAct = true;
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

        if (type == RoomType.Boss)
        {
            WaveCount = 1;
            Wave = 1;

            State = RunState.Fighting;

            string bossName = BossNameForAct();

            ShowBanner(bossName.ToUpperInvariant(), 2f);

            SpawnBoss(bossName);

            yield return WaitUntilCleared(0);
        }
        else
        {
            WaveCount = WavesForStage();

            for (Wave = 1; Wave <= WaveCount; Wave++)
            {
                State = RunState.Fighting;

                if (Wave == 1)
                {
                    ShowBanner(
                        type == RoomType.Elite
                            ? "ELİT"
                            : "PERDE " + Act + "  •  ODA " + RoomInAct,
                        1.4f
                    );
                }
                else
                {
                    ShowBanner("DALGA " + Wave + " / " + WaveCount, 1.2f);
                }

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

                if (playerHealth.IsDead)
                    yield break;

                if (Wave < WaveCount)
                    yield return new WaitForSeconds(timeBetweenWaves);
            }
        }

        if (playerHealth.IsDead)
            yield break;

        // ---------------- TEMİZLENDİ ----------------

        State = RunState.Cleared;

        Stats.EndStage(true);

        RaiseStage(StageCleared, Stage);

        if (!roomDamaged)
            AddGold(perfectRoomGold, "Hasarsız oda");

        yield return new WaitForSecondsRealtime(1.0f);

        float heal =
            type == RoomType.Boss
                ? bossHealPercent
                : (duelMode ? duelHealBetweenStagesPercent : healBetweenStagesPercent);

        HealPercent(heal);

        // ---------------- ÖDÜL ----------------

        if (type == RoomType.Elite)
        {
            yield return OfferRoutine(false, "ELİT ÖDÜLÜ  (1/2)", null, offerChoices);
            yield return OfferRoutine(false, "ELİT ÖDÜLÜ  (2/2)", null, offerChoices);
        }
        else if (type == RoomType.Boss)
        {
            if (Act < acts)
                yield return OfferRoutine(false, "BOSS ÖDÜLÜ", null, offerChoices + 1);
        }
        else
        {
            yield return OfferRoutine(false, "BİR CHARM SEÇ", null, offerChoices);
        }

        while (bonusOffers > 0 && !playerHealth.IsDead)
        {
            bonusOffers--;

            yield return OfferRoutine(false, "KUSURSUZ ODA: BONUS CHARM", null, offerChoices, true);
        }

        yield return new WaitForSecondsRealtime(0.3f);
    }

    private IEnumerator WaitUntilCleared(int target)
    {
        int lastAlive = AliveEnemies;
        float lastProgressTime = Time.time;

        while (!playerHealth.IsDead)
        {
            RefreshAlive();

            if (AliveEnemies <= target)
                break;

            if (AliveEnemies != lastAlive)
            {
                lastAlive = AliveEnemies;
                lastProgressTime = Time.time;
            }
            else if (
                stuckTimeout > 0f &&
                Time.time - lastProgressTime > stuckTimeout
            )
            {
                RecoverStragglers();
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

        leaveShop = false;

        State = RunState.Shop;

        yield return PausedWait(() => leaveShop);
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
                HeatPriceMultiplier
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

    // Oyunu durdurup bir karar bekler (seçim ekranları).
    private IEnumerator PausedWait(Func<bool> done)
    {
        HitStop.ClearAll();
        EnemyTime.Clear();

        player.canControl = false;

        Time.timeScale = 0f;

        while (!done())
        {
            if (Time.timeScale != 0f)
                Time.timeScale = 0f;

            yield return null;
        }

        Time.timeScale = 1f;

        // Seçim tıklamasının saldırı tamponu sönsün, sonra kontrolü ver.
        yield return new WaitForSecondsRealtime(0.3f);

        player.canControl = true;
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

        if (enemy == currentBoss)
        {
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

    private IEnumerator SpawnWave(int count, bool elite)
    {
        int firstSide = UnityEngine.Random.value < 0.5f ? -1 : 1;

        for (int i = 0; i < count; i++)
        {
            if (playerHealth.IsDead)
                yield break;

            int side = (i % 2 == 0) ? firstSide : -firstSide;

            EnemyController enemy = SpawnOne(side, spawnTemplate);

            if (enemy != null)
            {
                ConfigureEnemy(enemy);

                if (elite)
                    MakeElite(enemy);

                spawned.Add(enemy);
            }

            RefreshAlive();

            yield return new WaitForSeconds(spawnInterval);
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

        controller.Setup(bossName, phaseAt);

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
    }

    private EnemyController SpawnOne(int side, GameObject template)
    {
        Vector3 playerPosition = player.transform.position;

        Vector3 position;

        if (spawnPoints != null && spawnPoints.Length > 0)
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
            Mathf.Min(enemy.attackCommitPoint, runAttackCommitPoint);

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

        enemy.unblockableChance =
            Mathf.Min(
                unblockableChanceCap,
                enemy.unblockableChance + unblockableBonusPerStage * t
            );

        Health health = enemy.GetComponent<Health>();

        if (health != null)
        {
            int scaled =
                Mathf.RoundToInt(
                    health.MaxHealth *
                    (1f + healthBonusPerStage * t) *
                    HeatHealthMultiplier
                );

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
            ", silinen: " + removed
        );
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

            if (e != null && !e.IsDead)
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

            if (e != null)
                Destroy(e.gameObject);
        }

        spawned.Clear();
        eliteEnemies.Clear();
        currentBoss = null;
        shopItems.Clear();
        doorOptions.Clear();

        bonusOffers = 0;
        AliveEnemies = 0;
        Wave = 0;
        WaveCount = 0;
        BannerUntil = 0f;
        Gold = 0;

        Inventory.Clear();

        HitStop.ClearAll();
        EnemyTime.Clear();

        Time.timeScale = 1f;

        playerHealth.Revive();

        PlayerPosture posture = player.GetComponent<PlayerPosture>();

        if (posture != null)
            posture.ResetPosture();

        player.hitInvincibilityTimer = 0f;

        player.transform.position = playerStartPosition;

        if (player.rb != null)
            player.rb.linearVelocity = Vector2.zero;

        player.canControl = true;

        player.stateMachine.ChangeState(
            new GroundedState(player, player.stateMachine)
        );
    }
}