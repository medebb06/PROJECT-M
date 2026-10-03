using UnityEngine;

public enum HealTrigger
{
    OnParry,  // başarılı parry
    OnKill,   // düşman öldü
    OnDodge   // bir saldırıdan kaçıldı (dash)
}

/// <summary>
/// İyileştirme charm'ı: bir olayda belli bir İHTİMALLE oyuncuyu iyileştirir.
/// İhtimal = chancePerStack × istif (en fazla %100).
/// </summary>
[CreateAssetMenu(menuName = "Charms/Effect/Heal", fileName = "HealEffect")]
public class HealCharmEffect : CharmEffect
{
    public HealTrigger trigger = HealTrigger.OnParry;

    [Range(0f, 1f)]
    public float chancePerStack = 0.2f;

    [Min(1)]
    public int amount = 1;

    private CharmContext context;
    private int stacks;
    private bool subscribed;

    public override void Apply(CharmContext ctx, int newStacks)
    {
        context = ctx;
        stacks = newStacks;

        if (subscribed)
            return;

        switch (trigger)
        {
            case HealTrigger.OnParry:
                CombatEvents.ParrySucceeded += OnParry;
                break;

            case HealTrigger.OnKill:
                CombatEvents.EnemyKilled += OnKill;
                break;

            case HealTrigger.OnDodge:
                CombatEvents.Dodged += OnDodge;
                break;
        }

        subscribed = true;
    }

    public override void Remove(CharmContext ctx)
    {
        if (subscribed)
        {
            switch (trigger)
            {
                case HealTrigger.OnParry:
                    CombatEvents.ParrySucceeded -= OnParry;
                    break;

                case HealTrigger.OnKill:
                    CombatEvents.EnemyKilled -= OnKill;
                    break;

                case HealTrigger.OnDodge:
                    CombatEvents.Dodged -= OnDodge;
                    break;
            }
        }

        subscribed = false;
        stacks = 0;
        context = null;
    }

    private void OnParry(EnemyController enemy, bool brokeBalance) =>
        TryHeal();

    private void OnKill(EnemyController enemy) =>
        TryHeal();

    private void OnDodge(EnemyController enemy, bool unblockable) =>
        TryHeal();

    private void TryHeal()
    {
        if (
            context == null ||
            context.playerHealth == null ||
            context.playerHealth.IsDead
        )
        {
            return;
        }

        float chance =
            Mathf.Clamp01(chancePerStack * stacks);

        if (Random.value < chance)
            context.playerHealth.Heal(amount);
    }

    public override string Describe(int newStacks)
    {
        string when =
            trigger == HealTrigger.OnParry ? "başarılı parry'de"
            : trigger == HealTrigger.OnKill ? "düşman öldürünce"
            : "bir saldırıdan kaçınca";

        int percent =
            Mathf.RoundToInt(
                Mathf.Clamp01(chancePerStack * newStacks) * 100f
            );

        return
            "%" + percent + " ihtimalle " + amount + " can (" + when + ")";
    }
}