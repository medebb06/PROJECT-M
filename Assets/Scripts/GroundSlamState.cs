using UnityEngine;
using System.Collections.Generic;

public class GroundSlamState : IPlayerState
{
    private PlayerController player;
    private PlayerStateMachine sm;

    private bool impactTriggered;
    private float startY;

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

        startY = player.transform.position.y;

        // Slam'i başlatan Space zıplama tamponuna da yazıldı: inişte
        // istemeden zıplamasın.
        player.Movement.jumpBufferCounter = 0f;

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

            bool hitEnemy = SlamImpact();

            // Düşmana değdiyse sıçra (havada zincir), değmediyse in.
            if (hitEnemy && player.slamBounceVelocity > 0f)
            {
                sm.ChangeState(new AirState(player, sm));

                player.SetVelocity(new Vector2(0f, player.slamBounceVelocity));
            }
            else
            {
                sm.ChangeState(new GroundedState(player, sm));
            }
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

    // Düşmana değdiyse true.
    bool SlamImpact()
    {
        // =====================================================
        // GÜÇ: düşüş yüksekliğine göre ×1..×2
        // =====================================================

        float drop = Mathf.Max(0f, startY - player.transform.position.y);

        float k = Mathf.Clamp01(drop / Mathf.Max(0.5f, player.slamFullPowerDrop));

        float power = 1f + k;

        float radius =
            Mathf.Max(player.impactSettings.slamDamageRadius, player.slamMinRadius) *
            (1f + 0.3f * k);

        // =====================================================
        // HARD STOP
        // =====================================================

        player.rb.linearVelocity =
            Vector2.zero;

        player.SetVelocity(
            Vector2.zero
        );

        // =====================================================
        // FREEZE FRAME + SARSINTI + TOZ
        // =====================================================

        HitStop.Request(
            player.slamFreezeTime * (0.75f + 0.5f * k),
            0f
        );

        if (player.impulseSource)
        {
            player.impulseSource.GenerateImpulse(
                1.2f + 0.8f * k
            );
        }

        player.SpawnDust();

        Vector2 feet = player.col != null
            ? new Vector2(player.transform.position.x, player.col.bounds.min.y)
            : (Vector2)player.transform.position;

        if (PlayerAbility.Instance != null)
            PlayerAbility.Instance.SpawnRingFx(feet + Vector2.up * 0.3f, radius, new Color(1f, 0.9f, 0.7f, 0.8f));

        // =====================================================
        // DÜŞMANLAR (yarıçap içi, katmandan bağımsız)
        // =====================================================

        List<EnemyController> enemies =
            CharmUtil.EnemiesInRadius(feet + Vector2.up * 0.5f, radius);

        for (int i = 0; i < enemies.Count; i++)
            DamageEnemy(enemies[i], power, radius);

        // =====================================================
        // DÜŞMAN OLMAYAN, CAN'I OLAN NESNE (eski davranış)
        // =====================================================

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                player.transform.position,
                radius,
                player.impactSettings.enemyLayer
            );

        HashSet<Health> processedHealth =
            new HashSet<Health>();

        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit.GetComponentInParent<EnemyController>() != null)
                continue;

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

        // Vazo / sandık.
        LevelProps.HitArea(feet + Vector2.up * 0.6f, new Vector2(radius * 2f, 1.6f));

        bool hitEnemy = enemies.Count > 0;

        // Sıçramayacaksa eski iniş kilidi (kısa).
        if (!hitEnemy || player.slamBounceVelocity <= 0f)
        {
            player.inputLocked = true;
            player.inputLockTimer = player.inputLockDuration;

            player.slamGroundLock = true;
            player.slamLockTimer = player.slamLockDuration;
        }

        return hitEnemy;
    }

    // =========================================================
    // DAMAGE ENEMY
    // Normal saldırıyla (AttackState) aynı kural:
    // - Denge kırık değilse: sadece BALANCE hasarı
    // - Denge kırıksa (stagger): HEALTH hasarı
    // =========================================================

    void DamageEnemy(EnemyController enemy, float power, float radius)
    {
        if (enemy == null || enemy.IsDead)
            return;

        // Slam yönü: düşman oyuncunun neresindeyse o tarafa.
        float deltaX =
            enemy.transform.position.x -
            player.transform.position.x;

        float directionX =
            Mathf.Abs(deltaX) > 0.01f
                ? Mathf.Sign(deltaX)
                : (player.facingDir >= 0f ? 1f : -1f);

        EnemyBalance balance = enemy.GetComponent<EnemyBalance>();

        int balanceDamage = enemy.slamBalanceDamage;

        if (balance != null)
        {
            balanceDamage =
                Mathf.Max(
                    balanceDamage,
                    Mathf.RoundToInt(balance.MaxBalance * player.slamBalancePercent * power)
                );
        }

        PlayerDamage.HitEnemy(
            enemy,
            new DamageInfo
            {
                source = DamageSource.Slam,
                balanceDamage = balanceDamage,
                healthDamage = player.impactSettings.slamDamage,
                direction = new Vector2(directionX, 0f),
                hitPosition = enemy.transform.position
            }
        );

        if (enemy == null || enemy.IsDead)
            return;

        bool boss = enemy.GetComponent<BossController>() != null;

        // Slam zırhı deler: kararlı saldırıyı keser (boss hariç).
        if (!boss && enemy.IsAttackCommitted)
            enemy.ChangeState(new EnemyHitState(enemy, 0.25f));

        // GERİ İTME + havaya kaldırma (merkeze yakın daha güçlü).
        Rigidbody2D rb = enemy.GetComponent<Rigidbody2D>();

        if (rb == null)
            return;

        float dist = Mathf.Abs(deltaX);
        float falloff = 1f - 0.5f * Mathf.Clamp01(dist / Mathf.Max(0.1f, radius));
        float scale = boss ? 0.25f : 1f;

        rb.linearVelocity =
            new Vector2(
                directionX * player.slamPushForce * falloff * power * 0.75f * scale * EnemyTime.Scale,
                player.slamLiftForce * scale
            );
    }
}
