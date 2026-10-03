using UnityEngine;
using System.Collections.Generic;

public class GroundSlamState : IPlayerState
{
    private PlayerController player;
    private PlayerStateMachine sm;

    private bool impactTriggered;

    public GroundSlamState(
        PlayerController player,
        PlayerStateMachine sm
    )
    {
        this.player = player;
        this.sm = sm;
    }

    public void Enter()
    {
        impactTriggered = false;

        player.canControl = false;

        // =====================================================
        // SLAM BAŞLANGICI
        // =====================================================

        Vector2 vel =
            player.rb.linearVelocity;

        // Horizontal momentum kes
        vel.x = 0f;

        // Aşağı doğru hızlı düş
        vel.y =
            -player.impactSettings.slamSpeed;

        player.SetVelocity(vel);
    }

    public void Exit()
    {
        player.canControl = true;
    }

    public void Update()
    {
        // =====================================================
        // DASH CANCEL
        // =====================================================

        if (
            player.dashPressed &&
            player.dashCooldownTimer <= 0f
        )
        {
            sm.ChangeState(
                new DashState(player, sm)
            );

            return;
        }

        // =====================================================
        // IMPACT
        // =====================================================

        if (
            player.isGrounded &&
            !impactTriggered
        )
        {
            impactTriggered = true;

            SlamImpact();

            sm.ChangeState(
                new GroundedState(player, sm)
            );
        }
    }

    public void FixedUpdate()
    {
        // =====================================================
        // SLAM DEVAM
        // =====================================================

        Vector2 vel =
            player.rb.linearVelocity;

        vel.x = 0f;

        vel.y =
            -player.impactSettings.slamSpeed;

        player.SetVelocity(vel);
    }

    // =========================================================
    // IMPACT
    // =========================================================

    void SlamImpact()
    {
        // =====================================================
        // INPUT LOCK
        // =====================================================

        player.inputLocked = true;

        player.inputLockTimer =
            player.inputLockDuration;

        // =====================================================
        // GROUND LOCK
        // =====================================================

        player.slamGroundLock = true;

        player.slamLockTimer =
            player.slamLockDuration;

        // =====================================================
        // HARD STOP
        // =====================================================

        player.rb.linearVelocity =
            Vector2.zero;

        player.SetVelocity(
            Vector2.zero
        );

        // =====================================================
        // FREEZE FRAME
        // =====================================================

        HitStop.Request(
            player.slamFreezeTime,
            0f
        );

        // =====================================================
        // CAMERA SHAKE
        // =====================================================

        if (player.impulseSource)
        {
            player.impulseSource.GenerateImpulse(
                1.5f
            );
        }

        // =====================================================
        // DUST
        // =====================================================

        player.SpawnDust();

        // =====================================================
        // ENEMY HIT
        // =====================================================

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                player.transform.position,
                player.impactSettings.slamDamageRadius,
                player.impactSettings.enemyLayer
            );

        // Aynı düşmanda birden fazla collider varsa
        // aynı slam'in birden fazla kez vurmasını engeller.
        HashSet<EnemyController> processedEnemies =
            new HashSet<EnemyController>();

        HashSet<Health> processedHealth =
            new HashSet<Health>();

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
                continue;

            EnemyController enemy =
                hit.GetComponentInParent<EnemyController>();

            // -------------------------------------------------
            // DÜŞMAN (EnemyController var)
            // -------------------------------------------------

            if (enemy != null)
            {
                if (processedEnemies.Add(enemy))
                {
                    DamageEnemy(enemy);
                }

                continue;
            }

            // -------------------------------------------------
            // DÜŞMAN OLMAYAN, CAN'I OLAN NESNE
            // -------------------------------------------------

            Health health =
                hit.GetComponentInParent<Health>();

            if (
                health != null &&
                processedHealth.Add(health)
            )
            {
                health.TakeDamage(
                    player.impactSettings.slamDamage
                );
            }
        }
    }

    // =========================================================
    // DAMAGE ENEMY
    // Normal saldırıyla (AttackState) aynı kural:
    // - Denge kırık değilse: sadece BALANCE hasarı
    // - Denge kırıksa (stagger): HEALTH hasarı
    // =========================================================

    void DamageEnemy(EnemyController enemy)
    {
        // Slam yönü: düşman oyuncunun neresindeyse o tarafa.
        float deltaX =
            enemy.transform.position.x -
            player.transform.position.x;

        float directionX =
            Mathf.Abs(deltaX) > 0.01f
                ? Mathf.Sign(deltaX)
                : (player.facingDir >= 0f ? 1f : -1f);

        // Aynı kural (denge kırık değilse denge, kırıksa can) ve aynı
        // stat/kritik/olay akışı: PlayerDamage.
        PlayerDamage.HitEnemy(
            enemy,
            new DamageInfo
            {
                source = DamageSource.Slam,
                balanceDamage = enemy.slamBalanceDamage,
                healthDamage = player.impactSettings.slamDamage,
                direction = new Vector2(directionX, 0f),
                hitPosition = enemy.transform.position
            }
        );
    }
}