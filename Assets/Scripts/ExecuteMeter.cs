using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// İNFAZ BARI (3 PARÇALI). İyi oynadıkça dolar (parry, kusursuz kaçış, öldürme, vuruş).
/// E'yi ne kadar basılı tutarsan o kadar PARÇA harcarsın, o kadar çok hasar vurursun:
///   - normal düşman: 1 parça = canının yarısı, 2+ parça = öldürür,
///   - boss: parça sayısına göre canın %'si (BossController.ExecutePercentFor).
/// Tutturamazsan harcadığın parçalar boşa gider (kalanlar durur).
///
/// KURULUM YOK: sahne açılınca kendiliğinden oluşur. Ayarlamak istersen
/// sahnede bir objeye ekle, değerleri oradan değiştir.
/// </summary>
[DefaultExecutionOrder(-50)]
public class ExecuteMeter : MonoBehaviour
{
    public static ExecuteMeter Instance { get; private set; }

    // İnfazcı charm'ı koyar (1 = etkisiz).
    public static float CharmFillMultiplier = 1f;

    /// <summary>Barın parça sayısı. Her parça bir infaz hakkıdır.</summary>
    public const int Segments = 3;

    [Header("Dolma (barın tamamı = 1, bir parça ≈ 0.333)")]
    [Tooltip("Normal düşman öldürünce (4 öldürme ≈ dolu).")]
    [Range(0f, 1f)] public float killFill = 0.2f;

    [Tooltip("Kalabalık (zayıf) düşman öldürünce.")]
    [Range(0f, 1f)] public float swarmKillFill = 0.06f;

    [Tooltip("Dengeyi KIRAN parry.")]
    [Range(0f, 1f)] public float parryBreakFill = 0.1f;

    [Tooltip("Her başarılı parry.")]
    [Range(0f, 1f)] public float parryFill = 0.05f;

    [Tooltip("Kusursuz kaçış (dash ile saldırıdan kurtulma).")]
    [Range(0f, 1f)] public float dodgeFill = 0.06f;

    [Tooltip("Düşmana isabet eden her vuruş (az: savunma daha çok verir).")]
    [Range(0f, 1f)] public float hitFill = 0.012f;

    [Header("Boss (öldürme olmadığı için ayrı kaynaklar)")]
    [Tooltip("Boss'un dengesini kırmak (vuruş ya da parry).")]
    [Range(0f, 1f)] public float bossBreakFill = 0.3f;

    [Tooltip("Boss'a her parry.")]
    [Range(0f, 1f)] public float bossParryFill = 0.06f;

    [Tooltip("Boss'a verilen can hasarı × bu = dolum (max canın %50'si → 0.3).")]
    [Range(0f, 2f)] public float bossHealthDamageFill = 0.6f;

    [Tooltip("Koşu başında bar dolu başlasın (ilk infazı öğretmek için).")]
    public bool startFull = true;

    [Header("Öldürmeyi kolaylaştır")]
    [Tooltip("Sersemlemiş (dengesi kırık) düşmana vuruşların CAN hasarı çarpanı.")]
    [Min(1f)] public float staggeredHealthMultiplier = 2f;

    [Tooltip("Bar dolu değilken E'ye basınca uyarı yazısı.")]
    public string notReadyText = "İNFAZ BARI DOLU DEĞİL";

    public float Fill { get; private set; }

    public bool IsFull => Fill >= 0.999f;

    /// <summary>Tamamen dolmuş parça sayısı (0..3).</summary>
    public int FullSegments => Mathf.Clamp(Mathf.FloorToInt(Fill * Segments + 0.001f), 0, Segments);

    // Arayüz: son dolma zamanı (parlama efekti için).
    public float LastFilledTime { get; private set; } = -99f;

    // Hedef → o infaza harcanan parça sayısı.
    private static readonly Dictionary<EnemyController, int> power = new Dictionary<EnemyController, int>();
    private static EnemyController lastExecuted;
    private float nextWarn;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        CharmFillMultiplier = 1f;
        power.Clear();
        lastExecuted = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<ExecuteMeter>() != null)
            return;

        new GameObject("ExecuteMeter").AddComponent<ExecuteMeter>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        Fill = startFull ? 1f : 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        CombatEvents.EnemyKilled += OnKilled;
        CombatEvents.ParrySucceeded += OnParry;
        CombatEvents.EnemyHit += OnEnemyHit;
        CombatEvents.Dodged += OnDodged;
    }

    private void OnDisable()
    {
        CombatEvents.EnemyKilled -= OnKilled;
        CombatEvents.ParrySucceeded -= OnParry;
        CombatEvents.EnemyHit -= OnEnemyHit;
        CombatEvents.Dodged -= OnDodged;
    }

    private void OnKilled(EnemyController enemy)
    {
        // İnfazla öldürülen düşman barı geri doldurmaz (zincirleme olmasın).
        if (enemy != null && enemy == lastExecuted)
        {
            lastExecuted = null;
            return;
        }

        EnemyArchetype type = enemy != null ? enemy.GetComponent<EnemyArchetype>() : null;

        bool swarm =
            type != null &&
            (type.type == EnemyArchetypeType.Swarm || type.type == EnemyArchetypeType.Bomber);

        Add(swarm ? swarmKillFill : killFill);
    }

    private void OnDodged(EnemyController enemy, bool unblockable)
    {
        Add(dodgeFill);
    }

    private void OnParry(EnemyController enemy, bool brokeBalance)
    {
        Add(parryFill);

        if (brokeBalance)
            Add(parryBreakFill);

        if (IsBoss(enemy))
        {
            Add(bossParryFill);

            if (brokeBalance)
                Add(bossBreakFill);
        }
    }

    // Boss: denge kırma + can hasarı barı doldurur.
    private void OnEnemyHit(EnemyController enemy, DamageInfo info, HitResult result)
    {
        if (!result.hit)
            return;

        // Her isabet az da olsa doldurur (savunma çok daha fazla verir).
        Add(hitFill);

        if (!IsBoss(enemy))
            return;

        if (result.brokeBalance)
            Add(bossBreakFill);

        if (!result.onBalance && result.amount > 0)
        {
            Health h = enemy.GetComponent<Health>();

            if (h != null && h.MaxHealth > 0)
                Add((float)result.amount / h.MaxHealth * bossHealthDamageFill);
        }
    }

    private static bool IsBoss(EnemyController enemy)
    {
        return enemy != null && enemy.GetComponent<BossController>() != null;
    }

    public void Add(float amount)
    {
        if (amount <= 0f)
            return;

        int segsBefore = FullSegments;

        // Kalıcı gelişim: Cellat (dolum hızı). Charm: İnfazcı.
        amount *= MetaProgress.ExecuteFillMultiplier * CharmFillMultiplier;

        Fill = Mathf.Clamp01(Fill + amount);

        if (FullSegments > segsBefore)
        {
            LastFilledTime = Time.unscaledTime;

            PlayerController player = FindFirstObjectByType<PlayerController>();

            if (player != null)
            {
                int n = FullSegments;

                CombatCallout.Popup(
                    player.transform.position + Vector3.up * 2.6f,
                    n >= Segments ? "İNFAZ ×" + n + " (DOLU)" : "İNFAZ ×" + n,
                    new Color(1f, 0.82f, 0.3f),
                    1f
                );
            }
        }
    }

    // =========================================================
    // STATİK API
    // =========================================================

    /// <summary>En az bir parça dolu mu? (infaz denenebilir)</summary>
    public static bool CanExecute => Instance == null || Instance.FullSegments >= 1;

    public static float StaggeredHealthMultiplier =>
        Instance != null ? Instance.staggeredHealthMultiplier : 1f;

    /// <summary>Doluysa 'segs' parça harca ve hedefi o güçle işaretle. Yetmezse false.</summary>
    public static bool TrySpend(EnemyController target, int segs)
    {
        segs = Mathf.Clamp(segs, 1, Segments);

        if (Instance != null)
        {
            if (Instance.FullSegments < segs)
                return false;

            Instance.Fill = Mathf.Max(0f, Instance.Fill - segs / (float)Segments);
        }

        if (target != null)
        {
            power[target] = segs;
            lastExecuted = target;
        }

        return true;
    }

    /// <summary>Eski çağrı: dolu olan tüm parçaları harca.</summary>
    public static bool TryConsume(EnemyController target)
    {
        int segs = Instance != null ? Instance.FullSegments : Segments;

        if (segs < 1)
            return false;

        return TrySpend(target, segs);
    }

    /// <summary>EnemyExecuteState: bu infaza harcanan parça sayısı (bir kez; yoksa 0).</summary>
    public static int TakePower(EnemyController target)
    {
        if (target == null || !power.TryGetValue(target, out int segs))
            return 0;

        power.Remove(target);

        return segs;
    }

    /// <summary>Eski API: bu infaz işaretli miydi (bir kez).</summary>
    public static bool TakeLethal(EnemyController target)
    {
        return TakePower(target) > 0;
    }

    public static void WarnNotReady(EnemyController target)
    {
        if (Instance == null || Time.unscaledTime < Instance.nextWarn)
            return;

        Instance.nextWarn = Time.unscaledTime + 0.9f;

        Vector3 at =
            target != null
                ? target.transform.position + Vector3.up * 2.4f
                : Vector3.zero;

        CombatCallout.Popup(at, Instance.notReadyText, new Color(0.75f, 0.75f, 0.8f), 0.8f);
    }

    /// <summary>Yeni koşu.</summary>
    public static void ResetForRun()
    {
        power.Clear();
        lastExecuted = null;

        if (Instance != null)
            Instance.Fill = Instance.startFull ? 1f : 0f;
    }
}
