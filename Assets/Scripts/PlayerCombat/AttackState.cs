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

        start = player.rb.position;

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

        if (playerCollider != null &&
            moveDistance > 0f)
        {
            RaycastHit2D[] hits =
                new RaycastHit2D[10];

            ContactFilter2D filter =
                new ContactFilter2D();

            LayerMask movementCollisionMask =
                enemyLayer |
                player.wallMask;

            filter.SetLayerMask(
                movementCollisionMask
            );

            filter.useTriggers = false;

            int hitCount =
                playerCollider.Cast(
                    dir,
                    filter,
                    hits,
                    moveDistance + collisionSkin
                );

            float allowedDistance =
                moveDistance;

            for (int i = 0; i < hitCount; i++)
            {
                if (hits[i].collider == null)
                    continue;

                if (hits[i].distance <
                    allowedDistance)
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
        if (!player.canAttack ||
            player.isDashing)
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
                ? Mathf.Clamp01(t / duration)
                : 1f;

        // =====================================================
        // ATTACK MOVEMENT
        // =====================================================
        //
        // Örneğin:
        //
        // moveStart = 0.15
        // moveEnd   = 0.70
        //
        // %0 - %15   : hareket yok
        // %15 - %70  : ileri hareket
        // %70 - %100 : hareket yok
        //

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
        //
        // Inspector'dan tamamen ayarlanabilir.
        //
        // Örneğin:
        //
        // 0.35 = saldırının %35'i
        // 0.50 = saldırının %50'si
        // 0.70 = saldırının %70'i
        //

        if (!hasHit &&
            n >= hitTime)
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

    void UpdateFacing()
    {
        if (Mathf.Abs(player.moveInput) < 0.01f)
            return;

        float newDirection =
            Mathf.Sign(player.moveInput);

        if (player.facingDir == newDirection)
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

    void Hit()
    {
        Vector2 dir =
            player.facingDir > 0
                ? Vector2.right
                : Vector2.left;

        Vector2 boxCenter =
            player.attackPoint.position;

        var combat =
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
            var dmg =
                h.GetComponentInParent<
                    IDamageable
                >();

            if (dmg == null)
                continue;

            dmg.TakeDamage(
                1,
                dir * 6f
            );

            hitSomething = true;
        }

        // =====================================================
        // NORMAL ATTACK HIT STOP
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
    }
}