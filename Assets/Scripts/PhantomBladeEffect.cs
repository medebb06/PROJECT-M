using System.Collections;
using UnityEngine;

/// <summary>
/// EFSANEVİ · HAYALET KILIÇ: kombonun 4. vuruşu kısa süre sonra bir
/// HAYALET vuruşla tekrarlanır (aynı hasar, kritik atabilir). Komboyu
/// tamamlamak ödüllenir.
/// </summary>
[CreateAssetMenu(menuName = "Charms/Effect/Phantom Blade", fileName = "PhantomBladeEffect")]
public class PhantomBladeEffect : CharmEffect
{
    public float delay = 0.16f;

    private CharmContext context;
    private bool subscribed;

    public override void Apply(CharmContext ctx, int stacks)
    {
        context = ctx;

        if (subscribed)
            return;

        CombatEvents.EnemyHit += OnHit;
        subscribed = true;
    }

    public override void Remove(CharmContext ctx)
    {
        if (subscribed)
            CombatEvents.EnemyHit -= OnHit;

        subscribed = false;
        context = null;
    }

    private void OnHit(EnemyController enemy, DamageInfo info, HitResult result)
    {
        if (context == null || enemy == null || !result.hit)
            return;

        if (info.source != DamageSource.Attack || info.comboStep != 4)
            return;

        if (CharmRunner.Instance == null)
            return;

        CharmRunner.Instance.StartCoroutine(Echo(enemy, info));
    }

    private IEnumerator Echo(EnemyController enemy, DamageInfo info)
    {
        yield return new WaitForSeconds(delay);

        if (enemy == null || enemy.IsDead)
            yield break;

        DamageInfo echo = info;
        echo.source = DamageSource.Other;   // tekrar tetiklemesin
        echo.comboStep = 0;

        PlayerDamage.HitEnemy(enemy, echo);

        CombatCallout.PopupAbove(enemy, "HAYALET", new Color(0.7f, 0.8f, 1f), 0.7f);
    }

    public override string Describe(int stacks)
    {
        return "Kombonun 4. vuruşu " + delay.ToString("0.##") + " sn sonra hayalet bir vuruşla tekrarlanır.";
    }
}
