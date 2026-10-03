using UnityEngine;

/// <summary>
/// Oyuncunun düşmana verdiği TÜM hasarın geçtiği tek nokta.
///
/// Eskiden aynı mantık iki yerde kopyaydı (kombo vuruşu ve ground slam).
/// Artık ikisi de buradan geçer; stat'lar, kritik ve olaylar tek yerde.
///
/// Kural (değişmedi):
///   Denge kırık DEĞİLSE -> vuruş DENGEYE hasar verir.
///   Denge kırıksa (stagger) -> vuruş CANA hasar verir.
///
/// Sonrasında CombatEvents.EnemyHit tetiklenir: charm'lar (zehir, vampirizm,
/// ...) buraya bağlanır.
/// </summary>
public static class PlayerDamage
{
    public static HitResult HitEnemy(
        EnemyController enemy,
        DamageInfo info
    )
    {
        HitResult result = default;

        if (enemy == null)
            return result;

        Health health = enemy.GetComponent<Health>();

        if (health != null && health.IsDead)
            return result;

        EnemyBalance balance = enemy.GetComponent<EnemyBalance>();
        EnemyHitFeedback feedback = enemy.GetComponent<EnemyHitFeedback>();

        info.target = enemy;

        PlayerStats stats = PlayerStats.Current;

        // -----------------------------------------------------
        // KRİTİK
        // -----------------------------------------------------

        bool critical = false;

        if (!info.disallowCrit && stats != null)
            critical = Random.value < stats.CritChance;

        float critMultiplier =
            critical && stats != null
                ? stats.CritMultiplier
                : 1f;

        info.direction =
            info.direction.sqrMagnitude > 0.0001f
                ? info.direction
                : Vector2.right;

        // =====================================================
        // DENGE KATMANI
        // =====================================================

        if (balance != null && !balance.IsBroken)
        {
            int amount =
                Resolve(
                    StatType.BalanceDamage,
                    info.balanceDamage,
                    critMultiplier,
                    stats
                );

            if (!balance.AddBalanceDamage(amount))
                return result;

            if (feedback != null)
            {
                feedback.PlayBalanceHit(
                    info.hitPosition,
                    info.direction
                );
            }

            enemy.PlayPostureHitSound();

            bool broke = balance.IsBroken;

            if (!broke)
            {
                enemy.ApplyBalanceHit(info.direction);
            }
            else
            {
                // Denge bu vuruşta kırıldı: stagger EnemyBalance olayıyla
                // zaten başladı; sadece savrulma uygula.
                enemy.ApplyAttackHit(info.direction, false);
            }

            result = new HitResult
            {
                hit = true,
                onBalance = true,
                amount = amount,
                critical = critical,
                brokeBalance = broke,
                killed = false
            };

            Finish(enemy, info, result, stats);

            return result;
        }

        // =====================================================
        // CAN KATMANI (denge kırık ya da düşmanda denge yok)
        // =====================================================

        if (health == null)
            return result;

        int healthAmount =
            Resolve(
                StatType.HealthDamage,
                info.healthDamage,
                critMultiplier,
                stats
            );

        health.TakeDamage(healthAmount);

        if (feedback != null)
            feedback.PlayHealthHit(info.direction);

        enemy.PlayHealthHitSound();

        bool killed = health.IsDead;

        if (!killed)
        {
            enemy.ApplyAttackHit(
                info.direction,
                true,
                info.comboStep > 0 ? info.comboStep : 1
            );
        }

        result = new HitResult
        {
            hit = true,
            onBalance = false,
            amount = healthAmount,
            critical = critical,
            brokeBalance = false,
            killed = killed
        };

        Finish(enemy, info, result, stats);

        return result;
    }

    // Taban hasar -> stat değiştiricileri -> kritik -> tam sayı.
    private static int Resolve(
        StatType type,
        int baseAmount,
        float critMultiplier,
        PlayerStats stats
    )
    {
        float value =
            stats != null
                ? stats.Get(type, baseAmount)
                : baseAmount;

        value *= critMultiplier;

        int amount = Mathf.RoundToInt(value);

        // Taban hasar varsa en az 1 vursun (yuvarlama sıfıra düşürmesin).
        if (baseAmount > 0 && amount < 1)
            amount = 1;

        return Mathf.Max(0, amount);
    }

    private static void Finish(
        EnemyController enemy,
        DamageInfo info,
        HitResult result,
        PlayerStats stats
    )
    {
        if (stats != null && stats.DebugLog)
        {
            Debug.Log(
                "HIT [" + info.source + "] " +
                (result.onBalance ? "DENGE " : "CAN ") +
                result.amount +
                (result.critical ? "  KRİTİK" : "") +
                (result.brokeBalance ? "  (denge kırıldı)" : "") +
                (result.killed ? "  (öldü)" : "")
            );
        }

        CombatEvents.RaiseEnemyHit(enemy, info, result);
    }
}