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

    // Enemy'ye tamamen yapışmamak için küçük mesafe.
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

        Vector2 dir = player.facingDir > 0
            ? Vector2.right
            : Vector2.left;

        start = player.rb.position;

        // Normal hedef pozisyon.
        target = start + dir * moveDistance;

        // =====================================================
        // ENEMY COLLISION KONTROLÜ
        // =====================================================
        //
        // Player attack sırasında Enemy'nin içine giremesin.
        // Önündeki Enemy'yi bulup hedef pozisyonu sınırlandırıyoruz.
        //

        Collider2D playerCollider =
            player.GetComponent<Collider2D>();

        if (playerCollider != null && moveDistance > 0f)
        {
            RaycastHit2D[] hits = new RaycastHit2D[10];

            ContactFilter2D filter = new ContactFilter2D();

            filter.SetLayerMask(enemyLayer);
            filter.useTriggers = false;

            int hitCount = playerCollider.Cast(
                dir,
                filter,
                hits,
                moveDistance + collisionSkin
            );

            float allowedDistance = moveDistance;

            for (int i = 0; i < hitCount; i++)
            {
                if (hits[i].collider == null)
                    continue;

                if (hits[i].distance < allowedDistance)
                {
                    allowedDistance = Mathf.Max(
                        0f,
                        hits[i].distance - collisionSkin
                    );
                }
            }

            target = start + dir * allowedDistance;
        }

        // Attack başladığında yatay momentum temizlenir.
        // Y velocity korunur.
        player.rb.linearVelocity = new Vector2(
            0f,
            player.rb.linearVelocity.y
        );
    }

    public void Tick()
    {
        if (!player.canAttack || player.isDashing)
        {
            Exit();
            return;
        }

        t += Time.deltaTime;

        // =====================================================
        // ATTACK MOVE
        // =====================================================

        float n = Mathf.Clamp01(t / duration);

        float curve = moveCurve != null
            ? moveCurve.Evaluate(n)
            : n;

        float desiredX = Mathf.Lerp(
            start.x,
            target.x,
            curve
        );

        // Sadece X değişiyor.
        // Y tamamen physics'e bırakılıyor.
        player.rb.position = new Vector2(
            desiredX,
            player.rb.position.y
        );

        // =====================================================
        // HIT WINDOW
        // =====================================================

        if (!hasHit && t >= 0.04f && t <= 0.14f)
        {
            Hit();
            hasHit = true;
        }

        // =====================================================
        // END
        // =====================================================

        if (t >= duration)
        {
            Exit();
        }
    }

    public void Exit()
    {
        // Attack sonunda yatay momentum bırakma.
        // Y velocity korunuyor.
        player.rb.linearVelocity = new Vector2(
            0f,
            player.rb.linearVelocity.y
        );

        onEnd?.Invoke();
    }

    void Hit()
    {
        Vector2 dir = player.facingDir > 0
            ? Vector2.right
            : Vector2.left;

        Vector2 boxCenter = player.attackPoint.position;

        var combat =
            player.GetComponent<PlayerCombatController>();

        if (combat == null)
            return;

        Collider2D[] hits = Physics2D.OverlapBoxAll(
            boxCenter,
            combat.hitBoxSize,
            0f,
            enemyLayer
        );

        foreach (var h in hits)
        {
            var dmg =
                h.GetComponentInParent<IDamageable>();

            if (dmg != null)
            {
                dmg.TakeDamage(
                    1,
                    dir * 6f
                );
            }
        }
    }
}