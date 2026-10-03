using UnityEngine;

/// <summary>
/// PARRY ÖDÜLÜ (riposte). Başarılı parry'den sonra kısa bir süre (veya birkaç
/// vuruş boyunca) oyuncunun vuruşları güçlenir:
///   - denge hasarı ve can hasarı çarpanı
///   - kritik şansı bonusu
/// Ayrıca parry, bir miktar posture iade eder.
///
/// Böylece parry hem güvenli hem EN KÂRLI eylem olur: vurarak saldırıyı
/// kesmek ya da hasar yiyip vurmak parry'den daha az kazandırır.
///
/// Değerler RunManager'dan ayarlanır (Configure). PlayerStats üzerinden
/// geçici değiştirici ekler, süre/vuruş bitince kaldırır; charm'larla çarpılır.
/// </summary>
public class ParryRiposte : MonoBehaviour
{
    // Arayüz için.
    public static bool IsActive { get; private set; }
    public static int HitsLeft { get; private set; }
    public static float TimeLeftFraction { get; private set; }

    private float duration = 2f;
    private int maxHits = 3;
    private float balanceMultiplier = 2f;
    private float healthMultiplier = 1.5f;
    private float critChanceBonus = 0.5f;
    private int postureRefund = 25;

    private PlayerStats stats;
    private PlayerPosture posture;

    private float endTime;
    private bool active;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration
    )]
    private static void ResetStatics()
    {
        IsActive = false;
        HitsLeft = 0;
        TimeLeftFraction = 0f;
    }

    public void Configure(
        float duration,
        int maxHits,
        float balanceMultiplier,
        float healthMultiplier,
        float critChanceBonus,
        int postureRefund
    )
    {
        this.duration = Mathf.Max(0.1f, duration);
        this.maxHits = Mathf.Max(1, maxHits);
        this.balanceMultiplier = Mathf.Max(1f, balanceMultiplier);
        this.healthMultiplier = Mathf.Max(1f, healthMultiplier);
        this.critChanceBonus = Mathf.Max(0f, critChanceBonus);
        this.postureRefund = Mathf.Max(0, postureRefund);
    }

    private void Awake()
    {
        posture = GetComponent<PlayerPosture>();
    }

    private void OnEnable()
    {
        CombatEvents.ParrySucceeded += OnParry;
        CombatEvents.EnemyHit += OnEnemyHit;
    }

    private void OnDisable()
    {
        CombatEvents.ParrySucceeded -= OnParry;
        CombatEvents.EnemyHit -= OnEnemyHit;

        End();
    }

    private void OnParry(EnemyController enemy, bool brokeBalance)
    {
        // Parry posture'ı iade eder: guard'ı korumak için ek bir ödül.
        if (posture != null && postureRefund > 0)
            posture.RecoverPosture(postureRefund);

        Begin();
    }

    private void Begin()
    {
        stats = PlayerStats.Current;

        if (stats == null)
            stats = GetComponent<PlayerStats>();

        if (stats == null)
            return;

        // Yenilenen parry pencereyi tazeler (üst üste binmez).
        stats.RemoveModifiers(this);

        stats.AddModifier(this, StatType.BalanceDamage, 0f, balanceMultiplier);
        stats.AddModifier(this, StatType.HealthDamage, 0f, healthMultiplier);
        stats.AddModifier(this, StatType.CritChance, critChanceBonus, 1f);

        endTime = Time.time + duration;

        active = true;

        IsActive = true;
        HitsLeft = maxHits;
        TimeLeftFraction = 1f;
    }

    // Her isabetli vuruş bir hak tüketir. Olay, hasar HESAPLANDIKTAN sonra
    // gelir; yani son hak da güçlendirilmiş vuruş olarak sayılır.
    private void OnEnemyHit(
        EnemyController enemy,
        DamageInfo info,
        HitResult result
    )
    {
        if (!active || !result.hit)
            return;

        if (info.source == DamageSource.Poison)
            return;

        HitsLeft--;

        if (HitsLeft <= 0)
            End();
    }

    private void Update()
    {
        if (!active)
            return;

        if (Time.time >= endTime)
        {
            End();
            return;
        }

        TimeLeftFraction = (endTime - Time.time) / duration;
    }

    private void End()
    {
        if (stats != null)
            stats.RemoveModifiers(this);

        active = false;

        IsActive = false;
        HitsLeft = 0;
        TimeLeftFraction = 0f;
    }
}