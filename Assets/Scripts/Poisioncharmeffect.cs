using UnityEngine;

/// <summary>
/// Zehir charm'ı: oyuncunun her vuruşu düşmanı zehirler.
/// Zehir ZAMANLA önce düşmanın DENGESİNİ eritir (denge kırılırsa stagger),
/// denge zaten kırıksa CANINI eritir. Süre her vuruşta yenilenir.
///
/// Hasar, düşmanın MAKSİMUM değerinin YÜZDESİ olarak verilir; yani denge/can
/// ölçeği 7 de olsa 100 de olsa aynı etkiyi yapar.
///
/// İstif: şiddet = 1 + (istif - 1) × extraStrengthPerStack
/// </summary>
[CreateAssetMenu(menuName = "Charms/Effect/Poison", fileName = "PoisonEffect")]
public class PoisonCharmEffect : CharmEffect
{
    [Tooltip("1 istifte saniyede, düşmanın MAX dengesinin yüzdesi (0.05 = %5).")]
    [Range(0f, 1f)]
    public float balancePercentPerSecond = 0.05f;

    [Tooltip("Denge kırıkken saniyede, düşmanın MAX canının yüzdesi (0.015 = %1.5).")]
    [Range(0f, 1f)]
    public float healthPercentPerSecond = 0.015f;

    [Tooltip("Her ek istif şiddete bu kadar ekler (0.6 = taban hızın %60'ı).")]
    public float extraStrengthPerStack = 0.6f;

    [Tooltip("Zehrin süresi (düşman saniyesi). Her vuruşta yenilenir.")]
    public float duration = 4f;

    private int stacks;
    private bool subscribed;

    private float Strength(int s)
    {
        return 1f + Mathf.Max(0, s - 1) * extraStrengthPerStack;
    }

    public override void Apply(CharmContext context, int newStacks)
    {
        stacks = newStacks;

        if (subscribed)
            return;

        CombatEvents.EnemyHit += OnEnemyHit;

        subscribed = true;
    }

    public override void Remove(CharmContext context)
    {
        if (subscribed)
            CombatEvents.EnemyHit -= OnEnemyHit;

        subscribed = false;
        stacks = 0;
    }

    private void OnEnemyHit(
        EnemyController enemy,
        DamageInfo info,
        HitResult result
    )
    {
        if (!result.hit || enemy == null)
            return;

        if (info.source == DamageSource.Poison)
            return;

        float strength = Strength(stacks);

        EnemyStatus.Get(enemy).ApplyPoison(
            balancePercentPerSecond * strength,
            healthPercentPerSecond * strength,
            duration
        );
    }

    public override string Describe(int newStacks)
    {
        float strength = Strength(newStacks);

        return
            "Zehir: saniyede %" +
            (balancePercentPerSecond * strength * 100f).ToString("0.#") +
            " denge (sersemleyince %" +
            (healthPercentPerSecond * strength * 100f).ToString("0.#") +
            " can), " + duration.ToString("0.#") + " sn";
    }
}