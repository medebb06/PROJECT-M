using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// OYUNCUNUN SİLAHI (RunManager oyuncuya ekler). Silah değişince:
///   - Kombo süresi, vuruş kutusu (menzil), ileri hareket değişir
///     (PlayerCombatController / AttackState buradan okur).
///   - Denge / can hasarı çarpanı (sadece normal vuruşlar, PlayerDamage).
///   - Silaha özel: Hançer kritik + riposte, Mızrak atılma, Büyük Kılıç şok dalgası.
///   - Vuruş anında silah renginde KESİK İZİ (sprite gerekmez).
/// </summary>
public class PlayerWeapon : MonoBehaviour
{
    public static PlayerWeapon Instance { get; private set; }

    public WeaponType Current { get; private set; } = WeaponType.Sword;

    [Header("Kesik izi")]
    public float slashLife = 0.14f;

    [Header("Büyük Kılıç: 4. vuruş şok dalgası")]
    public float greatswordShockRadius = 2.6f;
    [Range(0f, 1f)] public float greatswordShockBalance = 0.2f;

    [Header("Mızrak: ilk vuruş atılması (ileri hareket çarpanı)")]
    public float spearFirstHitLunge = 2f;

    private PlayerController player;
    private PlayerStats stats;

    private static Sprite slashSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    public static PlayerWeapon Ensure(GameObject playerObject)
    {
        PlayerWeapon w = playerObject.GetComponent<PlayerWeapon>();

        if (w == null)
            w = playerObject.AddComponent<PlayerWeapon>();

        return w;
    }

    private void Awake()
    {
        Instance = this;
        player = GetComponent<PlayerController>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        PlayerDamage.WeaponBalanceMultiplier = 1f;
        PlayerDamage.WeaponHealthMultiplier = 1f;
    }

    private void Start()
    {
        Equip(Current);
    }

    // =========================================================
    // DEĞERLER
    // =========================================================

    public float DurationMultiplier => WeaponInfo.Duration(Current);

    public Vector2 HitBoxMultiplier => new Vector2(WeaponInfo.Reach(Current), WeaponInfo.Height(Current));

    public float LungeMultiplier(int comboStep)
    {
        float m = WeaponInfo.Lunge(Current);

        if (Current == WeaponType.Spear && comboStep == 1)
            m *= spearFirstHitLunge;

        return m;
    }

    // =========================================================
    // TAK
    // =========================================================

    public void Equip(WeaponType type)
    {
        Current = type;

        PlayerDamage.WeaponBalanceMultiplier = WeaponInfo.Balance(type);
        PlayerDamage.WeaponHealthMultiplier = WeaponInfo.Health(type);

        if (stats == null)
            stats = PlayerStats.Current != null ? PlayerStats.Current : GetComponent<PlayerStats>();

        if (stats != null)
        {
            stats.RemoveModifiers(this);

            if (type == WeaponType.Daggers)
            {
                stats.AddModifier(this, StatType.CritChance, 0.15f, 1f);
                stats.AddModifier(this, StatType.RiposteHits, 2f, 1f);
            }
        }
    }

    // =========================================================
    // VURUŞ ANI (AttackState / AirAttackState çağırır)
    // =========================================================

    public void OnSwing(int comboStep, Vector2 center, Vector2 box, Vector2 dir, bool hitSomething)
    {
        SpawnSlash(center, box, dir, comboStep);

        if (Current == WeaponType.Greatsword && comboStep == 4)
            GreatswordShock(center);
    }

    private void GreatswordShock(Vector2 center)
    {
        List<EnemyController> list = CharmUtil.EnemiesInRadius(center, greatswordShockRadius);

        for (int i = 0; i < list.Count; i++)
        {
            EnemyController e = list[i];

            CharmUtil.AddBalancePercent(e, greatswordShockBalance);

            if (e == null || e.IsDead || e.IsAttackCommitted)
                continue;

            Rigidbody2D rb = e.GetComponent<Rigidbody2D>();

            if (rb != null && e.GetComponent<BossController>() == null)
            {
                float side = Mathf.Sign(e.transform.position.x - center.x);
                rb.linearVelocity = new Vector2(side * 8f * EnemyTime.Scale, 3f);
            }
        }

        if (PlayerAbility.Instance != null)
            PlayerAbility.Instance.SpawnRingFx(center, greatswordShockRadius, new Color(1f, 0.6f, 0.4f, 0.8f));

        if (player != null && player.impulseSource != null)
            player.impulseSource.GenerateImpulse(0.6f);
    }

    // Hilal biçimli kesik izi: kutu boyunda, silah renginde, hızla söner.
    private void SpawnSlash(Vector2 center, Vector2 box, Vector2 dir, int step)
    {
        GameObject obj = new GameObject("Kesik");
        obj.transform.position = new Vector3(center.x, center.y, 0f);

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = SlashSprite();

        Color c = WeaponInfo.Color(Current);
        c.a = 0.85f;
        sr.color = c;

        if (player != null && player.playerSprite != null)
        {
            sr.sortingLayerID = player.playerSprite.sortingLayerID;
            sr.sortingOrder = player.playerSprite.sortingOrder + 3;
        }

        bool left = dir.x < 0f;
        sr.flipX = left;

        // Yukarıdan / aşağıdan dönüşümlü kesik.
        sr.flipY = step % 2 == 0;

        obj.transform.localScale = new Vector3(Mathf.Max(0.3f, box.x), Mathf.Max(0.3f, box.y), 1f);

        StartCoroutine(Fade(obj.transform, sr, slashLife));
    }

    private static IEnumerator Fade(Transform t, SpriteRenderer sr, float life)
    {
        float start = Time.unscaledTime;
        Color c = sr.color;
        Vector3 s0 = t.localScale;

        while (t != null)
        {
            float k = Mathf.Clamp01((Time.unscaledTime - start) / life);

            sr.color = new Color(c.r, c.g, c.b, c.a * (1f - k));
            t.localScale = s0 * (1f + 0.15f * k);

            if (k >= 1f)
                break;

            yield return null;
        }

        if (t != null)
            Destroy(t.gameObject);
    }

    // 1x1 birim hilal (sağa bakan).
    private static Sprite SlashSprite()
    {
        if (slashSprite != null)
            return slashSprite;

        const int w = 64;
        const int h = 48;

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Vector2 c1 = new Vector2(w * 0.15f, h * 0.5f);   // dış daire merkezi
        Vector2 c2 = new Vector2(w * 0.02f, h * 0.5f);   // iç (oyulan) daire
        float r1 = w * 0.85f;
        float r2 = w * 0.8f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);

                float d1 = Vector2.Distance(p, c1);
                float d2 = Vector2.Distance(p, c2);

                float a = 0f;

                if (d1 <= r1 && d2 > r2)
                {
                    // Uçlara doğru incelir.
                    float edge = Mathf.Clamp01((r1 - d1) / 4f) * Mathf.Clamp01((d2 - r2) / 4f);
                    float tip = 1f - Mathf.Abs(y + 0.5f - h * 0.5f) / (h * 0.5f);
                    a = edge * Mathf.Clamp01(tip * 1.6f);
                }

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply();

        slashSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Mathf.Max(w, h));

        return slashSprite;
    }
}
