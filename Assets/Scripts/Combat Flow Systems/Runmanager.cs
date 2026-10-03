using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum RunState
{
    Starting,   // koşu başlıyor
    Offer,      // charm seçimi (oyun duraklatılmış)
    Fighting,   // bölüm sürüyor
    Cleared,    // bölüm temizlendi
    Dead        // oyuncu öldü, yeniden başlatma bekleniyor
}

/// <summary>
/// Koşu (run) döngüsü:
///   [başlangıç charm'ı] -> bölüm 1 -> temizle -> charm seç -> bölüm 2 -> ...
///   Oyuncu ölünce koşu biter; Enter ile baştan başlar.
///
/// KURULUM: Sahnede boş bir objeye bu bileşeni ekle ve 'Enemy Prefab' ata.
/// Gerisi (charm'lar, arayüz, istatistikler) kendiliğinden kurulur.
/// Charm'lar istiflenir: aynı charm tekrar seçilirse güçlenir.
/// </summary>
public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    [Header("References")]
    public GameObject enemyPrefab;

    [SerializeField] private PlayerController player;

    [Header("Spawn")]
    [Tooltip("Boş bırakırsan düşmanlar oyuncunun iki yanında, zeminde doğar.")]
    [SerializeField] private Transform[] spawnPoints;

    [SerializeField] private float spawnDistanceMin = 8f;
    [SerializeField] private float spawnDistanceMax = 11f;

    [Tooltip("Doğan düşmanın zeminden yüksekliği (zemin bulunursa).")]
    [SerializeField] private float spawnHeightOffset = 1.5f;

    [Tooltip("Doğan düşmanın oyuncuyu fark etme mesafesi (chaseRange'in yerine geçer).")]
    [SerializeField] private float aggroRange = 14f;

    [SerializeField] private float spawnInterval = 0.35f;

    [Header("Stage")]
    [SerializeField] private int baseEnemyCount = 2;

    [Tooltip("Her bölümde eklenen düşman sayısı (kesirli birikir).")]
    [SerializeField] private float enemiesPerStage = 0.75f;

    [Tooltip("Bir bölümün TOPLAM düşman sayısı üst sınırı (sonsuz modda yüksek tutulur).")]
    [SerializeField] private int maxEnemiesPerStage = 30;

    [Tooltip("Bir dalgada AYNI ANDA ekranda olabilecek düşman sayısı üst sınırı.")]
    [SerializeField] private int maxEnemiesPerWave = 6;

    [Header("Waves (bölümü uzatır)")]
    [Tooltip(
        "Bölümün toplam düşman sayısı = (taban formül) × bu çarpan, " +
        "dalgalara bölünür. 1 = eski uzunluk, 1.5 = biraz daha uzun, 2 = iki kat.")]
    [Min(1f)]
    [SerializeField] private float stageLengthMultiplier = 1.5f;

    [Tooltip("Bir bölümdeki dalga sayısı (bölüm 1'de). 1 = eski davranış.")]
    [SerializeField] private int wavesPerStage = 2;

    [Tooltip("Kaç bölümde bir dalga sayısı +1 artsın. 0 = hiç artmaz.")]
    [SerializeField] private int extraWaveEveryNStages = 3;

    [SerializeField] private int maxWavesPerStage = 8;

    [Tooltip(
        "Sıradaki dalga, canlı düşman sayısı bu değere (veya altına) inince " +
        "gelir. 0 = hepsi ölünce. 1 = son düşman kalınca (akış kopmaz). " +
        "Son dalga her zaman hepsinin ölmesini bekler.")]
    [SerializeField] private int nextWaveAliveThreshold = 1;

    [Tooltip("Dalgalar arası bekleme (saniye).")]
    [SerializeField] private float timeBetweenWaves = 1.0f;

    [Header("Difficulty per stage")]
    [Tooltip("Her bölümde düşman hızına eklenen oran.")]
    [SerializeField] private float speedBonusPerStage = 0.02f;

    [Tooltip("Her bölümde engellenemez vuruş ihtimaline eklenen değer.")]
    [SerializeField] private float unblockableBonusPerStage = 0.02f;

    [SerializeField] private float unblockableChanceCap = 0.8f;

    [Tooltip("Düşman hızına eklenebilecek en yüksek oran (0.6 = %60).")]
    [SerializeField] private float maxSpeedBonus = 0.6f;

    [Tooltip(
        "Her bölümde düşman canına eklenen oran (0.25 = bölüm başına %25). " +
        "Düşmanın Execute Damage'ini aşınca execute tek vuruşta öldüremez; " +
        "can hasarı charm'ları (Ağır Darbe, kritik) o zaman anlam kazanır.")]
    [SerializeField] private float healthBonusPerStage = 0.25f;

    [Header("Charms")]
    [Tooltip("Koşunun en başında bir charm seçilsin (bölüm 1'den önce).")]
    [SerializeField] private bool offerAtRunStart = true;

    [Tooltip("İlk charm teklifinin geleceği bölüm (temizlendikten sonra).")]
    [SerializeField] private int firstOfferStage = 1;

    [Tooltip("Kaç bölümde bir charm teklifi (1 = her bölüm).")]
    [SerializeField] private int offerEveryNStages = 1;

    [SerializeField] private int offerChoices = 3;

    [SerializeField] private bool includeDefaultCharms = true;

    [Tooltip("Kendi CharmDefinition asset'lerin.")]
    [SerializeField]
    private List<CharmDefinition> extraCharms =
        new List<CharmDefinition>();

    [Header("Recovery")]
    [Tooltip("Her bölüm temizlenince iyileşen can birimi.")]
    [SerializeField] private int healBetweenStages = 1;

    // ---------------------------------------------------------
    // Durum (arayüz okur)
    // ---------------------------------------------------------

    public RunState State { get; private set; } = RunState.Starting;
    public int Stage { get; private set; }
    public int AliveEnemies { get; private set; }
    public int Wave { get; private set; }
    public int WaveCount { get; private set; }
    public string WaveBannerText { get; private set; } = "";
    public float WaveBannerUntil { get; private set; }
    public bool IsStartOffer { get; private set; }
    public CharmInventory Inventory { get; private set; }
    public IReadOnlyList<CharmDefinition> Offers => offers;

    private List<CharmDefinition> offers = new List<CharmDefinition>();
    private int chosenIndex = -1;
    private bool restartRequested;

    private readonly List<CharmDefinition> pool =
        new List<CharmDefinition>();

    private readonly List<EnemyController> spawned =
        new List<EnemyController>();

    private Health playerHealth;
    private Vector3 playerStartPosition;

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

        Inventory = new CharmInventory(player.gameObject);

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

    public void RequestRestart()
    {
        if (State == RunState.Dead)
            restartRequested = true;
    }

    // =========================================================
    // ANA DÖNGÜ
    // =========================================================

    private IEnumerator RunLoop()
    {
        while (true)
        {
            Stage = 0;
            State = RunState.Starting;

            if (offerAtRunStart)
                yield return OfferRoutine(true);

            while (!playerHealth.IsDead)
            {
                Stage++;

                spawned.Clear();

                WaveCount = WavesForStage();

                // ---------- DALGALAR ----------

                for (Wave = 1; Wave <= WaveCount; Wave++)
                {
                    State = RunState.Fighting;

                    WaveBannerText =
                        Wave == 1
                            ? "BÖLÜM " + Stage
                            : "DALGA " + Wave + " / " + WaveCount;

                    WaveBannerUntil = Time.unscaledTime + 1.6f;

                    yield return SpawnWave(EnemiesForWave(Wave));

                    // Sıradaki dalga için eşik; son dalga hepsinin ölmesini bekler.
                    int target =
                        Wave < WaveCount
                            ? Mathf.Max(0, nextWaveAliveThreshold)
                            : 0;

                    while (!playerHealth.IsDead)
                    {
                        RefreshAlive();

                        if (AliveEnemies <= target)
                            break;

                        yield return null;
                    }

                    if (playerHealth.IsDead)
                        break;

                    if (Wave < WaveCount)
                        yield return new WaitForSeconds(timeBetweenWaves);
                }

                if (playerHealth.IsDead)
                    break;

                // ---------- BÖLÜM TEMİZLENDİ ----------

                State = RunState.Cleared;

                yield return new WaitForSecondsRealtime(1.2f);

                if (healBetweenStages > 0)
                    playerHealth.Heal(healBetweenStages);

                if (ShouldOffer())
                    yield return OfferRoutine(false);

                yield return new WaitForSecondsRealtime(0.4f);
            }

            // ---------- ÖLDÜ ----------

            State = RunState.Dead;

            restartRequested = false;

            yield return new WaitForSecondsRealtime(1.0f);

            // Enter ile ya da (debug) R ile oyuncu dışarıdan dirildiyse.
            while (!restartRequested && playerHealth.IsDead)
                yield return null;

            ResetRun();
        }
    }

    private bool ShouldOffer()
    {
        if (Stage < firstOfferStage)
            return false;

        int every = Mathf.Max(1, offerEveryNStages);

        return (Stage - firstOfferStage) % every == 0;
    }

    // =========================================================
    // CHARM SEÇİMİ
    // =========================================================

    private IEnumerator OfferRoutine(bool isStartOffer)
    {
        List<CharmDefinition> rolled =
            CharmCatalog.Roll(pool, Inventory, offerChoices);

        // Seçilecek bir şey kalmadıysa (hepsi sınırda) atla.
        if (rolled.Count == 0)
            yield break;

        offers = rolled;
        chosenIndex = -1;
        IsStartOffer = isStartOffer;
        State = RunState.Offer;

        // Duraklatmadan önce kalan zaman efektlerini temizle.
        HitStop.ClearAll();
        EnemyTime.Clear();

        // Seçim sırasında tıklama oyuncuya saldırı verdirmesin.
        player.canControl = false;

        Time.timeScale = 0f;

        // Zamanı seçim boyunca 0'da TUT: başka bir script (hit-stop vb.)
        // zamanı açsa bile seçim ekranı sırasında oyun durmuş kalsın.
        while (chosenIndex < 0)
        {
            if (Time.timeScale != 0f)
                Time.timeScale = 0f;

            yield return null;
        }

        CharmDefinition picked = offers[chosenIndex];

        offers = new List<CharmDefinition>();

        Inventory.Add(picked);

        Time.timeScale = 1f;

        // Seçim tıklamasının saldırı tamponu sönsün, sonra kontrolü ver.
        yield return new WaitForSecondsRealtime(0.3f);

        player.canControl = true;
    }

    // =========================================================
    // DOĞURMA
    // =========================================================

    // Bölümdeki dalga sayısı: taban + her N bölümde +1 (üst sınırlı).
    private int WavesForStage()
    {
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

    // Bölümün TOPLAM düşman sayısı: (taban + bölüm artışı) × uzunluk çarpanı.
    private int TotalEnemiesForStage()
    {
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

    // Toplamı dalgalara böler (artan sayı ilk dalgalara dağılır).
    // Aynı anda ekranda olabilecek düşman sayısı maxEnemiesPerWave ile sınırlı.
    private int EnemiesForWave(int wave)
    {
        int total = TotalEnemiesForStage();

        int waves = Mathf.Max(1, WaveCount);

        int count = total / waves + (wave <= total % waves ? 1 : 0);

        return Mathf.Clamp(count, 1, Mathf.Max(1, maxEnemiesPerWave));
    }

    // Bir dalgayı doğurur. Önceki dalgaların düşmanları listede kalır.
    private IEnumerator SpawnWave(int count)
    {
        int firstSide = Random.value < 0.5f ? -1 : 1;

        for (int i = 0; i < count; i++)
        {
            if (playerHealth.IsDead)
                yield break;

            int side = (i % 2 == 0) ? firstSide : -firstSide;

            SpawnOne(side);

            RefreshAlive();

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnOne(int side)
    {
        Vector3 playerPosition = player.transform.position;

        Vector3 position;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform point =
                spawnPoints[Random.Range(0, spawnPoints.Length)];

            position = point.position;
        }
        else
        {
            float distance =
                Random.Range(spawnDistanceMin, spawnDistanceMax);

            float x = playerPosition.x + side * distance;
            float y = playerPosition.y;

            // Zemini bul: yukarıdan aşağı ışın at.
            RaycastHit2D hit =
                Physics2D.Raycast(
                    new Vector2(x, playerPosition.y + 6f),
                    Vector2.down,
                    30f,
                    player.Movement.groundMask
                );

            if (hit.collider != null)
                y = hit.point.y + spawnHeightOffset;

            position = new Vector3(x, y, playerPosition.z);
        }

        GameObject obj =
            Instantiate(enemyPrefab, position, Quaternion.identity);

        EnemyController enemy =
            obj.GetComponent<EnemyController>();

        if (enemy == null)
            return;

        ConfigureEnemy(enemy);

        spawned.Add(enemy);
    }

    // Bölüm zorluğu + doğan düşman oyuncuyu hemen fark etsin.
    private void ConfigureEnemy(EnemyController enemy)
    {
        float t = Stage - 1;

        enemy.chaseRange = aggroRange;

        // Hız: bölümle artar, üst sınırı var.
        enemy.chaseSpeed *=
            1f + Mathf.Min(maxSpeedBonus, speedBonusPerStage * t);

        enemy.unblockableChance =
            Mathf.Min(
                unblockableChanceCap,
                enemy.unblockableChance + unblockableBonusPerStage * t
            );

        // Can: bölümle artar. Execute Damage'i aşınca execute tek vuruşta
        // öldüremez, bu da can hasarı charm'larını anlamlı kılar.
        Health health = enemy.GetComponent<Health>();

        if (health != null && healthBonusPerStage > 0f)
        {
            int scaled =
                Mathf.RoundToInt(
                    health.MaxHealth *
                    (1f + healthBonusPerStage * t)
                );

            health.SetMaxHealth(scaled, true);
        }
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

        // Hâlâ doğma sürerken sayaç 'count'tan düşük görünmesin.
        AliveEnemies = Mathf.Max(alive, 0);
    }

    // =========================================================
    // YENİDEN BAŞLAT
    // =========================================================

    private void ResetRun()
    {
        // Sahnedeki tüm düşmanlar.
        for (int i = EnemyController.All.Count - 1; i >= 0; i--)
        {
            EnemyController e = EnemyController.All[i];

            if (e != null)
                Destroy(e.gameObject);
        }

        spawned.Clear();
        AliveEnemies = 0;
        Wave = 0;
        WaveCount = 0;
        WaveBannerUntil = 0f;

        // Charm'lar ve istatistikler sıfırlanır.
        Inventory.Clear();

        HitStop.ClearAll();
        EnemyTime.Clear();

        Time.timeScale = 1f;

        // Oyuncuyu başlangıca al.
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