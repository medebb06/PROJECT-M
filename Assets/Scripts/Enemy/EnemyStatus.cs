using UnityEngine;

/// <summary>
/// Düşman üzerindeki DURUM ETKİLERİ (şimdilik zehir). Düşmanda yoksa
/// EnemyStatus.Get(enemy) ile kendiliğinden eklenir.
///
/// Zehir, EnemyTime saatiyle akar: parry slow-mo'sunda zehir de yavaşlar.
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

    private float poisonRate;
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

    // Her vuruşta süre yenilenir; hız en son uygulanan değerdir.
    public void ApplyPoison(float ratePerSecond, float duration)
    {
        poisonRate = Mathf.Max(0f, ratePerSecond);
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

        // Kesirli hasar birikir; tam sayıya ulaşınca uygulanır.
        accumulator += poisonRate * dt;

        while (accumulator >= 1f)
        {
            accumulator -= 1f;

            Tick(1);
        }

        if (poisonTimeLeft <= 0f)
        {
            poisonTimeLeft = 0f;
            poisonRate = 0f;
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