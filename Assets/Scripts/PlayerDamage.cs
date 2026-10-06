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
    // Koşu dengesi (RunManager koyar): oyuncunun NORMAL vuruşlarının
    // (kombo + slam) denge hasarı çarpanı. Parry ana denge kırıcı olsun.
    public static float AttackBalanceMultiplier = 1f;

    // Silah (PlayerWeapon koyar): sadece normal vuruşlar (kombo + havada).
    // Düz vuruşların CAN hasarı: posture barının tamamını vuruşlarla doldurmak,
    // düşmanın MAX canının bu oranını götürür (parry'den dolan posture can götürmez).
    public static float PostureToHealthRatio = 0.375f;

    // Düz vuruşların (kombo, havada, slam) denge hasarı çarpanı. Kesirli: 2 → ortalama vuruş başına 2.
    public static float NormalHitBalanceBoost = 2f;

    public static float WeaponBalanceMultiplier = 1f;
    public static float WeaponHealthMultiplier = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        PostureToHealthRatio = 0.375f;
        NormalHitBalanceBoost = 2f;
        AttackBalanceMultiplier = 1f;
        WeaponBalanceMultiplier = 1f;
        WeaponHealthMultiplier = 1f;
    }

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

        // Koruyucu aura açıkken boss hasar almaz.
        BossAura aura = enemy.GetComponent<BossAura>();

        if (aura != null && aura.Active)
        {
            aura.OnBlockedHit();
            return result;
        }

        // Kalkanlı düşman önden gelen normal vuruşu engeller.
        if (EnemyShield.TryBlock(enemy, info))
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
            float sourceMultiplier =
                info.source == DamageSource.Attack || info.source == DamageSource.Slam
                    ? AttackBalanceMultiplier
                    : 1f;

            if (info.source == DamageSource.Attack)
                sourceMultiplier *= WeaponBalanceMultiplier;

            int amount =
                Resolve(
                    StatType.BalanceDamage,
                    info.balanceDamage,
                    critMultiplier * sourceMultiplier,
                    stats
                );

            // Düz vuruşlara ek denge hasarı (kesir birikir, yuvarlamada kaybolmaz).
            if ((info.source == DamageSource.Attack || info.source == DamageSource.Slam) && NormalHitBalanceBoost != 1f)
            {
                float scaled = amount * NormalHitBalanceBoost + balance.hitCarry;

                amount = Mathf.Max(1, Mathf.FloorToInt(scaled));
                balance.hitCarry = Mathf.Max(0f, scaled - amount);
            }

            int balanceBefore = balance.CurrentBalance;

            if (!balance.AddBalanceDamage(amount))
                return result;

            // Düz vuruş posture ilerlemesiyle orantılı küçük can hasarı da verir.
            if (health != null && !health.IsDead && PostureToHealthRatio > 0f && balance.MaxBalance > 0)
            {
                float progress = (balance.CurrentBalance - balanceBefore) / (float)balance.MaxBalance;

                balance.chipCarry += Mathf.Max(0f, progress) * PostureToHealthRatio * health.MaxHealth;

                int chip = Mathf.FloorToInt(balance.chipCarry);

                if (chip > 0)
                {
                    balance.chipCarry -= chip;
                    health.TakeDamage(chip);
                }
            }

            if (feedback != null)
            {
                feedback.PlayBalanceHit(
                    info.hitPosition,
                    info.direction
                );
            }

            enemy.PlayPostureHitSound();

            bool broke = balance.IsBroken;

            bool diedFromChip = health != null && health.IsDead;

            if (diedFromChip)
            {
                // Can hasarı öldürdü: ayrı savrulma yok.
            }
            else if (!broke)
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
                killed = diedFromChip
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
                critMultiplier * (info.source == DamageSource.Attack ? WeaponHealthMultiplier : 1f),
                stats
            );

        // Sersemlemiş düşman daha çabuk ölür (infaz barı boşken de bitirilebilsin).
        if (enemy.IsStaggered)
        {
            healthAmount =
                Mathf.Max(1, Mathf.RoundToInt(healthAmount * ExecuteMeter.StaggeredHealthMultiplier));
        }

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

        // OLASILIKSAL YUVARLAMA: hasar tam sayı (1 gibi) olduğu için
        // %25'lik bir bonus düz yuvarlamada hiç görünmezdi. Kesirli kısım
        // ihtimal olarak uygulanır: 1.25 -> %25 ihtimalle 2, aksi halde 1.
        // Uzun vadede beklenen hasar tam olarak 1.25 olur.
        int amount = Mathf.FloorToInt(value);

        float fraction = value - amount;

        if (fraction > 0f && Random.value < fraction)
            amount++;

        // Taban hasar varsa en az 1 vursun.
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