using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RÜZGAR KESİĞİ (dash): dash sırasında içinden geçilen her düşmanın
/// dengesi aşınır (her dash'te düşman başına bir kez).
/// Not: 'Dash' layer'ı düşmanlarla çarpışmıyorsa içinden geçilebilir.
/// </summary>
[CreateAssetMenu(menuName = "Charms/Effect/Wind Slash", fileName = "WindSlashEffect")]
public class WindSlashEffect : CharmEffect
{
    [Tooltip("İstif başına, max dengenin yüzdesi.")]
    public float balancePercentPerStack = 0.12f;

    [Tooltip("Düşman gövdesinin yarı genişliğine eklenen yatay pay.")]
    public float horizontalPadding = 0.4f;

    [Tooltip("Dikey tolerans (oyuncu ile düşman merkezi arası).")]
    public float verticalTolerance = 1.5f;

    private CharmContext context;
    private int stacks;
    private bool subscribed;

    private readonly HashSet<EnemyController> hitThisDash =
        new HashSet<EnemyController>();

    public override void Apply(CharmContext ctx, int newStacks)
    {
        context = ctx;
        stacks = newStacks;

        if (subscribed)
            return;

        CharmRunner.Tick += OnTick;
        CombatEvents.PlayerDashed += OnDashed;
        subscribed = true;
    }

    public override void Remove(CharmContext ctx)
    {
        if (subscribed)
        {
            CharmRunner.Tick -= OnTick;
            CombatEvents.PlayerDashed -= OnDashed;
        }

        subscribed = false;
        stacks = 0;
        context = null;
        hitThisDash.Clear();
    }

    private void OnDashed()
    {
        hitThisDash.Clear();
    }

    private void OnTick(float dt)
    {
        if (context == null || context.player == null)
            return;

        PlayerController player = context.player;

        if (!player.isDashing)
            return;

        Vector2 p = player.transform.position;

        List<EnemyController> all = EnemyController.All;

        for (int i = all.Count - 1; i >= 0; i--)
        {
            EnemyController e = all[i];

            if (e == null || e.IsDead || hitThisDash.Contains(e))
                continue;

            Vector2 d = (Vector2)e.transform.position - p;

            if (
                Mathf.Abs(d.x) <= e.BodyWidth * 0.5f + horizontalPadding &&
                Mathf.Abs(d.y) <= verticalTolerance
            )
            {
                hitThisDash.Add(e);

                CharmUtil.AddBalancePercent(
                    e,
                    balancePercentPerStack * stacks
                );
            }
        }
    }

    public override string Describe(int newStacks)
    {
        return
            "Dash'le içinden geçtiğin düşman max dengesinin " +
            CharmUtil.Percent(balancePercentPerStack * newStacks) +
            "'i kadar denge hasarı alır";
    }
}
