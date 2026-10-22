using UnityEngine;

/// <summary>
/// Boss'un KORUYUCU AURASI. Aktifken:
///  - boss oyuncudan hasar ALMAZ (vuruşlar aurada sönümlenir),
///  - infaz hedeflenemez,
///  - aura içine giren oyuncu hasar yer (dash ile geçilebilir, parry/blok işe yaramaz).
/// BossSlam geri çekilme + odaklanma sırasında açar (çizgi saldırısı hazırlığı).
/// </summary>
public class BossAura : MonoBehaviour
{
    [Header("Aura")]
    public float radius = 3.4f;

    [Tooltip("İçeri girene verilen hasar (boss'un engellenemez hasarı × bu).")]
    public float damageMultiplier = 0.6f;

    [Tooltip("Aura içindeyken iki hasar arası süre (sn).")]
    public float tickInterval = 0.8f;

    public Color color = new Color(0.75f, 0.55f, 1f, 1f);

    public bool Active { get; private set; }

    private EnemyController enemy;
    private Collider2D bodyCol;
    private Transform visual;
    private SpriteRenderer sr;
    private PlayerController player;

    private float nextTick;
    private float nextBlockPopup;
    private float shown;

    private static Sprite auraSprite;

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        bodyCol = GetComponent<Collider2D>();
    }

    public void SetActive(bool on)
    {
        if (Active == on)
            return;

        Active = on;

        if (on)
        {
            nextTick = Time.time + 0.15f;
            EnsureVisual();
        }
    }

    private Vector3 Center =>
        bodyCol != null ? bodyCol.bounds.center : transform.position;

    // Oyuncu vuruşu sönümlendi (PlayerDamage çağırır).
    public void OnBlockedHit()
    {
        if (Time.time < nextBlockPopup)
            return;

        nextBlockPopup = Time.time + 0.35f;

        if (enemy != null)
            CombatCallout.PopupAbove(enemy, "AURA!", color, 0.7f);

        HitStop.Request(0.04f, 0.3f);
    }

    private void EnsureVisual()
    {
        if (visual != null)
            return;

        if (auraSprite == null)
        {
            const int N = 128;

            Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f;
                    float dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    float fill = d < 1f ? 0.18f * (1f - d * 0.6f) : 0f;
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.96f) / 0.05f);
                    float a = Mathf.Clamp01(fill + ring * 0.85f);

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply();

            auraSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N / 2f);
        }

        GameObject go = new GameObject("BossAuraVfx");
        go.hideFlags = HideFlags.HideInHierarchy;
        visual = go.transform;

        sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = auraSprite;

        Shader shader = Shader.Find("Sprites/Default");

        if (shader != null)
            sr.sharedMaterial = new Material(shader);

        SpriteRenderer bossSr = GetComponentInChildren<SpriteRenderer>();

        if (bossSr != null)
        {
            sr.sortingLayerID = bossSr.sortingLayerID;
            sr.sortingOrder = bossSr.sortingOrder + 1;
        }
        else
        {
            sr.sortingOrder = 60;
        }
    }

    private void Update()
    {
        // Görsel yumuşakça açılır / kapanır.
        shown = Mathf.MoveTowards(shown, Active ? 1f : 0f, Time.deltaTime * 6f);

        if (visual != null)
        {
            bool visible = shown > 0.001f;

            if (sr != null)
                sr.enabled = visible;

            if (visible)
            {
                float pulse = 1f + 0.04f * Mathf.Sin(Time.time * 12f);
                float d = radius * 2f * Mathf.Lerp(0.6f, 1f, shown) * pulse;

                visual.position = Center;
                visual.localScale = new Vector3(d, d, 1f);

                Color c = color;
                c.a = shown * (0.8f + 0.2f * Mathf.Sin(Time.time * 9f));
                sr.color = c;
            }
        }

        if (!Active || enemy == null || enemy.IsDead)
            return;

        if (player == null)
            player = FindFirstObjectByType<PlayerController>();

        if (player == null || Time.time < nextTick)
            return;

        Vector2 delta = (Vector2)player.transform.position - (Vector2)Center;

        if (delta.magnitude > radius)
            return;

        if (player.hitInvincibilityTimer > 0f)
            return;

        PlayerDamageReceiver receiver = player.GetComponent<PlayerDamageReceiver>();

        if (receiver == null)
            return;

        nextTick = Time.time + tickInterval;

        float dir = Mathf.Sign(delta.x);

        if (dir == 0f)
            dir = 1f;

        int damage = Mathf.Max(1, Mathf.RoundToInt(enemy.unblockableDamage * damageMultiplier));

        // Alan hasarı: parry/blok yok; dash dokunulmazlığı kurtarır.
        receiver.TakeDamage(
            damage,
            new Vector2(dir, 0f),
            6f,
            3f,
            0.12f,
            40f,
            enemy,
            PlayerHitKind.Unblockable,
            false
        );

        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(0.5f);
    }

    private void OnDisable()
    {
        Active = false;
    }

    private void OnDestroy()
    {
        if (visual != null)
            Destroy(visual.gameObject);
    }
}
