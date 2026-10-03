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

    // Kararlı aşama (super armor) takibi
    private float warningDuration;
    private bool commitTriggered;

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

            // 1 = kararlı aşama yok (eski davranış)
            if (enemy.attackCommitPoint >= 1f)
                return false;

            float progress =
                1f -
                (warningTimer / warningDuration);

            return progress >= enemy.attackCommitPoint;
        }
    }

    private void TriggerCommit()
    {
        commitTriggered = true;

        if (telegraph != null)
            telegraph.SetCommitted();

        if (attackAudio != null)
            attackAudio.PlayCommit();
    }

    public void Enter()
    {
        enemy.PlayAttackAnimation();
        warningTimer =
            enemy.attackWarningTime;

        warningDuration =
            Mathf.Max(
                0.0001f,
                enemy.attackWarningTime
            );

        commitTriggered = false;

        recoveryTimer = 0f;

        attackDone = false;
        isRecovering = false;

        StopMovement();

        PlayWarning();

        if (telegraph != null)
            telegraph.StartWarning();
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

            warningTimer -= Time.deltaTime;

            if (!commitTriggered && IsCommitted)
                TriggerCommit();

            if (warningTimer > 0f)
                return;

            if (telegraph != null)
                telegraph.StopWarning();

            bool enemyStaggered =
                DoAttack();

            attackDone = true;

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
            enemy.StartAttackRecovery();

            recoveryTimer =
                enemy.attackRecoveryTime;

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

            recoveryTimer -= Time.deltaTime;

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
        // Saldırı herhangi bir sebeple yarıda kesilirse
        // (stagger, oyuncu öldü vb.) slotu serbest bırak.
        EnemyAttackCoordinator.ReleaseAttack(enemy);

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

        if (player.isInvincible)
        {
            Debug.Log(
                "ENEMY ATTACK CANCELLED → PLAYER INVINCIBLE"
            );

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

        if (distance > enemy.attackRange)
        {
            Debug.Log(
                "ENEMY ATTACK MISSED!"
            );

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

        // FIX: Hasar + knockback tek çağrıda.
        // Eskiden TakeDamage (HurtState knockback'i) ve
        // ApplyKnockback (PlayerKnockback) ayrı ayrı çağrılıp
        // birbirinin hızını eziyordu.
        damageReceiver.TakeDamage(
            enemy.attackDamage,
            hitDirection,
            enemy.attackKnockbackForce,
            enemy.attackKnockbackVerticalForce,
            enemy.attackKnockbackDuration
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