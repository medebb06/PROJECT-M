using System.Collections.Generic;
using UnityEngine;

// GEÇİCİ KÖPRÜ DOSYASI (Faz 1).
// Silinen Run/Level/Meta/Charm sistemlerine hâlâ bağlı olan oyuncu kodunun derlenmesi için.
// Faz 2'de (düşmanlar silinip ITarget'a geçilince) bu dosya da silinecek.

public static class MetaProgress
{
    public static float AbilityCooldownMultiplier => 1f;
}

public static class CharmUtil
{
    public static bool AddBalancePercent(EnemyBase enemy, float percent)
    {
        if (enemy == null || enemy.IsDead || percent <= 0f)
            return false;

        EnemyBalance balance = enemy.GetComponent<EnemyBalance>();

        if (balance == null || balance.IsBroken)
            return false;

        int amount =
            Mathf.Max(1, Mathf.RoundToInt(balance.MaxBalance * percent));

        if (!balance.AddBalanceDamage(amount))
            return false;

        if (!balance.IsBroken)
            enemy.PlayBalanceDamageFlash();

        return true;
    }

    public static List<EnemyBase> EnemiesInRadius(
        Vector2 center,
        float radius,
        EnemyBase exclude = null
    )
    {
        List<EnemyBase> result = new List<EnemyBase>();

        EnemyBase[] all = EnemyBase.All.ToArray();

        float sqr = radius * radius;

        for (int i = 0; i < all.Length; i++)
        {
            EnemyBase e = all[i];

            if (e == null || e == exclude || e.IsDead)
                continue;

            Vector2 d = (Vector2)e.transform.position - center;

            if (d.sqrMagnitude <= sqr)
                result.Add(e);
        }

        return result;
    }

    public static string Percent(float value)
    {
        return "%" + Mathf.RoundToInt(value * 100f);
    }
}
