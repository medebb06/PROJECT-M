using UnityEngine;

/// <summary>
/// SON NEFES (risk): canın eşik yüzdesinin altındayken denge ve can hasarın
/// büyük ölçüde artar. Can eşiğin üstüne çıkınca bonus kalkar.
/// </summary>
[CreateAssetMenu(menuName = "Charms/Effect/Last Breath", fileName = "LastBreathEffect")]
public class LastBreathEffect : CharmEffect
{
    [Tooltip("Bu oranın altında (max canın yüzdesi) bonus açılır.")]
    [Range(0.05f, 0.9f)]
    public float healthThreshold = 0.3f;

    [Tooltip("1 istifte hasar çarpanı (denge ve can).")]
    public float damageMultiplier = 1.5f;

    public float multiplierPerExtraStack = 0.25f;

    private CharmContext context;
    private Health health;
    private int stacks;
    private bool active;

    public override void Apply(CharmContext ctx, int newStacks)
    {
        context = ctx;
        stacks = newStacks;

        if (health == null && ctx != null && ctx.playerHealth != null)
        {
            health = ctx.playerHealth;
            health.OnHealthChanged += OnHealthChanged;
        }

        // İstif değişti: bonus aktifse yeni değerle yeniden kur.
        if (ctx != null && ctx.stats != null)
            ctx.stats.RemoveModifiers(ctx.owner);

        active = false;
        Evaluate();
    }

    public override void Remove(CharmContext ctx)
    {
        if (health != null)
            health.OnHealthChanged -= OnHealthChanged;

        if (ctx != null && ctx.stats != null)
            ctx.stats.RemoveModifiers(ctx.owner);

        health = null;
        context = null;
        stacks = 0;
        active = false;
    }

    private float Mult(int s) =>
        damageMultiplier + multiplierPerExtraStack * Mathf.Max(0, s - 1);

    private void OnHealthChanged(int current, int max)
    {
        Evaluate();
    }

    private void Evaluate()
    {
        if (context == null || context.stats == null || health == null)
            return;

        bool shouldBeActive =
            !health.IsDead &&
            health.CurrentHealth > 0 &&
            health.CurrentHealth <= health.MaxHealth * healthThreshold;

        if (shouldBeActive == active)
            return;

        active = shouldBeActive;

        context.stats.RemoveModifiers(context.owner);

        if (active)
        {
            float m = Mult(stacks);

            context.stats.AddModifier(
                context.owner, StatType.BalanceDamage, 0f, m
            );

            context.stats.AddModifier(
                context.owner, StatType.HealthDamage, 0f, m
            );
        }
    }

    public override string Status()
    {
        return active ? "AKTİF" : "";
    }

    public override string Describe(int newStacks)
    {
        return
            "Canın " + CharmUtil.Percent(healthThreshold) +
            " altındayken denge ve can hasarı x" +
            Mult(newStacks).ToString("0.##");
    }
}
