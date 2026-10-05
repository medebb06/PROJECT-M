using UnityEngine;

/// <summary>
/// Düşman üzerindeki DURUM ETKİLERİ (zehir) ve charm'ların düşmana özel
/// verisi (execute çarpanı). Düşmanda yoksa EnemyStatus.Get(enemy) ile
/// kendiliğinden eklenir.
///
/// Zehir, EnemyTime saatiyle akar: parry slow-mo'sunda zehir de yavaşlar.
/// Hasar düşmanın MAX değerinin yüzdesi olarak verilir (ölçekten bağımsız).
///
/// Zehir tikleri PlayerDamage hattından GEÇMEZ (vuruş tepkisi, savrulma ve
/// ses üretmez, düşmanın saldırısını kesmez); doğrudan denge/can azaltır.
/// Kural: denge kırık değilse DENGEYİ, kırıksa CANI eritir.
/// </summary>
[RequireComponent(typeof(EnemyController))]
public class EnemyStatus : MonoBehaviour
{
    private static readonly Color PoisonTint =
        new Color(0.45f, 1f, 0.35f);

    private static readonly Color BurnTint =
        new Color(1f, 0.5f, 0.15f);

    // Felç Edici Zehir: zehirli düşmanın saldırı uyarısı bu çarpanla uzar.
    // Charm koyar / kaldırır. 1 = etkisiz.
    public static float PoisonedWindupMultiplier = 1f;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration
    )]
    private static void ResetStatics()
    {
        PoisonedWindupMultiplier = 1f;
    }

    private EnemyController enemy;
    private EnemyBalance balance;
    private Health health;

    private float balanceFraction;   // max dengenin yüzdesi / sn
    private float healthFraction;    // max canın yüzdesi / sn
    private float poisonTimeLeft;
    private float accumulator;

    // YANIK (Alev Dalgası yeteneği): zehirden AYRI sayaç, aynı kural
    // (denge kırık değilse dengeyi, kırıksa canı eritir).
    private float burnBalanceFraction;
    private float burnHealthFraction;
    private float burnTimeLeft;
    private float burnAccumulator;

    public bool IsPoisoned => poisonTimeLeft > 0f;
    public bool IsBurning => burnTimeLeft > 0f;

    // Ölürken zehirli miydi? (Salgın charm'ı için.)
    public bool DiedPoisoned { get; private set; }

    // Şu anki zehir hızları (yayılma için).
    public float PoisonBalanceRate => balanceFraction;
    public float PoisonHealthRate => healthFraction;

    // Ezici Parry: bir sonraki execute'un hasar çarpanı.
    // Stagger başlarken 1'e döner; execute kullanınca sıfırlanır.
    public float ExecuteMultiplier { get; set; } = 1f;

    public static EnemyStatus Get(EnemyController enemy)
    {
        EnemyStatus status =
            enemy.GetComponent<EnemyStatus>();

        if (status == null)
            status = enemy.gameObject.AddComponent<EnemyStatus>();

        return status;
    }

    // Saldırı uyarısı çarpanı (zehirli değilse 1).
    public static float WindupMultiplierFor(EnemyController enemy)
    {
        if (enemy == null || PoisonedWindupMultiplier <= 1f)
            return 1f;

        EnemyStatus status = enemy.GetComponent<EnemyStatus>();

        return status != null && status.IsPoisoned
            ? PoisonedWindupMultiplier
            : 1f;
    }

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        balance = GetComponent<EnemyBalance>();
        health = GetComponent<Health>();
    }

    // Her vuruşta süre yenilenir; hızlar en güçlü olan tutulur
    // (zayıf bir kaynak — ör. yayılan zehir — güçlü zehri ezmesin).
    public void ApplyPoison(
        float balanceFractionPerSecond,
        float healthFractionPerSecond,
        float duration
    )
    {
        if (enemy != null && enemy.IsDead)
            return;

        if (!IsPoisoned)
        {
            balanceFraction = 0f;
            healthFraction = 0f;
        }

        balanceFraction =
            Mathf.Max(balanceFraction, balanceFractionPerSecond);

        healthFraction =
            Mathf.Max(healthFraction, healthFractionPerSecond);

        poisonTimeLeft = Mathf.Max(poisonTimeLeft, duration);
    }

    // Yanık: süre yenilenir, en güçlü hız tutulur.
    public void ApplyBurn(
        float balanceFractionPerSecond,
        float healthFractionPerSecond,
        float duration
    )
    {
        if (enemy != null && enemy.IsDead)
            return;

        if (!IsBurning)
        {
            burnBalanceFraction = 0f;
            burnHealthFraction = 0f;
        }

        burnBalanceFraction = Mathf.Max(burnBalanceFraction, balanceFractionPerSecond);
        burnHealthFraction = Mathf.Max(burnHealthFraction, healthFractionPerSecond);
        burnTimeLeft = Mathf.Max(burnTimeLeft, duration);
    }

    private void Update()
    {
        if (burnTimeLeft > 0f)
            UpdateBurn();

        if (poisonTimeLeft <= 0f)
            return;

        if (enemy == null || enemy.IsDead)
        {
            // Hızlar silinmez: Salgın ölüm anındaki zehri yayabilsin.
            DiedPoisoned = true;
            poisonTimeLeft = 0f;
            return;
        }

        float dt = EnemyTime.DeltaTime;

        poisonTimeLeft -= dt;

        // Şu an hangi katmandayız? Hız o katmanın max değerine göre.
        bool onBalance =
            balance != null && !balance.IsBroken;

        float perSecond;

        if (onBalance)
        {
            perSecond = balance.MaxBalance * balanceFraction;
        }
        else if (health != null && !health.IsDead)
        {
            perSecond = health.MaxHealth * healthFraction;
        }
        else
        {
            perSecond = 0f;
        }

        // Kesirli hasar birikir; tam sayıya ulaşınca uygulanır.
        accumulator += perSecond * dt;

        while (accumulator >= 1f)
        {
            accumulator -= 1f;

            Tick(1);
        }

        if (poisonTimeLeft <= 0f)
        {
            poisonTimeLeft = 0f;
            balanceFraction = 0f;
            healthFraction = 0f;
            accumulator = 0f;
        }
    }

    private void UpdateBurn()
    {
        if (enemy == null || enemy.IsDead)
        {
            burnTimeLeft = 0f;
            return;
        }

        float dt = EnemyTime.DeltaTime;

        burnTimeLeft -= dt;

        bool onBalance = balance != null && !balance.IsBroken;

        float perSecond =
            onBalance
                ? balance.MaxBalance * burnBalanceFraction
                : (health != null && !health.IsDead ? health.MaxHealth * burnHealthFraction : 0f);

        burnAccumulator += perSecond * dt;

        while (burnAccumulator >= 1f)
        {
            burnAccumulator -= 1f;

            Tick(1, BurnTint);
        }

        if (burnTimeLeft <= 0f)
        {
            burnTimeLeft = 0f;
            burnBalanceFraction = 0f;
            burnHealthFraction = 0f;
            burnAccumulator = 0f;
        }
    }

    private void Tick(int amount)
    {
        Tick(amount, PoisonTint);
    }

    private void Tick(int amount, Color tint)
    {
        if (balance != null && !balance.IsBroken)
        {
            // Denge kırılırsa EnemyBalance olayı stagger'ı zaten başlatır.
            balance.AddBalanceDamage(amount);
        }
        else if (health != null && !health.IsDead)
        {
            health.TakeDamage(amount);
        }

        enemy.PlayTintFlash(tint, 0.15f);
    }
}
