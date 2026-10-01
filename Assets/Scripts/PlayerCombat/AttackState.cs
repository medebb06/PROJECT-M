
using UnityEngine;
using System;

public class AttackState : ICombatState
{
    PlayerController player;
    LayerMask enemyLayer;
    int step;
    Action onEnd;

    float moveDistance;
    float moveSpeed;
    AnimationCurve moveCurve;

    float hitStopScale;
    float hitStopDuration;

    float duration;

    // =====================================================
    // CUSTOM ATTACK TIMING
    // =====================================================

    float moveStart;
    float moveEnd;
    float hitTime;

    Vector2 start;
    Vector2 target;

    float t;

    bool hasHit;

    const float collisionSkin = 0.02f;

    public AttackState(
        PlayerController player,
        LayerMask enemyLayer,
        int step,
        Action onEnd,
        float hitStopScale,
        float hitStopDuration,
        float moveDistance,
        float moveSpeed,
        AnimationCurve moveCurve,
        float attackDuration,
        float moveStart,
        float moveEnd,
        float hitTime
    )
    {
        this.player = player;
        this.enemyLayer = enemyLayer;
        this.step = step;
        this.onEnd = onEnd;

        this.hitStopScale = hitStopScale;
        this.hitStopDuration = hitStopDuration;

        this.moveDistance = moveDistance;
        this.moveSpeed = moveSpeed;
        this.moveCurve = moveCurve;

        this.duration = attackDuration;

        this.moveStart = moveStart;
        this.moveEnd = moveEnd;
        this.hitTime = hitTime;
    }

    public void Enter()
    {
        t = 0f;
        hasHit = false;

        // =====================================================
        // ATTACK ANIMATION
        // =====================================================

        player.PlayAttackAnimation(step);

        Vector2 dir =
            player.facingDir > 0
                ? Vector2.right
                : Vector2.left;

        start =
            player.rb.position;

        // =====================================================
        // ATTACK TARGET
        // =====================================================

        target =
            start +
            dir * moveDistance;

        // =====================================================
        // COLLISION CHECK
        // =====================================================

        Collider2D playerCollider =
            player.GetComponent<Collider2D>();

        if (
            playerCollider != null &&
            moveDistance > 0f
        )
        {
            RaycastHit2D[] hits =
                new RaycastHit2D[10];

            ContactFilter2D filter =
                new ContactFilter2D();

            LayerMask movementCollisionMask =
                enemyLayer |
                player.Movement.wallMask;

            filter.SetLayerMask(
                movementCollisionMask
            );

            filter.useTriggers = false;

            int hitCount =
                playerCollider.Cast(
                    dir,
                    filter,
                    hits,
                    moveDistance +
                    collisionSkin
                );

            float allowedDistance =
                moveDistance;

            for (
                int i = 0;
                i < hitCount;
                i++
            )
            {
                if (hits[i].collider == null)
                    continue;

                if (
                    hits[i].distance <
                    allowedDistance
                )
                {
                    allowedDistance =
                        Mathf.Max(
                            0f,
                            hits[i].distance -
                            collisionSkin
                        );
                }
            }

            target =
                start +
                dir * allowedDistance;
        }

        // =====================================================
        // CLEAR HORIZONTAL MOMENTUM
        // =====================================================

        player.rb.linearVelocity =
            new Vector2(
                0f,
                player.rb.linearVelocity.y
            );
    }

    public void Tick()
    {
        if (
            !player.canAttack ||
            player.isDashing
        )
        {
            Exit();
            return;
        }

        t += Time.deltaTime;

        UpdateFacing();

        // =====================================================
        // NORMALIZED ATTACK TIME
        // =====================================================

        float n =
            duration > 0f
                ? Mathf.Clamp01(
                    t / duration
                )
                : 1f;

        // =====================================================
        // ATTACK MOVEMENT
        // =====================================================

        float movementT =
            Mathf.InverseLerp(
                moveStart,
                moveEnd,
                n
            );

        movementT =
            Mathf.Clamp01(
                movementT
            );

        float curve =
            moveCurve != null
                ? moveCurve.Evaluate(
                    movementT
                )
                : movementT;

        float desiredX =
            Mathf.Lerp(
                start.x,
                target.x,
                curve
            );

        player.rb.position =
            new Vector2(
                desiredX,
                player.rb.position.y
            );

        // =====================================================
        // HIT WINDOW
        // =====================================================

        if (
            !hasHit &&
            n >= hitTime
        )
        {
            Hit();

            hasHit = true;
        }

        // =====================================================
        // END
        // =====================================================

        if (n >= 1f)
        {
            Exit();
        }
    }

    private void UpdateFacing()
    {
        if (
            Mathf.Abs(
                player.moveInput
            ) < 0.01f
        )
            return;

        float newDirection =
            Mathf.Sign(
                player.moveInput
            );

        if (
            player.facingDir ==
            newDirection
        )
            return;

        player.facingDir =
            newDirection;

        Vector3 scale =
            player.modelPivot.localScale;

        scale.x =
            Mathf.Abs(scale.x) *
            player.facingDir;

        player.modelPivot.localScale =
            scale;
    }

    public void Exit()
    {
        player.rb.linearVelocity =
            new Vector2(
                0f,
                player.rb.linearVelocity.y
            );

        onEnd?.Invoke();
    }

    // =========================================================
    // HIT
    // =========================================================

    private void Hit()
    {
        Vector2 dir =
            player.facingDir > 0
                ? Vector2.right
                : Vector2.left;

        Vector2 boxCenter =
            player.attackPoint.position;

        PlayerCombatController combat =
            player.GetComponent<
                PlayerCombatController
            >();

        if (combat == null)
            return;

        Collider2D[] hits =
            Physics2D.OverlapBoxAll(
                boxCenter,
                combat.hitBoxSize,
                0f,
                enemyLayer
            );

        bool hitSomething = false;

        foreach (var h in hits)
        {
            if (h == null)
                continue;

            EnemyController enemy =
                h.GetComponentInParent<
                    EnemyController
                >();

            if (enemy == null)
                continue;

            hitSomething = true;

            // =================================================
            // BALANCE
            // =================================================

            EnemyBalance balance =
                enemy.GetComponentInParent<
                    EnemyBalance
                >();

            // =================================================
            // HEALTH
            // =================================================

            Health health =
                enemy.GetComponentInParent<
                    Health
                >();

            // =================================================
            // NORMAL BALANCE HIT
            // =================================================

            if (
                balance != null &&
                !balance.IsBroken
            )
            {
                bool balanceDamaged =
                    balance.AddBalanceDamage(1);

                if (!balanceDamaged)
                    continue;

                // =================================================
                // BALANCE HIT FEEDBACK
                // =================================================

                EnemyHitFeedback hitFeedback =
                    enemy.GetComponentInParent<
                        EnemyHitFeedback
                    >();

                if (hitFeedback != null)
                {
                    hitFeedback.PlayBalanceHit(
                        boxCenter,
                        dir
                    );
                }

                // Mevcut posture hit sesi
                enemy.PlayPostureHitSound();

                // Balance kırılmadı:
                // EnemyController Inspector değerlerini kullanır.

                if (!balance.IsBroken)
                {
                    enemy.ApplyBalanceHit(
                        dir
                    );
                }

                // Bu vuruş balance'ı kırdı:
                // Posture knockback kullanılır.

                else
                {
                    enemy.ApplyAttackHit(
                        dir,
                        false
                    );
                }
            }

            // =================================================
            // BALANCE ALREADY BROKEN → HEALTH
            // =================================================

            else if (
                balance != null &&
                balance.IsBroken
            )
            {
                if (health != null)
                {
                    health.TakeDamage(1);

                    enemy.PlayHealthHitSound();

                    enemy.ApplyAttackHit(
                        dir,
                        true
                    );
                }
            }

            // =================================================
            // NO BALANCE → HEALTH
            // =================================================

            else if (
                balance == null &&
                health != null
            )
            {
                health.TakeDamage(1);

                enemy.PlayHealthHitSound();

                enemy.ApplyAttackHit(
                    dir,
                    true
                );
            }
        }

        // =====================================================
        // ATTACK FEEDBACK
        // =====================================================

        if (hitSomething)
        {
            CombatImpactFeedback combatFeedback =
                player.GetComponent<
                    CombatImpactFeedback
                >();

            if (combatFeedback != null)
            {
                combatFeedback.PlayAttackImpact();
            }
        }

        // Her saldırıda kendi combo sesini çal
        if (player.audioPlayer != null)
        {
            player.audioPlayer.PlayAttackWoosh(
                step
            );
        }
    }
}

