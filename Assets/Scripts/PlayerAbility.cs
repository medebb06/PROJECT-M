using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// YETENEK YUVASI (Sekiro protez aleti mantığı). Tek yuva, [Q] ile kullanılır.
/// Parry'nin YERİNE değil YANINDA: bekleme süresi uzun, parry ve öldürme
/// bekleme süresini kısaltır (şarj).
///
///   Şok Dalgası : çevredeki tüm düşmanlara denge hasarı + geri itme.
///   Gölge Adım  : en yakın düşmanın ARKASINA ışınlan + güçlü denge vuruşu.
///   Buz Nefesi  : düşmanların zamanı yavaşlar; öndekiler kesilir + denge.
///   Alev Dalgası: öndekileri yakar (sersemlemiş düşmana çift yanık).
///
/// Hasarlar düşmanın MAX denge / canının YÜZDESİ (ölçekten bağımsız) ve
/// PlayerDamage hattından geçer (kaynak = Ability): kritik, charm'lar
/// (zehir vb.) ve istatistikler çalışır; riposte hakkı yemez.
///
/// Seviye (1-3): +%25 güç, −%10 bekleme. Koşu başında seçilir, dükkanda
/// yükseltilir / değiştirilir. RunManager oyuncuya kendiliğinden ekler.
/// </summary>
public class PlayerAbility : MonoBehaviour
{
    public static PlayerAbility Instance { get; private set; }

    /// <summary>Yetenek kullanıldı (charm'lar abone olabilir).</summary>
    public static event Action<AbilityType> Used;

    [Header("Giriş")]
    public KeyCode key = KeyCode.Q;

    [Header("Şarj (bekleme kısaltma, sn)")]
    public float parryCharge = 2.5f;
    public float killCharge = 0.75f;

    [Tooltip("Boss'a karşı hasar / yanık çarpanı.")]
    [Range(0f, 1f)] public float bossMultiplier = 0.5f;

    [Header("Şok Dalgası")]
    public float shockRadius = 4.5f;
    [Range(0f, 1f)] public float shockBalancePercent = 0.3f;
    [Range(0f, 1f)] public float shockHealthPercent = 0.06f;
    public float shockPush = 9f;
    public float shockInvulnerable = 0.25f;

    [Header("Gölge Adım")]
    public float shadowRange = 9f;
    public float shadowBehind = 1.4f;
    [Range(0f, 1f)] public float shadowBalancePercent = 0.4f;
    [Range(0f, 1f)] public float shadowHealthPercent = 0.1f;
    public float shadowInvulnerable = 0.4f;

    [Header("Buz Nefesi")]
    public float frostRange = 6f;
    public float frostHeight = 3f;
    [Range(0.05f, 1f)] public float frostSlowScale = 0.35f;
    public float frostSlowDuration = 3.5f;
    [Range(0f, 1f)] public float frostBalancePercent = 0.2f;
    [Range(0f, 1f)] public float frostHealthPercent = 0.04f;
    [Tooltip("Öndeki düşmanlar bu süre yeni saldırı başlatamaz.")]
    public float frostFreeze = 1.5f;

    [Header("Alev Dalgası")]
    public float fireRange = 6.5f;
    public float fireHeight = 2.6f;
    [Range(0f, 1f)] public float fireHitBalancePercent = 0.1f;
    [Range(0f, 1f)] public float fireHitHealthPercent = 0.03f;
    [Range(0f, 1f)] public float burnBalancePerSecond = 0.07f;
    [Range(0f, 1f)] public float burnHealthPerSecond = 0.04f;
    public float burnDuration = 4f;
    public float staggeredBurnMultiplier = 2f;

    // ---------------------------------------------------------
    // DURUM
    // ---------------------------------------------------------

    public bool HasAbility { get; private set; }
    public AbilityType Type { get; private set; }
    public int Level { get; private set; }

    public float CooldownLeft { get; private set; }
    public float CooldownTotal { get; private set; } = 1f;
    public bool Ready => HasAbility && CooldownLeft <= 0f;

    // 0..1 (HUD dolumu)
    public float Charge01 =>
        !HasAbility ? 0f : 1f - Mathf.Clamp01(CooldownLeft / Mathf.Max(0.01f, CooldownTotal));

    public float LastReadyTime { get; private set; } = -99f;
    public float LastUsedTime { get; private set; } = -99f;

    private float Power => 1f + 0.25f * (Mathf.Max(1, Level) - 1);

    private PlayerController player;
    private Health health;
    private PlayerCombatController combat;
    private float nextWarn;

    private static Sprite ringSprite;
    private static Sprite squareSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        Used = null;
    }

    public static PlayerAbility Ensure(GameObject playerObject)
    {
        PlayerAbility a = playerObject.GetComponent<PlayerAbility>();

        if (a == null)
            a = playerObject.AddComponent<PlayerAbility>();

        return a;
    }

    private void Awake()
    {
        Instance = this;
        player = GetComponent<PlayerController>();
        health = GetComponent<Health>();
        combat = GetComponent<PlayerCombatController>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        CombatEvents.ParrySucceeded += OnParry;
        CombatEvents.EnemyKilled += OnKilled;
    }

    private void OnDisable()
    {
        CombatEvents.ParrySucceeded -= OnParry;
        CombatEvents.EnemyKilled -= OnKilled;
    }

    // =========================================================
    // API (RunManager / dükkan)
    // =========================================================

    public void Equip(AbilityType type, int level = 1)
    {
        HasAbility = true;
        Type = type;
        Level = Mathf.Clamp(level, 1, AbilityInfo.MaxLevel);

        CooldownTotal = ComputeCooldown();
        CooldownLeft = 0f;
        LastReadyTime = Time.unscaledTime;
    }

    public bool CanLevelUp => HasAbility && Level < AbilityInfo.MaxLevel;

    public void LevelUp()
    {
        if (!CanLevelUp)
            return;

        Level++;
        CooldownTotal = ComputeCooldown();
        CooldownLeft = Mathf.Min(CooldownLeft, CooldownTotal);
    }

    public void Clear()
    {
        HasAbility = false;
        Level = 0;
        CooldownLeft = 0f;
    }

    public void Refill()
    {
        if (CooldownLeft > 0f)
            LastReadyTime = Time.unscaledTime;

        CooldownLeft = 0f;
    }

    private float ComputeCooldown()
    {
        float cd =
            AbilityInfo.BaseCooldown(Type) *
            (1f - 0.1f * (Mathf.Max(1, Level) - 1)) *
            MetaProgress.AbilityCooldownMultiplier;

        return Mathf.Max(1f, cd);
    }

    // =========================================================
    // ŞARJ
    // =========================================================

    private void OnParry(EnemyController enemy, bool brokeBalance)
    {
        Charge(parryCharge);
    }

    private void OnKilled(EnemyController enemy)
    {
        Charge(killCharge);
    }

    private void Charge(float seconds)
    {
        if (!HasAbility || CooldownLeft <= 0f || seconds <= 0f)
            return;

        CooldownLeft -= seconds;

        if (CooldownLeft <= 0f)
        {
            CooldownLeft = 0f;
            OnBecameReady();
        }
    }

    private void OnBecameReady()
    {
        LastReadyTime = Time.unscaledTime;

        if (player != null)
        {
            CombatCallout.Popup(
                player.transform.position + Vector3.up * 2.4f,
                AbilityInfo.Name(Type).ToUpperInvariant() + " HAZIR",
                AbilityInfo.Color(Type),
                0.8f
            );
        }
    }

    // =========================================================
    // GÜNCELLEME
    // =========================================================

    private void Update()
    {
        if (!HasAbility || player == null)
            return;

        if (CooldownLeft > 0f)
        {
            // Ölçekli zaman: menüde / duraklamada akmaz.
            CooldownLeft -= Time.deltaTime;

            if (CooldownLeft <= 0f)
            {
                CooldownLeft = 0f;
                OnBecameReady();
            }
        }

        if (!Input.GetKeyDown(key))
            return;

        if (!CanAct())
            return;

        if (!Ready)
        {
            if (Time.unscaledTime >= nextWarn)
            {
                nextWarn = Time.unscaledTime + 0.8f;

                CombatCallout.Popup(
                    player.transform.position + Vector3.up * 2.4f,
                    "YETENEK " + Mathf.CeilToInt(CooldownLeft) + " SN",
                    new Color(0.75f, 0.75f, 0.8f),
                    0.7f
                );
            }

            return;
        }

        if (TryUse())
        {
            LastUsedTime = Time.unscaledTime;
            CooldownTotal = ComputeCooldown();
            CooldownLeft = CooldownTotal;

            Used?.Invoke(Type);
        }
    }

    private bool CanAct()
    {
        if (!player.canControl || player.inputLocked)
            return false;

        if (Time.timeScale < 0.01f)
            return false;

        if (health != null && health.IsDead)
            return false;

        IPlayerState s = player.stateMachine != null ? player.stateMachine.CurrentState : null;

        if (
            s is PlayerExecuteState ||
            s is PlayerHurtState ||
            s is PlayerDeathState ||
            s is PlayerPostureBreakState
        )
        {
            return false;
        }

        return true;
    }

    private bool TryUse()
    {
        // Saldırı ortasında basılırsa saldırı iptal (yetenek öncelikli).
        if (combat != null && combat.IsAttacking)
            combat.CancelAttack();

        switch (Type)
        {
            case AbilityType.Shockwave: return UseShockwave();
            case AbilityType.ShadowStep: return UseShadowStep();
            case AbilityType.Frost: return UseFrost();
            case AbilityType.Fire: return UseFire();
        }

        return false;
    }

    // =========================================================
    // ŞOK DALGASI
    // =========================================================

    private bool UseShockwave()
    {
        Vector2 center = player.transform.position + Vector3.up * 0.8f;

        float radius = shockRadius * (1f + 0.1f * (Level - 1));

        List<EnemyController> list = CharmUtil.EnemiesInRadius(center, radius);

        for (int i = 0; i < list.Count; i++)
        {
            EnemyController e = list[i];

            float dir = Mathf.Sign(e.transform.position.x - player.transform.position.x);

            if (dir == 0f)
                dir = 1f;

            HitPercent(e, shockBalancePercent, shockHealthPercent, new Vector2(dir, 0f));

            if (e == null || e.IsDead)
                continue;

            // Yetenek zırhı deler: zırhlı (kararlı) saldırıyı da keser.
            BreakArmor(e);

            if (e.IsAttackCommitted)
                continue;

            Rigidbody2D rb = e.GetComponent<Rigidbody2D>();

            float pushScale = IsBoss(e) ? 0.3f : 1f;

            if (rb != null)
                rb.linearVelocity = new Vector2(dir * shockPush * pushScale * EnemyTime.Scale, 3f * pushScale);
        }

        player.hitInvincibilityTimer = Mathf.Max(player.hitInvincibilityTimer, shockInvulnerable);

        SpawnRing(center, radius, AbilityInfo.Color(AbilityType.Shockwave));
        Shake(0.5f);
        HitStop.Request(0.05f, 0.1f);

        return true;
    }

    // =========================================================
    // GÖLGE ADIM
    // =========================================================

    private bool UseShadowStep()
    {
        EnemyController target = FindStepTarget();

        if (target == null)
        {
            CombatCallout.Popup(
                player.transform.position + Vector3.up * 2.4f,
                "HEDEF YOK",
                new Color(0.75f, 0.75f, 0.8f),
                0.7f
            );

            return false;
        }

        float side = Mathf.Sign(target.transform.position.x - player.transform.position.x);

        if (side == 0f)
            side = player.facingDir;

        // Önce arkası, olmazsa daha yakın arkası.
        if (
            !TryFindLanding(target, side, shadowBehind, out Vector3 landing) &&
            !TryFindLanding(target, side, shadowBehind * 0.6f, out landing)
        )
        {
            CombatCallout.Popup(
                player.transform.position + Vector3.up * 2.4f,
                "YER YOK",
                new Color(0.75f, 0.75f, 0.8f),
                0.7f
            );

            return false;
        }

        Vector3 from = player.transform.position;

        SpawnAfterImage(from);

        if (player.rb != null)
        {
            player.rb.position = landing;
            player.rb.linearVelocity = Vector2.zero;
        }

        player.transform.position = landing;

        // Düşmana dön.
        float face = Mathf.Sign(target.transform.position.x - landing.x);

        if (face != 0f)
        {
            player.facingDir = face;

            if (player.playerSprite != null)
                player.playerSprite.flipX = face < 0f;
        }

        player.hitInvincibilityTimer = Mathf.Max(player.hitInvincibilityTimer, shadowInvulnerable);

        HitPercent(target, shadowBalancePercent, shadowHealthPercent, new Vector2(face, 0f));

        CombatCallout.Popup(
            target.transform.position + Vector3.up * 2.2f,
            "ARKADA!",
            AbilityInfo.Color(AbilityType.ShadowStep),
            0.9f
        );

        Shake(0.35f);
        HitStop.Request(0.06f, 0.05f);

        return true;
    }

    private EnemyController FindStepTarget()
    {
        Vector2 p = player.transform.position;

        float range = shadowRange * (1f + 0.1f * (Level - 1));

        List<EnemyController> list = CharmUtil.EnemiesInRadius(p, range);

        EnemyController best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < list.Count; i++)
        {
            EnemyController e = list[i];

            Vector2 d = (Vector2)e.transform.position - p;

            // Çok farklı yükseklikteki (platform üstü) düşmanı tercih etme.
            if (Mathf.Abs(d.y) > 4f)
                continue;

            float score = d.magnitude;

            // Baktığı yöndekiler öncelikli.
            if (Mathf.Sign(d.x) == Mathf.Sign(player.facingDir))
                score *= 0.7f;

            if (score < bestScore)
            {
                bestScore = score;
                best = e;
            }
        }

        return best;
    }

    // Hedefin 'side' yönünde 'behind' kadar ötesinde zemin + boş yer ara.
    private bool TryFindLanding(EnemyController target, float side, float behind, out Vector3 landing)
    {
        landing = Vector3.zero;

        if (player.col == null || player.Movement == null)
            return false;

        Collider2D enemyCol = target.GetComponent<Collider2D>();

        float halfEnemy = enemyCol != null ? enemyCol.bounds.extents.x : 0.4f;

        float x = target.transform.position.x + side * (halfEnemy + behind);

        LayerMask ground = player.Movement.groundMask;
        LayerMask solid = player.Movement.groundMask | player.Movement.wallMask;

        float topY = target.transform.position.y + 3f;

        RaycastHit2D hit = Physics2D.Raycast(new Vector2(x, topY), Vector2.down, 8f, ground);

        if (hit.collider == null)
            return false;   // çukur

        Bounds b = player.col.bounds;

        // Pivot ile collider altı arası.
        float pivotToBottom = player.transform.position.y - b.min.y;
        float pivotToCenter = b.center.y - player.transform.position.y;

        float y = hit.point.y + pivotToBottom + 0.05f;

        Vector2 center = new Vector2(x, y + pivotToCenter);
        Vector2 size = new Vector2(b.size.x * 0.9f, b.size.y * 0.9f);

        if (Physics2D.OverlapBox(center, size, 0f, solid) != null)
            return false;

        landing = new Vector3(x, y, player.transform.position.z);

        return true;
    }

    // =========================================================
    // BUZ NEFESİ
    // =========================================================

    private bool UseFrost()
    {
        float power = Power;

        EnemyTime.RequestRamp(frostSlowDuration * (1f + 0.15f * (Level - 1)), frostSlowScale, 2f);

        List<EnemyController> list = EnemiesInFront(frostRange, frostHeight);

        Color c = AbilityInfo.Color(AbilityType.Frost);

        for (int i = 0; i < list.Count; i++)
        {
            EnemyController e = list[i];

            HitPercent(e, frostBalancePercent, frostHealthPercent, new Vector2(player.facingDir, 0f));

            if (e == null || e.IsDead)
                continue;

            BreakArmor(e);

            e.StartAttackRecovery(frostFreeze * power);
            e.PlayTintFlash(c, 0.5f);
        }

        SpawnBox(frostRange, frostHeight, c, 0.35f);

        CombatCallout.Popup(
            player.transform.position + Vector3.up * 2.4f,
            "ZAMAN DONDU",
            c,
            0.8f
        );

        Shake(0.25f);

        return true;
    }

    // =========================================================
    // ALEV DALGASI
    // =========================================================

    private bool UseFire()
    {
        float power = Power;

        List<EnemyController> list = EnemiesInFront(fireRange, fireHeight);

        for (int i = 0; i < list.Count; i++)
        {
            EnemyController e = list[i];

            HitPercent(e, fireHitBalancePercent, fireHitHealthPercent, new Vector2(player.facingDir, 0f));

            if (e == null || e.IsDead)
                continue;

            float boss = IsBoss(e) ? bossMultiplier : 1f;
            float staggered = e.IsStaggered ? staggeredBurnMultiplier : 1f;

            EnemyStatus.Get(e).ApplyBurn(
                burnBalancePerSecond * power * boss,
                burnHealthPerSecond * power * boss * staggered,
                burnDuration
            );
        }

        SpawnBox(fireRange, fireHeight, AbilityInfo.Color(AbilityType.Fire), 0.3f);
        Shake(0.3f);

        return true;
    }

    // =========================================================
    // YARDIMCILAR
    // =========================================================

    private HitResult HitPercent(EnemyController e, float balancePercent, float healthPercent, Vector2 dir)
    {
        if (e == null || e.IsDead)
            return default;

        float scale = Power * (IsBoss(e) ? bossMultiplier : 1f);

        EnemyBalance balance = e.GetComponent<EnemyBalance>();
        Health h = e.GetComponent<Health>();

        int bal =
            balance != null
                ? Mathf.Max(1, Mathf.RoundToInt(balance.MaxBalance * balancePercent * scale))
                : 0;

        int hp =
            h != null
                ? Mathf.Max(1, Mathf.RoundToInt(h.MaxHealth * healthPercent * scale))
                : 0;

        DamageInfo info = new DamageInfo
        {
            source = DamageSource.Ability,
            comboStep = 0,
            balanceDamage = bal,
            healthDamage = hp,
            direction = dir,
            hitPosition = e.transform.position + Vector3.up
        };

        return PlayerDamage.HitEnemy(e, info);
    }

    // Kararlı saldırıyı keser (boss hariç). Normal vuruşlar artık
    // uyarı boyunca kesemiyor; Şok ve Buz bunun cevabı.
    private static void BreakArmor(EnemyController e)
    {
        if (e == null || e.IsDead || IsBoss(e) || !e.IsAttackCommitted)
            return;

        e.ChangeState(new EnemyHitState(e, 0.2f));

        CombatCallout.PopupAbove(e, "KESİLDİ", new Color(0.8f, 0.9f, 1f), 0.7f);
    }

    private static bool IsBoss(EnemyController e)
    {
        return e != null && e.GetComponent<BossController>() != null;
    }

    // Oyuncunun baktığı yönde, kutu içindeki düşmanlar.
    private List<EnemyController> EnemiesInFront(float range, float height)
    {
        List<EnemyController> result = new List<EnemyController>();

        float dir = player.facingDir >= 0f ? 1f : -1f;

        Vector2 p = player.transform.position;

        EnemyController[] all = EnemyController.All.ToArray();

        for (int i = 0; i < all.Length; i++)
        {
            EnemyController e = all[i];

            if (e == null || e.IsDead)
                continue;

            Vector2 d = (Vector2)e.transform.position - p;

            float along = d.x * dir;

            if (along < -0.6f || along > range)
                continue;

            if (Mathf.Abs(d.y) > height)
                continue;

            result.Add(e);
        }

        return result;
    }

    private void Shake(float force)
    {
        if (player.impulseSource != null)
            player.impulseSource.GenerateImpulse(force);
    }

    private int SortingLayer => player.playerSprite != null ? player.playerSprite.sortingLayerID : 0;
    private int SortingOrder => player.playerSprite != null ? player.playerSprite.sortingOrder + 5 : 50;

    /// <summary>Genişleyen halka efekti (Ground Slam da kullanır).</summary>
    public void SpawnRingFx(Vector2 center, float radius, Color color)
    {
        SpawnRing(center, radius, color);
    }

    // Genişleyen halka (Şok Dalgası).
    private void SpawnRing(Vector2 center, float radius, Color color)
    {
        GameObject obj = new GameObject("AbilityRing");
        obj.transform.position = center;

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = RingSprite();
        sr.color = color;
        sr.sortingLayerID = SortingLayer;
        sr.sortingOrder = SortingOrder;

        StartCoroutine(AnimateFx(obj.transform, sr, 0.3f, Vector3.one * 0.3f, Vector3.one * radius * 2f));
    }

    // Önde parlayan dikdörtgen (Buz / Alev).
    private void SpawnBox(float range, float height, Color color, float life)
    {
        float dir = player.facingDir >= 0f ? 1f : -1f;

        GameObject obj = new GameObject("AbilityWave");

        obj.transform.position =
            player.transform.position + new Vector3(dir * range * 0.5f, height * 0.35f, 0f);

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = SquareSprite();
        sr.color = new Color(color.r, color.g, color.b, 0.55f);
        sr.sortingLayerID = SortingLayer;
        sr.sortingOrder = SortingOrder;

        Vector3 start = new Vector3(range * 0.3f, height * 0.6f, 1f);
        Vector3 end = new Vector3(range, height * 1.2f, 1f);

        StartCoroutine(AnimateFx(obj.transform, sr, life, start, end));
    }

    // Işınlanmadan önceki yerde mor gölge.
    private void SpawnAfterImage(Vector3 at)
    {
        if (player.playerSprite == null || player.playerSprite.sprite == null)
            return;

        GameObject obj = new GameObject("ShadowStepImage");

        Transform src = player.playerSprite.transform;

        obj.transform.position = src.position - player.transform.position + at;
        obj.transform.localScale = src.lossyScale;

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = player.playerSprite.sprite;
        sr.flipX = player.playerSprite.flipX;
        sr.color = new Color(0.6f, 0.35f, 1f, 0.7f);
        sr.sortingLayerID = player.playerSprite.sortingLayerID;
        sr.sortingOrder = player.playerSprite.sortingOrder - 1;

        StartCoroutine(AnimateFx(obj.transform, sr, 0.35f, obj.transform.localScale, obj.transform.localScale));
    }

    private static IEnumerator AnimateFx(Transform t, SpriteRenderer sr, float life, Vector3 fromScale, Vector3 toScale)
    {
        float start = Time.unscaledTime;
        Color c = sr.color;

        while (t != null)
        {
            float k = Mathf.Clamp01((Time.unscaledTime - start) / life);

            t.localScale = Vector3.Lerp(fromScale, toScale, 1f - (1f - k) * (1f - k));
            sr.color = new Color(c.r, c.g, c.b, c.a * (1f - k));

            if (k >= 1f)
                break;

            yield return null;
        }

        if (t != null)
            Destroy(t.gameObject);
    }

    // 1 birimlik halka sprite'ı (kodla).
    private static Sprite RingSprite()
    {
        if (ringSprite != null)
            return ringSprite;

        const int size = 64;

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        float r = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;

                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.88f) / 0.12f);

                // İçi hafif dolu.
                if (d < 0.88f)
                    a = Mathf.Max(a, 0.12f);

                if (d > 1f)
                    a = 0f;

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply();

        ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);

        return ringSprite;
    }

    // 1x1 birim beyaz kare, kenarlara doğru söner.
    private static Sprite SquareSprite()
    {
        if (squareSprite != null)
            return squareSprite;

        const int size = 32;

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float ex = Mathf.Min(x, size - 1 - x) / (size * 0.25f);
                float ey = Mathf.Min(y, size - 1 - y) / (size * 0.25f);

                float a = Mathf.Clamp01(Mathf.Min(ex, ey));

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply();

        squareSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);

        return squareSprite;
    }
}
