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

        Vector2 dir =
            player.facingDir > 0
                ? Vector2.right
                : Vector2.left;

        start = player.rb.position;

        // =====================================================
        // ATTACK HAREKETİ
        // =====================================================
        //
        // Attack başladığı andaki yönü kullanır.
        // Sonradan sağ/sol basmak attack'ın hareketini
        // değiştirmez.
        //

        target =
            start +
            dir * moveDistance;

        // =====================================================
        // ENEMY COLLISION
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

            filter.SetLayerMask(enemyLayer);
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

        // Attack başlangıcında yatay momentum temizlenir.
        // Y korunur.
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

        // =====================================================
        // SADECE YÖNÜ GÜNCELLE
        // =====================================================
        //
        // Burada movement YOK.
        //
        // Sağ/sol inputu sadece karakterin yüzünü değiştirir.
        //

        UpdateFacing();

        // =====================================================
        // ATTACK MOVE
        // =====================================================

        float n =
            Mathf.Clamp01(
                t / duration
            );

        float curve =
            moveCurve != null
                ? moveCurve.Evaluate(n)
                : n;

        float desiredX =
            Mathf.Lerp(
                start.x,
                target.x,
                curve
            );

        // Sadece attack'ın kendi hareketi.
        // Input bunu değiştiremez.
        player.rb.position =
            new Vector2(
                desiredX,
                player.rb.position.y
            );

        // =====================================================
        // HIT WINDOW
        // =====================================================

        if (!hasHit &&
            t >= 0.04f &&
            t <= 0.14f)
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

    void UpdateFacing()
    {
        // Hareket inputu yoksa yön değiştirme.
        if (Mathf.Abs(player.moveInput) < 0.01f)
            return;

        float newDirection =
            Mathf.Sign(player.moveInput);

        // Zaten o yöne bakıyorsa hiçbir şey yapma.
        if (player.facingDir == newDirection)
            return;

        // =====================================================
        // FACING
        // =====================================================

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
        // Attack sonunda yatay momentum bırakma.
        // Y velocity korunur.
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

        foreach (var h in hits)
        {
            var dmg =
                h.GetComponentInParent<
                    IDamageable
                >();

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