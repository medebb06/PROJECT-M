using System.Collections;
using UnityEngine;

/// <summary>
/// BOSS: ZIPLA-EZ (alan saldırısı). BossController, Gölge Hilali'ne ekler.
///
/// Akış (okunur → ani):
///   1) UYARI : yerde kırmızı alan belirir, boss çömelir ("ALANDAN ÇIK!").
///   2) YÜKSEL: boss çok yükseğe fırlar.
///   3) ASILI : havada, alanın tepesinde durur. Alan ve boss oyuncuyu takip
///              eder, havaya çıkarken KİLİTLENİR (alan beyaza döner).
///   4) DÜŞÜŞ : süre dolunca boss çok hızlı çakılır.
///   5) İNİŞ  : alanda ve YERDEYSEN engellenemez hasar (parry/block kurtarmaz).
///              Kurtaran: ALANDAN ÇIKMAK, ZIPLAMAK ("VUR!" penceresi), dash.
///   6) AÇIK  : boss kısa süre sersemler.
///   Faz 2: art arda iki çakılma.
/// </summary>
[RequireComponent(typeof(EnemyController))]
public class BossSlam : MonoBehaviour
{
    [Header("Zamanlama")]
    public float firstDelay = 3f;
    public float cooldown = 6f;
    public float cooldownPhase2 = 4f;

    [Tooltip("Çömelme/uyarı (sn).")]
    public float windup = 0.55f;

    [Tooltip("Yükselme süresi (sn).")]
    public float riseTime = 0.7f;

    [Tooltip("Yükseldiği yükseklik (birim).")]
    public float riseHeight = 11f;

    [Tooltip("Havada asılı kalma (sn).")]
    public float hangTime = 0.35f;

    [Tooltip("Boss'un çapraz inişe başladığı yatay uzaklık (inecek alana göre, birim).")]
    public float diagonalOffset = 6f;

    [Tooltip("Havadayken inecek alana doğru en fazla yatay hız (birim/sn). Alan, boss'un yetişebileceği menzille sınırlıdır.")]
    public float moveSpeed = 9f;

    [Tooltip("Çakılma hızı (birim/sn).")]
    public float plungeSpeed = 70f;

    [Tooltip("İnişten sonra boss'un hareketsiz kaldığı süre (sn): ceza penceresi.")]
    public float exhaustTime = 1.5f;

    [Header("Menzil")]
    public float minDistance = 2.5f;
    public float maxDistance = 11f;

    [Tooltip("Hasar alanı yarıçapı (yatay, birim).")]
    public float radius = 6f;

    [Tooltip("Açıkken alan yarıçapı oyuncunun DASH mesafesinden hesaplanır: bir dash (A/D + Shift) ile alanın kenarından 'ucu ucuna' çıkılır. Kapalıysa 'radius' kullanılır.")]
    public bool radiusFromDash = true;

    [Tooltip("Dash mesafesinden çıkarılan pay (birim): büyük = daha kolay kaçış.")]
    public float escapeFactor = 0.9f;

    [Tooltip("Alan yarıçapı üst sınırı (birim).")]
    public float maxZoneRadius = 12f;

    private float zoneRadius = 6f;

    [Tooltip("Oyuncunun ayağı zeminden bu kadar yukarıdaysa 'zıpladı' sayılır.")]
    public float jumpClearance = 0.8f;

    [Header("Hasar")]
    public float damageMultiplier = 1.2f;
    public float shake = 1.0f;

    public Color zoneColor = new Color(1f, 0.15f, 0.1f, 0.45f);
    public Color lockedColor = new Color(1f, 0.95f, 0.85f, 0.8f);

    private EnemyController enemy;
    private Rigidbody2D rb;
    private BossController boss;
    private Collider2D bodyCol;

    private bool busy;
    private bool overrideVelocity;
    private Vector2 forcedVelocity;
    private float nextTime;
    private float originalGravity;
    private bool gravityOverridden;

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

        StartCoroutine(SlamRoutine());
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

    private IEnumerator SlamRoutine()
    {
        busy = true;

        int jumps = boss != null && boss.InPhase2 ? 2 : 1;

        for (int i = 0; i < jumps; i++)
        {
            bool ok = true;

            yield return SingleSlam(i == 0, r => ok = r);

            if (!ok)
                break;
        }

        overrideVelocity = false;
        SetGravity(false);
        busy = false;

        BossSkillGate.End("slam");

        bool p2 = boss != null && boss.InPhase2;

        nextTime = Time.time + (p2 ? cooldownPhase2 : cooldown);
    }

    // Hedefe doğru yatay hız (ani tepkili takip).
    private float TrackVelocity(float targetX)
    {
        return Mathf.Clamp((targetX - rb.position.x) * 12f, -16f, 16f);
    }

    private void Abort(GameObject zone)
    {
        if (zone != null)
            Destroy(zone);

        overrideVelocity = false;
        SetGravity(false);
    }

    private IEnumerator SingleSlam(bool first, System.Action<bool> done)
    {
        Transform target = enemy.target;
        Collider2D tcol = target.GetComponent<Collider2D>();

        // Zemin = BOSS'un bastığı zemin (oyuncu havadaysa bile alan yerde olur).
        float groundY =
            bodyCol != null ? bodyCol.bounds.min.y : transform.position.y;

        float feetOffset =
            bodyCol != null
                ? transform.position.y - bodyCol.bounds.min.y
                : 1f;

        float lockedX = target.position.x;

        float windupTime = first ? windup : windup * 0.4f;
        float hang = first ? hangTime : hangTime * 0.6f;

        // Alan, boss'un havadayken yetişebileceği menzille sınırlı.
        float startX = rb.position.x;

        zoneRadius = ComputeRadius(target, riseTime + hang);
        // Boss yatayda hareket etmez (dikey yükselir); alan boss'a göre sınırlı.
        float reach = maxDistance + 1f;

        // Karar çömelmenin ilk %45'inde verilir; sonra alan sabit.
        float lockAt = first ? windupTime * 0.45f : 0f;

        bool locked = false;

        float hangX = rb.position.x;

        lockedX = Mathf.Clamp(target.position.x, startX - reach, startX + reach);

        GameObject zone = MakeZone(groundY);

        enemy.PlayAlertFlash();

        if (first)
            CombatCallout.PopupAbove(enemy, "ALANDAN ÇIK!", new Color(1f, 0.3f, 0.2f), 1.3f);

        // ---------- 1) UYARI (çömelme) ----------
        float t = 0f;

        while (t < windupTime)
        {
            if (Interrupted())
            {
                Abort(zone);
                done(false);
                yield break;
            }

            enemy.StartAttackRecovery(0.5f);

            overrideVelocity = true;
            forcedVelocity = new Vector2(0f, rb.linearVelocity.y);

            bool tracking = t < lockAt;

            if (tracking)
                lockedX = Mathf.Clamp(target.position.x, startX - reach, startX + reach);

            UpdateZone(zone, lockedX, groundY, 0.2f + 0.2f * (t / Mathf.Max(0.01f, windupTime)), !tracking);

            t += Time.deltaTime;

            yield return null;
        }

        // ---------- KARAR: havaya çıkarken alan ve iniş noktası KİLİTLENİR ----------
        locked = true;

        {
            hangX = startX;
        }

        // ---------- 2) YÜKSEL ----------
        float h = RiseClearance(riseHeight);

        SetGravity(true);

        t = 0f;

        while (t < riseTime)
        {
            if (Interrupted())
            {
                Abort(zone);
                done(false);
                yield break;
            }

            enemy.StartAttackRecovery(0.5f);

            overrideVelocity = true;
            forcedVelocity =
                new Vector2(
                    Mathf.Clamp((hangX - rb.position.x) * 6f, -moveSpeed * 1.4f, moveSpeed * 1.4f),
                    h / riseTime
                );

            UpdateZone(zone, lockedX, groundY, 0.4f + 0.2f * (t / riseTime), true);

            t += Time.deltaTime;

            yield return null;
        }

        // ---------- 3) ASILI (takip → kilit) ----------
        t = 0f;

        while (t < hang)
        {
            if (Interrupted())
            {
                Abort(zone);
                done(false);
                yield break;
            }

            enemy.StartAttackRecovery(0.5f);

            overrideVelocity = true;
            float dxv = hangX - rb.position.x;

            forcedVelocity =
                new Vector2(
                    Mathf.Clamp(dxv * 6f, -moveSpeed * 1.4f, moveSpeed * 1.4f),
                    0f
                );

            UpdateZone(zone, lockedX, groundY, 0.4f + 0.6f * (t / hang), locked);

            t += Time.deltaTime;

            yield return null;
        }

        // ---------- 4) DÜŞÜŞ ----------
        float fall = 0f;

        while (fall < 1.2f)
        {
            if (enemy == null || enemy.IsDead)
            {
                Abort(zone);
                done(false);
                yield break;
            }

            overrideVelocity = true;
            Vector2 to =
                new Vector2(lockedX, groundY + feetOffset) - rb.position;

            forcedVelocity = to.normalized * plungeSpeed;

            fall += Time.deltaTime;

            float feet = transform.position.y - feetOffset;

            if (feet <= groundY + 0.15f)
                break;

            yield return null;
        }

        // ---------- 5) İNİŞ ----------
        rb.linearVelocity = Vector2.zero;
        overrideVelocity = true;
        forcedVelocity = Vector2.zero;

        SetGravity(false);

        Impact(lockedX, groundY);

        FlashZone(zone, lockedX, groundY);

        // ---------- 6) AÇIK ----------
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

        enemy.StartAttackRecovery(1.2f);

        done(true);
    }

    private float ComputeRadius(Transform target, float airTime)
    {
        if (!radiusFromDash || target == null)
            return radius;

        PlayerController p = target.GetComponent<PlayerController>();

        if (p == null || p.dashDistance <= 0.5f)
            return radius;

        // Kilitten çarpmaya kadar süre içinde: bir dash + yürüyüş (verimi ~%50)
        // + süre izin veriyorsa ikinci dash. Alan, bunların %90'ı kadar:
        // kaçış 'ucu ucuna' olur.
        float dashCycle = Mathf.Max(0.05f, p.dashTime + p.dashCooldown);
        int dashes = 1 + Mathf.FloorToInt(airTime / dashCycle);

        float reachable = p.dashDistance * dashes + p.moveSpeed * 0.5f * airTime;

        return Mathf.Clamp(reachable * escapeFactor, 4f, maxZoneRadius);
    }

    // Tavan varsa yükselme yüksekliğini kıs.
    private float RiseClearance(float wanted)
    {
        if (bodyCol == null)
            return wanted;

        Bounds b = bodyCol.bounds;

        RaycastHit2D[] hits =
            Physics2D.RaycastAll(
                new Vector2(b.center.x, b.max.y),
                Vector2.up,
                wanted + 0.5f
            );

        float best = wanted;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D c = hits[i].collider;

            if (c == null || c.isTrigger)
                continue;

            if (c.transform.IsChildOf(transform) || c.GetComponentInParent<EnemyController>() != null)
                continue;

            if (c.GetComponentInParent<PlayerController>() != null)
                continue;

            best = Mathf.Min(best, Mathf.Max(1.5f, hits[i].distance - 0.5f));
        }

        return best;
    }

    private void Impact(float centerX, float groundY)
    {
        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(shake);

        HitStop.Request(0.08f, 0.05f);

        if (enemy.target == null)
            return;

        PlayerController player = enemy.target.GetComponent<PlayerController>();

        if (player == null)
            return;

        Collider2D col = player.GetComponent<Collider2D>();

        float feet = col != null ? col.bounds.min.y : player.transform.position.y;

        bool inside = Mathf.Abs(player.transform.position.x - centerX) <= zoneRadius;

        if (!inside)
            return;

        // Havadayken de vurur: kurtulmanın tek yolu ALANDAN ÇIKMAK (yatay).

        // Dash i-frame'i ve zıplama KURTARMAZ: alandan dışarı çıkmak gerekir.
        if (player.hitInvincibilityTimer > 0f)
            return;

        PlayerDamageReceiver receiver = player.GetComponent<PlayerDamageReceiver>();

        if (receiver == null)
            return;

        Vector2 dir =
            new Vector2(Mathf.Sign(player.transform.position.x - centerX), 0f);

        int damage = Mathf.Max(1, Mathf.RoundToInt(enemy.unblockableDamage * damageMultiplier));

        receiver.TakeDamage(
            damage,
            dir,
            4f,
            2f,
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

        GameObject go = new GameObject("BossSlamZone");

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

        go.transform.position = new Vector3(transform.position.x, groundY, 0f);

        // Alanın iki kenarına dikey direkler: okunurluk.
        for (int i = 0; i < 2; i++)
        {
            GameObject post = new GameObject(i == 0 ? "PostL" : "PostR");
            post.transform.SetParent(go.transform, false);

            SpriteRenderer psr = post.AddComponent<SpriteRenderer>();
            psr.sprite = whiteSprite;
            psr.sharedMaterial = sr.sharedMaterial;
            psr.sortingLayerID = sr.sortingLayerID;
            psr.sortingOrder = sr.sortingOrder + 1;
        }

        return go;
    }

    private void UpdateZone(GameObject zone, float x, float groundY, float progress, bool locked)
    {
        if (zone == null)
            return;

        zone.transform.position = new Vector3(x, groundY + 0.08f, 0f);
        zone.transform.localScale = new Vector3(zoneRadius * 2f, 0.22f, 1f);

        SpriteRenderer sr = zone.GetComponent<SpriteRenderer>();

        PlacePosts(zone, locked ? lockedColor : new Color(1f, 0.25f, 0.15f, Mathf.Lerp(0.5f, 1f, progress)));

        if (locked)
        {
            sr.color = lockedColor;
            return;
        }

        float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * (10f + 14f * progress));

        Color c = zoneColor;
        c.a = Mathf.Lerp(0.25f, 0.8f, progress) * pulse;

        sr.color = c;
    }

    // Direkler: dünya boyutu 0.14 x 1.8; ebeveyn ölçeğini telafi eder.
    private void PlacePosts(GameObject zone, Color color)
    {
        const float PostW = 0.14f;
        const float PostH = 1.8f;

        Vector3 ps = zone.transform.localScale;

        for (int i = 0; i < 2; i++)
        {
            Transform post = zone.transform.GetChild(i);

            post.localScale = new Vector3(PostW / ps.x, PostH / ps.y, 1f);

            post.localPosition =
                new Vector3(i == 0 ? -0.5f : 0.5f, (PostH * 0.5f - ps.y * 0.5f) / ps.y, 0f);

            post.GetComponent<SpriteRenderer>().color = color;
        }
    }

    private void FlashZone(GameObject zone, float x, float groundY)
    {
        if (zone == null)
            return;

        zone.transform.position = new Vector3(x, groundY + 0.3f, 0f);
        zone.transform.localScale = new Vector3(zoneRadius * 2.3f, 0.7f, 1f);
        zone.GetComponent<SpriteRenderer>().color = Color.white;

        PlacePosts(zone, Color.white);

        Destroy(zone, 0.12f);
    }

    private void OnDisable()
    {
        overrideVelocity = false;
        SetGravity(false);
        if (busy)
            BossSkillGate.End("slam");

        busy = false;
    }
}
