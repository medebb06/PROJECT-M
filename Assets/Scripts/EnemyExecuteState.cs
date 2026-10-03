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

        if (health != null)
        {
            health.TakeDamage(
                Mathf.Max(
                    1,
                    Mathf.RoundToInt(enemy.executeDamage * multiplier)
                )
            );
        }

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
