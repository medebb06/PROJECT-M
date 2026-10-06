using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BOSS: ATLAYIŞ (uzak mesafeden üstüne zıplar). BossController, Gölge Hilali'ne ekler.
///
///   1) ODAK : boss çömelir ("DASH!"). Yerde, oyuncunun altında kırmızı bir alan onu izler;
///             son kısımda KİLİTLENİR (beyaz). Oyuncu ne zaman kaçması gerektiğini bundan anlar.
///   2) ATLA : boss hızlı bir yay çizerek kilitli alana fırlar (oyuncunun içinden geçebilir).
///   3) İNİŞ : alandaki herkes hasar alır. ENGELLENEMEZ: parry/block işe yaramaz.
///             Kurtaran: DASH (i-frame) ya da ALANDAN ÇIKMAK.
///   4) AÇIK : boss kısa süre sersemler.
///   Faz 2: art arda iki atlayış.
/// </summary>
[RequireComponent(typeof(EnemyController))]
public class BossLeap : MonoBehaviour
{
    [Header("Zamanlama")]
    public float firstDelay = 4f;
    public float cooldown = 9f;
    public float cooldownPhase2 = 7f;

    [Tooltip("Yükselme (odaklanma) süresi (sn). Uzun: boşluğu bulup koşmak için zaman.")]
    public float riseTime = 2.0f;

    public float riseHeight = 9f;

    [Tooltip("Tepede asılı kalma (sn): her yer kilitlenir.")]
    public float hangTime = 0.35f;

    [Tooltip("Düşüş süresi (sn).")]
    public float dropTime = 0.22f;

    [Tooltip("Güvenli boşluğun genişliği (birim).")]
    public float gapWidth = 4.2f;

    public float gapMinOffset = 6f;
    public float gapMaxOffset = 11f;


    [Tooltip("Atlayış yatay hızı (birim/sn).")]
    public float leapSpeed = 40f;

    public float minFlightTime = 0.5f;
    public float maxFlightTime = 0.85f;

    [Tooltip("Yayın en yüksek noktası: taban + mesafe × çarpan.")]
    public float arcBase = 5f;
    public float arcPerDistance = 0.18f;

    public float exhaustTime = 1.4f;

    [Header("Menzil")]
    [Tooltip("Bu mesafeden YAKINSA atlamaz: bu yetenek UZAK mesafe içindir.")]
    public float minDistance = 0f;
    public float maxDistance = 40f;

    [Header("Hasar")]
    [Tooltip("İniş alanının yarıçapı (yatay, birim).")]
    public float radius = 3.2f;
    public float damageMultiplier = 1.2f;
    public float shake = 1.0f;

    public Color zoneColor = new Color(1f, 0.15f, 0.1f, 0.5f);
    public Color lockedColor = new Color(1f, 0.95f, 0.85f, 0.85f);

    private EnemyController enemy;
    private Rigidbody2D rb;
    private BossController boss;
    private Collider2D bodyCol;

    private bool busy;
    private bool overrideVelocity;
    private float nextTime;
    private float originalGravity;
    private bool gravityOverridden;

    private readonly List<Collider2D[]> ignoredPairs = new List<Collider2D[]>();

    public bool IsBusy => busy;

    /// <summary>Cooldown bitti, atlamaya hazır (BossSlam bu durumda öne geçmesine izin verir).</summary>
    public bool IsReady => !busy && Time.time >= nextTime;

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
            rb.linearVelocity = Vector2.zero;
    }

    private void Update()
    {
        if (busy || enemy == null || rb == null || enemy.IsDead)
            return;

        BossSlam slam = GetComponent<BossSlam>();
        BossNova nova = GetComponent<BossNova>();

        if ((slam != null && slam.IsBusy) || (nova != null && nova.IsBusy))
            return;

        BossBrain brain = GetComponent<BossBrain>();

        float early = brain != null ? brain.EarlySkillBonus : 0f;

        if (Time.time < nextTime - early || enemy.target == null || enemy.IsTargetDead)
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

        if (!BossSkillGate.CanStart("leap"))
            return;

        BossSkillGate.Begin("leap");

        StartCoroutine(LeapRoutine());
    }

    private bool Interrupted()
    {
        return enemy == null || enemy.IsDead || enemy.IsStaggered || enemy.IsTargetDead;
    }

    private void SetGravity(bool off)
    {
        if (rb == null)
            return;

        if (off && !gravityOverridden)
        {
            originalGravity = rb.gravityScale;
            rb.gravityScale = 0f;
            gravityOverridden = true;
        }
        else if (!off && gravityOverridden)
        {
            rb.gravityScale = originalGravity;
            gravityOverridden = false;
        }
    }

    private IEnumerator LeapRoutine()
    {
        busy = true;

        int leaps = 1;

        for (int i = 0; i < leaps; i++)
        {
            bool ok = true;

            yield return SingleLeap(i == 0, i == leaps - 1, r => ok = r);

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
        SetGravity(false);
        RestoreCollisions();
        busy = false;

        BossSkillGate.End("leap");
    }

    private void Abort(GameObject zone)
    {
        if (zone != null)
            Destroy(zone);

        Finish();
    }

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

    private float ClampToArena(float x)
    {
        if (boss != null && boss.HasArena)
            return Mathf.Clamp(x, boss.ArenaMinX, boss.ArenaMaxX);

        return x;
    }

    private IEnumerator SingleLeap(bool first, bool last, System.Action<bool> done)
    {
        Transform target = enemy.target;
        PlayerController player = target.GetComponent<PlayerController>();

        float groundY = bodyCol != null ? bodyCol.bounds.min.y : transform.position.y;

        GetArenaRange(out float minX, out float maxX);

        // Güvenli boşluk: oyuncudan uzakta bir yerde, arena içinde.
        float half = gapWidth * 0.5f;
        float px = target.position.x;

        float side = Random.value < 0.5f ? -1f : 1f;

        float gapX = px + side * Random.Range(gapMinOffset, gapMaxOffset);

        if (gapX - half < minX || gapX + half > maxX)
            gapX = px - side * Random.Range(gapMinOffset, gapMaxOffset);

        gapX = Mathf.Clamp(gapX, minX + half, maxX - half);

        GameObject left = MakeZone(groundY);
        GameObject right = MakeZone(groundY);

        enemy.PlayAlertFlash();

        CombatCallout.PopupAbove(enemy, "BOŞLUĞA KAÇ!", new Color(1f, 0.35f, 0.2f), 1.4f);

        IgnorePlayerCollisions(player);

        float time = riseTime;

        Vector2 start = rb.position;

        // ---------- 1) YÜKSELİŞ / ODAK ----------
        SetGravity(true);

        float t = 0f;

        while (t < time)
        {
            if (Interrupted())
            {
                Abort(left);
                if (right != null) Destroy(right);
                done(false);
                yield break;
            }

            enemy.StartAttackRecovery(0.5f);

            overrideVelocity = true;

            float u = Mathf.Clamp01(t / time);

            // Yavaşlayarak yükselir (odaklanma gibi), sonda titrer.
            float e = 1f - (1f - u) * (1f - u);

            float shakeX = u > 0.75f ? Mathf.Sin(Time.time * 70f) * 0.12f * (u - 0.75f) * 4f : 0f;

            rb.position = new Vector2(start.x + shakeX, start.y + riseHeight * e);

            float p = u;

            UpdateRange(left, minX, gapX - half, groundY, p, false);
            UpdateRange(right, gapX + half, maxX, groundY, p, false);

            t += Time.deltaTime;

            yield return null;
        }

        // Son bir an asılı kalır (kilit).
        float hold = 0f;

        while (hold < hangTime)
        {
            if (Interrupted())
            {
                Abort(left);
                if (right != null) Destroy(right);
                done(false);
                yield break;
            }

            enemy.StartAttackRecovery(0.5f);

            overrideVelocity = true;

            UpdateRange(left, minX, gapX - half, groundY, 1f, true);
            UpdateRange(right, gapX + half, maxX, groundY, 1f, true);

            hold += Time.deltaTime;

            yield return null;
        }

        // ---------- 2) İNİŞ (düşüş) ----------
        Vector2 top = rb.position;
        Vector2 land = new Vector2(Mathf.Clamp(top.x, minX, maxX), start.y);

        float f = 0f;

        while (f < dropTime)
        {
            if (enemy == null || enemy.IsDead)
            {
                Abort(left);
                if (right != null) Destroy(right);
                done(false);
                yield break;
            }

            enemy.StartAttackRecovery(0.5f);

            overrideVelocity = true;

            float u = Mathf.Clamp01(f / dropTime);

            rb.position = Vector2.Lerp(top, land, u * u);

            f += Time.deltaTime;

            yield return null;
        }

        rb.position = land;
        rb.linearVelocity = Vector2.zero;

        SetGravity(false);
        RestoreCollisions();

        Impact(player, gapX, half, minX, maxX);

        FlashRange(left, minX, gapX - half, groundY);
        FlashRange(right, gapX + half, maxX, groundY);

        // ---------- 3) AÇIK ----------
        enemy.openUntil = EnemyTime.Now + exhaustTime;

        float ex = 0f;

        while (ex < exhaustTime)
        {
            if (enemy == null || enemy.IsDead || enemy.IsStaggered)
                break;

            enemy.StartAttackRecovery(0.5f);

            overrideVelocity = true;

            ex += Time.deltaTime;

            yield return null;
        }

        overrideVelocity = false;

        enemy.StartAttackRecovery(1.0f);

        done(true);
    }

    private void GetArenaRange(out float minX, out float maxX)
    {
        if (boss != null && boss.HasArena)
        {
            minX = boss.ArenaMinX;
            maxX = boss.ArenaMaxX;
            return;
        }

        Camera cam = Camera.main;

        float cx = cam != null ? cam.transform.position.x : transform.position.x;
        float hw = cam != null ? cam.orthographicSize * cam.aspect : 18f;

        minX = cx - hw;
        maxX = cx + hw;
    }

    private void Impact(PlayerController player, float gapX, float half, float minX, float maxX)
    {
        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(shake);

        HitStop.Request(0.08f, 0.05f);

        if (player == null)
            return;

        // Boşlukta durmak tek kurtuluş.
        if (Mathf.Abs(player.transform.position.x - gapX) <= half)
            return;

        // Sadece hasar sonrası korumalı dönem sayılır; dash i-frame'i KURTARMAZ.
        if (player.hitInvincibilityTimer > 0f)
            return;

        PlayerDamageReceiver receiver = player.GetComponent<PlayerDamageReceiver>();

        if (receiver == null)
            return;

        Vector2 dir = new Vector2(Mathf.Sign(player.transform.position.x - gapX), 0f);

        if (dir.x == 0f)
            dir.x = 1f;

        int damage = Mathf.Max(1, Mathf.RoundToInt(enemy.unblockableDamage * damageMultiplier));

        receiver.TakeDamage(
            damage,
            dir,
            4f,
            1f,
            0.12f,
            40f,
            enemy,
            PlayerHitKind.Unblockable,
            true
        );

        EnemyAttackCoordinator.NotifyPlayerHit();

        StartCoroutine(ClampKnock(player));
    }

    private IEnumerator ClampKnock(PlayerController player)
    {
        if (player == null || player.rb == null)
            yield break;

        float x0 = player.rb.position.x;
        float t = 0f;

        while (t < 0.6f && player != null)
        {
            float dist = player.rb.position.x - x0;

            if (Mathf.Abs(dist) > 2.5f)
            {
                player.rb.position =
                    new Vector2(x0 + Mathf.Sign(dist) * 2.5f, player.rb.position.y);

                player.rb.linearVelocity =
                    new Vector2(0f, player.rb.linearVelocity.y);
            }

            t += Time.deltaTime;

            yield return null;
        }
    }

    // ---------------- Uyarı alanı ----------------

    private GameObject MakeZone(float groundY)
    {
        if (whiteSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();

            whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        GameObject go = new GameObject("BossLeapZone");

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = whiteSprite;
        sr.color = zoneColor;

        Shader shader = Shader.Find("Sprites/Default");

        if (shader != null)
            sr.sharedMaterial = new Material(shader);

        SpriteRenderer bossSr = GetComponentInChildren<SpriteRenderer>();

        if (bossSr != null)
        {
            sr.sortingLayerID = bossSr.sortingLayerID;
            sr.sortingOrder = bossSr.sortingOrder - 1;
        }

        // Gövde yüksekliğinde soluk bant (alanın "tehlikeli" olduğunu gösterir).
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

    private void UpdateRange(GameObject zone, float x0, float x1, float groundY, float progress, bool locked)
    {
        if (zone == null)
            return;

        const float FloorH = 0.3f;
        const float BandH = 3.2f;

        float w = Mathf.Max(0.01f, x1 - x0);

        zone.transform.position = new Vector3((x0 + x1) * 0.5f, groundY + FloorH * 0.5f, 0f);
        zone.transform.localScale = new Vector3(w, FloorH, 1f);

        SpriteRenderer sr = zone.GetComponent<SpriteRenderer>();

        Transform band = zone.transform.GetChild(0);

        band.localScale = new Vector3(1f, BandH / FloorH, 1f);
        band.localPosition = new Vector3(0f, (BandH * 0.5f - FloorH * 0.5f) / FloorH, 0f);

        SpriteRenderer bsr = band.GetComponent<SpriteRenderer>();

        if (locked)
        {
            sr.color = lockedColor;

            Color lc = lockedColor;
            lc.a = 0.3f;
            bsr.color = lc;

            return;
        }

        float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * (8f + 16f * progress));

        Color c = zoneColor;
        c.a = Mathf.Lerp(0.3f, 0.9f, progress) * pulse;

        sr.color = c;

        Color b = zoneColor;
        b.a = Mathf.Lerp(0.08f, 0.22f, progress) * pulse;
        bsr.color = b;
    }

    private void FlashRange(GameObject zone, float x0, float x1, float groundY)
    {
        if (zone == null)
            return;

        zone.transform.position = new Vector3((x0 + x1) * 0.5f, groundY + 0.35f, 0f);
        zone.transform.localScale = new Vector3(Mathf.Max(0.01f, x1 - x0), 0.7f, 1f);

        SpriteRenderer sr = zone.GetComponent<SpriteRenderer>();
        sr.color = Color.white;

        zone.transform.GetChild(0).GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, 0.45f);

        Destroy(zone, 0.18f);
    }

    private void OnDisable()
    {
        overrideVelocity = false;
        SetGravity(false);
        RestoreCollisions();

        if (busy)
            BossSkillGate.End("leap");

        busy = false;
    }
}
