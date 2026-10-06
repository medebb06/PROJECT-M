using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// İNFAZ BARI. Düşman öldürdükçe dolar; DOLUYKEN sersemlemiş düşmana E:
///   - normal düşman: TEK VURUŞTA ölür,
///   - boss: faz 1'de canı faz 2 eşiğine iner, faz 2'de ölür.
/// Bar boşken E çalışmaz: sersemlemiş düşmanı normal vuruşlarla bitirirsin
/// (sersemlemiş düşmana vuruşlar 'Staggered Health Multiplier' kat sert).
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

    [Header("Dolma")]
    [Tooltip("Normal düşman öldürünce (4 öldürme ≈ dolu).")]
    [Range(0f, 1f)] public float killFill = 0.25f;

    [Tooltip("Kalabalık (zayıf) düşman öldürünce.")]
    [Range(0f, 1f)] public float swarmKillFill = 0.1f;

    [Tooltip("Dengeyi KIRAN parry.")]
    [Range(0f, 1f)] public float parryBreakFill = 0.06f;

    [Header("Boss (öldürme olmadığı için ayrı kaynaklar)")]
    [Tooltip("Boss'un dengesini kırmak (vuruş ya da parry).")]
    [Range(0f, 1f)] public float bossBreakFill = 0.4f;

    [Tooltip("Boss'a her parry.")]
    [Range(0f, 1f)] public float bossParryFill = 0.05f;

    [Tooltip("Boss'a verilen can hasarı × bu = dolum (max canın %50'si → 0.3).")]
    [Range(0f, 2f)] public float bossHealthDamageFill = 0.5f;

    [Tooltip("Koşu başında bar dolu başlasın (ilk infazı öğretmek için).")]
    public bool startFull = true;

    [Header("Öldürmeyi kolaylaştır")]
    [Tooltip("Sersemlemiş (dengesi kırık) düşmana vuruşların CAN hasarı çarpanı.")]
    [Min(1f)] public float staggeredHealthMultiplier = 2f;

    [Tooltip("Bar dolu değilken E'ye basınca uyarı yazısı.")]
    public string notReadyText = "İNFAZ BARI DOLU DEĞİL";

    public float Fill { get; private set; }

    public bool IsFull => Fill >= 0.999f;

    // Arayüz: son dolma zamanı (parlama efekti için).
    public float LastFilledTime { get; private set; } = -99f;

    private static readonly HashSet<EnemyController> lethal = new HashSet<EnemyController>();
    private static EnemyController lastExecuted;
    private float nextWarn;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        CharmFillMultiplier = 1f;
        lethal.Clear();
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
    }

    private void OnDisable()
    {
        CombatEvents.EnemyKilled -= OnKilled;
        CombatEvents.ParrySucceeded -= OnParry;
        CombatEvents.EnemyHit -= OnEnemyHit;
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

    private void OnParry(EnemyController enemy, bool brokeBalance)
    {
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
        if (!result.hit || !IsBoss(enemy))
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

        bool wasFull = IsFull;

        // Kalıcı gelişim: Cellat (dolum hızı). Charm: İnfazcı.
        amount *= MetaProgress.ExecuteFillMultiplier * CharmFillMultiplier;

        Fill = Mathf.Clamp01(Fill + amount);

        if (!wasFull && IsFull)
        {
            LastFilledTime = Time.unscaledTime;

            PlayerController player = FindFirstObjectByType<PlayerController>();

            if (player != null)
            {
                CombatCallout.Popup(
                    player.transform.position + Vector3.up * 2.6f,
                    "İNFAZ HAZIR",
                    new Color(1f, 0.82f, 0.3f),
                    1f
                );
            }
        }
    }

    // =========================================================
    // STATİK API
    // =========================================================

    public static bool CanExecute => Instance == null || Instance.IsFull;

    public static float StaggeredHealthMultiplier =>
        Instance != null ? Instance.staggeredHealthMultiplier : 1f;

    /// <summary>Bar dolu: harca, hedefi "ölümcül infaz" olarak işaretle.</summary>
    public static bool TryConsume(EnemyController target)
    {
        if (Instance == null)
            return true;

        if (!Instance.IsFull)
            return false;

        Instance.Fill = 0f;

        if (target != null)
        {
            lethal.Add(target);
            lastExecuted = target;
        }

        return true;
    }

    /// <summary>EnemyExecuteState: bu infaz ölümcül mü (bir kez).</summary>
    public static bool TakeLethal(EnemyController target)
    {
        return target != null && lethal.Remove(target);
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
        lethal.Clear();
        lastExecuted = null;

        if (Instance != null)
            Instance.Fill = Instance.startFull ? 1f : 0f;
    }
}
