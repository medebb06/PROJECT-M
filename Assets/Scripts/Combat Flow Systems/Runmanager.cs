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

    [Tooltip(
        "Zemini ararken ışının oyuncunun ne kadar üstünden başlayacağı. " +
        "Büyük olursa oyuncunun üstündeki platformlara doğabilirler; " +
        "küçük tutmak ulaşılamaz yerde doğmayı azaltır.")]
    [SerializeField] private float spawnRaycastHeight = 3f;

    [Header("Stuck Recovery (takılma kurtarma)")]
    [Tooltip(
        "Bir dalgada bu kadar saniye hiç düşman ölmez/doğmazsa kurtarma çalışır: " +
        "uzakta ya da ulaşılamayan düşmanlar oyuncunun yanına taşınır, " +
        "haritadan düşenler silinir. 0 = kapalı.")]
    [SerializeField] private float stuckTimeout = 15f;

    [Tooltip("Oyuncudan bu kadar uzaktaki canlı düşman 'takılmış' sayılır.")]
    [SerializeField] private float stragglerDistance = 16f;

    [Tooltip("Oyuncunun bu kadar altına düşen düşman haritadan düşmüş sayılır ve silinir.")]
    [SerializeField] private float fallKillDepth = 25f;

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

    [Header("Parry odaklı denge (bedava takası bitir)")]
    [Tooltip(
        "AÇIK: oyuncu kendiliğinden can yenilemez (koşu boyunca). " +
        "Hasar yiyip yenilenerek vurmak parry'den kârlı olmasın; iyileşme " +
        "sadece charm'lardan, parry'den ve bölüm aralarından gelir.")]
    [SerializeField] private bool disablePlayerHealthRecovery = true;

    [Tooltip(
        "Koşuda düşman saldırısı, uyarının bu oranından sonra vurarak " +
        "KESİLEMEZ. Prefab'daki Attack Commit Point bunun üstündeyse buna " +
        "düşürülür (altındaysa dokunulmaz). Düşük değer = saldırıyı vurarak " +
        "iptal etmek zorlaşır, cevap parry/dash/geri çekilme olur.")]
    [Range(0f, 1f)]
    [SerializeField] private float runAttackCommitPoint = 0.15f;

    [Tooltip("Her bölümde düşmanın NORMAL saldırı hasarına eklenen oran (0.06 = %6).")]
    [SerializeField] private float damageBonusPerStage = 0.06f;

    [Tooltip("Hasar artışının üst sınırı (1.5 = en fazla +%150).")]
    [SerializeField] private float maxDamageBonus = 1.5f;

    [Tooltip("Bölüm temizlenince iyileşen can: oyuncunun MAX canının yüzdesi (0.1 = %10).")]
    [Range(0f, 1f)]
    [SerializeField] private float healBetweenStagesPercent = 0.10f;

    [Header("Riposte (parry ödülü)")]
    [Tooltip("Başarılı parry'den sonra güçlenmiş vuruşların süresi (sn).")]
    [SerializeField] private float riposteDuration = 2f;

    [Tooltip("En fazla kaç vuruş güçlenmiş sayılır (süre ya da vuruş, hangisi önce biterse).")]
    [SerializeField] private int riposteMaxHits = 3;

    [Tooltip("Riposte sırasında denge hasarı çarpanı.")]
    [SerializeField] private float riposteBalanceMultiplier = 2f;

    [Tooltip("Riposte sırasında can hasarı çarpanı.")]
    [SerializeField] private float riposteHealthMultiplier = 1.5f;

    [Tooltip("Riposte sırasında kritik şansına eklenen değer (0.5 = +%50).")]
    [Range(0f, 1f)]
    [SerializeField] private float riposteCritChanceBonus = 0.5f;

    [Tooltip("Parry'nin oyuncuya iade ettiği posture.")]
    [SerializeField] private int postureRefundOnParry = 25;

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

    // Doğurmada kullanılan şablon. 'Enemy Prefab' gerçek bir prefab ise
    // onun kendisi; sahne nesnesi atanmışsa gizli bir kopyası.
    private GameObject spawnTemplate;

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

        spawnTemplate = PrepareTemplate(enemyPrefab);

        Inventory = new CharmInventory(player.gameObject);

        // Koşuda bedava can yenilenmesi kapalı.
        if (disablePlayerHealthRecovery)
            playerHealth.SetRecoveryEnabled(false);

        // Parry ödülü (riposte).
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

                    int spawnedBefore = spawned.Count;

                    yield return SpawnWave(EnemiesForWave(Wave));

                    // Hiç düşman doğmadıysa (şablon geçersiz) döngüyü sessizce
                    // dönmek yerine net bir hatayla durdur.
                    if (
                        spawned.Count == spawnedBefore &&
                        !playerHealth.IsDead
                    )
                    {
                        Debug.LogError(
                            "RunManager: düşman doğurulamadı! " +
                            "'Enemy Prefab' alanını kontrol et."
                        );

                        yield break;
                    }

                    // Sıradaki dalga için eşik; son dalga hepsinin ölmesini bekler.
                    int target =
                        Wave < WaveCount
                            ? Mathf.Max(0, nextWaveAliveThreshold)
                            : 0;

                    // İlerleme izleme: canlı sayısı değişmiyorsa (kimse ölmüyor)
                    // takılma olabilir; stuckTimeout sonra kurtarma çalışır.
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

                if (healBetweenStagesPercent > 0f)
                {
                    int heal =
                        Mathf.Max(
                            1,
                            Mathf.RoundToInt(
                                playerHealth.MaxHealth *
                                healBetweenStagesPercent
                            )
                        );

                    playerHealth.Heal(heal);
                }

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
                    new Vector2(x, playerPosition.y + spawnRaycastHeight),
                    Vector2.down,
                    30f,
                    player.Movement.groundMask
                );

            if (hit.collider != null)
                y = hit.point.y + spawnHeightOffset;

            position = new Vector3(x, y, playerPosition.z);
        }

        // Şablon yok olduysa (ör. sahne düşmanı öldü) Instantiate hata
        // verir ve doğma coroutine'i çökerdi: kontrol et.
        if (spawnTemplate == null)
        {
            Debug.LogError(
                "RunManager: düşman şablonu yok edilmiş! 'Enemy Prefab' " +
                "alanına Project penceresinden PREFAB ata (sahne nesnesi değil)."
            );

            return;
        }

        GameObject obj =
            Instantiate(spawnTemplate, position, Quaternion.identity);

        // Gizli şablondan gelen kopya kapalıdır; aç (Awake/Start şimdi çalışır).
        if (!obj.activeSelf)
            obj.SetActive(true);

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

        // Uzakta Idle'a düşüp bölümü kilitlemesin.
        enemy.alwaysHunt = true;

        // Saldırı erken kararlı olsun: vurarak iptal etmek zorlaşsın,
        // parry en iyi cevap kalsın. (Prefab daha düşükse ona dokunma.)
        enemy.attackCommitPoint =
            Mathf.Min(enemy.attackCommitPoint, runAttackCommitPoint);

        // Normal saldırı hasarı bölümle artar: hasar yemek giderek pahalı.
        // (Engellenemez vuruş zaten büyük; ölçeklenmez.)
        float damageMultiplier =
            1f + Mathf.Min(maxDamageBonus, damageBonusPerStage * t);

        enemy.attackDamage =
            Mathf.Max(
                1,
                Mathf.RoundToInt(enemy.attackDamage * damageMultiplier)
            );

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

    // =========================================================
    // TAKILMA KURTARMA
    // =========================================================

    // Uzun süredir ilerleme yok: canlı düşmanlara bak, çözülebilenleri çöz.
    private void RecoverStragglers()
    {
        Vector3 playerPosition = player.transform.position;

        string report = "";
        int moved = 0;
        int removed = 0;

        for (int i = 0; i < spawned.Count; i++)
        {
            EnemyController e = spawned[i];

            if (e == null || e.IsDead)
                continue;

            Vector3 ep = e.transform.position;

            string stateName =
                e.CurrentState != null
                    ? e.CurrentState.GetType().Name
                    : "null";

            report +=
                "\n  " + e.name + " konum=" + ep +
                " durum=" + stateName;

            // 1) Haritadan düşmüş: sil.
            if (ep.y < playerPosition.y - fallKillDepth)
            {
                Destroy(e.gameObject);

                removed++;

                continue;
            }

            // 2) Uzakta ya da Idle'da takılı: oyuncunun yanına taşı.
            float distance =
                Vector2.Distance(ep, playerPosition);

            if (
                distance > stragglerDistance ||
                e.CurrentState is EnemyIdleState
            )
            {
                int side = (i % 2 == 0) ? -1 : 1;

                e.transform.position =
                    RecoverPosition(side);

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
            ", silinen: " + removed + ". Canlı düşmanlar:" + report
        );
    }

    // Oyuncunun yanında, oyuncunun durduğu (ulaşılabilir) zeminde bir nokta.
    private Vector3 RecoverPosition(int side)
    {
        Vector3 playerPosition = player.transform.position;

        float x =
            playerPosition.x +
            side * Random.Range(6f, 9f);

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

    // 'Enemy Prefab' alanına Project penceresinden gerçek bir prefab
    // atanmışsa onu kullanır. SAHNEDEKİ bir düşman atanmışsa, o düşman
    // ölünce nesne yok olur ve sonraki doğmalar kırılır; bu yüzden
    // başlangıçta gizli bir kopyasını çıkarıp onu şablon yapar.
    private GameObject PrepareTemplate(GameObject source)
    {
        // Prefab asset'lerinin sahnesi geçersizdir; sahne nesnelerinin geçerli.
        if (!source.scene.IsValid())
            return source;

        Debug.LogWarning(
            "RunManager: 'Enemy Prefab' alanına SAHNEDEKİ bir düşman (" +
            source.name + ") atanmış. Gizli bir şablon kopyası " +
            "oluşturuluyor. Doğrusu: Project penceresinden prefab'ı atamak."
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