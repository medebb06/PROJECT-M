using UnityEngine;

public class EnemyExecuteState : IEnemyState
{
    private EnemyController enemy;
    private PlayerController player;

    private float timer;
    private bool finished;

    private Collider2D playerCollider;
    private Collider2D enemyCollider;

    public EnemyExecuteState(
        EnemyController enemy
    )
    {
        this.enemy = enemy;
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
            enemy.executeDistance;

        Vector2 targetPosition =
            new Vector2(
                enemy.transform.position.x +
                direction * passDistance,
                player.rb.position.y
            );

        // =====================================================
        // EXECUTE
        // =====================================================

        float duration =
            Mathf.Max(
                enemy.executeDuration,
                0.06f
            );

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

        timer += Time.deltaTime;

        if (timer >= enemy.executeDuration)
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

            if (boss != null && boss.PercentExecute)
            {
                // Gölge Hilali: tek atmaz, canın bir yüzdesi.
                damage = Mathf.Max(1, Mathf.RoundToInt(health.MaxHealth * boss.ExecutePercent));
            }
            else if (boss != null && !boss.InPhase2)
            {
                int threshold =
                    Mathf.FloorToInt(health.MaxHealth * boss.Phase2At);

                damage = Mathf.Max(1, health.CurrentHealth - threshold);
            }
            else
            {
                damage = Mathf.Max(1, health.CurrentHealth);
            }

            CombatCallout.Popup(
                enemy.transform.position + Vector3.up * 2.4f,
                boss != null && !boss.InPhase2 && !boss.PercentExecute ? "İNFAZ! FAZ 2" : "İNFAZ!",
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
