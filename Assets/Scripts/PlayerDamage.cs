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

    // Çatışma (düz vuruşun düşmanın karar verilmiş saldırısıyla çarpışması): parry sesi + posture dolumu
    // yapıyordu, düz vuruş parry gibi hissettirmesin diye KAPALI. Geri açmak için true yap.
    public static bool ClashEnabled = false;

    // Silah (PlayerWeapon koyar): sadece normal vuruşlar (kombo + havada).
    // Düz vuruşların CAN hasarı: posture barının tamamını vuruşlarla doldurmak,
    // düşmanın MAX canının bu oranını götürür (parry'den dolan posture can götürmez).
    public static float PostureToHealthRatio = 0.5f;

    /// <summary>Kombonun 4. (son) vuruşunun denge ve can hasarı çarpanı.</summary>
    public static float FinalHitDamageMultiplier = 2.0f;

    /// <summary>Düz vuruşların (kombo, havada, slam) CAN hasarı çarpanı.</summary>
    public static float HealthDamageBoost = 1.4f;

    // Düz vuruşların (kombo, havada, slam) denge hasarı çarpanı. Kesirli: 2 → ortalama vuruş başına 2.
    public static float NormalHitBalanceBoost = 2f;

    // Duz vurus ZOR: postur ve can hasari bu carpanlarla kisilir (dogru parry sonrasi infaz firsat verir).
    public static float SpamBalanceMult = 0.5f;
    public static float SpamHealthMult = 0.6f;

    public static float WeaponBalanceMultiplier = 1f;
    public static float WeaponHealthMultiplier = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        PostureToHealthRatio = 0.5f;
        FinalHitDamageMultiplier = 2.0f;
        NormalHitBalanceBoost = 2f;
        AttackBalanceMultiplier = 1f;
        WeaponBalanceMultiplier = 1f;
        WeaponHealthMultiplier = 1f;
    }

    public static HitResult HitEnemy(
        EnemyBase enemy,
        DamageInfo info
    )
    {
        HitResult result = default;

        if (enemy == null)
            return result;

        Health health = enemy.GetComponent<Health>();

        if (health != null && health.IsDead)
            return result;

        // ÇATIŞMA: oyuncu saldırısı düşmanın karar verilmiş saldırısına denk geldi => saldırı iptal, hasar yok.
        if (ClashEnabled && info.source == DamageSource.Attack && enemy.TryClash(info.direction.sqrMagnitude > 0.0001f ? info.direction : Vector2.right))
        {
            result.hit = true;
            result.onBalance = true;
            return result;
        }

        EnemyBalance balance = enemy.GetComponent<EnemyBalance>();
        EnemyHitFeedback feedback = enemy.GetComponent<EnemyHitFeedback>();

        info.target = enemy;

        // ZIRH: yetenek / zehir gibi sadece can hasarı verenler, posture'lı (zırhlı) düşmanda önce zırhı yer.
        if (info.source == DamageSource.Ability && info.balanceDamage <= 0 && info.healthDamage > 0 && enemy.UsesPosture)
            info.balanceDamage = Mathf.Max(1, Mathf.RoundToInt(info.healthDamage * 0.8f));

        // AKIŞ: zincir / karşılık çarpanı (sadece düz vuruş ve slam).
        if (info.source == DamageSource.Attack || info.source == DamageSource.Slam)
        {
            float flow = CombatFlow.OnPlayerHit(enemy);

            if (flow > 1.001f)
            {
                if (info.balanceDamage > 0)
                    info.balanceDamage = Mathf.Max(info.balanceDamage + 1, Mathf.RoundToInt(info.balanceDamage * flow));

                if (info.healthDamage > 0)
                    info.healthDamage = Mathf.Max(info.healthDamage + 1, Mathf.RoundToInt(info.healthDamage * flow));
            }
        }

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

        // Ölüm efekti (kılıç kesiği) için vuruş bağlamı: yön, kombo adımı, infaz mı.
        EnemyDeathFx.SetContext(enemy, info, enemy.IsStaggered);

        // =====================================================
        // DENGE KATMANI
        // =====================================================

        if (balance != null && !balance.IsBroken && enemy.UsesPosture)
        {
            float sourceMultiplier =
                info.source == DamageSource.Attack || info.source == DamageSource.Slam
                    ? AttackBalanceMultiplier
                    : 1f;

            if (info.source == DamageSource.Attack)
                sourceMultiplier *= WeaponBalanceMultiplier;

            if (info.source == DamageSource.Attack && info.comboStep >= 4)
                sourceMultiplier *= FinalHitDamageMultiplier;

            int amount =
                Resolve(
                    StatType.BalanceDamage,
                    info.balanceDamage,
                    critMultiplier * sourceMultiplier,
                    stats
                );

            // Düz vuruşlara ek denge hasarı (kesir birikir, yuvarlamada kaybolmaz).
            float boost = NormalHitBalanceBoost * SpamBalanceMult;

            if ((info.source == DamageSource.Attack || info.source == DamageSource.Slam) && boost != 1f)
            {
                float scaled = amount * boost + balance.hitCarry;

                amount = Mathf.Max(1, Mathf.FloorToInt(scaled));
                balance.hitCarry = Mathf.Max(0f, scaled - amount);
            }

            int balanceBefore = balance.CurrentBalance;

            if (!balance.AddBalanceDamage(amount))
                return result;

            // Düz vuruş posture ilerlemesiyle orantılı küçük can hasarı da verir.
            float chipRatio = balance.chipRatioOverride >= 0f ? balance.chipRatioOverride : PostureToHealthRatio;
            if (health != null && !health.IsDead && chipRatio > 0f && balance.MaxBalance > 0)
            {
                float progress = (balance.CurrentBalance - balanceBefore + balance.lastOverflow) / (float)balance.MaxBalance;

                balance.chipCarry += Mathf.Max(0f, progress) * chipRatio * health.MaxHealth;

                int chip = Mathf.FloorToInt(balance.chipCarry);

                if (chip > 0)
                {
                    balance.chipCarry -= chip;
                    health.TakeDamage(chip);
                }
            }

            if (feedback != null && !enemy.BlocksPlainHits)
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
                critMultiplier * (info.source == DamageSource.Attack ? WeaponHealthMultiplier : 1f)
                * ((info.source == DamageSource.Attack || info.source == DamageSource.Slam) ? HealthDamageBoost : 1f)
                * (info.source == DamageSource.Attack && info.comboStep >= 4 ? FinalHitDamageMultiplier : 1f),
                stats
            );

        if ((info.source == DamageSource.Attack || info.source == DamageSource.Slam) && enemy.UsesPosture)
            healthAmount = Mathf.Max(1, Mathf.RoundToInt(healthAmount * SpamHealthMult));

        // Sersemlemiş düşman daha çabuk ölür (infaz barı boşken de bitirilebilsin).
        if (enemy.IsStaggered)
        {
            healthAmount =
                Mathf.Max(1, Mathf.RoundToInt(healthAmount * 2f));

            // İnfaz: tek düz vuruş MAX canın belirli oranını götürür (düşman tanımından).
            float execFrac = enemy.StaggerHitHealthFraction;
            if (execFrac > 0f)
            {
                int execAmount = Mathf.CeilToInt(health.MaxHealth * execFrac);
                if (execAmount > healthAmount)
                {
                    healthAmount = execAmount;
                    CombatCallout.PopupAbove(enemy, "İNFAZ!", new Color(1f, 0.85f, 0.3f), 0.9f);
                }
            }
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
        EnemyBase enemy,
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

        // Ufak vuruş duraksaması (4. vuruş biraz uzun).
        if (info.source == DamageSource.Attack && result.hit && !result.onBalance)
            HitStop.Request(info.comboStep >= 4 ? 0.07f : 0.035f);

        CombatEvents.RaiseEnemyHit(enemy, info, result);
    }
}