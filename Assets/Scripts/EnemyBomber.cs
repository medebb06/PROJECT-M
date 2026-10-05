using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PATLAYAN (Patlayan tip ve "Patlayıcı" elit eki).
///
///   Fitil : oyuncuya 'fuseRange' kadar yaklaşınca durur, kırmızı yanıp söner
///           ("!"), 'fuseTime' sonra PATLAR: oyuncuya (dash ile kaçılır) VE
///           çevredeki düşmanlara hasar.
///   Ölüm  : öldürülürse kısa süre sonra patlar ama SADECE düşmanlara vurur
///           (deathHurtsPlayer kapalıysa) → Slam ile sürünün içine it, vur.
///
/// Elit eki (onlyOnDeath = true): fitil yok, ölünce oyuncuya da vurur.
/// </summary>
public class EnemyBomber : MonoBehaviour
{
    public bool onlyOnDeath = false;
    public bool deathHurtsPlayer = false;

    public float fuseRange = 2.2f;
    public float fuseTime = 0.9f;
    public float deathFuseTime = 0.35f;

    public float radius = 2.6f;
    [Range(0f, 1f)] public float playerDamagePercent = 0.2f;
    [Range(0f, 1f)] public float enemyBalancePercent = 0.5f;
    [Range(0f, 1f)] public float enemyHealthPercent = 0.25f;

    private EnemyController enemy;
    private Health health;
    private bool fused;
    private bool exploded;
    private bool hurtPlayerOnBlast;
    private float fuseEnd;
    private float nextBlink;
    private Transform player;

    // Ölüm patlaması: düşman objesi solup silinirken ayrı bir obje patlar.
    private bool blastOnly;
    private Vector2 blastCenter;
    private EnemyController blastSource;

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        if (health != null)
            health.OnDeath += OnDeath;
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnDeath -= OnDeath;
    }

    private void OnDeath()
    {
        if (exploded)
            return;

        exploded = true;

        bool hurts = onlyOnDeath || deathHurtsPlayer;

        Vector2 center = transform.position + Vector3.up * 0.6f;

        if (hurts)
            CombatCallout.Popup(center + Vector2.up * 1.4f, "PATLAYACAK!", new Color(1f, 0.45f, 0.2f), 0.9f);

        EnemyBomber blast = new GameObject("Patlama").AddComponent<EnemyBomber>();

        blast.CopySettings(this);
        blast.blastOnly = true;
        blast.blastCenter = center;
        blast.blastSource = enemy;
        blast.hurtPlayerOnBlast = hurts;
        blast.fuseEnd = EnemyTime.Now + (hurts ? Mathf.Max(0.5f, deathFuseTime) : deathFuseTime);
    }

    private void CopySettings(EnemyBomber o)
    {
        radius = o.radius;
        playerDamagePercent = o.playerDamagePercent;
        enemyBalancePercent = o.enemyBalancePercent;
        enemyHealthPercent = o.enemyHealthPercent;
    }

    private void StartFuse(float time, bool hurtsPlayer)
    {
        fused = true;
        hurtPlayerOnBlast = hurtsPlayer;
        fuseEnd = EnemyTime.Now + time;

        CombatCallout.PopupAbove(enemy, hurtsPlayer ? "PATLAYACAK!" : "!", new Color(1f, 0.45f, 0.2f), 0.9f);
    }

    private void Update()
    {
        if (blastOnly)
        {
            if (EnemyTime.Now >= fuseEnd)
            {
                Blast(blastCenter, hurtPlayerOnBlast, blastSource);
                Destroy(gameObject);
            }

            return;
        }

        if (exploded || enemy == null)
            return;

        if (!fused)
        {
            if (onlyOnDeath || enemy.IsDead || enemy.IsStaggered)
                return;

            if (player == null)
            {
                PlayerController p = FindFirstObjectByType<PlayerController>();

                if (p == null)
                    return;

                player = p.transform;
            }

            if (Vector2.Distance(player.position, transform.position) <= fuseRange)
            {
                StartFuse(fuseTime, true);

                // Fitil yanarken saldırmasın, yerinde dursun.
                enemy.StartAttackRecovery(fuseTime + 1f);
            }

            return;
        }

        // Yanıp sönme.
        if (EnemyTime.Now >= nextBlink && !enemy.IsDead)
        {
            nextBlink = EnemyTime.Now + 0.12f;
            enemy.PlayTintFlash(new Color(1f, 0.3f, 0.2f), 0.06f);
        }

        if (EnemyTime.Now >= fuseEnd)
            Explode();
    }

    // Fitil bitti: patla, sonra kendisi ölür (OnDeath ikinci patlama yapmaz).
    private void Explode()
    {
        if (exploded)
            return;

        exploded = true;

        Blast(transform.position + Vector3.up * 0.6f, hurtPlayerOnBlast, enemy);

        if (health != null && !health.IsDead)
            health.TakeDamage(Mathf.Max(1, health.CurrentHealth));
    }

    private void Blast(Vector2 center, bool hurtsPlayer, EnemyController exclude)
    {
        // Efekt.
        if (PlayerAbility.Instance != null)
            PlayerAbility.Instance.SpawnRingFx(center, radius, new Color(1f, 0.5f, 0.2f, 0.9f));

        CombatCallout.Popup(center + Vector2.up * 1.2f, "BOOM", new Color(1f, 0.55f, 0.2f), 1.1f);

        // Oyuncu.
        if (hurtsPlayer)
        {
            PlayerController p = FindFirstObjectByType<PlayerController>();

            if (p != null && Vector2.Distance(p.transform.position, center) <= radius + 0.4f)
            {
                Health ph = p.GetComponent<Health>();
                PlayerDamageReceiver receiver = p.GetComponent<PlayerDamageReceiver>();

                if (ph != null && !ph.IsDead)
                {
                    int damage = Mathf.Max(1, Mathf.CeilToInt(ph.MaxHealth * playerDamagePercent));

                    float side = Mathf.Sign(p.transform.position.x - center.x);

                    if (receiver != null)
                        receiver.TakeDamage(damage, new Vector2(side, 0.5f).normalized, 9f, 6f, 0.2f, -1f, exclude, PlayerHitKind.Unblockable);
                    else
                        ph.TakeDamage(damage);
                }
            }
        }

        // Düşmanlar (zincir).
        List<EnemyController> near = CharmUtil.EnemiesInRadius(center, radius, exclude);

        for (int i = 0; i < near.Count; i++)
        {
            EnemyController e = near[i];

            if (e.GetComponent<BossController>() != null)
                continue;

            if (!CharmUtil.AddBalancePercent(e, enemyBalancePercent))
            {
                Health h = e.GetComponent<Health>();

                if (h != null && !h.IsDead)
                    h.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(h.MaxHealth * enemyHealthPercent)));
            }

            Rigidbody2D rb = e.GetComponent<Rigidbody2D>();

            if (rb != null && !e.IsAttackCommitted)
            {
                float side = Mathf.Sign(e.transform.position.x - center.x);
                rb.linearVelocity = new Vector2(side * 8f * EnemyTime.Scale, 4f);
            }
        }
    }
}
