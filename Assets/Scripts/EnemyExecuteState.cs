using System.Collections.Generic;
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
    // Hat uzadıkça sabit hızla uzar (min..max).
    private float passTime = 0.2f;
    private const float PassSpeed = 34f;
    private const float MinPassTime = 0.2f;
    private const float MaxPassTime = 0.6f;

    // Düz zeminde: karşıdaki bu mesafedeki, aynı hizadaki TÜM düşmanlar da vurulur.
    public const float LineHeight = 1.6f;

    private readonly List<EnemyController> extras = new List<EnemyController>();
    private readonly List<Collider2D> extraColliders = new List<Collider2D>();

    // Geçiş mesafesi en az bu kadar olsun (görünür bir "içinden geçiş").
    private const float MinPassDistance = 2.2f;

    private Collider2D playerCollider;
    private Collider2D enemyCollider;

    public EnemyExecuteState(
        EnemyController enemy
    )
    {
        this.enemy = enemy;

        // Düşman açık anda / sersemlemişse tam hasar; değilse (kör vuruş) ×0.75.
        openOnly = enemy != null && !enemy.IsOpen;
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

        // Çizgi: düz zeminde aynı yöndeki tüm düşmanlar da hattın içine girer.
        float farthestX = enemy.transform.position.x;

        extras.Clear();
        extraColliders.Clear();

        if (player.IsGrounded())
        {
            for (int i = 0; i < EnemyController.All.Count; i++)
            {
                EnemyController other = EnemyController.All[i];

                if (other == null || other == enemy || other.IsDead || other.CurrentState is EnemyExecuteState)
                    continue;

                float dx = other.transform.position.x - player.transform.position.x;
                float dy = other.transform.position.y - player.transform.position.y;

                if (Mathf.Sign(dx) != direction || Mathf.Abs(dx) > PlayerFinisher.LastLineRange || Mathf.Abs(dy) > LineHeight)
                    continue;

                extras.Add(other);

                Collider2D oc = other.GetComponent<Collider2D>();

                if (oc != null)
                {
                    extraColliders.Add(oc);

                    if (playerCollider != null)
                        Physics2D.IgnoreCollision(playerCollider, oc, true);
                }

                if (direction * other.transform.position.x > direction * farthestX)
                    farthestX = other.transform.position.x;
            }
        }

        Vector2 targetPosition =
            new Vector2(
                farthestX +
                direction * passDistance,
                player.rb.position.y
            );

        // =====================================================
        // EXECUTE
        // =====================================================

        passTime = Mathf.Clamp(
            Mathf.Abs(targetPosition.x - player.rb.position.x) / PassSpeed,
            MinPassTime,
            MaxPassTime
        );

        float duration = passTime;

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

        if (timer >= passTime)
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
        int segs = ExecuteMeter.TakePower(enemy);

        if (segs > 0 && health != null)
        {
            BossController boss = enemy.GetComponent<BossController>();

            if (boss != null)
            {
                // Boss: infaz tek atmaz, canın bir yüzdesini alır (faz atlatmaz).
                // Cana doğrudan hasar + denge itmesi; canı azsa bitirici vuruş.
                damage = boss.ExecuteHealthDamage(segs, openOnly ? OpenOnlyDamageMultiplier : 1f);
            }
            else
            {
                // 1 parça ≈ canın yarısı, 2+ parça öldürür.
                damage = segs >= 2
                    ? Mathf.Max(1, health.CurrentHealth)
                    : Mathf.Max(1, Mathf.RoundToInt(health.MaxHealth * 0.5f));
            }

            CombatCallout.Popup(
                enemy.transform.position + Vector3.up * 2.4f,
                segs >= ExecuteMeter.Segments ? "İNFAZ ×" + segs + "!!" : "İNFAZ ×" + segs + "!",
                new Color(1f, 0.8f, 0.3f),
                1.3f
            );
        }

        if (health != null && damage > 0)
            health.TakeDamage(damage);

        // Hattaki diğer düşmanlar aynı güçle vurulur.
        if (segs > 0)
        {
            for (int i = 0; i < extras.Count; i++)
                HitExtra(extras[i], segs);
        }

        // Sinematik: öldürme anı (kısa sarsıntı) + kamera açılmaya başlar.
        ExecuteCinematic.End(health != null && health.IsDead);

        // =====================================================
        // COLLISION GERİ AÇ
        // =====================================================

        RestoreCollisions();

        // =====================================================
        // ENEMY ÖLDÜYSE CHASE'E DÖNME
        // =====================================================

        if (health != null &&
            health.IsDead)
        {
            return;
        }

        // Denge kırılınca başka bir state'e geçildiyse (sersemleme vb.) ezme.
        if (enemy.CurrentState != this)
            return;

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

        RestoreCollisions();
    }

    private void RestoreCollisions()
    {
        if (playerCollider == null)
            return;

        if (enemyCollider != null)
            Physics2D.IgnoreCollision(playerCollider, enemyCollider, false);

        for (int i = 0; i < extraColliders.Count; i++)
        {
            if (extraColliders[i] != null)
                Physics2D.IgnoreCollision(playerCollider, extraColliders[i], false);
        }

        extraColliders.Clear();
    }

    private void HitExtra(EnemyController other, int segs)
    {
        if (other == null || other.IsDead)
            return;

        Health h = other.GetComponent<Health>();

        if (h == null)
            return;

        BossController boss = other.GetComponent<BossController>();

        int dmg;

        if (boss != null)
            dmg = boss.ExecuteHealthDamage(segs, 1f);
        else
            dmg = segs >= 2
                ? Mathf.Max(1, h.CurrentHealth)
                : Mathf.Max(1, Mathf.RoundToInt(h.MaxHealth * 0.5f));

        CombatCallout.Popup(
            other.transform.position + Vector3.up * 2.4f,
            "İNFAZ ×" + segs + "!",
            new Color(1f, 0.8f, 0.3f),
            1.1f
        );

        if (dmg > 0)
            h.TakeDamage(dmg);
    }
}
