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

        PlayerCombatController combat =
            player.GetComponent<PlayerCombatController>();

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
            // HASAR: tek akış (PlayerDamage)
            // Stat'lar, kritik ve CombatEvents buradan geçer.
            // Denge kırık değilse denge, kırıksa can hasarı verir.
            // =================================================

            PlayerDamage.HitEnemy(
                enemy,
                new DamageInfo
                {
                    source = DamageSource.Attack,
                    comboStep = step,
                    balanceDamage = combat.GetBalanceDamage(step),
                    healthDamage = 1,
                    direction = dir,
                    hitPosition = hitPosition
                }
            );
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
}