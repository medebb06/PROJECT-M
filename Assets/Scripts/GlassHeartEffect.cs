using UnityEngine;

/// <summary>
/// EFSANEVİ · CAM KALP (risk): max can %30 AZALIR; o odada her öldürme
/// hasarı +%10 artırır (en çok +%60). Yeni odada sıfırlanır.
/// </summary>
[CreateAssetMenu(menuName = "Charms/Effect/Glass Heart", fileName = "GlassHeartEffect")]
public class GlassHeartEffect : CharmEffect
{
    [Range(0.3f, 1f)] public float maxHealthMultiplier = 0.7f;
    public float bonusPerKill = 0.1f;
    public float maxBonus = 0.6f;

    private CharmContext context;
    private bool subscribed;
    private int originalMax;
    private float bonus;
    private readonly object bonusOwner = new object();

    public override void Apply(CharmContext ctx, int stacks)
    {
        context = ctx;

        if (subscribed)
            return;

        Health h = ctx.playerHealth;

        if (h != null)
        {
            originalMax = h.MaxHealth;

            int newMax = Mathf.Max(1, Mathf.RoundToInt(originalMax * maxHealthMultiplier));
            int current = Mathf.Min(h.CurrentHealth, newMax);

            h.SetMaxHealth(newMax, false);
            h.SetHealth(current);
        }

        CombatEvents.EnemyKilled += OnKilled;
        RunManager.StageStarted += OnStage;
        subscribed = true;
    }

    public override void Remove(CharmContext ctx)
    {
        if (subscribed)
        {
            CombatEvents.EnemyKilled -= OnKilled;
            RunManager.StageStarted -= OnStage;

            if (ctx != null && ctx.playerHealth != null && originalMax > 0)
                ctx.playerHealth.SetMaxHealth(originalMax, false);
        }

        ClearBonus();

        subscribed = false;
        context = null;
    }

    private void OnKilled(EnemyController enemy)
    {
        if (context == null || context.stats == null)
            return;

        bonus = Mathf.Min(maxBonus, bonus + bonusPerKill);

        context.stats.RemoveModifiers(bonusOwner);
        context.stats.AddModifier(bonusOwner, StatType.BalanceDamage, 0f, 1f + bonus);
        context.stats.AddModifier(bonusOwner, StatType.HealthDamage, 0f, 1f + bonus);
    }

    private void OnStage(int stage)
    {
        ClearBonus();
    }

    private void ClearBonus()
    {
        bonus = 0f;

        if (context != null && context.stats != null)
            context.stats.RemoveModifiers(bonusOwner);
    }

    public override string Status()
    {
        return bonus > 0f ? "+" + Mathf.RoundToInt(bonus * 100f) + "%" : "";
    }

    public override string Describe(int stacks)
    {
        return "Max can ×" + maxHealthMultiplier.ToString("0.0#") + ". Odada her öldürme +%" + Mathf.RoundToInt(bonusPerKill * 100f) + " hasar (en çok +%" + Mathf.RoundToInt(maxBonus * 100f) + "), yeni odada sıfırlanır.";
    }
}
