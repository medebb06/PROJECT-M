using UnityEngine;

/// <summary>
/// KALKAN (Kalkanlı tip ve "Kalkanlı" elit eki). ÖNDEN gelen normal
/// vuruşları (kombo, havada yan vuruş) engeller.
///
/// Cevaplar: ARKASINA geç (dash / Gölge Adım), YUKARIDAN vur (aşağı vuruş /
/// pogo), Ground Slam, yetenekler, PARRY (denge kırılınca kalkan iner).
/// Önden üst üste 'hitsToBreak' vuruş kalkanı 'guardDownTime' sn düşürür.
///
/// PlayerDamage.HitEnemy her vuruştan önce TryBlock'u sorar.
/// Görsel: düşmanın önünde açık mavi dikey kalkan çubuğu (sprite gerekmez).
/// </summary>
[RequireComponent(typeof(EnemyController))]
public class EnemyShield : MonoBehaviour
{
    public int hitsToBreak = 5;
    public float guardDownTime = 2.5f;
    [Tooltip("Engellenen vuruşta oyuncunun geri itilme hızı.")]
    public float playerPushback = 6f;

    public Color shieldColor = new Color(0.7f, 0.85f, 1f, 0.9f);

    private EnemyController enemy;
    private int frontHits;
    private float downUntil = -99f;
    private float lastBlockTime = -99f;
    private SpriteRenderer visual;

    private static Sprite white;

    public bool IsUp =>
        enemy != null &&
        !enemy.IsDead &&
        !enemy.IsStaggered &&
        EnemyTime.Now >= downUntil;

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
    }

    private void Start()
    {
        CreateVisual();
    }

    /// <summary>Bu vuruş kalkana mı geldi? Geldiyse engeller (true).</summary>
    public static bool TryBlock(EnemyController target, DamageInfo info)
    {
        if (target == null)
            return false;

        if (info.source != DamageSource.Attack || info.fromAbove)
            return false;

        EnemyShield shield = target.GetComponent<EnemyShield>();

        if (shield == null || !shield.IsUp)
            return false;

        // Vuruş yönü düşmanın yüzüne doğru mu? (önden)
        float attackDir = Mathf.Sign(info.direction.x);

        if (attackDir == 0f || attackDir != -target.FacingDirection)
        {
            shield.frontHits = 0;
            return false;
        }

        shield.Block(attackDir);

        return true;
    }

    private void Block(float attackDir)
    {
        frontHits++;
        lastBlockTime = EnemyTime.Now;

        enemy.PlayTintFlash(new Color(0.75f, 0.85f, 1f), 0.08f);

        PlayerController player = FindFirstObjectByType<PlayerController>();

        if (player != null && player.rb != null)
        {
            Vector2 v = player.rb.linearVelocity;
            v.x = -attackDir * playerPushback;
            player.rb.linearVelocity = v;
        }

        if (frontHits >= hitsToBreak)
        {
            frontHits = 0;
            downUntil = EnemyTime.Now + guardDownTime;

            CombatCallout.PopupAbove(enemy, "KALKAN DÜŞTÜ", new Color(1f, 0.85f, 0.4f), 0.85f);
        }
        else
        {
            CombatCallout.PopupAbove(enemy, "KALKAN", new Color(0.75f, 0.85f, 1f), 0.65f);
        }
    }

    private void CreateVisual()
    {
        if (white == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            white = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), tex.width);
        }

        GameObject obj = new GameObject("Kalkan");
        obj.transform.SetParent(transform, false);

        visual = obj.AddComponent<SpriteRenderer>();
        visual.sprite = white;
        visual.color = shieldColor;

        SpriteRenderer body = GetComponentInChildren<SpriteRenderer>();

        if (body != null && body != visual)
        {
            visual.sortingLayerID = body.sortingLayerID;
            visual.sortingOrder = body.sortingOrder + 2;
        }
    }

    private void LateUpdate()
    {
        if (visual == null)
            return;

        bool up = IsUp;

        visual.enabled = up && !enemy.IsDead;

        if (!visual.enabled)
            return;

        Collider2D col = GetComponent<Collider2D>();

        Bounds b = col != null ? col.bounds : new Bounds(transform.position, Vector3.one);

        float face = enemy.FacingDirection;

        // Dünya boyutunda: gövdenin önünde ince dikey çubuk.
        Vector3 pos = new Vector3(b.center.x + face * (b.extents.x + 0.12f), b.min.y + b.size.y * 0.1f, transform.position.z);

        visual.transform.position = pos;

        Vector3 parentScale = transform.lossyScale;

        float w = 0.18f;
        float h = b.size.y * 0.75f;

        visual.transform.localScale =
            new Vector3(
                w / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
                h / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)),
                1f
            );

        // Engelleyince kısa parlama.
        float flash = Mathf.Clamp01(1f - (EnemyTime.Now - lastBlockTime) / 0.15f);

        visual.color = Color.Lerp(shieldColor, Color.white, flash);
    }
}
