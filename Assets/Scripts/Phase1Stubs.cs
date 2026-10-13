using System.Collections.Generic;
using UnityEngine;

// GEÇİCİ KÖPRÜ DOSYASI (Faz 1).
// Silinen Run/Level/Meta/Charm sistemlerine hâlâ bağlı olan oyuncu kodunun derlenmesi için.
// Faz 2'de (düşmanlar silinip ITarget'a geçilince) bu dosya da silinecek.

public static class LevelProps
{
    public static bool HitArea(Vector2 center, Vector2 size) { return false; }
    public static bool SpikeIn(Vector2 center, Vector2 size) { return false; }
}

public static class MetaProgress
{
    public static float ExecuteFillMultiplier => 1f;
    public static float AbilityCooldownMultiplier => 1f;
}

public static class RunUI
{
    public static bool MenuVisible => false;
}

public class RunManager
{
    public static RunManager Instance => null;
    public bool IsPaused => false;
    public void ShowBanner(string text, float seconds) { }
}

public static class CharmUtil
{
    public static bool AddBalancePercent(EnemyController enemy, float percent)
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

    public static List<EnemyController> EnemiesInRadius(
        Vector2 center,
        float radius,
        EnemyController exclude = null
    )
    {
        List<EnemyController> result = new List<EnemyController>();

        EnemyController[] all = EnemyController.All.ToArray();

        float sqr = radius * radius;

        for (int i = 0; i < all.Length; i++)
        {
            EnemyController e = all[i];

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
