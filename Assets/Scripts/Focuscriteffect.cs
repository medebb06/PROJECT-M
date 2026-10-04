using UnityEngine;

/// <summary>
/// ODAK (risk): hasar yemediğin sürece isabet eden her DÜZ vuruş (kombo
/// saldırısı; slam ve zehir sayılmaz) kritik şansını artırır. Bir tavanı
/// vardır. Gerçek bir vuruş yiyince (Kan Bedeli'nin block bedeli hariç)
/// birikim sıfırlanır.
///
/// İstif: vuruş başına artış aynı, TAVAN yükselir.
/// </summary>
[CreateAssetMenu(menuName = "Charms/Effect/Focus Crit", fileName = "FocusCritEffect")]
public class FocusCritEffect : CharmEffect
{
    [Tooltip("İsabet eden her düz vuruşta eklenen kritik şansı (0.02 = %2).")]
    [Range(0f, 0.2f)]
    public float critPerHit = 0.02f;

    [Tooltip("1 istifte birikimin tavanı (0.3 = +%30).")]
    [Range(0f, 1f)]
    public float maxBonus = 0.3f;

    [Tooltip("Her ek istifte tavana eklenen (0.1 = +%10).")]
    [Range(0f, 1f)]
    public float maxBonusPerExtraStack = 0.1f;

    private CharmContext context;
    private int stacks;
    private int hitCount;
    private bool subscribed;

    public override void Apply(CharmContext ctx, int newStacks)
    {
        context = ctx;
        stacks = newStacks;

        if (!subscribed)
        {
            CombatEvents.EnemyHit += OnEnemyHit;
            CombatEvents.PlayerDamaged += OnDamaged;
            subscribed = true;
        }

        Refresh();
    }

    public override void Remove(CharmContext ctx)
    {
        if (subscribed)
        {
            CombatEvents.EnemyHit -= OnEnemyHit;
            CombatEvents.PlayerDamaged -= OnDamaged;
        }

        if (ctx != null && ctx.stats != null)
            ctx.stats.RemoveModifiers(ctx.owner);

        subscribed = false;
        stacks = 0;
        hitCount = 0;
        context = null;
    }

    private float Cap(int s) =>
        maxBonus + maxBonusPerExtraStack * Mathf.Max(0, s - 1);

    private float Bonus =>
        Mathf.Min(Cap(stacks), critPerHit * hitCount);

    private void OnEnemyHit(
        EnemyController enemy,
        DamageInfo info,
        HitResult result
    )
    {
        if (!result.hit || info.source != DamageSource.Attack)
            return;

        // Tavandaysa saymaya gerek yok (taşma olmasın).
        if (critPerHit * hitCount >= Cap(stacks))
            return;

        hitCount++;
        Refresh();
    }

    private void OnDamaged(PlayerDamageReport report)
    {
        if (report.kind == PlayerHitKind.BlockCost)
            return;

        if (hitCount == 0)
            return;

        hitCount = 0;
        Refresh();
    }

    private void Refresh()
    {
        if (context == null || context.stats == null)
            return;

        context.stats.RemoveModifiers(context.owner);

        if (hitCount > 0)
        {
            context.stats.AddModifier(
                context.owner,
                StatType.CritChance,
                Bonus,
                1f
            );
        }
    }

    public override string Status()
    {
        return hitCount > 0
            ? "Odak +" + CharmUtil.Percent(Bonus)
            : "";
    }

    public override string Describe(int newStacks)
    {
        return
            "Hasar yemeden her düz vuruş kritik şansına +" +
            CharmUtil.Percent(critPerHit) + " (en fazla +" +
            CharmUtil.Percent(Cap(newStacks)) + "). Hasar yiyince sıfırlanır";
    }
}