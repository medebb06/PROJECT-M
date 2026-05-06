using UnityEngine;
using System.Collections;

public class PlayerCombatController : MonoBehaviour
{
    public Transform attackPoint;
    public LayerMask enemyLayer;

    PlayerController player;

    [Header("Combo")]
    public float comboResetTime = 0.8f;
    public float inputBufferTime = 0.2f;

    [Header("Attack")]
    public float attackRange = 0.9f;
    public float attackHeight = 1.2f;

    public int[] damage = { 1, 1, 2, 3 };

    public float[] moveDistance = { 0.3f, 0.5f, 0.7f, 1.0f };

    public Vector2[] knockback =
    {
        new Vector2(3,1),
        new Vector2(4,1),
        new Vector2(5,1.2f),
        new Vector2(6,1.5f)
    };

    int comboStep;
    float comboTimer;
    float bufferTimer;

    bool isAttacking;
    bool bufferedAttack;

    void Awake()
    {
        player = GetComponent<PlayerController>();
    }

    void Update()
    {
        comboTimer -= Time.deltaTime;
        bufferTimer -= Time.deltaTime;

        // 🔥 INPUT
        if (Input.GetMouseButtonDown(0))
        {
            bufferTimer = inputBufferTime;
        }

        if (comboTimer <= 0f)
            comboStep = 0;

        // 🔥 buffered attack ALWAYS works (dash sonrası da)
        if (bufferTimer > 0f && !isAttacking)
        {
            bufferTimer = 0f;
            Attack();
        }
    }

    void Attack()
    {
        if (!player.canAttack)
            return;

        isAttacking = true;

        comboStep++;
        if (comboStep > 4) comboStep = 1;

        comboTimer = comboResetTime;

        StartCoroutine(DoAttack(comboStep - 1));
    }

    IEnumerator DoAttack(int i)
    {
        isAttacking = true;

        // 🔥 micro hit delay (feel)
        yield return new WaitForSeconds(0.03f);

        float t = 0f;
        float duration = 0.18f;

        Vector2 dir = player.facingDir > 0 ? Vector2.right : Vector2.left;

        Vector2 start = player.rb.position;
        Vector2 target = start + dir * moveDistance[i];

        while (t < duration)
        {
            // ❌ DASH veya ATTACK LOCK olursa çık
            if (!player.canAttack || player.isDashing)
            {
                isAttacking = false;
                yield break;
            }

            t += Time.deltaTime;

            float lerp = t / duration;

            player.rb.MovePosition(Vector2.Lerp(start, target, lerp));

            yield return null;
        }

        // 🔥 HIT CHECK
        Vector2 boxCenter = player.rb.position + dir * 0.6f;

        Collider2D[] hits = Physics2D.OverlapBoxAll(
            boxCenter,
            new Vector2(attackRange, attackHeight),
            0f,
            enemyLayer
        );

        foreach (var h in hits)
        {
            var dmg = h.GetComponentInParent<IDamageable>();
            if (dmg == null) continue;

            Vector2 kb = new Vector2(
                dir.x * knockback[i].x,
                knockback[i].y
            );

            dmg.TakeDamage(damage[i], kb);
        }

        isAttacking = false;
    }
}