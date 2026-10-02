
using UnityEngine;
using System;
using System.Collections.Generic;

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
    // PLAYER COMBAT CONTROLLER
    // =====================================================

    private PlayerCombatController combat;

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

    // =====================================================
    // ATTACK DIRECTION
    // =====================================================

    private float attackDirection;

    const float collisionSkin = 0.02f;

    // =====================================================
    // PREVENT MULTIPLE COLLIDERS
    // =====================================================

    private readonly HashSet<EnemyController> processedEnemies =
        new HashSet<EnemyController>();

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
        float hitTime,
        PlayerCombatController combat
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

        this.combat = combat;
    }

    public void Enter()
    {
        t = 0f;
        hasHit = false;

        processedEnemies.Clear();

        // =====================================================
        // LOCK PLAYER FACING
        // =====================================================

        player.attackFacingLocked = true;

        // =====================================================
        // ATTACK DIRECTION
        // =====================================================

        attackDirection =
            player.facingDir >= 0f
                ? 1f
                : -1f;

        player.facingDir =
            attackDirection;

        // =====================================================
        // ATTACK ANIMATION
        // =====================================================

        player.PlayAttackAnimation(step);

        // =====================================================
        // ATTACK DIRECTION VECTOR
        // =====================================================

        Vector2 dir =
            attackDirection > 0f
                ? Vector2.right
                : Vector2.left;

        // =====================================================
        // ATTACK START POSITION
        // =====================================================

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

    public void Exit()
    {
        // =====================================================
        // UNLOCK PLAYER FACING
        // =====================================================

        player.attackFacingLocked = false;

        player.rb.linearVelocity =
            new Vector2(
                0f,
                player.rb.linearVelocity.y
            );

        onEnd?.Invoke();
    }

    // =====================================================
    // HIT
    // =====================================================

    private void Hit()
    {
        Vector2 dir =
            attackDirection > 0f
                ? Vector2.right
                : Vector2.left;

        // =====================================================
        // ATTACK POINT DIRECTION
        // =====================================================

        Vector3 attackPointLocal =
            player.attackPoint.localPosition;

        attackPointLocal.x =
            Mathf.Abs(
                attackPointLocal.x
            ) *
            attackDirection;

        Vector2 boxCenter =
            player.transform.TransformPoint(
                attackPointLocal
            );

        if (combat == null)
            return;

        // =====================================================
        // HITBOX
        // =====================================================

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
                h.GetComponentInParent<EnemyController>();

            if (enemy == null)
                continue;

            // =================================================
            // SAME ENEMY CAN HAVE MULTIPLE COLLIDERS
            // =================================================

            if (
                processedEnemies.Contains(
                    enemy
                )
            )
            {
                continue;
            }

            processedEnemies.Add(enemy);

            hitSomething = true;

            // =================================================
            // VFX POSITION
            // =================================================

            Vector2 contactPoint =
                h.ClosestPoint(
                    boxCenter
                );

            Vector2 enemyCenter =
                h.bounds.center;

            Vector2 hitPosition =
                Vector2.Lerp(
                    contactPoint,
                    enemyCenter,
                    combat.HitVFXInsideAmount
                );

            hitPosition +=
                dir *
                combat.HitVFXForwardOffset;

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
            // HIT FEEDBACK
            // =================================================

            EnemyHitFeedback hitFeedback =
                enemy.GetComponentInParent<
                    EnemyHitFeedback
                >();

            // =================================================
            // NORMAL BALANCE HIT
            // =================================================

            if (
                balance != null &&
                !balance.IsBroken
            )
            {
                // =================================================
                // COMBO STEP'E GÖRE BALANCE DAMAGE
                // =================================================

                int balanceDamage =
                    GetBalanceDamage();

                Debug.Log(
                    "PLAYER ATTACK → BALANCE DAMAGE: " +
                    balanceDamage +
                    " | CURRENT: " +
                    balance.CurrentBalance +
                    "/" +
                    balance.MaxBalance
                );

                bool balanceDamaged =
                    balance.AddBalanceDamage(
                        balanceDamage
                    );

                Debug.Log(
                    "AFTER BALANCE → " +
                    balance.CurrentBalance +
                    "/" +
                    balance.MaxBalance +
                    " | BROKEN: " +
                    balance.IsBroken
                );

                if (!balanceDamaged)
                    continue;

                if (hitFeedback != null)
                {
                    hitFeedback.PlayBalanceHit(
                        hitPosition,
                        dir
                    );
                }

                enemy.PlayPostureHitSound();

                // =================================================
                // BALANCE DID NOT BREAK
                // =================================================

                if (!balance.IsBroken)
                {
                    enemy.ApplyBalanceHit(
                        dir
                    );
                }

                // =================================================
                // BALANCE BROKE ON THIS HIT
                // =================================================

                else
                {
                    Debug.Log(
                        "ATTACK → BALANCE BROKEN → " +
                        "NO HEALTH DAMAGE"
                    );

                    enemy.ApplyAttackHit(
                        dir,
                        false
                    );
                }

                // =================================================
                // IMPORTANT
                // =================================================

                continue;
            }

            // =====================================================
            // BALANCE ALREADY BROKEN → HEALTH
            // =====================================================

            if (
                balance != null &&
                balance.IsBroken
            )
            {
                if (health != null)
                {
                    int healthDamage =
                        combat.GetHealthDamage(
                            step
                        );

                    Debug.Log(
                        "PLAYER ATTACK → HEALTH DAMAGE: " +
                        healthDamage
                    );

                    health.TakeDamage(
                        healthDamage
                    );

                    if (hitFeedback != null)
                    {
                        hitFeedback.PlayHealthHit(
                            dir
                        );
                    }

                    enemy.PlayHealthHitSound();

                    enemy.ApplyAttackHit(
                        dir,
                        true,
                        step
                    );
                }

                continue;
            }

            // =====================================================
            // NO BALANCE → HEALTH
            // =====================================================

            if (
                balance == null &&
                health != null
            )
            {
                int healthDamage =
                    combat.GetHealthDamage(
                        step
                    );

                Debug.Log(
                    "PLAYER ATTACK → HEALTH DAMAGE: " +
                    healthDamage
                );

                health.TakeDamage(
                    healthDamage
                );

                if (hitFeedback != null)
                {
                    hitFeedback.PlayHealthHit(
                        dir
                    );
                }

                enemy.PlayHealthHitSound();

                enemy.ApplyAttackHit(
                    dir,
                    true,
                    step
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

        // =====================================================
        // ATTACK SOUND
        // =====================================================

        if (player.audioPlayer != null)
        {
            player.audioPlayer.PlayAttackWoosh(
                step
            );
        }
    }

    // =====================================================
    // BALANCE DAMAGE
    // =====================================================

    private int GetBalanceDamage()
    {
        switch (step)
        {
            case 1:
                return 10;

            case 2:
                return 10;

            case 3:
                return 15;

            case 4:
                return 20;

            default:
                return 10;
        }
    }
}

