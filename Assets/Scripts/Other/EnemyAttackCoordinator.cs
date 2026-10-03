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
    [SerializeField] private float beatInterval = 0.9f;

    [Tooltip(
        "Ardışık vuruşlar arasındaki boşluk, beatInterval ile çarpılır. " +
        "Döngüsel okunur. Örn: 1,1,1,2 = üç hızlı vuruş sonra bir nefes. " +
        "Hepsini 1 yaparsan sabit tempo olur.")]
    [SerializeField] private float[] beatPattern = { 1f, 1f, 1f, 2f };

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
    private readonly Dictionary<EnemyController, int> firstAsk =
        new Dictionary<EnemyController, int>();

    private readonly Dictionary<EnemyController, float> lastAsk =
        new Dictionary<EnemyController, float>();

    // Menzilde olup saldırı izni isteyen düşmanların son istek zamanı.
    // Sıradaki düşman menzile takılırsa kuyruğu kilitlemesin diye
    // izin sırası SADECE bunlar arasından belirlenir.
    private readonly Dictionary<EnemyController, float> lastRangeAsk =
        new Dictionary<EnemyController, float>();

    // Uyarı (wind-up) halindeki düşmanlar -> vuruş zamanı
    private readonly Dictionary<EnemyController, float> activeAttackers =
        new Dictionary<EnemyController, float>();

    private readonly List<EnemyController> removeBuffer =
        new List<EnemyController>();

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
        EnemyController enemy,
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
    public static void JoinQueue(EnemyController enemy)
    {
        if (!Application.isPlaying || enemy == null)
            return;

        Instance.JoinInternal(enemy);
    }

    /// <summary>
    /// Düşmanın bulunduğu taraftaki sıra numarası.
    /// 0 = o tarafın en önündeki (oyuncuya yaklaşıp saldırı bekler),
    /// 1, 2, ... = geride kademeli bekler.
    /// Sıraya girmemişse 0.
    /// </summary>
    public static int GetStandbyRank(EnemyController enemy)
    {
        if (instance == null || enemy == null)
            return 0;

        return instance.RankInternal(enemy);
    }

    /// <summary>
    /// Düşmanın vuruşu gerçekleşince (veya saldırı iptal olunca) çağrılır.
    /// Birden fazla çağrılması zararsızdır.
    /// </summary>
    public static void ReleaseAttack(EnemyController enemy)
    {
        if (instance == null || enemy == null)
            return;

        instance.activeAttackers.Remove(enemy);
    }

    /// <summary>
    /// Oyuncu hasar aldığında çağrılır: ardışık vuruş yağmurunu keser.
    /// </summary>
    public static void NotifyPlayerHit()
    {
        if (instance == null)
            return;

        instance.nextAllowedHitTime =
            Mathf.Max(
                instance.nextAllowedHitTime,
                Time.time + instance.postHitGrace
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
        EnemyController enemy,
        float windupTime
    )
    {
        float now = Time.time;

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

        if (
            !activeAttackers.ContainsKey(enemy) &&
            activeAttackers.Count >= maxConcurrentAttackers
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

        return beatInterval * multiplier;
    }

    private void JoinInternal(EnemyController enemy)
    {
        float now = Time.time;

        PurgeStale(now);

        lastAsk[enemy] = now;

        if (!firstAsk.ContainsKey(enemy))
            firstAsk[enemy] = askCounter++;
    }

    private int RankInternal(EnemyController enemy)
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
        EnemyController enemy,
        float targetX
    )
    {
        return enemy.transform.position.x >= targetX
            ? 1
            : -1;
    }

    private bool IsFrontOfQueue(
        EnemyController enemy
    )
    {
        EnemyController front = null;
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
                Time.time - rangeTime > queueMemory
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
            nextAllowedHitTime - Time.time;

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