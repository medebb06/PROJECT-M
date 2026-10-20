using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kalabalık savaşta düşman vuruşlarını bir RİTME dizer.
///
/// Sorun: 1vX'te herkes aynı anda saldırınca vuruşlar üst üste biniyor
/// ve parry (0.12 sn pencere) imkansızlaşıyor.
///
/// Çözüm: Düşman saldırmadan önce buradan izin ister. Koordinatör
/// vuruş ANLARINI (uyarı süresi bittiği an) birbirinden sabit aralıkla
/// ayırır. Böylece vuruşlar bir tempoda gelir: tak - tak - tak ... (nefes).
///
/// Önemli: Bir düşmanın uyarısı ile vuruşu arasındaki süre DEĞİŞMEZ
/// (hâlâ attackWarningTime). Yani parry kas hafızan 1v1 ile aynı kalır;
/// sadece düşmanlar sırayla başlıyor.
///
/// Tek düşmanda hiçbir etkisi yoktur (1v1 hissi aynı kalır).
///
/// Sahnede yoksa varsayılan değerlerle kendiliğinden oluşur.
/// Ayarlamak için sahneye boş bir objeye ekleyip Inspector'dan değiştir.
/// </summary>
public class EnemyAttackCoordinator : MonoBehaviour
{
    private static EnemyAttackCoordinator instance;

    // =========================================================
    // INSPECTOR
    // =========================================================

    [Header("Rhythm")]
    [Tooltip("Bir 'vuruş' birimi (saniye). Küçük = hızlı tempo.")]
    [SerializeField] private float beatInterval = 1.15f;

    [Tooltip(
        "Ardışık vuruşlar arasındaki boşluk, beatInterval ile çarpılır. " +
        "Döngüsel okunur. Örn: 1,1,1,2 = üç hızlı vuruş sonra bir nefes. " +
        "Hepsini 1 yaparsan sabit tempo olur.")]
    [SerializeField] private float[] beatPattern = { 1f, 1f, 1.5f };

    [Tooltip("Vuruş, planlanan ana bu kadar yakınsa tam ritme oturtulur.")]
    [SerializeField] private float onBeatTolerance = 0.08f;

    [Header("Limits")]
    [Tooltip("Aynı anda en fazla kaç düşman uyarı (wind-up) halinde olabilir.")]
    [SerializeField] private int maxConcurrentAttackers = 2;

    [Tooltip("Sıradaki düşman bu süre istek göndermezse sıradan düşer.")]
    [SerializeField] private float queueMemory = 0.3f;

    [Tooltip("Saldırı bu kadar süre hiç olmadıysa ritim deseni başa sarar.")]
    [SerializeField] private float idleResetTime = 3f;

    [Header("After Player Hit")]
    [Tooltip("Oyuncu hasar alınca bir sonraki vuruş en az bu kadar sonra gelir.")]
    [SerializeField] private float postHitGrace = 0.6f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    // =========================================================
    // RUNTIME
    // =========================================================

    // Sıradaki düşmanlar: sıraya giriş numarası (küçük olan öndedir)
    // ve son istek zamanı.
    private readonly Dictionary<EnemyBase, int> firstAsk =
        new Dictionary<EnemyBase, int>();

    private readonly Dictionary<EnemyBase, float> lastAsk =
        new Dictionary<EnemyBase, float>();

    // Menzilde olup saldırı izni isteyen düşmanların son istek zamanı.
    // Sıradaki düşman menzile takılırsa kuyruğu kilitlemesin diye
    // izin sırası SADECE bunlar arasından belirlenir.
    private readonly Dictionary<EnemyBase, float> lastRangeAsk =
        new Dictionary<EnemyBase, float>();

    // Uyarı (wind-up) halindeki düşmanlar -> vuruş zamanı
    private readonly Dictionary<EnemyBase, float> activeAttackers =
        new Dictionary<EnemyBase, float>();

    private readonly List<EnemyBase> removeBuffer =
        new List<EnemyBase>();

    private float nextAllowedHitTime;
    private int patternIndex;
    private int askCounter;

    // =========================================================
    // STATIC API
    // =========================================================

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration
    )]
    private static void ResetStatics()
    {
        instance = null;
    }

    private static EnemyAttackCoordinator Instance
    {
        get
        {
            if (instance == null)
            {
                instance =
                    FindFirstObjectByType<EnemyAttackCoordinator>();

                if (instance == null)
                {
                    GameObject obj =
                        new GameObject("EnemyAttackCoordinator");

                    instance =
                        obj.AddComponent<EnemyAttackCoordinator>();
                }
            }

            return instance;
        }
    }

    /// <summary>
    /// Düşman saldırıya başlamak istediğinde HER KARE çağırır.
    /// true dönerse saldırıya başlayabilir (uyarı hemen başlar).
    /// false ise beklemeye devam eder.
    /// </summary>
    public static bool TryRequestAttack(
        EnemyBase enemy,
        float windupTime
    )
    {
        // Koordinatör devre dışıyken eski davranış: hep izin ver.
        if (!Application.isPlaying || enemy == null)
            return true;

        return Instance.RequestInternal(
            enemy,
            windupTime
        );
    }

    /// <summary>
    /// Saldırıya hazır düşman sıraya girer / yerini korur (HER KARE çağrılır).
    /// Menzilde olmasa bile sıra numarası alır; böylece geride bekleyenler
    /// sıralarını kaybetmez. Saldırı izni için TryRequestAttack kullanılır.
    /// </summary>
    public static void JoinQueue(EnemyBase enemy)
    {
        if (!Application.isPlaying || enemy == null)
            return;

        Instance.JoinInternal(enemy);
    }

    /// <summary>
    /// Düşman şu an sırada mı (yakın zamanda JoinQueue / TryRequestAttack
    /// çağırdı mı)?
    /// </summary>
    public static bool IsQueued(EnemyBase enemy)
    {
        if (instance == null || enemy == null)
            return false;

        return
            instance.firstAsk.ContainsKey(enemy) &&
            instance.lastAsk.TryGetValue(enemy, out float last) &&
            EnemyTime.Now - last <= instance.queueMemory;
    }

    /// <summary>
    /// Düşmanın bulunduğu taraftaki sıra numarası.
    /// 0 = o tarafın en önündeki (oyuncuya yaklaşıp saldırı bekler),
    /// 1, 2, ... = geride kademeli bekler.
    /// Sıraya girmemişse 0.
    /// </summary>
    public static int GetStandbyRank(EnemyBase enemy)
    {
        if (instance == null || enemy == null)
            return 0;

        return instance.RankInternal(enemy);
    }

    /// <summary>
    /// Düşmanın vuruşu gerçekleşince (veya saldırı iptal olunca) çağrılır.
    /// Birden fazla çağrılması zararsızdır.
    /// </summary>
    public static void ReleaseAttack(EnemyBase enemy)
    {
        if (instance == null || enemy == null)
            return;

        instance.activeAttackers.Remove(enemy);
    }

    /// <summary>
    /// Oyuncu hasar aldığında çağrılır: ardışık vuruş yağmurunu keser.
    /// </summary>
    /// <summary>Siradaki vurusu en az 'seconds' sonraya ertele (parry odulu).</summary>
    public static void DelayNextHit(float seconds)
    {
        if (instance == null) return;
        instance.nextAllowedHitTime = Mathf.Max(instance.nextAllowedHitTime, EnemyTime.Now + seconds);
    }

    public static void NotifyPlayerHit()
    {
        if (instance == null)
            return;

        instance.nextAllowedHitTime =
            Mathf.Max(
                instance.nextAllowedHitTime,
                EnemyTime.Now + instance.postHitGrace
            );
    }

    // =========================================================
    // INTERNAL
    // =========================================================

    private void Awake()
    {
        if (
            instance != null &&
            instance != this
        )
        {
            Destroy(this);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private bool RequestInternal(
        EnemyBase enemy,
        float windupTime
    )
    {
        float now = EnemyTime.Now;

        PurgeStale(now);

        // -----------------------------------------------------
        // 1) SIRAYA GİR
        // -----------------------------------------------------

        lastAsk[enemy] = now;
        lastRangeAsk[enemy] = now;

        if (!firstAsk.ContainsKey(enemy))
            firstAsk[enemy] = askCounter++;

        // Sadece en uzun süredir bekleyen düşman saldırabilir.
        // (Aksi halde şanslı düşman hep öne geçer, diğeri aç kalır.)
        if (!IsFrontOfQueue(enemy))
            return false;

        // -----------------------------------------------------
        // 2) AYNI ANDA UYARI LİMİTİ
        // -----------------------------------------------------

        // Kalabalikta ritim seyrelir: 4+ dusmanda ayni anda tek uyari, vuruslar arasi bosluk acilir.
        int crowd = CountCrowd(enemy);
        crowdScale = Mathf.Clamp(1f + 0.12f * (crowd - 2), 1f, 2f);
        int limit = crowd >= 2 ? 1 : maxConcurrentAttackers;   // kalabalikta ayni anda TEK saldiri: sirayla, okunabilir

        if (
            !activeAttackers.ContainsKey(enemy) &&
            activeAttackers.Count >= limit
        )
        {
            return false;
        }

        // -----------------------------------------------------
        // 3) RİTİM: Şimdi başlarsam vuruşum ne zaman olur?
        // -----------------------------------------------------

        float hitTime =
            now + windupTime;

        // Henüz benim vuruş sıram değil, bekle.
        if (hitTime < nextAllowedHitTime)
            return false;

        // Uzun süredir saldırı yoksa desen başa sarar.
        if (now > nextAllowedHitTime + idleResetTime)
            patternIndex = 0;

        // Planlanan ana çok yakınsak tam ritme oturt (kayma birikmesin).
        float scheduled =
            (hitTime - nextAllowedHitTime) < onBeatTolerance
                ? nextAllowedHitTime
                : hitTime;

        nextAllowedHitTime =
            scheduled + GetNextInterval();

        // -----------------------------------------------------
        // İZİN VERİLDİ
        // -----------------------------------------------------

        firstAsk.Remove(enemy);
        lastAsk.Remove(enemy);
        lastRangeAsk.Remove(enemy);

        activeAttackers[enemy] = hitTime;

        return true;
    }

    private float GetNextInterval()
    {
        float multiplier = 1f;

        if (
            beatPattern != null &&
            beatPattern.Length > 0
        )
        {
            multiplier =
                Mathf.Max(
                    0.1f,
                    beatPattern[
                        patternIndex % beatPattern.Length
                    ]
                );
        }

        patternIndex++;

        return beatInterval * multiplier * crowdScale;
    }

    private float crowdScale = 1f;

    private int CountCrowd(EnemyBase enemy)
    {
        if (enemy == null || enemy.target == null) return 0;
        Vector2 tp = enemy.target.position;
        int n = 0;
        for (int i = 0; i < EnemyBase.All.Count; i++)
        {
            var e = EnemyBase.All[i];
            if (e == null || e.IsDead) continue;
            if (((Vector2)e.transform.position - tp).sqrMagnitude <= 16f * 16f) n++;
        }
        return n;
    }

    private void JoinInternal(EnemyBase enemy)
    {
        float now = EnemyTime.Now;

        PurgeStale(now);

        lastAsk[enemy] = now;

        if (!firstAsk.ContainsKey(enemy))
            firstAsk[enemy] = askCounter++;
    }

    private int RankInternal(EnemyBase enemy)
    {
        if (
            enemy.target == null ||
            !firstAsk.TryGetValue(enemy, out int myOrder)
        )
        {
            return 0;
        }

        float targetX =
            enemy.target.position.x;

        int mySide =
            SideOf(enemy, targetX);

        int rank = 0;

        foreach (var pair in firstAsk)
        {
            if (
                pair.Key == null ||
                pair.Key == enemy
            )
            {
                continue;
            }

            // Aynı tarafta, benden önce sıraya girmiş olanları say.
            if (
                pair.Value < myOrder &&
                SideOf(pair.Key, targetX) == mySide
            )
            {
                rank++;
            }
        }

        return rank;
    }

    private static int SideOf(
        EnemyBase enemy,
        float targetX
    )
    {
        return enemy.transform.position.x >= targetX
            ? 1
            : -1;
    }

    private bool IsFrontOfQueue(
        EnemyBase enemy
    )
    {
        EnemyBase front = null;
        int frontOrder = int.MaxValue;

        foreach (var pair in firstAsk)
        {
            if (pair.Key == null)
                continue;

            // Menzilde olup izin isteyenler arasından seç:
            // geride bekleyen ya da menzile takılan bir düşman
            // kuyruğu kilitlemesin.
            if (
                !lastRangeAsk.TryGetValue(pair.Key, out float rangeTime) ||
                EnemyTime.Now - rangeTime > queueMemory
            )
            {
                continue;
            }

            // Sıra numarası her düşmanda benzersiz,
            // yani eşitlik / tie-break sorunu yok.
            if (pair.Value < frontOrder)
            {
                front = pair.Key;
                frontOrder = pair.Value;
            }
        }

        return front == enemy;
    }

    private void PurgeStale(float now)
    {
        // Ölen / yok edilen / istek göndermeyi bırakan düşmanları sıradan çıkar.
        removeBuffer.Clear();

        foreach (var pair in lastAsk)
        {
            if (
                pair.Key == null ||
                now - pair.Value > queueMemory
            )
            {
                removeBuffer.Add(pair.Key);
            }
        }

        for (int i = 0; i < removeBuffer.Count; i++)
        {
            lastAsk.Remove(removeBuffer[i]);
            firstAsk.Remove(removeBuffer[i]);
            lastRangeAsk.Remove(removeBuffer[i]);
        }

        // Güvenlik: Release hiç çağrılmadıysa takılı kalmasın.
        removeBuffer.Clear();

        foreach (var pair in activeAttackers)
        {
            if (
                pair.Key == null ||
                now > pair.Value + 2f
            )
            {
                removeBuffer.Add(pair.Key);
            }
        }

        for (int i = 0; i < removeBuffer.Count; i++)
        {
            activeAttackers.Remove(removeBuffer[i]);
        }
    }

    private void OnDisable()
    {
        firstAsk.Clear();
        lastAsk.Clear();
        lastRangeAsk.Clear();
        activeAttackers.Clear();
    }

    // =========================================================
    // DEBUG
    // =========================================================

    private void OnGUI()
    {
        if (!showDebug)
            return;

        float untilNext =
            nextAllowedHitTime - EnemyTime.Now;

        GUI.Label(
            new Rect(10, 10, 420, 60),
            "RHYTHM  uyarıda: " + activeAttackers.Count +
            "/" + maxConcurrentAttackers +
            "  sırada: " + firstAsk.Count +
            "\nSonraki vuruş slotu: " +
            (untilNext > 0f
                ? untilNext.ToString("0.00") + " sn sonra"
                : "şimdi") +
            "  (desen #" + patternIndex + ")"
        );
    }
}