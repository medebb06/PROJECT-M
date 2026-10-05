using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EFSANEVİ · DENGE PATLAMASI: oyuncu bir düşmanın dengesini kırınca
/// (vuruş ya da parry) düşman PATLAR: çevresindeki diğer düşmanların
/// max dengesinin %30'u kadar denge hasarı + itme. Kalabalıkta zincir kurar.
/// </summary>
[CreateAssetMenu(menuName = "Charms/Effect/Balance Burst", fileName = "BalanceBurstEffect")]
public class BalanceBurstEffect : CharmEffect
{
    public float radius = 3.2f;
    [Range(0f, 1f)] public float balancePercent = 0.3f;

    private CharmContext context;
    private bool subscribed;

    public override void Apply(CharmContext ctx, int stacks)
    {
        context = ctx;

        if (subscribed)
            return;

        CombatEvents.EnemyHit += OnHit;
        CombatEvents.ParrySucceeded += OnParry;
        subscribed = true;
    }

    public override void Remove(CharmContext ctx)
    {
        if (subscribed)
        {
            CombatEvents.EnemyHit -= OnHit;
            CombatEvents.ParrySucceeded -= OnParry;
        }

        subscribed = false;
        context = null;
    }

    private void OnHit(EnemyController enemy, DamageInfo info, HitResult result)
    {
        if (result.brokeBalance)
            Burst(enemy);
    }

    private void OnParry(EnemyController enemy, bool brokeBalance)
    {
        if (brokeBalance)
            Burst(enemy);
    }

    private void Burst(EnemyController source)
    {
        if (context == null || source == null)
            return;

        Vector2 center = source.transform.position + Vector3.up * 0.8f;

        List<EnemyController> near = CharmUtil.EnemiesInRadius(center, radius, source);

        for (int i = 0; i < near.Count; i++)
        {
            EnemyController e = near[i];

            CharmUtil.AddBalancePercent(e, balancePercent);

            if (e == null || e.IsDead || e.IsAttackCommitted)
                continue;

            Rigidbody2D rb = e.GetComponent<Rigidbody2D>();

            if (rb != null && e.GetComponent<BossController>() == null)
            {
                float side = Mathf.Sign(e.transform.position.x - center.x);
                rb.linearVelocity = new Vector2(side * 7f * EnemyTime.Scale, 3f);
            }
        }

        if (PlayerAbility.Instance != null)
            PlayerAbility.Instance.SpawnRingFx(center, radius, new Color(1f, 0.85f, 0.35f, 0.85f));
    }

    public override string Describe(int stacks)
    {
        return "Dengesi kırılan düşman patlar: çevredekilere max dengelerinin " + CharmUtil.Percent(balancePercent) + "'u kadar denge hasarı + itme.";
    }
}
