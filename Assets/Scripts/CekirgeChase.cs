using UnityEngine;

/// <summary>
/// Cekirge: oyuncuyu kovalar, menzile girince zıplayıp vurur.
/// Hareket Transform ile (fizik yok); poz animasyondan gelir.
/// </summary>
public class CekirgeChase : MonoBehaviour
{
    public Transform target;
    public float moveSpeed = 2f;
    public float stopDistance = 2.6f;
    public float aggroRange = 40f;
    public float turnTime = 0.08f;
    public string idleState = "Idle";
    public string walkState = "Walk";
    public string attackState = "Attack";

    [Header("Zıplayıp vurma")]
    public float attackRange = 6.5f;
    public float attackCooldown = 1.6f;
    public float windup = 0.55f;      // Attack klibindeki firlama zamani
    public float airTime = 0.5f;      // havada kalma suresi
    public float attackLength = 1.3f; // klip uzunlugu
    public float jumpHeight = 2.2f;
    public float landOffset = 1.4f;   // oyuncunun onune iner
    public float maxJump = 6.5f;
    public float hitStart = 0.90f, hitEnd = 1.10f;
    public float hitRadius = 1.9f;
    public int damage = 1;

    Animator anim;
    float baseScaleX;
    float shownFacing = 1f, facingVel;
    string current;
    float cooldown;
    bool attacking, hitDone;
    float attackT, startX, endX, groundY, dir;

    void Awake()
    {
        anim = GetComponent<Animator>();
        baseScaleX = Mathf.Abs(transform.localScale.x);
    }

    void Update()
    {
        if (target == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) target = p.transform;
        }

        if (cooldown > 0f) cooldown -= Time.deltaTime;

        if (attacking) { UpdateAttack(); return; }

        bool walking = false;
        if (target != null)
        {
            float dx = target.position.x - transform.position.x;
            float dist = Mathf.Abs(dx);
            float want = dx >= 0f ? 1f : -1f;

            if (dist <= aggroRange)
            {
                if (dist > 0.3f) shownFacing = Mathf.SmoothDamp(shownFacing, want, ref facingVel, turnTime);

                if (dist <= attackRange && cooldown <= 0f) { StartAttack(want); return; }

                if (dist > stopDistance)
                {
                    transform.position += new Vector3(want * moveSpeed * Time.deltaTime, 0f, 0f);
                    walking = true;
                }
            }
        }
        ApplyFacing();
        Play(walking ? walkState : idleState, 0.12f);
    }

    void StartAttack(float want)
    {
        attacking = true; hitDone = false; attackT = 0f;
        dir = want; shownFacing = want; facingVel = 0f;
        groundY = transform.position.y;
        ApplyFacing();
        current = null;
        Play(attackState, 0.08f);
    }

    void UpdateAttack()
    {
        attackT += Time.deltaTime;
        var pos = transform.position;

        if (attackT < windup)
        {
            if (target != null)
            {
                float dx = target.position.x - pos.x;
                if (Mathf.Abs(dx) > 0.3f) { dir = dx >= 0f ? 1f : -1f; shownFacing = dir; ApplyFacing(); }
            }
            startX = pos.x;
            float tx = target != null ? target.position.x - dir * landOffset : pos.x + dir * 2f;
            endX = startX + Mathf.Clamp(tx - startX, -maxJump, maxJump);
        }
        else if (attackT < windup + airTime)
        {
            float u = (attackT - windup) / airTime;
            pos.x = Mathf.Lerp(startX, endX, u);
            pos.y = groundY + 4f * jumpHeight * u * (1f - u);
            transform.position = pos;
        }
        else
        {
            pos.x = endX; pos.y = groundY; transform.position = pos;
        }

        if (!hitDone && attackT >= hitStart && attackT <= hitEnd && target != null)
        {
            Vector2 claw = new Vector2(transform.position.x + dir * 1.2f, transform.position.y + 1.0f);
            Vector2 tp = target.position;
            if (Vector2.Distance(claw, tp + Vector2.up * 0.8f) <= hitRadius)
            {
                var rec = target.GetComponent<PlayerDamageReceiver>();
                if (rec != null) { rec.TakeDamage(damage, new Vector2(dir, 0.35f)); hitDone = true; }
            }
        }

        if (attackT >= attackLength)
        {
            attacking = false;
            cooldown = attackCooldown;
            var p2 = transform.position; p2.y = groundY; transform.position = p2;
            current = null;
            Play(idleState, 0.1f);
        }
    }

    void ApplyFacing()
    {
        var s = transform.localScale;
        s.x = baseScaleX * shownFacing;
        transform.localScale = s;
    }

    void Play(string state, float fade)
    {
        if (anim == null || current == state) return;
        current = state;
        anim.CrossFadeInFixedTime(state, fade);
    }
}
