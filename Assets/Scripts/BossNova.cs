using System.Collections;
using UnityEngine;

/// <summary>
/// BOSS: ZEMİN DALGASI (haritanın TÜM zeminine vuran saldırı). BossController ekler.
///
/// Mavi süpürme gibi okunur: uyarı boyunca bütün zemin boyunca mavi bir şerit
/// yanıp söner ve boss'un üstünde "ZIPLA!" çıkar. Vuruş anında şerit beyaza
/// patlar. O anda YERDEYSEN ağır, engellenemez hasar yersin: dash, parry ve
/// block KURTARMAZ. Tek çare ZIPLAMAK.
///
/// Zıplayarak kurtulursan "VUR!" karşı vuruş penceresi açılır.
/// Boss uyarı sırasında sersemlerse saldırı İPTAL olur.
/// </summary>
[RequireComponent(typeof(EnemyController))]
public class BossNova : MonoBehaviour
{
    [Header("Zamanlama")]
    public float firstDelay = 8f;
    public float cooldown = 16f;
    public float cooldownPhase2 = 11f;

    [Tooltip("Uyarı süresi (sn): zemin şeridi yanıp söner, vuruş sonunda.")]
    public float windup = 1.4f;

    [Tooltip("Vuruştan önce bu kadar sn içinde havadaysan kurtarır (erken zıplama payı).")]
    public float jumpEarly = 0.15f;

    [Tooltip("Vuruştan sonra bu kadar sn içinde havadaysan kurtarır.")]
    public float jumpLate = 0.08f;

    [Tooltip("İniş sonrası boss'un toparlanma süresi (sn).")]
    public float recoverAfter = 1.2f;

    [Tooltip("Ayağın zeminden bu kadar yukarıdaysa 'havada' sayılır.")]
    public float airClearance = 0.4f;

    [Header("Hasar")]
    public float damageMultiplier = 1.6f;

    public Color warnColor = new Color(0.35f, 0.7f, 1f, 1f);

    private EnemyController enemy;
    private BossController boss;
    private Rigidbody2D rb;
    private Collider2D bodyCol;

    private bool busy;
    private float nextTime;

    private Texture2D tex;
    private float flashAlpha;
    private float warnAlpha;
    private GUIStyle warnStyle;

    private static Sprite whiteSprite;
    private static Sprite glowSprite;

    public bool IsBusy => busy;

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        boss = GetComponent<BossController>();
        rb = GetComponent<Rigidbody2D>();
        bodyCol = GetComponent<Collider2D>();

        nextTime = Time.time + firstDelay;

        tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
    }

    private void OnDestroy()
    {
        if (tex != null)
            Destroy(tex);
    }

    // Vuruş anında kısa beyaz ekran parlaması.
    private void OnGUI()
    {
        DrawWarning();

        if (flashAlpha <= 0.001f)
            return;

        GUI.color = new Color(1f, 1f, 1f, flashAlpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), tex);
        GUI.color = Color.white;
    }

    // Uyarı: ekranın dört kenarında yanıp sönen çerçeve + üstte büyük yazı.
    private void DrawWarning()
    {
        if (warnAlpha <= 0.001f)
            return;

        float th = Screen.height * 0.035f;

        Color c = warnColor;
        c.a = warnAlpha;

        GUI.color = c;

        GUI.DrawTexture(new Rect(0, 0, Screen.width, th), tex);
        GUI.DrawTexture(new Rect(0, Screen.height - th, Screen.width, th), tex);
        GUI.DrawTexture(new Rect(0, 0, th, Screen.height), tex);
        GUI.DrawTexture(new Rect(Screen.width - th, 0, th, Screen.height), tex);

        if (warnStyle == null)
        {
            warnStyle = new GUIStyle(GUI.skin.label);
            warnStyle.alignment = TextAnchor.MiddleCenter;
            warnStyle.fontStyle = FontStyle.Bold;
        }

        warnStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.07f);

        Rect r = new Rect(0, Screen.height * 0.12f, Screen.width, Screen.height * 0.12f);

        GUI.color = new Color(0f, 0f, 0f, warnAlpha);
        GUI.Label(new Rect(r.x + 3, r.y + 3, r.width, r.height), "ZEMİN DALGASI  -  ZIPLA!", warnStyle);

        GUI.color = new Color(warnColor.r, warnColor.g, warnColor.b, Mathf.Clamp01(warnAlpha * 1.2f));
        GUI.Label(r, "ZEMİN DALGASI  -  ZIPLA!", warnStyle);

        GUI.color = Color.white;
    }

    private void Update()
    {
        if (busy || enemy == null || enemy.IsDead)
            return;

        if (Time.time < nextTime || enemy.target == null || enemy.IsTargetDead)
            return;

        BossSlam slam = GetComponent<BossSlam>();

        if (slam != null && slam.IsBusy)
            return;

        if (
            enemy.IsStaggered ||
            enemy.CurrentState is EnemyAttackState ||
            enemy.CurrentState is EnemyExecuteState
        )
        {
            return;
        }

        if (rb != null && Mathf.Abs(rb.linearVelocity.y) > 0.2f)
            return;

        if (!BossSkillGate.CanStart("nova"))
            return;

        BossSkillGate.Begin("nova");

        StartCoroutine(NovaRoutine());
    }

    private bool PlayerAirborne(PlayerController player, float groundY)
    {
        if (player == null)
            return false;

        Collider2D col = player.GetComponent<Collider2D>();

        float feet = col != null ? col.bounds.min.y : player.transform.position.y;

        return feet > groundY + airClearance;
    }

    private IEnumerator NovaRoutine()
    {
        busy = true;

        PlayerController player = enemy.target.GetComponent<PlayerController>();

        float groundY =
            bodyCol != null ? bodyCol.bounds.min.y : transform.position.y;

        GameObject strip = MakeStrip(groundY);

        enemy.PlayAlertFlash();

        CombatCallout.PopupAbove(enemy, "ZIPLA!", warnColor, 1.5f);

        bool dodged = false;
        bool cancelled = false;

        // ---------- UYARI ----------
        float t = 0f;

        while (t < windup)
        {
            if (enemy == null || enemy.IsDead || enemy.IsStaggered)
            {
                cancelled = true;
                break;
            }

            // Boss yerinde durur, başka saldırıya geçmez.
            enemy.StartAttackRecovery(0.5f);

            if (rb != null)
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            float p = t / windup;

            UpdateStrip(strip, groundY, p, false);


            // Vuruştan hemen önce zaten havadaysa kurtarır.
            if (t >= windup - jumpEarly && PlayerAirborne(player, groundY))
                dodged = true;

            t += Time.deltaTime;

            yield return null;
        }

        if (cancelled)
        {
            warnAlpha = 0f;

            if (strip != null)
                Destroy(strip);

            busy = false;
            BossSkillGate.End("nova");
            nextTime = Time.time + 6f;

            yield break;
        }

        warnAlpha = 0f;

        // ---------- VURUŞ ANI ----------
        flashAlpha = 0.35f;

        UpdateStrip(strip, groundY, 1f, true);

        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(1.1f);

        float w = 0f;

        while (w < jumpLate)
        {
            if (PlayerAirborne(player, groundY))
                dodged = true;

            flashAlpha = Mathf.Lerp(0.35f, 0f, w / Mathf.Max(0.05f, jumpLate));

            w += Time.deltaTime;

            yield return null;
        }

        flashAlpha = 0f;

        if (strip != null)
            Destroy(strip, 0.1f);

        // ---------- SONUÇ ----------
        // Süpürmedeki gibi: dash dokunulmazlığı işe yaramaz; sadece zıplamak.
        if (player != null && player.hitInvincibilityTimer > 0f)
            dodged = true;

        if (dodged)
        {
            UnblockableCounter counter = GetComponent<UnblockableCounter>();

            if (counter == null)
                counter = gameObject.AddComponent<UnblockableCounter>();

            counter.OpenJumpWindow();

            HitStop.Request(0.08f, 0.05f);
        }
        else if (player != null)
        {
            Hit(player);
        }

        // Faz 2: dalganın ardından yerde patlayan tehlike alanları.
        if (boss != null && boss.InPhase2)
            StartCoroutine(HazardRoutine(player, groundY));

        // ---------- TOPARLANMA ----------
        float e = 0f;

        while (e < recoverAfter)
        {
            if (enemy == null || enemy.IsDead || enemy.IsStaggered)
                break;

            enemy.StartAttackRecovery(0.5f);

            if (rb != null)
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            e += Time.deltaTime;

            yield return null;
        }

        // Nefes payı: dalgadan sonra kısa süre başka saldırı yok.
        enemy.StartAttackRecovery(0.9f);

        bool p2 = boss != null && boss.InPhase2;

        nextTime = Time.time + (p2 ? cooldownPhase2 : cooldown);

        busy = false;

        BossSkillGate.End("nova");
    }

    private void Hit(PlayerController player, float damageScale = 1f)
    {
        PlayerDamageReceiver receiver = player.GetComponent<PlayerDamageReceiver>();

        if (receiver == null)
            return;

        Vector2 dir =
            new Vector2(Mathf.Sign(player.transform.position.x - transform.position.x), 0f);

        int damage = Mathf.Max(1, Mathf.RoundToInt(enemy.unblockableDamage * damageMultiplier * damageScale));

        receiver.TakeDamage(
            damage,
            dir,
            2.5f,
            0f,
            0.1f,
            40f,
            enemy,
            PlayerHitKind.Unblockable,
            false
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

    // ---------------- Zemin şeridi ----------------

    private GameObject MakeStrip(float groundY)
    {
        if (whiteSprite == null)
        {
            Texture2D t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, Color.white);
            t.Apply();

            whiteSprite = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        GameObject go = new GameObject("BossNovaStrip");

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = whiteSprite;

        Shader shader = Shader.Find("Sprites/Default");

        if (shader != null)
            sr.sharedMaterial = new Material(shader);

        SpriteRenderer bossSr = GetComponentInChildren<SpriteRenderer>();

        if (bossSr != null)
        {
            sr.sortingLayerID = bossSr.sortingLayerID;
            sr.sortingOrder = bossSr.sortingOrder - 1;
        }

        // Işık: şeridin üstüne doğru sönen mavi parıltı (yalnızca zeminde).
        if (glowSprite == null)
        {
            Texture2D g = new Texture2D(1, 32, TextureFormat.RGBA32, false);

            for (int y = 0; y < 32; y++)
            {
                float a = 1f - y / 31f;
                g.SetPixel(0, y, new Color(1f, 1f, 1f, a * a));
            }

            g.Apply();

            glowSprite = Sprite.Create(g, new Rect(0, 0, 1, 32), new Vector2(0.5f, 0f), 32f);
        }

        GameObject glow = new GameObject("Glow");
        glow.transform.SetParent(go.transform, false);

        SpriteRenderer gsr = glow.AddComponent<SpriteRenderer>();
        gsr.sprite = glowSprite;
        gsr.sharedMaterial = sr.sharedMaterial;
        gsr.sortingLayerID = sr.sortingLayerID;
        gsr.sortingOrder = sr.sortingOrder;

        return go;
    }

    private void UpdateStrip(GameObject strip, float groundY, float progress, bool impact)
    {
        if (strip == null)
            return;

        Camera cam = Camera.main;

        float cx = cam != null ? cam.transform.position.x : transform.position.x;

        float width =
            cam != null
                ? cam.orthographicSize * 2f * cam.aspect + 6f
                : 60f;

        strip.transform.position = new Vector3(cx, groundY + (impact ? 0.35f : 0.12f), 0f);
        strip.transform.localScale = new Vector3(width, impact ? 0.8f : 0.26f, 1f);

        SpriteRenderer sr = strip.GetComponent<SpriteRenderer>();

        Transform glow = strip.transform.Find("Glow");
        SpriteRenderer gsr = glow != null ? glow.GetComponent<SpriteRenderer>() : null;

        if (glow != null)
        {
            // Ebeveyn ölçeğini telafi et: dünya yüksekliği ~2.4 birim.
            Vector3 ps = strip.transform.localScale;

            glow.localScale = new Vector3(1f, (impact ? 3.2f : 2.4f) / ps.y / 32f * 32f / 1f * 1f, 1f);
            glow.localPosition = new Vector3(0f, 0.5f, 0f);
        }

        if (impact)
        {
            sr.color = Color.white;

            if (gsr != null)
                gsr.color = new Color(1f, 1f, 1f, 0.9f);

            return;
        }

        float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * (9f + 22f * progress));

        Color c = warnColor;
        c.a = Mathf.Lerp(0.3f, 0.85f, progress) * pulse;

        sr.color = c;

        if (gsr != null)
        {
            c.a *= 0.75f;
            gsr.color = c;
        }
    }

    // ---------------- Faz 2: tehlike alanları ----------------

    private readonly System.Collections.Generic.List<GameObject> hazardObjects =
        new System.Collections.Generic.List<GameObject>();

    private IEnumerator HazardRoutine(PlayerController player, float groundY)
    {
        if (player == null)
            yield break;

        const int Count = 3;
        const float Radius = 2.5f;
        const float Delay = 1.3f;

        float[] xs = new float[Count];
        GameObject[] zones = new GameObject[Count];

        float px = player.transform.position.x;

        for (int i = 0; i < Count; i++)
        {
            // Oyuncunun çevresinde, birbirinden ayrık noktalar.
            float x = px + (i - 1) * Random.Range(4.5f, 7f) + Random.Range(-1f, 1f);

            xs[i] = x;

            zones[i] = MakeStrip(groundY);
            hazardObjects.Add(zones[i]);
        }

        float t = 0f;

        while (t < Delay)
        {
            if (enemy == null || enemy.IsDead)
                break;

            float p = t / Delay;

            for (int i = 0; i < Count; i++)
            {
                if (zones[i] == null)
                    continue;

                zones[i].transform.position = new Vector3(xs[i], groundY + 0.1f, 0f);
                zones[i].transform.localScale = new Vector3(Radius * 2f, 0.24f, 1f);

                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * (10f + 18f * p));

                Color c = new Color(1f, 0.25f, 0.15f, Mathf.Lerp(0.3f, 0.9f, p) * pulse);

                zones[i].GetComponent<SpriteRenderer>().color = c;

                Transform glow = zones[i].transform.Find("Glow");

                if (glow != null)
                {
                    glow.localScale = new Vector3(1f, 1.8f / 0.24f, 1f);
                    glow.localPosition = new Vector3(0f, 0.5f, 0f);

                    c.a *= 0.7f;
                    glow.GetComponent<SpriteRenderer>().color = c;
                }
            }

            t += Time.deltaTime;

            yield return null;
        }

        // Patlama.
        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(0.7f);

        for (int i = 0; i < Count; i++)
        {
            if (zones[i] != null)
            {
                zones[i].GetComponent<SpriteRenderer>().color = Color.white;
                zones[i].transform.localScale = new Vector3(Radius * 2.2f, 0.7f, 1f);

                Destroy(zones[i], 0.12f);
            }
        }

        if (player != null && enemy != null && !enemy.IsDead && player.hitInvincibilityTimer <= 0f)
        {
            float px2 = player.transform.position.x;

            for (int i = 0; i < Count; i++)
            {
                if (Mathf.Abs(px2 - xs[i]) > Radius)
                    continue;

                // Dalgadaki gibi: zıplamak kurtarır, dash kurtarmaz.
                if (PlayerAirborne(player, groundY))
                    break;

                Hit(player, 0.7f);

                break;
            }
        }

        hazardObjects.Clear();
    }

    private void OnDisable()
    {
        for (int i = 0; i < hazardObjects.Count; i++)
        {
            if (hazardObjects[i] != null)
                Destroy(hazardObjects[i]);
        }

        hazardObjects.Clear();

        warnAlpha = 0f;
        flashAlpha = 0f;
        if (busy)
            BossSkillGate.End("nova");

        busy = false;
    }
}
