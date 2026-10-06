using UnityEngine;

public class EnemyExecuteState : IEnemyState
{
    private EnemyController enemy;
    private PlayerController player;

    private float timer;
    private bool finished;

    // Sersemlik yokken (sadece "açık an"da) atılan infaz daha az hasar verir.
    private readonly bool openOnly;
    private const float OpenOnlyDamageMultiplier = 0.75f;

    // Geçiş süresi (GERÇEK sn): dünya ağır çekimdeyken bile oyuncu hızlı geçer.
    private const float PassRealTime = 0.2f;

    // Geçiş mesafesi en az bu kadar olsun (görünür bir "içinden geçiş").
    private const float MinPassDistance = 2.2f;

    private Collider2D playerCollider;
    private Collider2D enemyCollider;

    public EnemyExecuteState(
        EnemyController enemy
    )
    {
        this.enemy = enemy;

        // Execute() çağrısında düşman henüz sersemleme durumundaysa tam hasar.
        openOnly = enemy != null && !enemy.IsStaggered;
    }

    public void Enter()
    {
        finished = false;
        timer = 0f;

        // =====================================================
        // PLAYER
        // =====================================================

        if (enemy.target == null)
        {
            enemy.ChangeState(
                new EnemyChaseState(enemy)
            );

            return;
        }

        player =
            enemy.target.GetComponent<PlayerController>();

        if (player == null)
        {
            enemy.ChangeState(
                new EnemyChaseState(enemy)
            );

            return;
        }

        // =====================================================
        // COLLIDER
        // Execute sırasında enemy'ye takılma.
        // =====================================================

        playerCollider =
            player.GetComponent<Collider2D>();

        enemyCollider =
            enemy.GetComponent<Collider2D>();

        if (playerCollider != null &&
            enemyCollider != null)
        {
            Physics2D.IgnoreCollision(
                playerCollider,
                enemyCollider,
                true
            );
        }

        // =====================================================
        // YÖN
        // Player enemy'nin hangi tarafında?
        // =====================================================

        float direction =
            Mathf.Sign(
                enemy.transform.position.x -
                player.transform.position.x
            );

        if (direction == 0f)
            direction = player.facingDir;

        // =====================================================
        // ENEMY'NİN ARKASINA GEÇİŞ
        // =====================================================

        float passDistance =
            Mathf.Max(enemy.executeDistance, MinPassDistance);

        Vector2 targetPosition =
            new Vector2(
                enemy.transform.position.x +
                direction * passDistance,
                player.rb.position.y
            );

        // =====================================================
        // EXECUTE
        // =====================================================

        float duration = PassRealTime;

        player.stateMachine.ChangeState(
            new PlayerExecuteState(
                player,
                player.stateMachine,
                targetPosition,
                duration
            )
        );

        // =====================================================
        // ENEMY DUR
        // =====================================================

        Rigidbody2D enemyRb =
            enemy.GetComponent<Rigidbody2D>();

        if (enemyRb != null)
        {
            enemyRb.linearVelocity =
                Vector2.zero;
        }

        // Sinematik: kamera yakınlaşır + ağır çekim.
        ExecuteCinematic.Begin();
    }

    public void Tick()
    {
        if (finished)
            return;

        // Gerçek zaman: ağır çekimde de geçişle aynı anda biter.
        timer += Time.unscaledDeltaTime;

        if (timer >= PassRealTime)
        {
            FinishExecute();
        }
    }

    private void FinishExecute()
    {
        if (finished)
            return;

        finished = true;

        // =====================================================
        // DAMAGE
        // Ezici Parry charm'ı execute hasarını çarpabilir (tek kullanım).
        // =====================================================

        Health health =
            enemy.GetComponent<Health>();

        EnemyStatus status =
            enemy.GetComponent<EnemyStatus>();

        float multiplier = 1f;

        if (status != null)
        {
            multiplier = Mathf.Max(1f, status.ExecuteMultiplier);
            status.ExecuteMultiplier = 1f;
        }

        int damage =
            Mathf.Max(
                1,
                Mathf.RoundToInt(enemy.executeDamage * multiplier)
            );

        // İNFAZ BARI ile yapılan infaz: normal düşman ölür; boss faz 1'de
        // faz 2 eşiğine iner, faz 2'de ölür.
        if (ExecuteMeter.TakeLethal(enemy) && health != null)
        {
            BossController boss = enemy.GetComponent<BossController>();

            if (boss != null)
            {
                // Boss: infaz tek atmaz, canın bir yüzdesini alır (faz atlatmaz).
                float pct = boss.ExecuteDamagePercent * (openOnly ? OpenOnlyDamageMultiplier : 1f);

                damage = Mathf.Max(1, Mathf.RoundToInt(health.MaxHealth * pct));
            }
            else
            {
                damage = Mathf.Max(1, health.CurrentHealth);
            }

            CombatCallout.Popup(
                enemy.transform.position + Vector3.up * 2.4f,
                "İNFAZ!",
                new Color(1f, 0.8f, 0.3f),
                1.3f
            );
        }

        if (health != null)
            health.TakeDamage(damage);

        // Sinematik: öldürme anı (kısa sarsıntı) + kamera açılmaya başlar.
        ExecuteCinematic.End(health != null && health.IsDead);

        // =====================================================
        // COLLISION GERİ AÇ
        // =====================================================

        if (playerCollider != null &&
            enemyCollider != null)
        {
            Physics2D.IgnoreCollision(
                playerCollider,
                enemyCollider,
                false
            );
        }

        // =====================================================
        // ENEMY ÖLDÜYSE CHASE'E DÖNME
        // =====================================================

        if (health != null &&
            health.IsDead)
        {
            return;
        }

        enemy.ChangeState(
            new EnemyChaseState(enemy)
        );
    }

    public void Exit()
    {
        // Güvenlik:
        // State dışarıdan değiştirilirse collider
        // açık kalmasın.

        ExecuteCinematic.End(false);

        if (playerCollider != null &&
            enemyCollider != null)
        {
            Physics2D.IgnoreCollision(
                playerCollider,
                enemyCollider,
                false
            );
        }
    }
}
