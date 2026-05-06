using UnityEngine;

public class PlayerCombatController : MonoBehaviour
{
    public Transform attackPoint;
    public LayerMask enemyLayer;

    PlayerController player;

    public float comboResetTime = 0.8f;
    public float inputBufferTime = 0.2f;

    public float attackRange = 0.8f;

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

        if (Input.GetMouseButtonDown(0))
        {
            if (!player.canControl)
            {
                bufferedAttack = true;
                return;
            }

            bufferTimer = inputBufferTime;
        }

        if (comboTimer <= 0f)
            comboStep = 0;

        if (!player.canControl)
            return;

        if (bufferedAttack)
        {
            bufferedAttack = false;
            Attack();
            return;
        }

        if (bufferTimer > 0f && !isAttacking)
        {
            Attack();
            bufferTimer = 0f;
        }
    }

    void Attack()
    {
        isAttacking = true;

        comboStep++;
        if (comboStep > 4)
            comboStep = 1;

        comboTimer = comboResetTime;

        StartCoroutine(DoAttack(comboStep - 1));
    }

    System.Collections.IEnumerator DoAttack(int i)
    {
        float t = 0f;
        float duration = 0.18f;

        Vector2 dir = new Vector2(player.facingDir, 0f);

        while (t < duration)
        {
            if (!player.canControl) yield break;

            t += Time.deltaTime;

            player.rb.linearVelocity = new Vector2(
                dir.x * (moveDistance[i] / duration),
                player.rb.linearVelocity.y
            );

            yield return null;
        }

        Vector2 pos = (Vector2)attackPoint.position + dir * moveDistance[i];

        Collider2D[] hits = Physics2D.OverlapCircleAll(pos, attackRange, enemyLayer);

        foreach (var h in hits)
        {
            IDamageable dmg = h.GetComponent<IDamageable>();

            if (dmg != null)
            {
                Vector2 kb = new Vector2(
                    dir.x * knockback[i].x,
                    knockback[i].y
                );

                dmg.TakeDamage(damage[i], kb);
            }
        }

        isAttacking = false;
    }
}