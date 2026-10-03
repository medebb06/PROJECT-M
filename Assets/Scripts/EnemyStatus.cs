using UnityEngine;

/// <summary>
/// Düşman üzerindeki DURUM ETKİLERİ (şimdilik zehir). Düşmanda yoksa
/// EnemyStatus.Get(enemy) ile kendiliğinden eklenir.
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

    private EnemyController enemy;
    private EnemyBalance balance;
    private Health health;

    private float balanceFraction;   // max dengenin yüzdesi / sn
    private float healthFraction;    // max canın yüzdesi / sn
    private float poisonTimeLeft;
    private float accumulator;

    public bool IsPoisoned => poisonTimeLeft > 0f;

    public static EnemyStatus Get(EnemyController enemy)
    {
        EnemyStatus status =
            enemy.GetComponent<EnemyStatus>();

        if (status == null)
            status = enemy.gameObject.AddComponent<EnemyStatus>();

        return status;
    }

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        balance = GetComponent<EnemyBalance>();
        health = GetComponent<Health>();
    }

    // Her vuruşta süre yenilenir; hızlar en son uygulanan değerdir.
    public void ApplyPoison(
        float balanceFractionPerSecond,
        float healthFractionPerSecond,
        float duration
    )
    {
        balanceFraction = Mathf.Max(0f, balanceFractionPerSecond);
        healthFraction = Mathf.Max(0f, healthFractionPerSecond);

        poisonTimeLeft = Mathf.Max(poisonTimeLeft, duration);
    }

    private void Update()
    {
        if (poisonTimeLeft <= 0f)
            return;

        if (enemy == null || enemy.IsDead)
        {
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

    private void Tick(int amount)
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

        enemy.PlayTintFlash(PoisonTint, 0.15f);
    }
}