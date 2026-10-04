using UnityEngine;

/// <summary>
/// SERİ ÖLDÜRME. 'Window' saniye içinde art arda öldürmek seriyi büyütür:
///   - her öldürme vuruşları %3 güçlendirir (denge + can hasarı, en çok %30),
///   - her 5 öldürmede canın %3'ü dolar,
///   - süre dolunca seri ve bonus biter.
/// PlayerHud seriyi can barının üstünde gösterir.
///
/// KURULUM YOK: kendiliğinden oluşur.
/// </summary>
public class KillStreak : MonoBehaviour
{
    public static KillStreak Instance { get; private set; }

    [Tooltip("Bir sonraki öldürme için süre (sn).")]
    [Min(0.5f)] public float window = 3f;

    [Tooltip("Seri başına hasar bonusu (öldürme başına).")]
    [Range(0f, 0.2f)] public float damagePerKill = 0.03f;

    [Range(0f, 2f)] public float maxDamageBonus = 0.3f;

    [Tooltip("Her bu kadar öldürmede iyileş (0 = kapalı).")]
    [Min(0)] public int healEveryKills = 5;

    [Range(0f, 0.5f)] public float healPercent = 0.03f;

    public int Count { get; private set; }
    public int Best { get; private set; }

    public float DamageBonus { get; private set; }

    // 1 → 0: serinin kalan süresi (arayüz).
    public float TimeLeft01 =>
        Count > 0 ? Mathf.Clamp01(1f - (Time.time - lastKill) / window) : 0f;

    private float lastKill = -99f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<KillStreak>() != null)
            return;

        new GameObject("KillStreak").AddComponent<KillStreak>();
    }

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
        CombatEvents.EnemyKilled += OnKilled;
    }

    private void OnDisable()
    {
        CombatEvents.EnemyKilled -= OnKilled;
        EndStreak();
    }

    private void OnKilled(EnemyController enemy)
    {
        Count = Time.time - lastKill <= window ? Count + 1 : 1;
        lastKill = Time.time;

        Best = Mathf.Max(Best, Count);

        ApplyBonus();

        if (healEveryKills > 0 && Count % healEveryKills == 0)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            Health health = player != null ? player.GetComponent<Health>() : null;

            if (health != null && !health.IsDead && healPercent > 0f)
            {
                health.Heal(Mathf.Max(1, Mathf.RoundToInt(health.MaxHealth * healPercent)));

                CombatCallout.Popup(
                    player.transform.position + Vector3.up * 2.4f,
                    "SERİ ×" + Count + "  +" + Mathf.RoundToInt(healPercent * 100f) + "% CAN",
                    new Color(1f, 0.75f, 0.3f),
                    1f
                );
            }
        }
    }

    private void Update()
    {
        if (Count > 0 && Time.time - lastKill > window)
            EndStreak();
    }

    private void ApplyBonus()
    {
        PlayerStats stats = PlayerStats.Current;

        DamageBonus = Mathf.Min(maxDamageBonus, damagePerKill * Mathf.Max(0, Count - 1));

        if (stats == null)
            return;

        stats.RemoveModifiers(this);

        if (DamageBonus > 0f)
        {
            stats.AddModifier(this, StatType.HealthDamage, 0f, 1f + DamageBonus);
            stats.AddModifier(this, StatType.BalanceDamage, 0f, 1f + DamageBonus);
        }
    }

    private void EndStreak()
    {
        Count = 0;
        DamageBonus = 0f;

        PlayerStats stats = PlayerStats.Current;

        if (stats != null)
            stats.RemoveModifiers(this);
    }

    public static void ResetForRun()
    {
        if (Instance == null)
            return;

        Instance.EndStreak();
        Instance.Best = 0;
        Instance.lastKill = -99f;
    }
}
