using UnityEngine;

/// <summary>
/// DÜŞMAN OKU (mermi). EnemyArcher.Fire üretir. Fizik/katman ayarı
/// gerektirmez: her karede oyuncunun gövdesiyle (collider sınırları) ve
/// zeminle (oyuncunun Ground Mask'i) kendisi çakışma bakar.
///
///   Oyuncu dash'te (dokunulmaz) → ok içinden geçer (kaçış sayılır)
///   Parry penceresi açık        → ok OKÇUYA geri yansır, okçunun
///                                 dengesine parry hasarı (normal parry gibi;
///                                 charm'lar, riposte, istatistik çalışır)
///   Block                       → posture yer, ok kırılır
///   Aksi halde                  → normal vuruş hasarı (tek vuruş tavanı dahil)
///
/// Düşman zamanıyla uçar (parry slow-mo'sunda yavaşlar); yansıyan ok
/// gerçek zamanla uçar.
/// </summary>
public class EnemyProjectile : MonoBehaviour
{
    private static Sprite defaultSprite;
    private static Material unlitMaterial;

    private EnemyArcher settings;
    private EnemyController owner;
    private PlayerController player;
    private SpriteRenderer sr;

    private Vector2 velocity;
    private int damage;
    private float life;
    private int groundMask;

    private bool reflected;
    private bool dodged;
    private bool done;

    // =========================================================
    // ÜRETİM
    // =========================================================

    public static EnemyProjectile Spawn(
        EnemyArcher settings,
        EnemyController owner,
        Vector2 origin,
        Vector2 direction,
        int damage
    )
    {
        GameObject obj = new GameObject("EnemyArrow");

        obj.transform.position = new Vector3(origin.x, origin.y, 0f);

        EnemyProjectile projectile = obj.AddComponent<EnemyProjectile>();

        projectile.Init(settings, owner, direction, damage);

        return projectile;
    }

    private void Init(
        EnemyArcher settings,
        EnemyController owner,
        Vector2 direction,
        int damage
    )
    {
        this.settings = settings;
        this.owner = owner;
        this.damage = damage;

        player =
            owner != null && owner.target != null
                ? owner.target.GetComponent<PlayerController>()
                : null;

        groundMask =
            player != null && player.Movement != null
                ? (int)player.Movement.groundMask
                : 0;

        life = settings.arrowLifetime;
        velocity = direction * settings.arrowSpeed;

        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = settings.arrowSprite != null ? settings.arrowSprite : GetDefaultSprite();
        sr.color = settings.arrowColor;

        if (settings.arrowSprite == null)
        {
            Material m = GetUnlitMaterial();

            if (m != null)
                sr.sharedMaterial = m;
        }

        SpriteRenderer ownerSprite =
            owner != null ? owner.GetComponentInChildren<SpriteRenderer>() : null;

        if (ownerSprite != null)
        {
            sr.sortingLayerID = ownerSprite.sortingLayerID;
            sr.sortingOrder = ownerSprite.sortingOrder + 5;
        }

        transform.localScale = Vector3.one * Mathf.Max(0.1f, settings.arrowScale);

        UpdateRotation();
    }

    // =========================================================
    // UÇUŞ
    // =========================================================

    private void Update()
    {
        if (done)
            return;

        float dt = reflected ? Time.deltaTime : EnemyTime.DeltaTime;

        if (dt <= 0f)
            return;

        Vector2 position = transform.position;
        Vector2 next = position + velocity * dt;

        // Zemine / duvara çarptı.
        if (groundMask != 0)
        {
            RaycastHit2D hit = Physics2D.Linecast(position, next, groundMask);

            if (hit.collider != null)
            {
                Miss();
                return;
            }
        }

        transform.position = new Vector3(next.x, next.y, transform.position.z);

        life -= dt;

        if (life <= 0f)
        {
            Miss();
            return;
        }

        if (reflected)
            CheckOwner(next);
        else
            CheckPlayer(next);
    }

    private void UpdateRotation()
    {
        float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;

        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private bool Touches(Collider2D col, Vector2 point)
    {
        if (col == null)
            return false;

        Bounds b = col.bounds;

        b.Expand(settings.hitRadius * 2f);

        return b.Contains(new Vector3(point.x, point.y, b.center.z));
    }

    // =========================================================
    // OYUNCUYA DEĞDİ
    // =========================================================

    private void CheckPlayer(Vector2 point)
    {
        // Dash ile içinden geçildiyse oyuncuyu bir daha vurmaz.
        if (dodged || player == null || player.col == null)
            return;

        if (!Touches(player.col, point))
            return;

        Health playerHealth = player.GetComponent<Health>();

        if (playerHealth != null && playerHealth.IsDead)
        {
            Finish();
            return;
        }

        // Dash / hasar sonrası korumalı dönem: içinden geç.
        if (player.isInvincible)
        {
            dodged = true;

            if (owner != null)
                CombatEvents.RaiseDodge(owner, false);

            return;
        }

        Vector2 dir = velocity.normalized;

        PlayerDefenseController defense = player.GetComponent<PlayerDefenseController>();

        if (defense != null && defense.CanParry())
        {
            Parry(defense, dir);
            return;
        }

        if (defense != null && defense.CanBlock())
        {
            Block(defense, dir);
            return;
        }

        Hit(dir);
    }

    private void Parry(PlayerDefenseController defense, Vector2 dir)
    {
        defense.PlayParryFeedback();

        bool broke = false;

        if (owner != null && !owner.IsDead)
        {
            EnemyBalance balance = owner.GetComponent<EnemyBalance>();

            if (balance != null && !balance.IsBroken)
                balance.AddParryBalanceDamage(owner.parryBalanceDamage);

            broke = balance != null && balance.IsBroken;

            owner.PlayBalanceDamageFlash();

            // Normal oklarda slow-mo yok (akış bozulmasın); denge kırılınca var.
            if (broke)
                owner.PlayParrySlowMotion(true);

            CombatEvents.RaiseParry(owner, broke);
        }

        // OKÇUYA GERİ YANSIT
        reflected = true;
        dodged = true;

        Vector2 position = transform.position;
        Vector2 target = position - dir * 6f;

        if (owner != null && !owner.IsDead)
        {
            Collider2D ownerCol = owner.GetComponent<Collider2D>();

            target =
                ownerCol != null
                    ? (Vector2)ownerCol.bounds.center
                    : (Vector2)owner.transform.position;
        }

        velocity =
            (target - position).normalized *
            settings.arrowSpeed *
            settings.reflectSpeedMultiplier;

        life = settings.arrowLifetime;

        sr.color = settings.reflectedColor;

        UpdateRotation();
    }

    private void Block(PlayerDefenseController defense, Vector2 dir)
    {
        int posture =
            owner != null
                ? Mathf.Max(1, owner.blockPostureDamage)
                : 20;

        defense.HandleBlockHit(dir, posture);

        CombatImpactFeedback feedback = player.GetComponent<CombatImpactFeedback>();

        if (feedback != null)
            feedback.PlayBlockImpact();

        PlayerPosture playerPosture = player.GetComponent<PlayerPosture>();

        if (owner != null)
        {
            CombatEvents.RaisePlayerBlocked(
                owner,
                playerPosture != null && playerPosture.IsBroken
            );
        }

        Finish();
    }

    private void Hit(Vector2 dir)
    {
        PlayerDamageReceiver receiver = player.GetComponent<PlayerDamageReceiver>();

        if (receiver != null)
        {
            float kb = settings.knockbackMultiplier;

            receiver.TakeDamage(
                damage,
                dir,
                owner != null ? owner.attackKnockbackForce * kb : 4f,
                owner != null ? owner.attackKnockbackVerticalForce * kb : 2f,
                owner != null ? owner.attackKnockbackDuration : 0.1f,
                owner != null ? owner.attackKnockbackDeceleration : -1f,
                owner,
                PlayerHitKind.Normal
            );

            EnemyAttackCoordinator.NotifyPlayerHit();
        }

        Finish();
    }

    // =========================================================
    // YANSIYAN OK OKÇUYA DEĞDİ
    // =========================================================

    private void CheckOwner(Vector2 point)
    {
        if (owner == null || owner.IsDead)
            return;

        if (!Touches(owner.GetComponent<Collider2D>(), point))
            return;

        EnemyBalance balance = owner.GetComponent<EnemyBalance>();

        if (balance != null && !balance.IsBroken)
        {
            int bonus =
                Mathf.Max(
                    1,
                    Mathf.RoundToInt(owner.parryBalanceDamage * settings.reflectBalanceBonus)
                );

            balance.AddBalanceDamage(bonus);
        }

        owner.PlayBalanceDamageFlash();

        HitStop.Request(0.06f, 0.05f);

        CombatCallout.PopupAbove(owner, settings.reflectText, settings.reflectedColor, 1.1f);

        // Boss: yansıtılan ok doğrudan CAN hasarı verir.
        BossController reflectBoss = owner.GetComponent<BossController>();

        if (reflectBoss != null)
            reflectBoss.OnReflectedHit();

        Finish();
    }

    // =========================================================
    // BİTİŞ
    // =========================================================

    private void Miss()
    {
        // Kimseye değmeden düştü: istatistikte "ıskaladı".
        if (!reflected && !dodged && owner != null)
            CombatEvents.RaiseAttackMissed(owner, false);

        Finish();
    }

    private void Finish()
    {
        done = true;

        Destroy(gameObject);
    }

    // =========================================================
    // VARSAYILAN GÖRSEL: 12x3 pixel ok (sağa bakar)
    // =========================================================

    private static Sprite GetDefaultSprite()
    {
        if (defaultSprite != null)
            return defaultSprite;

        string[] rows =
        {
            "ff........#.",
            ".##########h",
            "ff........#."
        };

        int w = rows[0].Length;
        int h = rows.Length;

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color shaft = new Color(0.85f, 0.75f, 0.6f);
        Color head = Color.white;
        Color feather = new Color(0.9f, 0.35f, 0.3f);

        for (int y = 0; y < h; y++)
        {
            // Dizideki ilk satır üstte.
            string row = rows[h - 1 - y];

            for (int x = 0; x < w; x++)
            {
                char c = row[x];

                Color color =
                    c == '#' ? shaft :
                    c == 'h' ? head :
                    c == 'f' ? feather :
                    clear;

                tex.SetPixel(x, y, color);
            }
        }

        tex.Apply();

        // 16 piksel = 1 birim: ok ~0.75 birim uzunluğunda.
        defaultSprite =
            Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);

        return defaultSprite;
    }

    private static Material GetUnlitMaterial()
    {
        if (unlitMaterial != null)
            return unlitMaterial;

        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
            return null;

        unlitMaterial = new Material(shader);

        return unlitMaterial;
    }
}
