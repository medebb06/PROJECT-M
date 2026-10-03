using UnityEngine;

public class EnemyAttackState : IEnemyState
{
    private EnemyController enemy;
    private EnemyAttackAudio attackAudio;
    private EnemyAttackTelegraph telegraph;
    private Rigidbody2D rb;

    private float warningTimer;
    private float recoveryTimer;

    private bool attackDone;
    private bool isRecovering;

    // Vuruş anı çözülürken (DoAttack) state değişirse (ör. parry dengeyi
    // kırıp stagger başlatırsa) bu "kesilme" sayılmasın.
    private bool resolvingHit;

    // Kararlı aşama (super armor) takibi
    private float warningDuration;
    private bool commitTriggered;

    // Engellenemez vuruş: parry/block işe yaramaz, dash/geri çekilme gerekir.
    private bool isUnblockable;

    // Saldırı animasyonu zamanlaması: uzun uyarıda animasyon geç başlatılır,
    // vuruş karesi hasar anına denk gelsin.
    private bool animationStarted;
    private float animationDelay;

    // Engellenemez vuruş okunurluğu + kaçış payı
    private EnemyDangerIndicator indicator;
    private PlayerController playerRef;
    private bool dodgeCueTriggered;
    private float lastDashSeenTime;

    public EnemyAttackState(EnemyController enemy)
    {
        this.enemy = enemy;

        attackAudio =
            enemy.GetComponent<EnemyAttackAudio>();

        telegraph =
            enemy.GetComponent<EnemyAttackTelegraph>();

        rb =
            enemy.GetComponent<Rigidbody2D>();
    }

    // =========================================================
    // COMMIT (KARARLI AŞAMA)
    // Uyarının attackCommitPoint oranından sonra saldırı artık
    // hasar alınca kesilmez. Cevap: parry, dash veya geri çekilme.
    // Vuruş anından sonraki recovery'de düşman yine açık hedeftir.
    // =========================================================

    public bool IsCommitted
    {
        get
        {
            if (attackDone)
                return false;

            float commitPoint =
                isUnblockable
                    ? enemy.unblockableCommitPoint
                    : enemy.attackCommitPoint;

            // 1 = kararlı aşama yok (eski davranış)
            if (commitPoint >= 1f)
                return false;

            float progress =
                1f -
                (warningTimer / warningDuration);

            return progress >= commitPoint;
        }
    }

    // Derinlik sıralaması (EnemyDepthSorter) için:
    // uyarı (wind-up) aşamasında mı, vuruşa ne kadar kaldı?
    public bool IsWindingUp => !attackDone;

    public bool IsUnblockable => isUnblockable;

    public float RemainingWindup =>
        Mathf.Max(0f, warningTimer);

    // Engellenemez vuruşun uyarı aşaması her karede:
    // işaretleri ilerlet, dash'i takip et, "ŞİMDİ KAÇ" zamanını yakala.
    private void UpdateUnblockableWindup()
    {
        float progress =
            1f -
            (warningTimer / warningDuration);

        if (telegraph != null)
            telegraph.SetProgress(progress);

        if (indicator != null)
            indicator.SetProgress(progress);

        // Kaçış payı için: oyuncunun dash'te olduğu son anı hatırla.
        if (playerRef != null && playerRef.isDashing)
            lastDashSeenTime = Time.time;

        // Uyarı kısaysa işaret hemen başta çıkmasın.
        float lead =
            Mathf.Min(
                enemy.unblockableDodgeCueLead,
                warningDuration * 0.7f
            );

        if (!dodgeCueTriggered && warningTimer <= lead)
            TriggerDodgeCue();
    }

    // Vuruş isabet alanında mı?
    //  - Engellenemez + yönlü: baktığı yönde uzun kutu (arkaya ve yüksekliğe duyarlı)
    //  - Normal + yönlü (isteğe bağlı): aynı kutu, menzil = attackRange
    //  - Diğer: eski dairesel erişim
    private bool IsInReach(
        PlayerController player,
        float distance,
        float circularReach
    )
    {
        if (isUnblockable && enemy.unblockableFrontOnly)
        {
            return IsInFrontArea(
                player,
                enemy.unblockableForwardReach,
                enemy.unblockableHitHeight
            );
        }

        if (!isUnblockable && enemy.normalAttackFrontOnly)
        {
            return IsInFrontArea(
                player,
                enemy.attackRange,
                enemy.normalAttackHitHeight
            );
        }

        return distance <= circularReach;
    }

    private bool IsInFrontArea(
        PlayerController player,
        float forwardReach,
        float hitHeight
    )
    {
        // Düşmanın baktığı yönde ne kadar ilerideyim?
        // (+ = önünde, - = arkasında)
        float forward =
            (
                player.transform.position.x -
                enemy.transform.position.x
            ) * enemy.FacingDirection;

        if (
            forward < -enemy.attackBackTolerance ||
            forward > forwardReach
        )
        {
            return false;
        }

        // Yüksekliğe göre: yeterince zıplayan oyuncu kutunun üstünden geçer.
        float playerFeet =
            player.col != null
                ? player.col.bounds.min.y
                : player.transform.position.y;

        return playerFeet <= enemy.FeetY + hitHeight;
    }

    private void TriggerDodgeCue()
    {
        dodgeCueTriggered = true;

        if (indicator != null)
            indicator.TriggerNowCue();

        if (telegraph != null)
            telegraph.TriggerCue();

        if (attackAudio != null)
            attackAudio.PlayDodgeCue();
    }

    private void TriggerCommit()
    {
        commitTriggered = true;

        if (telegraph != null)
            telegraph.SetCommitted();

        if (attackAudio != null && !isUnblockable)
            attackAudio.PlayCommit();
    }

    public void Enter()
    {
        // Animasyon aşağıda, uyarı süresi bilindikten sonra başlatılır.

        // Planlanan saldırı engellenemez mi? (ChaseState koordinatöre
        // aynı uyarı süresini bildirdi; tutarlı kalsın.)
        isUnblockable =
            enemy.ConsumePlannedAttack();

        dodgeCueTriggered = false;
        lastDashSeenTime = -999f;

        playerRef =
            enemy.target != null
                ? enemy.target.GetComponent<PlayerController>()
                : null;

        float baseWindup =
            enemy.WindupFor(isUnblockable);

        // Felç Edici Zehir: zehirli düşmanın uyarısı uzar.
        float windup =
            baseWindup * EnemyStatus.WindupMultiplierFor(enemy);

        warningTimer = windup;

        warningDuration =
            Mathf.Max(
                0.0001f,
                windup
            );

        // Animasyon zamanlaması:
        // Normal saldırı: animasyon hemen başlar (eskisi gibi); zehirle uyarı
        // uzadıysa uzama kadar gecikir, vuruş karesi hasar anına denk gelir.
        // Engellenemez: uyarı uzun olduğu için animasyon, vuruş karesi
        // hasar anına gelecek şekilde GEÇ başlatılır; önce sarı uyarı
        // (flaş, "!", kutu), sonra vuruş animasyonu.
        animationStarted = false;

        animationDelay =
            isUnblockable
                ? Mathf.Max(
                    0f,
                    windup - enemy.AttackAnimationHitTime
                )
                : Mathf.Max(0f, windup - baseWindup);

        if (animationDelay <= 0f)
        {
            enemy.PlayAttackAnimation();
            animationStarted = true;
        }

        commitTriggered = false;

        recoveryTimer = 0f;

        attackDone = false;
        isRecovering = false;
        resolvingHit = false;

        // YÖN KİLİDİ: saldırı başladığı anda baktığı yönü sabitle ve
        // saldırı bitene kadar oyuncuya dönme. Dash ile arkasına geçen
        // oyuncu vurulmaz (vuruş sadece ön alana isabet eder).
        if (enemy.lockFacingDuringAttack)
            enemy.LockFacing(true);

        StopMovement();

        PlayWarning();

        if (telegraph != null)
            telegraph.StartWarning(isUnblockable);

        // StartWarning flaş bastırmasını sıfırladığı için SONRA çağrılır.
        if (isUnblockable)
        {
            enemy.PlayAlertFlash();

            // Başın üstünde "!" ve zeminde erişim bandı.
            indicator =
                enemy.GetComponent<EnemyDangerIndicator>();

            if (indicator == null)
            {
                indicator =
                    enemy.gameObject
                        .AddComponent<EnemyDangerIndicator>();
            }

            if (enemy.unblockableFrontOnly)
            {
                // Düşmanın baktığı yöne doğru uzun kutu.
                indicator.Show(
                    enemy.unblockableForwardReach,
                    enemy.unblockableHitHeight,
                    true
                );
            }
            else
            {
                indicator.Show(
                    enemy.attackRange *
                    enemy.unblockableReachMultiplier
                );
            }

            Debug.Log(
                "ENEMY UNBLOCKABLE ATTACK STARTED"
            );
        }
    }

    public void Tick()
    {
        // FIX: Oyuncu ölünce düşman ölü oyuncuya
        // sonsuza kadar saldırmaya devam ediyordu.
        if (
            enemy.target == null ||
            enemy.IsTargetDead
        )
        {
            enemy.ChangeState(
                new EnemyIdleState(enemy)
            );

            return;
        }

        // =====================================================
        // ATTACK
        // =====================================================

        if (!attackDone)
        {
            StopMovement();

            // Düşman zamanıyla ilerler: parry slow-mo'sunda animasyon ve
            // ses ile birlikte yavaşlar.
            warningTimer -= EnemyTime.DeltaTime;

            // Geciktirilmiş animasyonu zamanı gelince başlat.
            if (
                !animationStarted &&
                warningDuration - warningTimer >= animationDelay
            )
            {
                enemy.PlayAttackAnimation();
                animationStarted = true;
            }

            if (!commitTriggered && IsCommitted)
                TriggerCommit();

            if (isUnblockable)
                UpdateUnblockableWindup();

            if (warningTimer > 0f)
                return;

            if (indicator != null)
                indicator.Hide();

            if (telegraph != null)
                telegraph.StopWarning();

            resolvingHit = true;

            bool enemyStaggered =
                DoAttack();

            resolvingHit = false;

            attackDone = true;

            // Varsayılan: kilit saldırı bitene (recovery dahil) kadar sürer;
            // Exit'te açılır. İstenirse vuruş anında açılır.
            if (enemy.releaseFacingAtHit)
                enemy.LockFacing(false);

            // Vuruş anı geçti: ritim koordinatörüne bildir.
            EnemyAttackCoordinator.ReleaseAttack(enemy);

            // Parry sonucu BALANCE KIRILDIYSA
            // EnemyController zaten stagger state'e geçti.
            if (enemyStaggered)
            {
                Debug.Log(
                    "ENEMY ATTACK → PARRY BROKE BALANCE → STAGGER"
                );

                return;
            }

            // Normal attack / block / normal parry
            // sonrası recovery başlat.
            // Engellenemez vuruşun recovery'si uzun: kaçınılan (dash)
            // vuruş düşmanı uzun süre açık hedef bırakır.
            float recovery =
                isUnblockable
                    ? enemy.attackRecoveryTime *
                      enemy.unblockableRecoveryMultiplier
                    : enemy.attackRecoveryTime;

            enemy.StartAttackRecovery(recovery);

            recoveryTimer = recovery;

            isRecovering = true;

            StopMovement();

            return;
        }

        // =====================================================
        // RECOVERY
        // =====================================================

        if (isRecovering)
        {
            StopMovement();

            recoveryTimer -= EnemyTime.DeltaTime;

            if (recoveryTimer > 0f)
                return;

            isRecovering = false;

            Debug.Log(
                "ENEMY ATTACK RECOVERY FINISHED → CHASE"
            );

            enemy.ChangeState(
                new EnemyChaseState(enemy)
            );

            return;
        }
    }

    public void Exit()
    {
        // İSTATİSTİK: saldırı vuruş anına ulaşmadan bitti mi?
        // (Oyuncu vurdu / denge kırıldı / zehir.) Düşman öldüyse ya da
        // oyuncu öldüğü için Idle'a dönüldüyse sayılmaz.
        if (
            !attackDone &&
            !resolvingHit &&
            !enemy.IsDead &&
            !enemy.IsTargetDead
        )
        {
            CombatEvents.RaiseAttackInterrupted(enemy, isUnblockable);
        }

        // Saldırı herhangi bir sebeple yarıda kesilirse
        // (stagger, oyuncu öldü vb.) slotu serbest bırak.
        EnemyAttackCoordinator.ReleaseAttack(enemy);

        // Yarıda kesilirse yön kilidi asla açık kalmasın.
        enemy.LockFacing(false);

        if (indicator != null)
            indicator.Hide();

        if (telegraph != null)
            telegraph.StopWarning();

        StopMovement();
    }

    // =========================================================
    // MOVEMENT
    // =========================================================

    private void StopMovement()
    {
        if (rb == null)
            return;

        rb.linearVelocity =
            new Vector2(
                0f,
                rb.linearVelocity.y
            );
    }

    // =========================================================
    // WARNING
    // =========================================================

    private void PlayWarning()
    {
        if (attackAudio == null)
            return;

        if (isUnblockable)
            attackAudio.PlayUnblockableWarning();
        else
            attackAudio.PlayWarning();
    }

    // =========================================================
    // ATTACK
    // =========================================================

    private bool DoAttack()
    {
        if (enemy.target == null)
            return false;

        PlayerController player =
            enemy.target.GetComponent<PlayerController>();

        if (player == null)
        {
            Debug.LogWarning(
                "EnemyAttackState: PlayerController bulunamadı!"
            );

            return false;
        }

        // -----------------------------------------------------
        // PLAYER INVINCIBLE
        // -----------------------------------------------------

        // Dash i-frame'i VEYA (engellenemez vuruşta) dash'in hemen
        // ardından gelen kısa kaçış payı.
        bool dodged =
            player.isInvincible ||
            (
                isUnblockable &&
                Time.time - lastDashSeenTime <=
                enemy.unblockableDodgeGrace
            );

        if (dodged)
        {
            Debug.Log(
                "ENEMY ATTACK CANCELLED → PLAYER DODGED"
            );

            CombatEvents.RaiseDodge(enemy, isUnblockable);

            return false;
        }

        // -----------------------------------------------------
        // RANGE
        // -----------------------------------------------------

        float distance =
            Vector2.Distance(
                enemy.transform.position,
                enemy.target.position
            );

        // Engellenemez vuruşta erişim biraz kısalır: geri çekilmek kolaylaşır.
        float reach =
            isUnblockable
                ? enemy.attackRange * enemy.unblockableReachMultiplier
                : enemy.attackRange;

        if (!IsInReach(player, distance, reach))
        {
            Debug.Log(
                "ENEMY ATTACK MISSED!"
            );

            CombatEvents.RaiseAttackMissed(enemy, isUnblockable);

            return false;
        }

        Vector2 hitDirection =
            (
                player.transform.position -
                enemy.transform.position
            ).normalized;

        PlayerDefenseController defense =
            enemy.target.GetComponent<
                PlayerDefenseController
            >();

        // =====================================================
        // ENGELLENEMEZ VURUŞ
        // Parry ve block TAMAMEN etkisiz: hasar direkt can birimine gider.
        // Tek cevap: dash (i-frame, yukarıda kontrol edildi), zıplama,
        // arkaya geçme ya da kutunun dışına çıkma.
        // =====================================================

        if (isUnblockable)
        {
            Debug.Log(
                "UNBLOCKABLE HIT → PARRY/BLOCK IGNORED → DIRECT HEALTH DAMAGE"
            );

            return DealDirectHit(hitDirection);
        }

        // =====================================================
        // PARRY
        // =====================================================

        if (
            defense != null &&
            defense.CanParry()
        )
        {
            Debug.Log("PLAYER PARRY!");

            defense.PlayParryFeedback();

            EnemyHitFeedback hitFeedback =
                enemy.GetComponent<
                    EnemyHitFeedback
                >();

            if (hitFeedback != null)
            {
                hitFeedback.PlayParry(
                    hitDirection
                );
            }

            bool enemyStaggered =
                HandleParry(
                    hitDirection
                );

            if (enemyStaggered)
            {
                Debug.Log(
                    "PARRY → ENEMY BALANCE BROKEN → STAGGER"
                );
            }
            else
            {
                Debug.Log(
                    "PARRY → ENEMY BALANCE DAMAGED → RECOVERY"
                );
            }

            return enemyStaggered;
        }

        // =====================================================
        // BLOCK
        // =====================================================

        if (
            defense != null &&
            defense.CanBlock()
        )
        {
            Debug.Log("PLAYER BLOCK!");

            EnemyHitFeedback hitFeedback =
                enemy.GetComponent<
                    EnemyHitFeedback
                >();

            if (hitFeedback != null)
            {
                hitFeedback.PlayBlock(
                    hitDirection
                );
            }

            CombatImpactFeedback combatFeedback =
                enemy.target.GetComponent<
                    CombatImpactFeedback
                >();

            if (combatFeedback != null)
            {
                combatFeedback.PlayBlockImpact();
            }

            HandleBlock(
                hitDirection
            );

            return false;
        }

        // =====================================================
        // NORMAL HIT
        // =====================================================

        return DealDirectHit(hitDirection);
    }

    // Hasar + knockback tek çağrıda (normal ve engellenemez vuruş ortak).
    private bool DealDirectHit(Vector2 hitDirection)
    {
        PlayerDamageReceiver damageReceiver =
            enemy.target.GetComponent<
                PlayerDamageReceiver
            >();

        if (damageReceiver == null)
        {
            Debug.LogError(
                "EnemyAttackState: " +
                "PlayerDamageReceiver " +
                "Player üzerinde bulunamadı!"
            );

            return false;
        }

        Debug.Log(
            "ENEMY HIT PLAYER"
        );

        int damage =
            isUnblockable
                ? enemy.unblockableDamage
                : enemy.attackDamage;

        float knockbackMultiplier =
            isUnblockable
                ? enemy.unblockableKnockbackMultiplier
                : 1f;

        damageReceiver.TakeDamage(
            damage,
            hitDirection,
            enemy.attackKnockbackForce * knockbackMultiplier,
            enemy.attackKnockbackVerticalForce * knockbackMultiplier,
            enemy.attackKnockbackDuration,
            enemy.attackKnockbackDeceleration,
            enemy,
            isUnblockable
                ? PlayerHitKind.Unblockable
                : PlayerHitKind.Normal
        );

        // Oyuncu vuruldu: ardışık vuruş yağmurunu kes.
        EnemyAttackCoordinator.NotifyPlayerHit();

        return false;
    }

    // =========================================================
    // BLOCK
    // =========================================================

    private void HandleBlock(
        Vector2 hitDirection
    )
    {
        enemy.ApplyBlockKnockback(
            hitDirection
        );

        PlayerDefenseController defense =
            enemy.target.GetComponent<
                PlayerDefenseController
            >();

        if (defense != null)
        {
            // FIX: PlayerPosture max 100 iken block başına
            // sadece 1 hasar (blockBalanceDamage) veriliyordu;
            // posture neredeyse hiç kırılmıyordu.
            defense.HandleBlockHit(
                hitDirection,
                enemy.blockPostureDamage
            );
        }

        PlayerPosture posture =
            enemy.target.GetComponent<PlayerPosture>();

        CombatEvents.RaisePlayerBlocked(
            enemy,
            posture != null && posture.IsBroken
        );

        Debug.Log(
            "PLAYER BLOCK → " +
            "NO ENEMY BALANCE DAMAGE"
        );
    }

    // =========================================================
    // PARRY
    // =========================================================

    private bool HandleParry(
        Vector2 hitDirection
    )
    {
        EnemyBalance balance =
            enemy.GetComponent<
                EnemyBalance
            >();

        if (balance == null)
        {
            Debug.LogWarning(
                "EnemyAttackState: " +
                "EnemyBalance bulunamadı!"
            );

            return false;
        }

        Debug.Log(
            "PARRY → ENEMY BALANCE +" +
            enemy.parryBalanceDamage
        );

        balance.AddBalanceDamage(
            enemy.parryBalanceDamage
        );

        // Parry de düşmanın dengesine vuruyor: beyaz flaş.
        enemy.PlayBalanceDamageFlash();

        // Parry DÜŞMANLAR için zamanı yavaşlatır (oyuncu için değil):
        // karşı saldırı için zaman. Animasyon ve ses de yavaşlar.
        enemy.PlayParrySlowMotion(balance.IsBroken);

        CombatEvents.RaiseParry(enemy, balance.IsBroken);

        // Balance kırıldıysa EnemyBalance.OnBalanceBroken
        // üzerinden EnemyController.HandleBalanceBroken()
        // zaten ForceStagger() çağırıyor.
        if (balance.IsBroken)
        {
            Debug.Log(
                "PARRY → ENEMY BALANCE BROKEN → STAGGER"
            );

            return true;
        }

        // Balance kırılmadıysa enemy stagger'a girmez.
        // AttackState recovery'ye devam eder.
        return false;
    }
}
