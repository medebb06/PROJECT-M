using UnityEngine;

/// <summary>
/// GÖLGE ADIM (dash): DASH ile kaçılan saldırının sahibi denge hasarı alır
/// (engellenemez vuruşta daha fazla) ve oyuncuya posture iade edilir.
/// Hasar sonrası korumalı dönemde "kaçılan" saldırılar sayılmaz.
/// </summary>
[CreateAssetMenu(menuName = "Charms/Effect/Shadow Step", fileName = "ShadowStepEffect")]
public class ShadowStepEffect : CharmEffect
{
    [Tooltip("1 istifte, saldıranın max dengesinin yüzdesi.")]
    public float balancePercent = 0.2f;

    public float balancePercentPerExtraStack = 0.1f;

    [Tooltip("Engellenemez vuruştan kaçınca denge hasarı çarpanı.")]
    public float unblockableMultiplier = 1.5f;

    [Tooltip("İstif başına iade edilen posture.")]
    public int postureRefundPerStack = 15;

    private CharmContext context;
    private int stacks;
    private bool subscribed;

    public override void Apply(CharmContext ctx, int newStacks)
    {
        context = ctx;
        stacks = newStacks;

        if (subscribed)
            return;

        CombatEvents.Dodged += OnDodged;
        subscribed = true;
    }

    public override void Remove(CharmContext ctx)
    {
        if (subscribed)
            CombatEvents.Dodged -= OnDodged;

        subscribed = false;
        stacks = 0;
        context = null;
    }

    private float Percent(int s) =>
        balancePercent + balancePercentPerExtraStack * Mathf.Max(0, s - 1);

    private void OnDodged(EnemyController enemy, bool unblockable)
    {
        if (context == null)
            return;

        if (!CharmRunner.IsDashDodge(context.player))
            return;

        float percent = Percent(stacks);

        if (unblockable)
            percent *= unblockableMultiplier;

        CharmUtil.AddBalancePercent(enemy, percent);

        if (context.posture != null)
            context.posture.RecoverPosture(postureRefundPerStack * stacks);
    }

    public override string Describe(int newStacks)
    {
        return
            "Dash'le kaçınca saldıran max dengesinin " +
            CharmUtil.Percent(Percent(newStacks)) +
            "'i kadar denge hasarı alır (engellenemezde x" +
            unblockableMultiplier.ToString("0.#") + "), +" +
            (postureRefundPerStack * newStacks) + " posture";
    }
}
