using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// YANKI (parry): başarılı parry, oyuncunun çevresindeki DİĞER düşmanların
/// dengesine de vurur (max dengelerinin yüzdesi). Kalabalıkta parry'i
/// değerli kılar; dengesi kırılan düşman sersemler.
/// </summary>
[CreateAssetMenu(menuName = "Charms/Effect/Echo Parry", fileName = "EchoParryEffect")]
public class EchoParryEffect : CharmEffect
{
    [Tooltip("İstif başına, max dengenin yüzdesi (0.15 = %15).")]
    public float balancePercentPerStack = 0.15f;

    public float radius = 4f;
    public float radiusPerExtraStack = 0.5f;

    private CharmContext context;
    private int stacks;
    private bool subscribed;

    public override void Apply(CharmContext ctx, int newStacks)
    {
        context = ctx;
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
        context = null;
    }

    private float Radius(int s) =>
        radius + radiusPerExtraStack * Mathf.Max(0, s - 1);

    private void OnParry(EnemyController parried, bool brokeBalance)
    {
        if (context == null || context.player == null)
            return;

        List<EnemyController> near =
            CharmUtil.EnemiesInRadius(
                context.player.transform.position,
                Radius(stacks),
                parried
            );

        float percent = balancePercentPerStack * stacks;

        for (int i = 0; i < near.Count; i++)
            CharmUtil.AddBalancePercent(near[i], percent);
    }

    public override string Describe(int newStacks)
    {
        return
            "Parry, " + Radius(newStacks).ToString("0.#") +
            " birim içindeki diğer düşmanların dengesine max dengenin " +
            CharmUtil.Percent(balancePercentPerStack * newStacks) +
            "'i kadar vurur";
    }
}
