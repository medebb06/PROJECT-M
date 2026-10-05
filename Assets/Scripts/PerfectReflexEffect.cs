using UnityEngine;

/// <summary>
/// EFSANEVİ · KUSURSUZ REFLEKS (risk): parry penceresi %30 DARALIR; ama her
/// başarılı parry'de riposte ×1.6 güçlenir ve yeteneğin (Q) 3 sn dolar.
/// </summary>
[CreateAssetMenu(menuName = "Charms/Effect/Perfect Reflex", fileName = "PerfectReflexEffect")]
public class PerfectReflexEffect : CharmEffect
{
    [Range(0.3f, 1f)] public float windowMultiplier = 0.7f;
    public float riposteStrength = 1.6f;
    public float abilityCharge = 3f;

    private CharmContext context;
    private bool subscribed;

    public override void Apply(CharmContext ctx, int stacks)
    {
        context = ctx;

        if (ctx.stats != null)
        {
            ctx.stats.RemoveModifiers(ctx.owner);
            ctx.stats.AddModifier(ctx.owner, StatType.ParryWindow, 0f, windowMultiplier);
            ctx.stats.AddModifier(ctx.owner, StatType.RiposteStrength, 0f, riposteStrength);
        }

        if (subscribed)
            return;

        CombatEvents.ParrySucceeded += OnParry;
        subscribed = true;
    }

    public override void Remove(CharmContext ctx)
    {
        if (subscribed)
            CombatEvents.ParrySucceeded -= OnParry;

        if (ctx != null && ctx.stats != null)
            ctx.stats.RemoveModifiers(ctx.owner);

        subscribed = false;
        context = null;
    }

    private void OnParry(EnemyController enemy, bool brokeBalance)
    {
        if (PlayerAbility.Instance != null)
            PlayerAbility.Instance.ReduceCooldown(abilityCharge);
    }

    public override string Describe(int stacks)
    {
        return "Parry penceresi ×" + windowMultiplier.ToString("0.0#") + " (daha zor). Riposte ×" + riposteStrength.ToString("0.0#") + ", her parry yetenek beklemesini " + abilityCharge.ToString("0") + " sn kısaltır.";
    }
}
