using UnityEngine;

/// <summary>
/// EFSANEVİ · İNFAZCI: infaz barı %50 daha hızlı dolar; her İNFAZ max canın
/// %8'ini iyileştirir ve yeteneği (Q) anında doldurur.
/// </summary>
[CreateAssetMenu(menuName = "Charms/Effect/Executioner", fileName = "ExecutionerEffect")]
public class ExecutionerEffect : CharmEffect
{
    [Range(0f, 1f)] public float healPercent = 0.08f;
    public float fillMultiplier = 1.5f;

    private CharmContext context;
    private bool subscribed;

    public override void Apply(CharmContext ctx, int stacks)
    {
        context = ctx;

        ExecuteMeter.CharmFillMultiplier = fillMultiplier;

        if (subscribed)
            return;

        CombatEvents.EnemyKilled += OnKilled;
        subscribed = true;
    }

    public override void Remove(CharmContext ctx)
    {
        if (subscribed)
            CombatEvents.EnemyKilled -= OnKilled;

        ExecuteMeter.CharmFillMultiplier = 1f;

        subscribed = false;
        context = null;
    }

    private void OnKilled(EnemyController enemy)
    {
        if (context == null || enemy == null)
            return;

        if (!(enemy.CurrentState is EnemyExecuteState))
            return;

        Health h = context.playerHealth;

        if (h != null && !h.IsDead)
            h.Heal(Mathf.Max(1, Mathf.RoundToInt(h.MaxHealth * healPercent)));

        if (PlayerAbility.Instance != null)
            PlayerAbility.Instance.Refill();
    }

    public override string Describe(int stacks)
    {
        return "İnfaz barı ×" + fillMultiplier.ToString("0.#") + " hızlı dolar. Her infaz +" + CharmUtil.Percent(healPercent) + " can ve yetenek (Q) hemen hazır.";
    }
}
