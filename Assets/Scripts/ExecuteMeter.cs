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

    [Header("Dolma")]
    [Tooltip("Normal düşman öldürünce (3 öldürme ≈ dolu).")]
    [Range(0f, 1f)] public float killFill = 0.34f;

    [Tooltip("Kalabalık (zayıf) düşman öldürünce.")]
    [Range(0f, 1f)] public float swarmKillFill = 0.15f;

    [Tooltip("Dengeyi KIRAN parry.")]
    [Range(0f, 1f)] public float parryBreakFill = 0.08f;

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
    }

    private void OnDisable()
    {
        CombatEvents.EnemyKilled -= OnKilled;
        CombatEvents.ParrySucceeded -= OnParry;
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

        bool swarm = type != null && type.type == EnemyArchetypeType.Swarm;

        Add(swarm ? swarmKillFill : killFill);
    }

    private void OnParry(EnemyController enemy, bool brokeBalance)
    {
        if (brokeBalance)
            Add(parryBreakFill);
    }

    public void Add(float amount)
    {
        if (amount <= 0f)
            return;

        bool wasFull = IsFull;

        // Kalıcı gelişim: Cellat (dolum hızı).
        amount *= MetaProgress.ExecuteFillMultiplier;

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
