
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

        player.StartCoroutine(
            player.FreezeFrame(
                player.slamFreezeTime
            )
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

        // Aynı enemy'de birden fazla collider varsa
        // aynı Slam'in birden fazla kez vurmasını engeller.
        HashSet<EnemyPosture> processedPostures =
            new HashSet<EnemyPosture>();

        HashSet<Health> processedHealth =
            new HashSet<Health>();

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
                continue;

            // =================================================
            // POSTURE
            // =================================================

            EnemyPosture posture =
                hit.GetComponentInParent<EnemyPosture>();

            // =================================================
            // HEALTH
            // =================================================

            Health health =
                hit.GetComponentInParent<Health>();

            // =================================================
            // POSTURE VARSA
            // =================================================

            if (posture != null)
            {
                // -------------------------------------------------
                // POSTURE HENÜZ KIRILMADI
                // -------------------------------------------------

                if (!posture.IsBroken)
                {
                    if (processedPostures.Add(posture))
                    {
                        posture.TakeDamage(
                            player.impactSettings.slamDamage
                        );

                        Debug.Log(
                            "SLAM → POSTURE DAMAGE: " +
                            posture.gameObject.name
                        );
                    }
                }

                // -------------------------------------------------
                // POSTURE ZATEN KIRIK
                // -------------------------------------------------

                else if (health != null)
                {
                    if (processedHealth.Add(health))
                    {
                        health.TakeDamage(
                            player.impactSettings.slamDamage
                        );

                        Debug.Log(
                            "SLAM → HEALTH DAMAGE: " +
                            health.gameObject.name
                        );
                    }
                }
            }

            // =================================================
            // POSTURE YOKSA → DIRECT HEALTH
            // =================================================

            else if (health != null)
            {
                if (processedHealth.Add(health))
                {
                    health.TakeDamage(
                        player.impactSettings.slamDamage
                    );

                    Debug.Log(
                        "SLAM → DIRECT HEALTH DAMAGE: " +
                        health.gameObject.name
                    );
                }
            }

            // =================================================
            // SMALL KNOCKBACK
            // =================================================

            Rigidbody2D enemyRb =
                hit.GetComponentInParent<Rigidbody2D>();

            if (enemyRb != null)
            {
                float direction =
                    Mathf.Sign(
                        enemyRb.position.x -
                        player.rb.position.x
                    );

                if (direction == 0f)
                {
                    direction =
                        player.facingDir;
                }

                enemyRb.linearVelocity =
                    new Vector2(
                        direction * 1.5f,
                        0.25f
                    );
            }
        }
    }
}
