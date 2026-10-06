using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BOSS: KOŞU ATAĞI (eski adıyla Zıpla-Ez; sınıf adı BossController uyumu için aynı).
/// BossController, Gölge Hilali'ne ekler.
///
/// Akış:
///   0) KAÇIŞ : boss oyuncudan hızla uzaklaşır (duvar / zemin sonuna dek).
///   1) UYARI : döner, oyuncuya doğru çok KISA bir uyarı şeridi belirir ("ZIPLA!"),
///              hemen KİLİTLENİR (beyaz).
///   2) KOŞU  : boss şeridi çok hızlı koşar, oyuncunun İÇİNDEN geçer.
///              Parry / block / dash dokunulmazlığı KURTARMAZ; tek çözüm ÜSTÜNDEN ZIPLAMAK.
///              (Oyuncuyu geçerken yerdeyse engellenemez hasar.)
///   3) AÇIK  : boss durur, sersemler; üstünden atlandıysa "VUR!" karşı vuruş penceresi açılır.
///   Faz 2: art arda iki koşu (gidiş-dönüş).
/// </summary>
[RequireComponent(typeof(EnemyController))]
public class BossSlam : MonoBehaviour
{
    [Header("Zamanlama")]
    public float firstDelay = 3f;
    public float cooldown = 6f;
    public float cooldownPhase2 = 4f;

    [Tooltip("İlk uyarı süresi (sn). Şerit bu sürenin ilk kısmında oyuncuyu izler.")]
    public float windup = 0.5f;

    [Tooltip("Şeridin oyuncuyu izlediği kısım (uyarının yüzdesi). Kalanında KİLİTLİ beklenir = zıplama hazırlığı.")]
    [Range(0.2f, 0.9f)] public float trackFraction = 0.4f;

    [Tooltip("Koşu hızı (birim/sn).")]
    public float dashSpeed = 90f;

    [Tooltip("Kaçış (geri çekilme) hızı çarpanı: boss'un normal koşu hızı × bu değer. 1 = normal hız.")]
    public float retreatSpeedMultiplier = 1f;

    [Tooltip("Boss koşuya başlamadan önce oyuncudan kaç birim uzağa kaçar (duvar/zemin sonu izin verdiği kadar).")]
    public float retreatDistance = 16f;

    [Tooltip("Boss oyuncunun kaç birim ötesine kadar koşar.")]
    public float overshoot = 7f;

    [Tooltip("Koşu sonrası boss'un hareketsiz kaldığı süre (sn): ceza penceresi.")]
    public float exhaustTime = 1.0f;

    [Header("Menzil")]
    public float minDistance = 3.5f;
    public float maxDistance = 40f;

    [Tooltip("Oyuncunun ayağı zeminden bu kadar yukarıdaysa 'zıpladı' sayılır.")]
    public float jumpClearance = 0.9f;

    [Tooltip("Boss'un yatay çarpma payı (gövde yarı genişliğine eklenir).")]
    public float hitPadding = 0.5f;

    [Header("Hasar")]
    public float damageMultiplier = 1.1f;
    public float shake = 0.8f;

    public Color laneColor = new Color(1f, 0.15f, 0.1f, 0.5f);
    public Color lockedColor = new Color(1f, 0.95f, 0.85f, 0.85f);

    private EnemyController enemy;
    private Rigidbody2D rb;
    private BossController boss;
    private Collider2D bodyCol;

    private bool busy;
    private bool overrideVelocity;
    private Vector2 forcedVelocity;
    private float nextTime;

    private readonly List<Collider2D[]> ignoredPairs = new List<Collider2D[]>();

    public bool IsBusy => busy;

    private static Sprite whiteSprite;

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        rb = GetComponent<Rigidbody2D>();
        boss = GetComponent<BossController>();
        bodyCol = GetComponent<Collider2D>();

        nextTime = Time.time + firstDelay;
    }

    // Chase/Attack durumları hızı Update'te yazar; biz LateUpdate'te ezeriz.
    private void LateUpdate()
    {
        if (overrideVelocity && rb != null)
            rb.linearVelocity = forcedVelocity;
    }

    private void Update()
    {
        if (busy || enemy == null || rb == null || enemy.IsDead)
            return;

        BossNova nova = GetComponent<BossNova>();

        if (nova != null && nova.IsBusy)
            return;

        if (Time.time < nextTime || enemy.target == null || enemy.IsTargetDead)
            return;

        if (
            enemy.IsStaggered ||
            enemy.CurrentState is EnemyAttackState ||
            enemy.CurrentState is EnemyExecuteState
        )
        {
            return;
        }

        if (Mathf.Abs(rb.linearVelocity.y) > 0.2f)
            return;

        float d = Mathf.Abs(enemy.target.position.x - transform.position.x);

        if (d < minDistance || d > maxDistance)
            return;

        if (!BossSkillGate.CanStart("slam"))
            return;

        BossSkillGate.Begin("slam");

        StartCoroutine(ChargeRoutine());
    }

    private bool Interrupted()
    {
        return enemy == null || enemy.IsDead || enemy.IsStaggered || enemy.IsTargetDead;
    }

    // Yönde ilerlenebilecek mesafe: duvara çarpmadan ve ZEMİN BİTMEDEN (uçuruma düşmeden).
    private float ClearDistance(float dir, float wanted)
    {
        if (bodyCol == null)
            return wanted;

        Bounds bb = bodyCol.bounds;

        float best = wanted;

        // Dövüş alanı sınırı
        if (boss != null && boss.HasArena)
        {
            float room = dir > 0f
                ? boss.ArenaMaxX - bb.center.x
                : bb.center.x - boss.ArenaMinX;

            best = Mathf.Min(best, Mathf.Max(0f, room));
        }

        // Duvar
        RaycastHit2D[] hits =
            Physics2D.RaycastAll(bb.center, new Vector2(dir, 0f), wanted + bb.extents.x + 0.3f);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D c = hits[i].collider;

            if (c == null || c.isTrigger)
                continue;

            if (c.transform.IsChildOf(transform) || c.GetComponentInParent<EnemyController>() != null)
                continue;

            if (c.GetComponentInParent<PlayerController>() != null)
                continue;

            best = Mathf.Min(best, Mathf.Max(0f, hits[i].distance - bb.extents.x - 0.3f));
        }

        // Zemin: 1 birimlik adımlarla ileriye bak, zemin biterse orada dur.
        float footY = bb.min.y + 0.2f;

        for (float d = 1f; d <= best; d += 1f)
        {
            Vector2 p = new Vector2(bb.center.x + dir * (d + bb.extents.x), footY);

            RaycastHit2D[] g = Physics2D.RaycastAll(p, Vector2.down, 2.5f);

            bool ground = false;

            for (int i = 0; i < g.Length; i++)
            {
                Collider2D c = g[i].collider;

                if (c == null || c.isTrigger)
                    continue;

                if (c.transform.IsChildOf(transform) || c.GetComponentInParent<EnemyController>() != null)
                    continue;

                if (c.GetComponentInParent<PlayerController>() != null)
                    continue;

                // Boss'un bastığı seviyede olmalı (altta bir kat varsa uçurumdur).
                if (g[i].point.y < bb.min.y - 0.7f)
                    continue;

                ground = true;
                break;
            }

            if (!ground)
            {
                best = Mathf.Max(0f, d - 1.5f);
                break;
            }
        }

        return best;
    }

    private IEnumerator ChargeRoutine()
    {
        busy = true;

        int runs = boss != null && boss.InPhase2 ? 2 : 1;

        for (int i = 0; i < runs; i++)
        {
            bool ok = true;

            yield return SingleCharge(i == 0, i == runs - 1, r => ok = r);

            if (!ok)
                break;
        }

        Finish();

        bool p2 = boss != null && boss.InPhase2;

        nextTime = Time.time + (p2 ? cooldownPhase2 : cooldown);
    }

    private void Finish()
    {
        overrideVelocity = false;
        RestoreCollisions();
        busy = false;

        BossSkillGate.End("slam");
    }

    // Oyuncunun içinden geçebilmek için boss ↔ oyuncu çarpışmasını kapat.
    private void IgnorePlayerCollisions(PlayerController player)
    {
        RestoreCollisions();

        if (player == null)
            return;

        Collider2D[] pc = player.GetComponentsInChildren<Collider2D>();
        Collider2D[] bc = GetComponentsInChildren<Collider2D>();

        for (int i = 0; i < pc.Length; i++)
        {
            for (int j = 0; j < bc.Length; j++)
            {
                if (pc[i] == null || bc[j] == null)
                    continue;

                Physics2D.IgnoreCollision(pc[i], bc[j], true);

                ignoredPairs.Add(new[] { pc[i], bc[j] });
            }
        }
    }

    private void RestoreCollisions()
    {
        for (int i = 0; i < ignoredPairs.Count; i++)
        {
            Collider2D a = ignoredPairs[i][0];
            Collider2D b = ignoredPairs[i][1];

            if (a != null && b != null)
                Physics2D.IgnoreCollision(a, b, false);
        }

        ignoredPairs.Clear();
    }

    private IEnumerator SingleCharge(bool first, bool last, System.Action<bool> done)
    {
        Transform target = enemy.target;
        PlayerController player = target.GetComponent<PlayerController>();

        float groundY = bodyCol != null ? bodyCol.bounds.min.y : transform.position.y;

        GameObject lane = MakeLane(groundY);

        enemy.PlayAlertFlash();

        float dir;

        // ---------- 0) UZAĞA KOŞ (ilk koşuda): oyuncudan uzaklaş, arkaya geç ----------
        if (first)
        {
            float away = Mathf.Sign(rb.position.x - target.position.x);

            if (away == 0f)
                away = -enemy.FacingDirection;

            float retreat = ClearDistance(away, retreatDistance);

            float fromX = rb.position.x + away * retreat;

            IgnorePlayerCollisions(player);

            // Normal koşu hızında geri çekil (hızlı olan sadece asıl koşu).
            float sprintSpeed = Mathf.Max(3f, enemy.chaseSpeed * retreatSpeedMultiplier);

            float maxSprint = retreat / sprintSpeed + 1f;

            float s = 0f;

            while ((fromX - rb.position.x) * away > 0.3f && s < maxSprint)
            {
                if (Interrupted())
                {
                    Abort(lane);
                    done(false);
                    yield break;
                }

                enemy.StartAttackRecovery(0.5f);

                overrideVelocity = true;
                forcedVelocity = new Vector2(away * sprintSpeed, 0f);

                s += Time.deltaTime;

                yield return null;
            }

            RestoreCollisions();

            dir = -away;
        }
        else
        {
            dir = Mathf.Sign(target.position.x - rb.position.x);

            if (dir == 0f)
                dir = enemy.FacingDirection;
        }

        // Dur, oyuncuya dön.
        rb.linearVelocity = Vector2.zero;
        forcedVelocity = Vector2.zero;
        overrideVelocity = true;

        enemy.RetargetFacing();

        if (first)
            CombatCallout.PopupAbove(enemy, "ZIPLA!", new Color(1f, 0.35f, 0.2f), 1.0f);

        // ---------- 1) UYARI: kısa. Şerit oyuncuyu izler, sonra KİLİTLENİR ----------
        float windupTime = first ? windup : windup * 0.7f;
        float lockAt = windupTime * trackFraction;

        float endX = rb.position.x;

        float t = 0f;

        while (t < windupTime)
        {
            if (Interrupted())
            {
                Abort(lane);
                done(false);
                yield break;
            }

            enemy.StartAttackRecovery(0.5f);

            overrideVelocity = true;
            forcedVelocity = new Vector2(0f, rb.linearVelocity.y);

            float startX = rb.position.x;

            bool tracking = t < lockAt;

            if (tracking)
            {
                float d = Mathf.Sign(target.position.x - startX);

                if (d != 0f)
                    dir = d;

                float want = Mathf.Abs(target.position.x - startX) + overshoot;

                endX = startX + dir * ClearDistance(dir, want);

                enemy.RetargetFacing();
            }

            float progress = t / Mathf.Max(0.01f, windupTime);

            UpdateLane(lane, startX, endX, groundY, progress, !tracking);

            t += Time.deltaTime;

            yield return null;
        }

        // ---------- 2) KOŞU ----------
        IgnorePlayerCollisions(player);

        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(shake * 0.5f);

        bool hit = false;
        bool cleared = false;

        float halfW = (bodyCol != null ? bodyCol.bounds.extents.x : 0.8f) + hitPadding;

        float prevX = rb.position.x;

        float maxRun = Mathf.Abs(endX - rb.position.x) / Mathf.Max(1f, dashSpeed) + 0.5f;

        float run = 0f;

        while (run < maxRun)
        {
            if (enemy == null || enemy.IsDead)
            {
                Abort(lane);
                done(false);
                yield break;
            }

            enemy.StartAttackRecovery(0.5f);

            overrideVelocity = true;
            forcedVelocity = new Vector2(dir * dashSpeed, 0f);

            UpdateLane(lane, rb.position.x, endX, groundY, 1f, true);

            float curX = rb.position.x;

            if (player != null && !hit)
            {
                // Bu karede boss'un süpürdüğü aralık (hızlı koşuda kare atlamasın).
                float lo = Mathf.Min(prevX, curX) - halfW;
                float hi = Mathf.Max(prevX, curX) + halfW;

                float px = player.transform.position.x;

                if (px >= lo && px <= hi)
                {
                    Collider2D pc = player.GetComponent<Collider2D>();

                    float feet = pc != null ? pc.bounds.min.y : player.transform.position.y;

                    if (feet >= groundY + jumpClearance)
                    {
                        cleared = true; // üstünden atladı
                    }
                    else if (player.hitInvincibilityTimer <= 0f)
                    {
                        hit = true;

                        DealHit(player, dir);
                    }
                }
            }

            prevX = curX;

            run += Time.deltaTime;

            if ((curX - endX) * dir >= 0f)
                break;

            yield return null;
        }

        // ---------- 3) DUR ----------
        rb.linearVelocity = Vector2.zero;
        overrideVelocity = true;
        forcedVelocity = Vector2.zero;

        RestoreCollisions();

        if (lane != null)
            Destroy(lane);

        if (cleared && !hit)
        {
            UnblockableCounter counter = GetComponent<UnblockableCounter>();

            if (counter != null)
                counter.OpenJumpWindow();
        }

        if (last)
        {
            float e = 0f;

            while (e < exhaustTime)
            {
                if (enemy == null || enemy.IsDead || enemy.IsStaggered)
                    break;

                enemy.StartAttackRecovery(0.5f);

                overrideVelocity = true;
                forcedVelocity = new Vector2(0f, rb.linearVelocity.y);

                e += Time.deltaTime;

                yield return null;
            }

            overrideVelocity = false;

            enemy.StartAttackRecovery(1.0f);
        }
        else
        {
            // Gidiş-dönüş arası çok kısa nefes: boss döner.
            float e = 0f;

            while (e < 0.12f)
            {
                if (Interrupted())
                    break;

                enemy.StartAttackRecovery(0.5f);

                overrideVelocity = true;
                forcedVelocity = new Vector2(0f, rb.linearVelocity.y);

                e += Time.deltaTime;

                yield return null;
            }
        }

        done(true);
    }

    private void Abort(GameObject lane)
    {
        if (lane != null)
            Destroy(lane);

        overrideVelocity = false;
        RestoreCollisions();
    }

    private void DealHit(PlayerController player, float dir)
    {
        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(shake);

        HitStop.Request(0.08f, 0.05f);

        PlayerDamageReceiver receiver = player.GetComponent<PlayerDamageReceiver>();

        if (receiver == null)
            return;

        int damage = Mathf.Max(1, Mathf.RoundToInt(enemy.unblockableDamage * damageMultiplier));

        // Engellenemez: parry/block işe yaramaz, dash dokunulmazlığı da saymaz.
        receiver.TakeDamage(
            damage,
            new Vector2(dir, 0f),
            6f,
            3f,
            0.12f,
            40f,
            enemy,
            PlayerHitKind.Unblockable,
            true
        );

        EnemyAttackCoordinator.NotifyPlayerHit();

        StartCoroutine(ClampKnock(player));
    }

    // Hasar sonrası savrulmayı sınırla (oyuncu haritadan fırlamasın).
    private IEnumerator ClampKnock(PlayerController player)
    {
        if (player == null || player.rb == null)
            yield break;

        float x0 = player.rb.position.x;
        float t = 0f;

        while (t < 0.6f && player != null)
        {
            float dist = player.rb.position.x - x0;

            if (Mathf.Abs(dist) > 3f)
            {
                player.rb.position =
                    new Vector2(x0 + Mathf.Sign(dist) * 3f, player.rb.position.y);

                player.rb.linearVelocity =
                    new Vector2(0f, player.rb.linearVelocity.y);
            }

            t += Time.deltaTime;

            yield return null;
        }
    }

    // ---------------- Uyarı şeridi ----------------

    private GameObject MakeLane(float groundY)
    {
        if (whiteSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();

            whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        GameObject go = new GameObject("BossChargeLane");

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = whiteSprite;
        sr.color = laneColor;

        Shader shader = Shader.Find("Sprites/Default");

        if (shader != null)
            sr.sharedMaterial = new Material(shader);

        SpriteRenderer bossSr = GetComponentInChildren<SpriteRenderer>();

        if (bossSr != null)
        {
            sr.sortingLayerID = bossSr.sortingLayerID;
            sr.sortingOrder = bossSr.sortingOrder - 1;
        }

        // Gövde yüksekliğinde soluk bant: şeridin "tehlikeli" olduğunu gösterir.
        GameObject band = new GameObject("Band");
        band.transform.SetParent(go.transform, false);

        SpriteRenderer bsr = band.AddComponent<SpriteRenderer>();
        bsr.sprite = whiteSprite;
        bsr.sharedMaterial = sr.sharedMaterial;
        bsr.sortingLayerID = sr.sortingLayerID;
        bsr.sortingOrder = sr.sortingOrder - 1;

        go.transform.position = new Vector3(transform.position.x, groundY, 0f);

        return go;
    }

    private void UpdateLane(GameObject lane, float fromX, float toX, float groundY, float progress, bool locked)
    {
        if (lane == null)
            return;

        float len = Mathf.Max(0.1f, Mathf.Abs(toX - fromX));
        float cx = (fromX + toX) * 0.5f;

        const float FloorH = 0.3f;
        const float BandH = 1.8f;

        lane.transform.position = new Vector3(cx, groundY + FloorH * 0.5f, 0f);
        lane.transform.localScale = new Vector3(len, FloorH, 1f);

        SpriteRenderer sr = lane.GetComponent<SpriteRenderer>();

        Transform band = lane.transform.GetChild(0);

        band.localScale = new Vector3(1f, BandH / FloorH, 1f);
        band.localPosition = new Vector3(0f, (BandH * 0.5f - FloorH * 0.5f) / FloorH, 0f);

        SpriteRenderer bsr = band.GetComponent<SpriteRenderer>();

        if (locked)
        {
            sr.color = lockedColor;

            Color bc = lockedColor;
            bc.a = 0.22f;
            bsr.color = bc;

            return;
        }

        float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * (10f + 14f * progress));

        Color c = laneColor;
        c.a = Mathf.Lerp(0.3f, 0.85f, progress) * pulse;

        sr.color = c;

        Color b = laneColor;
        b.a = 0.12f * pulse;
        bsr.color = b;
    }

    private void OnDisable()
    {
        overrideVelocity = false;
        RestoreCollisions();

        if (busy)
            BossSkillGate.End("slam");

        busy = false;
    }
}
