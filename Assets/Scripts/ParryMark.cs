using UnityEngine;

/// <summary>
/// SEKIRO TARZI "ACIK" ISARET: dogru parry yapilan dusman birkac saniye posture-kirik rengine (staggerFlashColor) boyanir;
/// bu surede ona isabet eden ilk NORMAL vurus onu tek vurusta oldurur (infaz).
/// Duz vurusla postur/can kirmak zor, parry ise oldurucu firsat verir.
/// Boss'a uygulanmaz. Oyun baslayinca kendiliginden kurulur.
/// </summary>
public class ParryMarkSystem : MonoBehaviour
{
    [Tooltip("Isaret suresi (sn). Yeni parry sureyi tazeler.")] public float duration = 3.5f;
    [Tooltip("Parry'den hemen sonra gelen otomatik vuruslar infaz sayilmasin (sn).")] public float grace = 0.2f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindAnyObjectByType<ParryMarkSystem>() != null) return;
        var go = new GameObject("ParryMarkSystem");
        DontDestroyOnLoad(go);
        go.AddComponent<ParryMarkSystem>();
    }

    void OnEnable()
    {
        CombatEvents.ParrySucceeded += OnParry;
        CombatEvents.EnemyHit += OnHit;
    }

    void OnDisable()
    {
        CombatEvents.ParrySucceeded -= OnParry;
        CombatEvents.EnemyHit -= OnHit;
    }

    void OnParry(EnemyController e, bool brokeBalance)
    {
        if (e == null || e.IsDead) return;
        if (e.GetComponent<BossController>() != null) return;

        var m = e.GetComponent<ParryMark>();
        if (m == null) m = e.gameObject.AddComponent<ParryMark>();
        m.Refresh(duration, grace);
    }

    void OnHit(EnemyController e, DamageInfo info, HitResult r)
    {
        if (e == null || !r.hit || e.IsDead) return;

        var m = e.GetComponent<ParryMark>();
        if (m == null || !m.Lethal) return;

        Health h = e.GetComponent<Health>();
        if (h == null || h.IsDead) return;

        m.Consume();
        h.TakeDamage(h.MaxHealth);
        CombatCallout.PopupAbove(e, "INFAZ!", new Color(1f, 0.25f, 0.2f), 1.2f);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.6f);
    }
}

public class ParryMark : MonoBehaviour
{
    float until, lethalFrom;
    EnemyController enemy;
    SpriteRenderer[] rends;
    Color[] orig;
    bool tinted;

    public bool Lethal => enabled && Time.time >= lethalFrom && Time.time < until;

    public static bool IsMarked(EnemyController e)
    {
        if (e == null) return false;
        var m = e.GetComponent<ParryMark>();
        return m != null && m.enabled && Time.time < m.until;
    }

    public void Refresh(float duration, float grace)
    {
        until = Time.time + duration;
        lethalFrom = Time.time + grace;
        enabled = true;
        if (enemy == null) enemy = GetComponent<EnemyController>();
    }

    public void Consume()
    {
        until = 0f;
        Restore();
        enabled = false;
    }

    void Collect()
    {
        if (rends != null) return;
        var all = GetComponentsInChildren<SpriteRenderer>(true);
        var list = new System.Collections.Generic.List<SpriteRenderer>();
        foreach (var r in all)
            if (r != null && !r.gameObject.name.StartsWith("~")) list.Add(r);
        rends = list.ToArray();
        orig = new Color[rends.Length];
    }

    void Restore()
    {
        if (!tinted || rends == null) { tinted = false; return; }

        // Gercek posture kirikligi sirasinda stagger rengi zaten kendi geri alinir.
        if (enemy != null && !enemy.IsStaggered)
        {
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] == null) continue;
                Color c = rends[i].color;
                c.r = orig[i].r; c.g = orig[i].g; c.b = orig[i].b;
                rends[i].color = c;
            }
        }

        tinted = false;
    }

    void LateUpdate()
    {
        if (Time.time >= until)
        {
            Consume();
            return;
        }

        if (enemy == null || enemy.IsDead) { Consume(); return; }

        Collect();

        // Posture kirildiginda kullanilan renk (EnemyController.staggerFlashColor) acik sure boyunca kalir.
        Color tint = enemy.staggerFlashColor;

        for (int i = 0; i < rends.Length; i++)
        {
            var r = rends[i];
            if (r == null) continue;

            if (!tinted) orig[i] = r.color;

            Color c = r.color;
            c.r = tint.r; c.g = tint.g; c.b = tint.b;
            r.color = c;
        }

        tinted = true;
    }

    void OnDisable()
    {
        Restore();
    }
}

/// <summary>
/// SOFT TARGET: parry ile isaretlenen dusman menzildeyse, yon tusuna basmiyorken oyuncu ona doner.
/// Yon tusu = oyuncunun secimi (ezmez). Blok / saldiri kilidi / rage sirasinda calismaz.
/// </summary>
public class ParrySoftTarget : MonoBehaviour
{
    public float range = 12f;
    public float maxHeightDiff = 4f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindAnyObjectByType<ParrySoftTarget>() != null) return;
        var go = new GameObject("ParrySoftTarget");
        DontDestroyOnLoad(go);
        go.AddComponent<ParrySoftTarget>();
    }

    /// <summary>Menzildeki en yakin isaretli dusman (yoksa null).</summary>
    public static EnemyController Find(Vector2 from, float range, float maxDy)
    {
        EnemyController best = null; float bd = float.MaxValue;
        for (int i = 0; i < EnemyController.All.Count; i++)
        {
            EnemyController e = EnemyController.All[i];
            if (e == null || e.IsDead || !ParryMark.IsMarked(e)) continue;
            Vector2 d = (Vector2)e.transform.position - from;
            if (Mathf.Abs(d.x) > range || Mathf.Abs(d.y) > maxDy) continue;
            if (Mathf.Abs(d.x) < bd) { bd = Mathf.Abs(d.x); best = e; }
        }
        return best;
    }

    /// <summary>Oyuncuyu yon tusu basili degilse isaretli hedefe cevirir.</summary>
    public static bool FaceTarget(PlayerController p, float range = 12f, float maxDy = 4f)
    {
        if (p == null || Mathf.Abs(p.moveInput) > 0.1f) return false;
        EnemyController t = Find(p.transform.position, range, maxDy);
        if (t == null) return false;
        float dx = t.transform.position.x - p.transform.position.x;
        if (Mathf.Abs(dx) < 0.3f) return false;
        p.facingDir = dx < 0f ? -1f : 1f;
        if (p.playerSprite != null) p.playerSprite.flipX = p.facingDir < 0f;
        return true;
    }

    PlayerController pc;
    EnemyController parried; float parriedUntil;

    void OnEnable() { CombatEvents.ParrySucceeded += OnParry; }
    void OnDisable() { CombatEvents.ParrySucceeded -= OnParry; }

    // Basarili parry: oyuncu (blok durusundayken bile) parry'lenen dusmana doner.
    void OnParry(EnemyController e, bool brokeBalance)
    {
        if (e == null) return;
        parried = e; parriedUntil = Time.time + 0.4f;
        if (pc == null) pc = FindFirstObjectByType<PlayerController>();
        FaceEnemy(pc, e);
    }

    static void FaceEnemy(PlayerController p, EnemyController e)
    {
        if (p == null || e == null) return;
        float dx = e.transform.position.x - p.transform.position.x;
        if (Mathf.Abs(dx) < 0.2f) return;
        p.facingDir = dx < 0f ? -1f : 1f;
        if (p.playerSprite != null) p.playerSprite.flipX = p.facingDir < 0f;
    }

    void LateUpdate()
    {
        if (MainMenu.Active) return;
        if (pc == null) pc = FindFirstObjectByType<PlayerController>();
        if (pc != null && parried != null && Time.time < parriedUntil && !parried.IsDead)
        {
            FaceEnemy(pc, parried);
            return;
        }
        if (pc == null || pc.attackFacingLocked || !pc.canControl) return;
        var def = pc.GetComponent<PlayerDefenseController>();
        if (def != null && def.IsDefending) return;
        if (RageMeter.Instance != null && RageMeter.Instance.Busy) return;
        Health h = pc.GetComponent<Health>();
        if (h != null && h.IsDead) return;
        FaceTarget(pc, range, maxHeightDiff);
    }
}
