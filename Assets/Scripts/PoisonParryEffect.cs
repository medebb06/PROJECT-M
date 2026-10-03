using UnityEngine;

/// <summary>
/// ZEHİRLİ PARRY (zehir): parry, saldıranı zehirler. Zehir charm'ı olmadan
/// da çalışır (kendi başına bir zehir kaynağıdır).
/// İstif: şiddet = 1 + (istif - 1) × extraStrengthPerStack
/// </summary>
[CreateAssetMenu(menuName = "Charms/Effect/Poison Parry", fileName = "PoisonParryEffect")]
public class PoisonParryEffect : CharmEffect
{
    [Tooltip("1 istifte saniyede, max dengenin yüzdesi.")]
    [Range(0f, 1f)]
    public float balancePercentPerSecond = 0.06f;

    [Tooltip("Denge kırıkken saniyede, max canın yüzdesi.")]
    [Range(0f, 1f)]
    public float healthPercentPerSecond = 0.015f;

    public float extraStrengthPerStack = 0.6f;

    [Tooltip("Zehrin süresi (düşman saniyesi).")]
    public float duration = 4f;

    private int stacks;
    private bool subscribed;

    public override void Apply(CharmContext ctx, int newStacks)
    {
        stacks = newStacks;

        if (subscribed)
            return;

        CombatEvents.ParrySucceeded += OnParry;
        subscribed = true;
    }

    public override void Remove(CharmContext ctx)
    {
        if (subscribed)
            CombatEvents.ParrySucceeded -= OnParry;

        subscribed = false;
        stacks = 0;
    }

    private float Strength(int s) =>
        1f + Mathf.Max(0, s - 1) * extraStrengthPerStack;

    private void OnParry(EnemyController enemy, bool brokeBalance)
    {
        if (enemy == null || enemy.IsDead)
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
            "Parry saldıranı zehirler: saniyede %" +
            (balancePercentPerSecond * strength * 100f).ToString("0.#") +
            " denge (sersemleyince %" +
            (healthPercentPerSecond * strength * 100f).ToString("0.#") +
            " can), " + duration.ToString("0.#") + " sn";
    }
}
