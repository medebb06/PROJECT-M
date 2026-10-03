using UnityEngine;

/// <summary>
/// KESİNTİSİZ RİTİM (parry): hasar yemeden yapılan her parry bir kademe
/// ekler; her kademe denge hasarını artırır. Gerçek bir vuruş yiyince
/// (Kan Bedeli'nin block bedeli hariç) sayaç sıfırlanır.
/// </summary>
[CreateAssetMenu(menuName = "Charms/Effect/Parry Rhythm", fileName = "ParryRhythmEffect")]
public class ParryRhythmEffect : CharmEffect
{
    [Min(1)]
    public int maxTier = 5;

    [Tooltip("Kademe ve istif başına denge hasarı bonusu (0.08 = %8).")]
    public float balanceBonusPerTierPerStack = 0.08f;

    private CharmContext context;
    private int stacks;
    private int tier;
    private bool subscribed;

    public override void Apply(CharmContext ctx, int newStacks)
    {
        context = ctx;
        stacks = newStacks;

        if (!subscribed)
        {
            CombatEvents.ParrySucceeded += OnParry;
            CombatEvents.PlayerDamaged += OnDamaged;
            subscribed = true;
        }

        Refresh();
    }

    public override void Remove(CharmContext ctx)
    {
        if (subscribed)
        {
            CombatEvents.ParrySucceeded -= OnParry;
            CombatEvents.PlayerDamaged -= OnDamaged;
        }

        if (ctx != null && ctx.stats != null)
            ctx.stats.RemoveModifiers(ctx.owner);

        subscribed = false;
        stacks = 0;
        tier = 0;
        context = null;
    }

    private void OnParry(EnemyController enemy, bool brokeBalance)
    {
        tier = Mathf.Min(maxTier, tier + 1);
        Refresh();
    }

    private void OnDamaged(PlayerDamageReport report)
    {
        if (report.kind == PlayerHitKind.BlockCost)
            return;

        if (tier == 0)
            return;

        tier = 0;
        Refresh();
    }

    private float Bonus(int t, int s) =>
        balanceBonusPerTierPerStack * t * s;

    private void Refresh()
    {
        if (context == null || context.stats == null)
            return;

        context.stats.RemoveModifiers(context.owner);

        if (tier > 0)
        {
            context.stats.AddModifier(
                context.owner,
                StatType.BalanceDamage,
                0f,
                1f + Bonus(tier, stacks)
            );
        }
    }

    public override string Status()
    {
        return tier > 0 ? "Ritim " + tier + "/" + maxTier : "";
    }

    public override string Describe(int newStacks)
    {
        return
            "Her ardışık parry denge hasarına +" +
            CharmUtil.Percent(Bonus(1, newStacks)) +
            " (en fazla " + maxTier + " kademe = +" +
            CharmUtil.Percent(Bonus(maxTier, newStacks)) +
            "). Hasar yiyince sıfırlanır";
    }
}
