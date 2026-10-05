using UnityEngine;

/// <summary>
/// Düşman saldırısı. İki mod:
///
///  KLASİK (EnemyMoveset yok): tek vuruş, normal ya da engellenemez
///  (eski davranışın aynısı).
///
///  HAMLE (EnemyMoveset var): seçilen hamlenin vuruşlarını SIRAYLA oynar
///  (kombo). Her vuruşun türü farklı cevap ister:
///    Normal → parry/block, Sweep → zıpla, Grab → dash/kaç,
///    Shot (ok, mermi) → parry = geri yansıt, block, dash = içinden geç.
///  Kombonun 2.+ vuruşları kesilemez; block'lanan ara vuruşlar düşmanı
///  geri itmez.
/// </summary>
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

    // Parry/block işe yaramayan vuruş (Grab ya da Sweep).
    private bool isUnblockable;

    // Saldırı animasyonu zamanlaması: uzun uyarıda animasyon geç başlatılır,
    // vuruş karesi hasar anına denk gelsin.
    private bool animationStarted;
    private float animationDelay;
    private float baseWindup;

    // Engellenemez vuruş okunurluğu + kaçış payı
    private EnemyDangerIndicator indicator;
    private PlayerController playerRef;
    private bool dodgeCueTriggered;
    private float lastDashSeenTime;

    // Hamle (kombo)
    private EnemyMoveset moveset;
    private AttackMove move;
    private int stepIndex;
    private MoveHitType hitType = MoveHitType.Normal;
    private float stepDamageMultiplier = 1f;
    private float stepReachMultiplier = 1f;

    // Kombo okunurluğu: başın üstünde vuruş noktaları.
    private EnemyComboIndicator comboIndicator;

    // Süpürme uyarısında kutunun rengini geçici değiştiririz.
    private Color originalDangerColor;
    private bool dangerColorChanged;

    // Engellenemez vuruşa karşılıklar (kusursuz kaçış / atla-vur) ve
    // "ne yapmalıyım" yazısı.
    private UnblockableCounter counter;
    private EnemyDangerLabel dangerLabel;

    // Bu vuruşun uyarısı sırasında dash'in BAŞLADIĞI an.
    private bool prevPlayerDashing;
    private float dashStartRemaining = -1f;
    private float dashStartX;

    // "Şimdi kaç" işaretinin vuruştan ne kadar önce çıktığı.
    private float cueLead;

    // Kusursuz kaçış oldu: kombo kesilir, düşman uzun süre açık kalır.
    private bool counterLanded;

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
    // DIŞARIYA AÇIK BİLGİ (animasyon, derinlik sıralaması, istatistik)
    // =========================================================

    // Uyarının attackCommitPoint oranından sonra saldırı hasar alınca
    // kesilmez. Kombonun 2.+ vuruşları her zaman kesilmez.
    public bool IsCommitted
    {
        get
        {
            if (attackDone)
                return false;

            if (stepIndex > 0)
                return true;

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

    public bool IsWindingUp => !attackDone;

    public bool IsUnblockable => isUnblockable;

    public float RemainingWindup =>
        Mathf.Max(0f, warningTimer);

    // Kombo içinde kaçıncı vuruş (animasyon her vuruşta baştan başlar).
    public int StepIndex => stepIndex;

    public MoveHitType CurrentHitType => hitType;

    public string MoveName => move != null ? move.name : "";

    public bool HasMoreSteps =>
        move != null &&
        move.hits != null &&
        stepIndex < move.hits.Count - 1;

    private bool IsSweep => hitType == MoveHitType.Sweep;

    private bool IsCombo =>
        move != null &&
        move.hits != null &&
        move.hits.Count > 1;

    private void ShowCombo()
    {
        if (!IsCombo || moveset == null || !moveset.showComboIndicator)
            return;

        comboIndicator = enemy.GetComponent<EnemyComboIndicator>();

        if (comboIndicator == null)
        {
            comboIndicator =
                enemy.gameObject.AddComponent<EnemyComboIndicator>();
        }

        comboIndicator.Show(move.hits, moveset.sweepColor);
    }

    private void HideCombo()
    {
        if (comboIndicator != null)
            comboIndicator.Hide();
    }

    // =========================================================
    // ENTER
    // =========================================================

    public void Enter()
    {
        playerRef =
            enemy.target != null
                ? enemy.target.GetComponent<PlayerController>()
                : null;

        lastDashSeenTime = -999f;
        stepIndex = 0;
        counterLanded = false;

        counter = enemy.GetComponent<UnblockableCounter>();

        if (counter == null)
            counter = enemy.gameObject.AddComponent<UnblockableCounter>();

        originalDangerColor = enemy.dangerColor;
        dangerColorChanged = false;

        moveset = enemy.GetComponent<EnemyMoveset>();

        move =
            moveset != null && moveset.enabled
                ? moveset.PickMove(DistanceToTarget())
                : null;

        // Planlanan normal/engellenemez zarı her durumda tüket
        // (EnemyController'ın sayaçları bozulmasın).
        bool plannedUnblockable =
            enemy.ConsumePlannedAttack();

        // YÖN KİLİDİ: saldırı başladığı anda baktığı yönü sabitle ve
        // saldırı bitene kadar oyuncuya dönme. Dash ile arkasına geçen
        // oyuncu vurulmaz (vuruş sadece ön alana isabet eder).
        if (enemy.lockFacingDuringAttack)
            enemy.LockFacing(true);

        if (move != null)
        {
            Debug.Log("ENEMY MOVE: " + move.name);

            ShowCombo();

            BeginStep(move.hits[0]);
            return;
        }

        // ---------------- KLASİK TEK VURUŞ ----------------

        hitType =
            plannedUnblockable
                ? MoveHitType.Grab
                : MoveHitType.Normal;

        stepDamageMultiplier = 1f;
        stepReachMultiplier = 1f;

        baseWindup =
            enemy.WindupFor(plannedUnblockable);

        // Felç Edici Zehir: zehirli düşmanın uyarısı uzar.
        BeginWindup(
            baseWindup * EnemyStatus.WindupMultiplierFor(enemy)
        );
    }

    private void BeginStep(MoveHit hit)
    {
        hitType = hit.type;
        stepDamageMultiplier = hit.damageMultiplier;
        stepReachMultiplier = hit.reachMultiplier;

        baseWindup =
            hit.windup +
            (hit.windupRandom > 0f ? Random.Range(0f, hit.windupRandom) : 0f);

        // Kombonun İLK vuruşu biraz daha uzun hazırlanır: noktaları okuyacak zaman.
        if (stepIndex == 0 && IsCombo && moveset != null)
            baseWindup += moveset.comboFirstWindupBonus;

        if (comboIndicator != null)
            comboIndicator.SetCurrent(stepIndex);

        BeginWindup(
            baseWindup * EnemyStatus.WindupMultiplierFor(enemy)
        );
    }

    // Bir vuruşun uyarı aşamasını başlatır (ilk vuruş ya da kombo adımı).
    private void BeginWindup(float windup)
    {
        // Ok (Shot) parry/block edilebilir: engellenemez değil.
        isUnblockable =
            hitType == MoveHitType.Sweep ||
            hitType == MoveHitType.Grab;

        dodgeCueTriggered = false;

        // Dash takibi her vuruşta sıfırlanır. Uyarı başladığında zaten
        // dash'teyse bu "yeni" bir dash sayılmaz.
        prevPlayerDashing = playerRef != null && playerRef.isDashing;
        dashStartRemaining = -1f;

        warningTimer = windup;

        warningDuration =
            Mathf.Max(
                0.0001f,
                windup
            );

        // Animasyon zamanlaması:
        // Normal: hemen başlar (zehirle uzadıysa uzama kadar gecikir).
        // Engellenemez: vuruş karesi hasar anına gelecek şekilde GEÇ başlar.
        // (EnemyAnimationDriver varsa hizalamayı o yapar.)
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

        StopMovement();

        PlayWarning();

        if (telegraph != null)
            telegraph.StartWarning(isUnblockable);

        if (isUnblockable)
        {
            // StartWarning flaş bastırmasını sıfırladığı için SONRA çağrılır.
            enemy.PlayAlertFlash();

            ShowIndicator();

            Debug.Log(
                IsSweep
                    ? "ENEMY SWEEP STARTED (ZIPLA)"
                    : "ENEMY UNBLOCKABLE ATTACK STARTED"
            );
        }
        else
        {
            HideIndicator();
        }
    }

    private void ShowIndicator()
    {
        indicator =
            enemy.GetComponent<EnemyDangerIndicator>();

        if (indicator == null)
        {
            indicator =
                enemy.gameObject
                    .AddComponent<EnemyDangerIndicator>();
        }

        ShowDangerLabel();

        if (IsSweep)
        {
            // Alçak, renkli kutu: "üstünden zıpla".
            if (moveset != null)
            {
                enemy.dangerColor = moveset.sweepColor;
                dangerColorChanged = true;
            }

            indicator.Show(SweepReach, SweepHeight, true);
            return;
        }

        RestoreDangerColor();

        if (enemy.unblockableFrontOnly)
        {
            // Düşmanın baktığı yöne doğru uzun kutu.
            indicator.Show(
                enemy.unblockableForwardReach * stepReachMultiplier,
                enemy.unblockableHitHeight,
                true
            );
        }
        else
        {
            indicator.Show(
                enemy.attackRange *
                enemy.unblockableReachMultiplier *
                stepReachMultiplier
            );
        }
    }

    private void HideIndicator()
    {
        if (indicator != null)
            indicator.Hide();

        if (dangerLabel != null)
            dangerLabel.Hide();

        RestoreDangerColor();
    }

    // Başın üstünde "DASH!" / "ZIPLA!" yazısı.
    private void ShowDangerLabel()
    {
        dangerLabel = enemy.GetComponent<EnemyDangerLabel>();

        if (dangerLabel == null)
            dangerLabel = enemy.gameObject.AddComponent<EnemyDangerLabel>();

        if (IsSweep && moveset != null)
            dangerLabel.Show(hitType, moveset.sweepColor);
        else
            dangerLabel.Show(hitType);
    }

    private void RestoreDangerColor()
    {
        if (!dangerColorChanged)
            return;

        enemy.dangerColor = originalDangerColor;
        dangerColorChanged = false;
    }

    private float SweepReach =>
        enemy.attackRange *
        (moveset != null ? moveset.sweepReachMultiplier : 1.4f) *
        stepReachMultiplier;

    private float SweepHeight =>
        moveset != null ? moveset.sweepHitHeight : 0.6f;

    private float DistanceToTarget()
    {
        return enemy.target != null
            ? Vector2.Distance(
                enemy.transform.position,
                enemy.target.position
            )
            : 0f;
    }

    // =========================================================
    // ENGELLENEMEZ UYARI AŞAMASI
    // =========================================================

    // Her karede: işaretleri ilerlet, dash'i takip et, "ŞİMDİ KAÇ" zamanını yakala.
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

        // Kusursuz kaçış için: dash'in BAŞLADIĞI anı (vuruşa kalan süre
        // ve oyuncunun konumu) kaydet. Sadece ilk dash sayılır.
        bool dashingNow = playerRef != null && playerRef.isDashing;

        if (dashingNow && !prevPlayerDashing && dashStartRemaining < 0f)
        {
            dashStartRemaining = Mathf.Max(0f, warningTimer);
            dashStartX = playerRef.transform.position.x;
        }

        prevPlayerDashing = dashingNow;

        // Uyarı kısaysa işaret hemen başta çıkmasın.
        float lead =
            Mathf.Min(
                enemy.unblockableDodgeCueLead,
                warningDuration * 0.7f
            );

        cueLead = lead;

        if (!dodgeCueTriggered && warningTimer <= lead)
            TriggerDodgeCue();
    }

    // Vuruş isabet alanında mı?
    private bool IsInReach(
        PlayerController player,
        float distance
    )
    {
        if (IsSweep)
        {
            // Alçak kutu: yeterince zıplayan oyuncu üstünden geçer.
            return IsInFrontArea(player, SweepReach, SweepHeight);
        }

        if (isUnblockable)
        {
            if (enemy.unblockableFrontOnly)
            {
                return IsInFrontArea(
                    player,
                    enemy.unblockableForwardReach * stepReachMultiplier,
                    enemy.unblockableHitHeight
                );
            }

            return
                distance <=
                enemy.attackRange *
                enemy.unblockableReachMultiplier *
                stepReachMultiplier;
        }

        if (enemy.normalAttackFrontOnly)
        {
            return IsInFrontArea(
                player,
                enemy.attackRange * stepReachMultiplier,
                enemy.normalAttackHitHeight
            );
        }

        return distance <= enemy.attackRange * stepReachMultiplier;
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

        if (dangerLabel != null)
            dangerLabel.SetUrgent();

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

    // =========================================================
    // TICK
    // =========================================================

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

            HideIndicator();

            if (telegraph != null)
                telegraph.StopWarning();

            bool enemyStaggered;

            if (hitType == MoveHitType.Shot)
            {
                // OK: vuruş anında mermi çıkar; sonucu mermi çözer
                // (parry / block / dash / isabet).
                FireShot();
                enemyStaggered = false;
            }
            else
            {
                resolvingHit = true;

                enemyStaggered =
                    DoAttack();

                resolvingHit = false;
            }

            // Vuruş sırasında state değiştiyse (parry dengeyi kırdı, block
            // savrulması...) bu saldırı bitti; Exit zaten temizledi.
            if (
                enemyStaggered ||
                !ReferenceEquals(enemy.CurrentState, this)
            )
            {
                attackDone = true;

                EnemyAttackCoordinator.ReleaseAttack(enemy);

                if (enemyStaggered)
                {
                    Debug.Log(
                        "ENEMY ATTACK → PARRY BROKE BALANCE → STAGGER"
                    );
                }

                return;
            }

            // ---------------- KOMBO: SIRADAKİ VURUŞ ----------------

            // Kusursuz kaçış komboyu keser.
            if (HasMoreSteps && !enemy.IsTargetDead && !counterLanded)
            {
                stepIndex++;

                BeginStep(move.hits[stepIndex]);

                return;
            }

            attackDone = true;

            HideCombo();

            // Varsayılan: kilit saldırı bitene (recovery dahil) kadar sürer;
            // Exit'te açılır. İstenirse vuruş anında açılır.
            if (enemy.releaseFacingAtHit)
                enemy.LockFacing(false);

            // Vuruş anı geçti: ritim koordinatörüne bildir.
            EnemyAttackCoordinator.ReleaseAttack(enemy);

            // Engellenemez/süpürme recovery'si uzun: kaçılan vuruş
            // düşmanı uzun süre açık hedef bırakır. Uzun kombo da öyle.
            float recovery =
                isUnblockable
                    ? enemy.attackRecoveryTime *
                      enemy.unblockableRecoveryMultiplier
                    : enemy.attackRecoveryTime;

            if (move != null)
                recovery *= move.recoveryMultiplier;

            // Kusursuz kaçış: düşman daha uzun süre açık kalır.
            if (counterLanded && counter != null)
                recovery *= counter.RecoveryMultiplier;

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

        HideIndicator();

        HideCombo();

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

        bool dodged;

        if (IsSweep && SweepIgnoresDash)
        {
            // Süpürme: dash'in dokunulmazlığı işe yaramaz (zıplamak gerekir).
            // Hasar sonrası korumalı dönem yine korur.
            dodged =
                player.hitInvincibilityTimer > 0f ||
                (player.isInvincible && !player.isDashing);
        }
        else
        {
            // Dash i-frame'i VEYA (engellenemez vuruşta) dash'in hemen
            // ardından gelen kısa kaçış payı.
            dodged =
                player.isInvincible ||
                (
                    isUnblockable &&
                    Time.time - lastDashSeenTime <=
                    enemy.unblockableDodgeGrace
                );
        }

        if (dodged)
        {
            Debug.Log(
                "ENEMY ATTACK CANCELLED → PLAYER DODGED"
            );

            CombatEvents.RaiseDodge(enemy, isUnblockable);

            // KUSURSUZ KAÇIŞ: yakalamaya son anda, düşmana doğru dash.
            if (
                hitType == MoveHitType.Grab &&
                counter != null &&
                counter.TryDashCounter(
                    player,
                    dashStartRemaining,
                    dashStartX,
                    cueLead,
                    out bool counterBroke
                )
            )
            {
                counterLanded = true;

                // Denge kırıldıysa stagger başladı (parry ile aynı yol).
                return counterBroke;
            }

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

        if (!IsInReach(player, distance))
        {
            Debug.Log(
                IsSweep
                    ? "ENEMY SWEEP MISSED (ZIPLADI / MENZİL DIŞI)"
                    : "ENEMY ATTACK MISSED!"
            );

            CombatEvents.RaiseAttackMissed(enemy, isUnblockable);

            // ATLA-VUR: süpürmenin TAM üstündeydi (menzil içinde ama
            // yüksekte) → karşı vuruş penceresi.
            if (IsSweep && counter != null && JumpedOverSweep(player))
                counter.OpenJumpWindow();

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
        // ENGELLENEMEZ / SÜPÜRME
        // Parry ve block TAMAMEN etkisiz: hasar direkt cana gider.
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

            Debug.Log(
                enemyStaggered
                    ? "PARRY → ENEMY BALANCE BROKEN → STAGGER"
                    : "PARRY → ENEMY BALANCE DAMAGED"
            );

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

    // Ok bırak: baktığı yöne (yön kilitli). Oyuncu arkasındaysa düz ileri
    // atar (ıska): dash ile arkasına geçmek oku da boşa çıkarır.
    private void FireShot()
    {
        EnemyArcher archer = enemy.GetComponent<EnemyArcher>();

        if (archer == null)
            archer = enemy.gameObject.AddComponent<EnemyArcher>();

        int damage =
            Mathf.Max(
                1,
                Mathf.RoundToInt(enemy.attackDamage * stepDamageMultiplier)
            );

        archer.Fire(damage);
    }

    private bool SweepIgnoresDash =>
        moveset == null || moveset.sweepIgnoresDash;

    // Yatayda süpürmenin menzilinde ama kutunun ÜSTÜNDE: atladı.
    // (Sadece geri çekilip menzil dışında kalan oyuncu ödül almaz.)
    private bool JumpedOverSweep(PlayerController player)
    {
        return
            IsInFrontArea(player, SweepReach, 1000f) &&
            !IsInFrontArea(player, SweepReach, SweepHeight);
    }

    // Hasar + knockback tek çağrıda (normal, süpürme ve engellenemez ortak).
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

        int baseDamage =
            hitType == MoveHitType.Grab
                ? enemy.unblockableDamage
                : enemy.attackDamage;

        int damage =
            Mathf.Max(
                1,
                Mathf.RoundToInt(baseDamage * stepDamageMultiplier)
            );

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
                : PlayerHitKind.Normal,
            IsSweep && SweepIgnoresDash
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
        // Kombonun ara vuruşu block'lanınca düşman geri itilmez:
        // kombo sürer, her vuruş posture yer (parry'e teşvik).
        if (!HasMoreSteps)
        {
            enemy.ApplyBlockKnockback(
                hitDirection
            );
        }

        PlayerDefenseController defense =
            enemy.target.GetComponent<
                PlayerDefenseController
            >();

        if (defense != null)
        {
            // FIX: PlayerPosture max 100 iken block başına
            // sadece 1 hasar (blockBalanceDamage) veriliyordu;
            // posture neredeyse hiç kırılmıyordu.
            // Kombo vuruşları block'ta daha az posture yer: kombo boyunca
            // block'ta durmak YAPILABİLİR olsun (ama parry daha kârlı).
            float postureMultiplier =
                IsCombo && moveset != null
                    ? moveset.comboBlockPostureMultiplier
                    : 1f;

            defense.HandleBlockHit(
                hitDirection,
                Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        enemy.blockPostureDamage * postureMultiplier
                    )
                )
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

        // İmza saldırısının son vuruşu gibi: vuruşa özel parry ödülü.
        float reward =
            move != null && move.hits != null && stepIndex >= 0 && stepIndex < move.hits.Count
                ? move.hits[stepIndex].parryBalanceMultiplier
                : 1f;

        int parryDamage =
            Mathf.Max(1, Mathf.RoundToInt(enemy.parryBalanceDamage * Mathf.Max(0f, reward)));

        balance.AddBalanceDamage(parryDamage);

        if (reward >= 2f)
        {
            CombatCallout.PopupAbove(enemy, "KUSURSUZ!", new Color(1f, 0.85f, 0.3f), 1f);
        }

        // Parry de düşmanın dengesine vuruyor: beyaz flaş.
        enemy.PlayBalanceDamageFlash();

        // Parry DÜŞMANLAR için zamanı yavaşlatır (oyuncu için değil):
        // karşı saldırı için zaman. Animasyon ve ses de yavaşlar.
        // Kombo ORTASINDA yavaşlatmaz (ritim bozulmasın, sonraki vuruşlar
        // okunabilsin); son vuruşta ya da denge kırılınca yavaşlatır.
        bool midCombo =
            HasMoreSteps &&
            (moveset == null || moveset.noParrySlowMoMidCombo);

        if (!midCombo || balance.IsBroken)
            enemy.PlayParrySlowMotion(balance.IsBroken);

        CombatEvents.RaiseParry(enemy, balance.IsBroken);

        // Balance kırıldıysa EnemyBalance.OnBalanceBroken
        // üzerinden EnemyController.HandleBalanceBroken()
        // zaten ForceStagger() çağırıyor.
        return balance.IsBroken;
    }
}
