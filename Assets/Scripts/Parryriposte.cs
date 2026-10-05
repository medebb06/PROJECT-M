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
///
/// Charm'lar riposte'u şu stat'larla değiştirir:
///   RiposteHits (+vuruş), RiposteDuration (+sn),
///   RiposteStrength (bonusların çarpanı: 2x denge bonusu ×1.5 → 2.5x).
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
    private float activeDuration;
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

        // Charm katkıları (kendi değiştiricilerimiz kaldırıldıktan SONRA oku).
        float strength =
            Mathf.Max(0f, stats.Get(StatType.RiposteStrength, 1f));

        int hits =
            Mathf.Max(
                1,
                maxHits +
                Mathf.RoundToInt(stats.Get(StatType.RiposteHits, 0f))
            );

        activeDuration =
            Mathf.Max(
                0.1f,
                duration + stats.Get(StatType.RiposteDuration, 0f)
            );

        // Güç, çarpanın BONUS kısmını ölçekler (2x → 1 + 1×güç).
        float balanceMult = 1f + (balanceMultiplier - 1f) * strength;
        float healthMult = 1f + (healthMultiplier - 1f) * strength;
        float critBonus = critChanceBonus * strength;

        stats.AddModifier(this, StatType.BalanceDamage, 0f, balanceMult);
        stats.AddModifier(this, StatType.HealthDamage, 0f, healthMult);
        stats.AddModifier(this, StatType.CritChance, critBonus, 1f);

        endTime = Time.time + activeDuration;

        active = true;

        IsActive = true;
        HitsLeft = hits;
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

        // Zehir ve yetenek vuruşları riposte hakkı yemez.
        if (info.source == DamageSource.Poison || info.source == DamageSource.Ability)
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

        TimeLeftFraction = (endTime - Time.time) / activeDuration;
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
