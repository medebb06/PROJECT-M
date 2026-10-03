using UnityEngine;

/// <summary>
/// FIRSAT PENCERESİ (dash): DASH ile bir saldırıdan kaçınca kısa bir süre
/// sonraki vuruşların güçlenir (parry riposte'unun daha zayıf dash hali).
/// Süre ya da vuruş hakkı, hangisi önce biterse.
/// </summary>
[CreateAssetMenu(menuName = "Charms/Effect/Opportunity", fileName = "OpportunityEffect")]
public class OpportunityEffect : CharmEffect
{
    public float duration = 1.5f;

    [Min(1)]
    public int maxHits = 2;

    [Tooltip("1 istifte denge hasarı çarpanı.")]
    public float balanceMultiplier = 1.4f;
    public float balancePerExtraStack = 0.2f;

    [Tooltip("1 istifte can hasarı çarpanı.")]
    public float healthMultiplier = 1.25f;
    public float healthPerExtraStack = 0.15f;

    private CharmContext context;
    private int stacks;
    private bool subscribed;

    private bool active;
    private float timeLeft;
    private int hitsLeft;

    public override void Apply(CharmContext ctx, int newStacks)
    {
        context = ctx;
        stacks = newStacks;

        if (subscribed)
            return;

        CombatEvents.Dodged += OnDodged;
        CombatEvents.EnemyHit += OnEnemyHit;
        CharmRunner.Tick += OnTick;
        subscribed = true;
    }

    public override void Remove(CharmContext ctx)
    {
        if (subscribed)
        {
            CombatEvents.Dodged -= OnDodged;
            CombatEvents.EnemyHit -= OnEnemyHit;
            CharmRunner.Tick -= OnTick;
        }

        End();

        subscribed = false;
        stacks = 0;
        context = null;
    }

    private float BalanceMult(int s) =>
        balanceMultiplier + balancePerExtraStack * Mathf.Max(0, s - 1);

    private float HealthMult(int s) =>
        healthMultiplier + healthPerExtraStack * Mathf.Max(0, s - 1);

    private void OnDodged(EnemyController enemy, bool unblockable)
    {
        if (context == null || context.stats == null)
            return;

        if (!CharmRunner.IsDashDodge(context.player))
            return;

        // Yenilenen kaçış pencereyi tazeler (üst üste binmez).
        context.stats.RemoveModifiers(context.owner);

        context.stats.AddModifier(
            context.owner, StatType.BalanceDamage, 0f, BalanceMult(stacks)
        );

        context.stats.AddModifier(
            context.owner, StatType.HealthDamage, 0f, HealthMult(stacks)
        );

        active = true;
        timeLeft = duration;
        hitsLeft = maxHits;
    }

    private void OnEnemyHit(
        EnemyController enemy,
        DamageInfo info,
        HitResult result
    )
    {
        if (!active || !result.hit)
            return;

        if (info.source == DamageSource.Poison)
            return;

        hitsLeft--;

        if (hitsLeft <= 0)
            End();
    }

    private void OnTick(float dt)
    {
        if (!active)
            return;

        timeLeft -= dt;

        if (timeLeft <= 0f)
            End();
    }

    private void End()
    {
        active = false;
        timeLeft = 0f;
        hitsLeft = 0;

        if (context != null && context.stats != null)
            context.stats.RemoveModifiers(context.owner);
    }

    public override string Status()
    {
        return active ? "FIRSAT " + new string('●', hitsLeft) : "";
    }

    public override string Describe(int newStacks)
    {
        return
            "Dash'le kaçınca " + duration.ToString("0.#") + " sn / " +
            maxHits + " vuruş: denge x" + BalanceMult(newStacks).ToString("0.##") +
            ", can x" + HealthMult(newStacks).ToString("0.##");
    }
}
