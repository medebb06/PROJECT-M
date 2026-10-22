using UnityEngine;

/// <summary>Cekirge: saldiri sesi (baslangic + firlama) ve olunce parcalara ayrilma.</summary>
public class CekirgeFx : MonoBehaviour
{
    public AudioClip attackClip;
    [Range(0f, 1f)] public float volume = 0.9f;
    public Transform rig;
    public Animator rigAnimator;
    public float midSoundTime = 0.55f;   // Attack klibinde 2. ses (firlama)

    [Header("Parcalanma")]
    public float gravity = 26f;
    public float burstSpeed = 5.5f;
    public float upSpeed = 7f;
    public float holdTime = 2.0f;
    public float fadeTime = 0.7f;

    EnemyController enemy;
    Health health;
    CekirgeVisual visual;
    AudioSource src;
    bool wasAttacking, midPlayed;

    void Awake()
    {
        enemy = GetComponent<EnemyController>();
        health = GetComponent<Health>();
        visual = GetComponent<CekirgeVisual>();
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        if (rig == null) { var a = GetComponentInChildren<Animator>(); if (a != null) rig = a.transform; }
        if (rigAnimator == null && rig != null) rigAnimator = rig.GetComponent<Animator>();
    }

    void OnEnable() { if (health != null) health.OnDeath += Shatter; }
    void OnDisable() { if (health != null) health.OnDeath -= Shatter; }

    void Update()
    {
        bool attacking = enemy != null && enemy.CurrentState is EnemyAttackState && !enemy.IsDead;

        if (attacking && !wasAttacking) { Play(1f); midPlayed = false; }

        if (attacking && !midPlayed && rigAnimator != null)
        {
            var st = rigAnimator.GetCurrentAnimatorStateInfo(0);
            if (st.IsName("Attack") && st.normalizedTime * st.length >= midSoundTime) { midPlayed = true; Play(1.12f); }
        }

        wasAttacking = attacking;
    }

    void Play(float pitch)
    {
        if (attackClip == null || src == null) return;
        src.pitch = pitch;
        src.PlayOneShot(attackClip, volume);
    }

    bool shattered;

    void Shatter()
    {
        if (rig == null || shattered) return;
        shattered = true;

        float groundY = transform.position.y;
        var srs = rig.GetComponentsInChildren<SpriteRenderer>();

        Vector3 center = Vector3.zero; int n = 0;
        foreach (var sr in srs) { if (sr.sprite == null || !sr.enabled) continue; center += sr.transform.position; n++; }
        if (n == 0) return;
        center /= n;

        foreach (var sr in srs)
        {
            if (sr.sprite == null || !sr.enabled) continue;

            var go = new GameObject("CekirgePiece");
            go.transform.position = sr.transform.position;
            go.transform.rotation = sr.transform.rotation;
            go.transform.localScale = sr.transform.lossyScale;

            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sr.sprite; r.sharedMaterial = visual != null ? visual.BaseMaterialOf(sr) : sr.sharedMaterial;
            Color c = visual != null ? visual.BaseColorOf(sr) : sr.color;
            c.a = 1f; r.color = c;
            r.sortingLayerID = sr.sortingLayerID; r.sortingOrder = sr.sortingOrder; r.flipX = sr.flipX;

            Vector2 away = (Vector2)(sr.transform.position - center);
            float sx = Mathf.Abs(away.x) > 0.15f ? Mathf.Sign(away.x) : (Random.value < 0.5f ? -1f : 1f);

            var d = go.AddComponent<CekirgePiece>();
            d.Init(groundY, gravity,
                new Vector2(sx * Random.Range(0.35f, 1f) * burstSpeed + away.x * 1.2f, Random.Range(0.45f, 1f) * upSpeed + Mathf.Max(0f, away.y) * 1.0f),
                Random.Range(-540f, 540f), holdTime, fadeTime);
        }
    }
}

public class CekirgePiece : MonoBehaviour
{
    Vector2 vel; float ang, groundY, g, hold, fade, rest;
    int bounces; bool landed;
    SpriteRenderer sr;
    Vector2 ext;       // sprite yarim boyutu (olcekli)
    float settleAngle;

    public void Init(float groundY, float g, Vector2 v, float angVel, float hold, float fade)
    {
        this.groundY = groundY; this.g = g; this.hold = hold; this.fade = fade;
        vel = v; ang = angVel;
        sr = GetComponent<SpriteRenderer>();
        var b = sr.sprite.bounds;
        var s = transform.localScale;
        ext = new Vector2(b.extents.x * Mathf.Abs(s.x), b.extents.y * Mathf.Abs(s.y));
    }

    float HalfHeight()
    {
        float a = transform.eulerAngles.z * Mathf.Deg2Rad;
        return Mathf.Abs(ext.x * Mathf.Sin(a)) + Mathf.Abs(ext.y * Mathf.Cos(a));
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (!landed)
        {
            vel.y -= g * dt;
            var p = transform.position; p.x += vel.x * dt; p.y += vel.y * dt;
            transform.position = p;
            transform.Rotate(0f, 0f, ang * dt);

            float floor = groundY + HalfHeight() * 0.8f;
            if (transform.position.y <= floor && vel.y < 0f)
            {
                p = transform.position; p.y = floor; transform.position = p;
                if (bounces < 2) { vel.y = -vel.y * 0.35f; vel.x *= 0.55f; ang *= 0.5f; bounces++; }
                else
                {
                    landed = true; vel = Vector2.zero; ang = 0f;
                    // Uzun ekseni yataya yatacak sekilde en yakin acı
                    float z = transform.eulerAngles.z;
                    float baseA = ext.y > ext.x ? 90f : 0f;
                    float best = baseA, bd = 1e9f;
                    for (int k = -4; k <= 4; k++)
                    {
                        float cand = baseA + 180f * k;
                        float d = Mathf.Abs(Mathf.DeltaAngle(z, cand));
                        if (d < bd) { bd = d; best = cand; }
                    }
                    settleAngle = best;
                }
            }
        }
        else
        {
            rest += dt;
            float z = Mathf.MoveTowardsAngle(transform.eulerAngles.z, settleAngle, 600f * dt);
            transform.rotation = Quaternion.Euler(0f, 0f, z);
            var p = transform.position;
            p.y = Mathf.MoveTowards(p.y, groundY + HalfHeight() * 0.8f, 6f * dt);
            transform.position = p;

            if (rest > hold && sr != null)
            {
                float a = 1f - (rest - hold) / Mathf.Max(0.01f, fade);
                var c = sr.color; c.a = Mathf.Clamp01(a); sr.color = c;
                if (a <= 0f) Destroy(gameObject);
            }
        }
    }
}
