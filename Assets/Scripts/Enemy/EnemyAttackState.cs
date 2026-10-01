using UnityEngine;

public class EnemyAttackState : IEnemyState
{
    private EnemyController enemy;
    private EnemyAttackAudio attackAudio;
    private EnemyAttackTelegraph telegraph;

    private float warningTimer;
    private float recoveryTimer;

    private bool attackDone;
    private bool isRecovering;

    public EnemyAttackState(EnemyController enemy)
    {
        this.enemy = enemy;

        attackAudio =
            enemy.GetComponent<EnemyAttackAudio>();

        telegraph =
            enemy.GetComponent<EnemyAttackTelegraph>();
    }

    public void Enter()
    {
        warningTimer =
            enemy.attackWarningTime;

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
        if (enemy.target == null)
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

            if (warningTimer > 0f)
                return;

            if (telegraph != null)
                telegraph.StopWarning();

            bool enemyStaggered =
                DoAttack();

            attackDone = true;

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
        if (telegraph != null)
            telegraph.StopWarning();

        StopMovement();
    }

    // =========================================================
    // MOVEMENT
    // =========================================================

    private void StopMovement()
    {
        Rigidbody2D rb =
            enemy.GetComponent<Rigidbody2D>();

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

        damageReceiver.TakeDamage(
            1,
            hitDirection
        );

        damageReceiver.ApplyKnockback(
            hitDirection,
            enemy.attackKnockbackForce,
            enemy.attackKnockbackVerticalForce,
            enemy.attackKnockbackDuration
        );

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
            defense.HandleBlockHit(
                hitDirection,
                enemy.blockBalanceDamage
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