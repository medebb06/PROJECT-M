using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HAVADA SALDIRI (PlayerCombatController başlatır).
///
///   Yan vuruş  : önde kısa vuruş. Düşüş yavaşlar (havada asılı kalma hissi).
///                İnişe kadar en fazla 'maxAirAttacks' kez.
///   AŞAĞI vuruş (↓ + saldırı) : ayağın altına vurur. Düşmana (ya da düşman
///                mermisine) değerse ZIPLATIR (pogo), dash beklemesi sıfırlanır
///                ve havada saldırı hakkı yenilenir → yere değmeden zincir.
///
/// Hareket AirState / JumpState'te devam eder; bu state oyuncuyu ileri itmez.
/// Hasar PlayerDamage hattından (kaynak Attack) geçer.
/// </summary>
public class AirAttackState : ICombatState
{
    private readonly PlayerController player;
    private readonly PlayerCombatController combat;
    private readonly LayerMask enemyLayer;
    private readonly bool down;
    private readonly int step;
    private readonly Action onEnd;

    private readonly float duration;
    private readonly float hitTime;

    private float t;
    private bool hasHit;
    private bool ended;
    private float direction;

    private readonly HashSet<EnemyController> processed = new HashSet<EnemyController>();

    public bool Down => down;

    public AirAttackState(
        PlayerController player,
        PlayerCombatController combat,
        LayerMask enemyLayer,
        bool down,
        int step,
        float duration,
        float hitTime,
        Action onEnd
    )
    {
        this.player = player;
        this.combat = combat;
        this.enemyLayer = enemyLayer;
        this.down = down;
        this.step = step;
        this.duration = Mathf.Max(0.05f, duration);
        this.hitTime = Mathf.Clamp01(hitTime);
        this.onEnd = onEnd;
    }

    public void Enter()
    {
        t = 0f;
        hasHit = false;
        ended = false;
        processed.Clear();

        direction = player.facingDir >= 0f ? 1f : -1f;

        // Yan vuruşta yön kilitlenir; aşağı vuruşta havada dönebilsin.
        if (!down)
            player.attackFacingLocked = true;

        combat.PlayAirAttackAnimation(down, step);

        if (player.audioPlayer != null)
            player.audioPlayer.PlayAttackWoosh(down ? 3 : step);
    }

    public void Tick()
    {
        if (ended)
            return;

        if (!player.canAttack || player.isDashing)
        {
            Exit();
            return;
        }

        // Yere indiyse (aşağı vuruş vurmadan) bitir: yerde kombo başlasın.
        if (player.isGrounded && t > 0.05f)
        {
            Exit();
            return;
        }

        t += Time.deltaTime;

        float n = Mathf.Clamp01(t / duration);

        // Yan vuruşta düşüş yavaşlar.
        if (!down && player.rb != null)
        {
            Vector2 v = player.rb.linearVelocity;

            if (v.y < -combat.airAttackMaxFallSpeed)
            {
                v.y = -combat.airAttackMaxFallSpeed;
                player.rb.linearVelocity = v;
            }
        }

        if (!hasHit && n >= hitTime)
        {
            hasHit = true;
            Hit();
        }

        if (n >= 1f)
            Exit();
    }

    public void Exit()
    {
        if (ended)
            return;

        ended = true;

        player.attackFacingLocked = false;

        onEnd?.Invoke();
    }

    private void Hit()
    {
        Vector2 center;
        Vector2 size;
        Vector2 dir;

        if (down)
        {
            Bounds b = player.col != null ? player.col.bounds : new Bounds(player.transform.position, Vector3.one);

            size = combat.downAttackBoxSize;
            center = new Vector2(b.center.x, b.min.y - size.y * 0.5f + 0.2f);
            dir = Vector2.down;
        }
        else
        {
            Vector3 local = player.attackPoint != null ? player.attackPoint.localPosition : new Vector3(0.8f, 0.6f, 0f);
            local.x = Mathf.Abs(local.x) * direction;

            center = player.transform.TransformPoint(local);
            size = combat.EffectiveHitBox;
            center += new Vector2(direction, 0f) * (size.x - combat.hitBoxSize.x) * 0.5f;
            dir = direction > 0f ? Vector2.right : Vector2.left;
        }

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, enemyLayer);

        bool hitSomething = false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D h = hits[i];

            if (h == null)
                continue;

            EnemyController enemy = h.GetComponentInParent<EnemyController>();

            if (enemy == null || enemy.IsDead || !processed.Add(enemy))
                continue;

            hitSomething = true;

            // Aşağı vuruşta savrulma yönü: düşman oyuncunun neresindeyse.
            Vector2 knock = dir;

            if (down)
            {
                float dx = enemy.transform.position.x - player.transform.position.x;
                knock = new Vector2(Mathf.Abs(dx) > 0.05f ? Mathf.Sign(dx) : direction, 0f);
            }

            PlayerDamage.HitEnemy(
                enemy,
                new DamageInfo
                {
                    source = DamageSource.Attack,
                    comboStep = step,
                    balanceDamage = Mathf.Max(1, Mathf.RoundToInt(combat.GetBalanceDamage(1) * (down ? combat.downAttackBalanceMultiplier : 1f))),
                    healthDamage = combat.attackHealthDamage,
                    direction = knock,
                    hitPosition = h.ClosestPoint(center),
                    fromAbove = down
                }
            );
        }

        // Vazo / sandık da kırılır (aşağı vuruşta da zıplatır).
        if (LevelProps.HitArea(center, size))
            hitSomething = true;

        // Pogo: düşmana, mermiye ya da DİKENE değen aşağı vuruş.
        if (down && !hitSomething)
            hitSomething = HitProjectileBelow(center, size) || LevelProps.SpikeIn(center, size);

        if (PlayerWeapon.Instance != null)
            PlayerWeapon.Instance.OnSwing(down ? 0 : step, center, size, dir, hitSomething);

        if (!hitSomething)
            return;

        CombatImpactFeedback feedback = player.GetComponent<CombatImpactFeedback>();

        if (feedback != null)
            feedback.PlayAttackImpact();

        if (down)
            Pogo();
    }

    // Aşağı vuruş bir düşman mermisine (ok) değdiyse onu yok et ve zıpla.
    // Mermilerin collider'ı yok: konumla bakılır.
    private bool HitProjectileBelow(Vector2 center, Vector2 size)
    {
        EnemyProjectile[] all =
            UnityEngine.Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None);

        Rect box = new Rect(center - size * 0.5f, size);

        for (int i = 0; i < all.Length; i++)
        {
            EnemyProjectile p = all[i];

            if (p == null || !box.Contains((Vector2)p.transform.position))
                continue;

            UnityEngine.Object.Destroy(p.gameObject);
            return true;
        }

        return false;
    }

    private void Pogo()
    {
        if (player.rb == null)
            return;

        Vector2 v = player.rb.linearVelocity;
        v.y = combat.pogoBounceVelocity;
        player.rb.linearVelocity = v;

        // Akış: dash ve havada saldırı hakları yenilenir, kısa korunma.
        player.dashCooldownTimer = 0f;
        player.jumpConsumed = false;
        player.hitInvincibilityTimer = Mathf.Max(player.hitInvincibilityTimer, 0.12f);

        combat.RefillAirAttacks();

        // Düşüş takibi (iniş efekti) baştan başlasın.
        if (player.Movement != null)
            player.Movement.ResetAirData();

        HitStop.Request(0.035f, 0.05f);
    }
}
