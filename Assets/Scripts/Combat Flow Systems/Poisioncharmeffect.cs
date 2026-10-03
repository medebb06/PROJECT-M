using UnityEngine;

/// <summary>
/// Zehir charm'ı: oyuncunun her vuruşu düşmanı zehirler.
/// Zehir ZAMANLA önce düşmanın DENGESİNİ eritir (denge kırılırsa stagger),
/// denge zaten kırıksa CANINI eritir. Süre her vuruşta yenilenir.
///
/// İstif: saniyedeki hasar artar (baseRate + (istif-1) × ratePerExtraStack).
/// </summary>
[CreateAssetMenu(menuName = "Charms/Effect/Poison", fileName = "PoisonEffect")]
public class PoisonCharmEffect : CharmEffect
{
    [Tooltip("1 istifte saniyedeki hasar (denge/can birimi).")]
    public float baseRate = 0.7f;

    [Tooltip("Her ek istif için saniyedeki ek hasar.")]
    public float ratePerExtraStack = 0.5f;

    [Tooltip("Zehrin süresi (düşman saniyesi). Her vuruşta yenilenir.")]
    public float duration = 4f;

    private int stacks;
    private bool subscribed;

    private float Rate =>
        baseRate + Mathf.Max(0, stacks - 1) * ratePerExtraStack;

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

        // Zehir tikleri zaten bu yoldan geçmez, ama güvenli olsun.
        if (info.source == DamageSource.Poison)
            return;

        EnemyStatus.Get(enemy).ApplyPoison(Rate, duration);
    }

    public override string Describe(int newStacks)
    {
        float rate =
            baseRate + Mathf.Max(0, newStacks - 1) * ratePerExtraStack;

        return
            "Zehir: saniyede " + rate.ToString("0.0") +
            " hasar, " + duration.ToString("0.#") + " sn";
    }
}