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

    Vector2 start;
    Vector2 target;

    float t;
    float duration = 0.18f;

    bool hasHit;

    public AttackState(
        PlayerController player,
        LayerMask enemyLayer,
        int step,
        Action onEnd,
        float hitStopScale,
        float hitStopDuration,
        float moveDistance,
        float moveSpeed,
        AnimationCurve moveCurve
    )
    {
        this.player = player;
        this.enemyLayer = enemyLayer;
        this.step = step;
        this.onEnd = onEnd;

        this.moveDistance = moveDistance;
        this.moveSpeed = moveSpeed;
        this.moveCurve = moveCurve;
    }

    public void Enter()
    {
        t = 0f;
        hasHit = false;

        Vector2 dir = player.facingDir > 0 ? Vector2.right : Vector2.left;

        start = player.rb.position;
        target = start + dir * moveDistance;
    }

    public void Tick()
    {
        if (!player.canAttack || player.isDashing)
        {
            Exit();
            return;
        }

        t += Time.deltaTime;

        float n = Mathf.Clamp01(t * moveSpeed);
        float curve = moveCurve != null ? moveCurve.Evaluate(n) : n;

        player.rb.MovePosition(Vector2.Lerp(start, target, curve));

        // 🔥 HIT WINDOW (daha geniş)
        if (!hasHit && t >= 0.04f && t <= 0.14f)
        {
            Hit();
            hasHit = true;
        }

        if (t >= duration)
            Exit();
    }

    public void Exit()
    {
        onEnd?.Invoke();
    }

    void Hit()
    {
        Vector2 dir = player.facingDir > 0 ? Vector2.right : Vector2.left;

        Vector2 boxCenter = player.attackPoint.position;

        var combat = player.GetComponent<PlayerCombatController>();

        Collider2D[] hits = Physics2D.OverlapBoxAll(
            boxCenter,
            combat.hitBoxSize,
            0f,
            enemyLayer
        );

        foreach (var h in hits)
        {
            var dmg = h.GetComponentInParent<IDamageable>();

            if (dmg != null)
            {
                dmg.TakeDamage(1, dir * 6f);
            }
        }
    }
}