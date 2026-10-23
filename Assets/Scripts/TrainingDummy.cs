using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// SABİT ANTRENMAN DUMMY'Sİ. Ölmez, sınırsız vurulur.
/// Her vuruşta: hasar sayısı, flaş, sarsılma, hit stop, kamera sarsıntısı,
/// combo sayacı, DPS, toplam hasar ve posture çubuğu gösterir.
/// T tuşu istatistikleri sıfırlar.
///
/// Oyuncunun saldırı kodu bu dummy'yi TrainingDummy.TryHit ile bulur
/// (collider'ı "Enemy" layer'ında olmalı; Tools > Create Training Dummy kurar).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TrainingDummy : MonoBehaviour
{
    [Header("Boyut (dünya birimi, genişlik x yükseklik)")]
    public Vector2 size = new Vector2(7.5f, 15f);

    [Tooltip("Boyut çarpanı: 1 = yukarıdaki boyut, 0.5 = yarısı. Dummy'yi küçültmek için bunu kullan.")]
    [Range(0.2f, 2f)] public float sizeMultiplier = 0.5f;

    [Header("Görünüm")]
    [Tooltip("Açık: gövde yüksek çözünürlüklü, yumuşak kenarlı kapsül sprite'ı olur.")]
    public bool useCapsuleSprite = true;
    public bool showStatsText = true;

    [Header("Ses (boşsa sahnedeki/prefab'taki düşman sesleri kullanılır)")]
    public AudioClip[] balanceHitSounds;
    public AudioClip[] healthHitSounds;
    public AudioClip balanceBreakSound;
    [Range(0f, 1f)] public float hitVolume = 1f;

    [Header("Posture")]
    public float maxPosture = 100f;
    [Tooltip("Posture bu süre vurulmazsa azalmaya başlar.")]
    public float postureRegenDelay = 2.5f;
    public float postureRegenPerSecond = 30f;
    [Tooltip("Posture kırılınca bu süre 'kırık' kalır (can hasarı bu sürede sayılır).")]
    public float breakDuration = 1.4f;

    [Header("Geri bildirim")]
    public float hitStopNormal = 0.045f;
    public float hitStopHeavy = 0.09f;
    public float cameraShake = 0.35f;
    public float knockbackWobble = 0.22f;
    public float flashTime = 0.07f;
    [Tooltip("Combo bu kadar saniye vuruş gelmezse sıfırlanır.")]
    public float comboResetTime = 2.0f;
    public float dpsWindow = 3f;

    // Son istatistikler (inspector'da izlemek için açık).
    public int Combo { get; private set; }
    public int LastDamage { get; private set; }
    public int TotalDamage { get; private set; }
    public float Posture { get; private set; }
    public bool IsBroken => Time.time < brokenUntil;

    public static readonly List<TrainingDummy> All = new List<TrainingDummy>();

    private struct Sample { public float time; public int dmg; }

    private readonly List<Sample> samples = new List<Sample>();

    private SpriteRenderer[] renderers;
    private Color[] baseColors;
    private Vector3 homePos;
    private Vector3 homeScale;
    private Vector2 wobbleOffset;
    private Vector2 wobbleVel;
    private float squash;
    private float flashUntil;
    private float lastHitTime = -999f;
    private float brokenUntil = -999f;
    private int lastHitFrame = -1;
    private DamageSource lastHitSource;

    private TextMesh statsText;
    private Transform barRoot;
    private Transform barFill;
    private SpriteRenderer barFillSr;
    private float nextTextUpdate;

    private static Sprite whiteSprite;

    private void OnEnable()
    {
        All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    // Oyun içinden (menüden) dummy kurar. pos = yaklaşık zemin noktası; yere oturtulur.
    public static TrainingDummy Spawn(Vector3 pos)
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");

        if (enemyLayer < 0)
        {
            Debug.LogError("TrainingDummy.Spawn: 'Enemy' layer'ı yok.");
            return null;
        }

        pos.z = 0f;

        RaycastHit2D hit = Physics2D.Raycast((Vector2)pos + Vector2.up * 6f, Vector2.down, 40f, LayerMask.GetMask("Ground"));

        if (hit.collider != null)
            pos.y = hit.point.y;

        GameObject root = new GameObject("TrainingDummy");
        root.layer = enemyLayer;
        root.transform.position = pos;

        GameObject body = new GameObject("Body");
        body.transform.SetParent(root.transform, false);
        body.layer = enemyLayer;

        SpriteRenderer sr = body.AddComponent<SpriteRenderer>();
        sr.sprite = GetCapsuleSprite();
        sr.color = new Color(0.82f, 0.7f, 0.52f);
        sr.sortingOrder = 5;

        root.AddComponent<BoxCollider2D>();

        // Awake bileşen eklenir eklenmez çalışır: collider/görsel boyutu orada uygulanır.
        return root.AddComponent<TrainingDummy>();
    }

    // Collider'ı ve "Body" görselini 'size' değerine göre ayarlar.
    [ContextMenu("Boyutu Uygula")]
    public void ApplySize()
    {
        Vector2 size = this.size * sizeMultiplier;

        BoxCollider2D col = GetComponent<BoxCollider2D>();

        if (col != null)
        {
            col.size = size;
            col.offset = new Vector2(0f, size.y * 0.5f);
        }

        Transform body = transform.Find("Body");

        if (body == null)
            return;

        body.localPosition = new Vector3(0f, size.y * 0.5f, 0f);

        SpriteRenderer sr = body.GetComponent<SpriteRenderer>();

        if (sr != null && useCapsuleSprite)
            sr.sprite = GetCapsuleSprite();

        Vector2 sp = sr != null && sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;

        body.localScale = new Vector3(size.x / sp.x, size.y / sp.y, 1f);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Inspector'da 'size' değişince anında uygula.
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && !Application.isPlaying)
                ApplySize();
        };
    }
#endif

    private void Awake()
    {
        ApplySize();
        SetupAudio();

        homePos = transform.position;
        homeScale = transform.localScale;

        renderers = GetComponentsInChildren<SpriteRenderer>();
        baseColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
            baseColors[i] = renderers[i].color;

        BuildHud();
    }

    // =========================================================
    // DIŞARIDAN ÇAĞRI: oyuncu saldırıları buraya uğrar
    // =========================================================

    // Collider bir dummy'ye aitse vuruşu işler ve true döner.
    public static bool TryHit(
        Collider2D col,
        DamageSource source,
        int comboStep,
        int balanceDamage,
        int healthDamage,
        Vector2 direction,
        Vector3 hitPosition,
        bool fromAbove = false
    )
    {
        if (col == null)
            return false;

        TrainingDummy d = col.GetComponentInParent<TrainingDummy>();

        if (d == null)
            return false;

        d.ReceiveHit(source, comboStep, balanceDamage, healthDamage, direction, hitPosition, fromAbove);

        return true;
    }

    private void ReceiveHit(
        DamageSource source,
        int comboStep,
        int balanceDamage,
        int healthDamage,
        Vector2 direction,
        Vector3 hitPosition,
        bool fromAbove
    )
    {
        // Aynı vuruşta birden fazla collider dummy'ye değebilir: tek say.
        if (lastHitFrame == Time.frameCount && lastHitSource == source)
            return;

        lastHitFrame = Time.frameCount;
        lastHitSource = source;

        float now = Time.time;

        // Combo: süre dolduysa baştan.
        if (now - lastHitTime > comboResetTime)
            Combo = 0;

        Combo++;
        lastHitTime = now;

        // Kritik (oyuncunun gerçek stat'ından).
        bool crit = false;
        PlayerStats stats = PlayerStats.Current;

        if (stats != null)
            crit = Random.value < stats.CritChance;

        float mult = crit && stats != null ? stats.CritMultiplier : 1f;

        // Posture kırık değilse posture'a, kırıksa cana (dummy ölmez, sadece sayar).
        bool wasBroken = IsBroken;
        int amount;
        bool brokeNow = false;

        if (!wasBroken)
        {
            amount = Mathf.Max(1, Mathf.RoundToInt(balanceDamage * mult));
            Posture += amount;

            if (Posture >= maxPosture)
            {
                Posture = maxPosture;
                brokeNow = true;
                brokenUntil = now + breakDuration;
            }
        }
        else
        {
            amount = Mathf.Max(1, Mathf.RoundToInt(healthDamage * mult));
        }

        LastDamage = amount;
        TotalDamage += amount;
        samples.Add(new Sample { time = now, dmg = amount });

        // ---- görsel geri bildirim ----

        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;

        wobbleVel += dir * (knockbackWobble * 18f);

        if (fromAbove)
            wobbleVel += Vector2.down * 3f;

        squash = Mathf.Min(1f, squash + 0.6f);
        flashUntil = Time.unscaledTime + flashTime;

        // Ses (düşmanla aynı klipler).
        if (wasBroken)
            PlaySfx(healthHitSounds);
        // (posture vuruş sesi: HitClickAudio adım sesleri)

        // Test için: dummy'de HER vuruşta ses çalar (posture: adım, can: ateş).
        if (wasBroken)
            HitClickAudio.PlayEnemyHealthHit();
        else
            HitClickAudio.PlayEnemyPostureHit();

        if (brokeNow)
            HitClickAudio.PlayPostureBreak();

        // Vuruş efekti (normal düşmanla aynı CombatVFXManager hattı).
        if (CombatVFXManager.Instance == null)
            Debug.LogWarning("TrainingDummy: sahnede CombatVFXManager yok, impact VFX oynatılamaz.");

        if (CombatVFXManager.Instance != null)
        {
            if (wasBroken)
                CombatVFXManager.Instance.PlayHealthHit(hitPosition, dir);
            else
                CombatVFXManager.Instance.PlayBalanceHit(hitPosition, dir);

            if (brokeNow)
                CombatVFXManager.Instance.PlayBalanceBreak(transform.position + Vector3.up * (TopY - transform.position.y) * 0.5f, dir);
        }

        Color numColor =
            brokeNow ? new Color(1f, 0.85f, 0.2f) :
            wasBroken ? new Color(1f, 0.45f, 0.35f) :
            crit ? new Color(1f, 0.6f, 0.15f) :
            Color.white;

        string label = amount.ToString();

        if (crit)
            label += "!";

        float size = crit ? 1.35f : (wasBroken ? 1.1f : 0.9f);

        CombatCallout.Popup(hitPosition + new Vector3(Random.Range(-0.15f, 0.15f), 0.2f, 0f), label, numColor, size);

        if (brokeNow)
        {
            CombatCallout.Popup(new Vector3(transform.position.x, TopY + 0.9f, 0f), "POSTURE KIRILDI", new Color(1f, 0.85f, 0.2f), 1.2f);
        }

        // Combo kilometre taşları
        if (Combo > 1 && Combo % 5 == 0)
            CombatCallout.Popup(new Vector3(transform.position.x, TopY + 1.4f, 0f), Combo + " HIT", new Color(0.6f, 0.9f, 1f), 1.0f);

        bool heavy = crit || brokeNow || comboStep >= 3;

        // Hitstop kaldırıldı: dummy'de vuruş donmadan, direkt kesiyormuş gibi hissettirir.

        // Su varsa vuruşta halka + damla.
        if (WaterSurface.Instance != null)
            WaterSurface.Instance.Impact(transform.position.x, heavy ? 1f : 0.5f);

        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(heavy ? cameraShake * 1.6f : cameraShake);

        RefreshText();
    }

    // =========================================================
    // SES
    // =========================================================

    private AudioSource audioSrc;

    private void SetupAudio()
    {
        audioSrc = GetComponent<AudioSource>();

        if (audioSrc == null)
            audioSrc = gameObject.AddComponent<AudioSource>();

        audioSrc.playOnAwake = false;
        audioSrc.loop = false;
        audioSrc.spatialBlend = 0f;

        bool need =
            (balanceHitSounds == null || balanceHitSounds.Length == 0) ||
            (healthHitSounds == null || healthHitSounds.Length == 0) ||
            balanceBreakSound == null;

        if (!need)
            return;

        // Sahnede / sonradan doğan düşmanlardan ödünç al.
        EnemyHitFeedback[] fbs =
            FindObjectsByType<EnemyHitFeedback>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < fbs.Length; i++)
        {
            EnemyHitFeedback f = fbs[i];

            if ((balanceHitSounds == null || balanceHitSounds.Length == 0) && f.BalanceHitSounds != null && f.BalanceHitSounds.Length > 0)
                balanceHitSounds = f.BalanceHitSounds;

            if ((healthHitSounds == null || healthHitSounds.Length == 0) && f.HealthHitSounds != null && f.HealthHitSounds.Length > 0)
                healthHitSounds = f.HealthHitSounds;

            if (balanceBreakSound == null && f.BalanceBreakSound != null)
                balanceBreakSound = f.BalanceBreakSound;

            if (f.Source != null && audioSrc.outputAudioMixerGroup == null)
                audioSrc.outputAudioMixerGroup = f.Source.outputAudioMixerGroup;
        }
    }

    private void PlaySfx(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
        {
            // Düşman sonradan sahneye girmiş olabilir: bir kez daha dene.
            if (Time.unscaledTime > nextAudioRetry)
            {
                nextAudioRetry = Time.unscaledTime + 2f;
                SetupAudio();
            }

            return;
        }

        PlaySfx(clips[Random.Range(0, clips.Length)]);
    }

    private float nextAudioRetry;

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null || audioSrc == null)
            return;

        audioSrc.PlayOneShot(clip, hitVolume);
    }

    // =========================================================
    // KAPSÜL SPRITE (yüksek çözünürlük, yumuşak kenar)
    // =========================================================

    private static Sprite capsuleSprite;

    public static Sprite GetCapsuleSprite()
    {
        if (capsuleSprite != null)
            return capsuleSprite;

        const int W = 256;
        const int H = 512;

        Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.name = "DummyCapsule";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] px = new Color[W * H];

        float r = W * 0.5f - 3f;            // yarıçap (kenarda AA payı)
        float cx = W * 0.5f;
        float y0 = r + 3f;                  // alt yarım daire merkezi
        float y1 = H - r - 3f;              // üst yarım daire merkezi

        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float cy = Mathf.Clamp(y, y0, y1);
                float dx = x + 0.5f - cx;
                float dy = y + 0.5f - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha = Mathf.Clamp01(r - d + 0.5f);

                // Hafif silindir gölgesi (sol parlak, sağ koyu) + ince koyu kenar.
                float nx = Mathf.Clamp(dx / r, -1f, 1f);
                float shade = 1f - 0.22f * (nx * 0.5f + 0.5f);
                float rim = Mathf.SmoothStep(0.9f, 1f, d / r);
                shade *= 1f - 0.18f * rim;

                px[y * W + x] = new Color(shade, shade, shade, alpha);
            }
        }

        tex.SetPixels(px);
        tex.Apply(false, true);

        // 1 x 2 birimlik sprite (ApplySize bunu size'a ölçekler).
        capsuleSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), W);

        return capsuleSprite;
    }

    // =========================================================
    // GÜNCELLEME
    // =========================================================

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float now = Time.time;

        // Yaylı sarsılma: vuruş yönünde kayıp yerine döner.
        Vector2 force = -wobbleOffset * 220f - wobbleVel * 16f;
        wobbleVel += force * dt;
        wobbleOffset += wobbleVel * dt;

        transform.position = homePos + (Vector3)wobbleOffset;

        // Squash & stretch
        squash = Mathf.MoveTowards(squash, 0f, dt * 5f);
        transform.localScale = new Vector3(
            homeScale.x * (1f + squash * 0.14f),
            homeScale.y * (1f - squash * 0.12f),
            homeScale.z
        );

        // Flaş + kırık tonu
        bool flash = Time.unscaledTime < flashUntil;
        bool broken = IsBroken;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || renderers[i].transform == barFill || renderers[i].transform == barRoot)
                continue;

            if (flash)
                renderers[i].color = Color.white;
            else if (broken)
                renderers[i].color = Color.Lerp(baseColors[i], new Color(1f, 0.85f, 0.25f), 0.55f);
            else
                renderers[i].color = baseColors[i];
        }

        // Posture azalması
        if (!broken && now - lastHitTime > postureRegenDelay && Posture > 0f)
            Posture = Mathf.Max(0f, Posture - postureRegenPerSecond * dt);

        if (!broken && Posture >= maxPosture && now > brokenUntil)
            Posture = 0f; // kırık süresi bitti, yeniden başla

        // Combo süresi doldu
        if (Combo > 0 && now - lastHitTime > comboResetTime)
            Combo = 0;

        // DPS penceresi
        while (samples.Count > 0 && now - samples[0].time > dpsWindow)
            samples.RemoveAt(0);

        UpdateBar();

        if (Time.unscaledTime >= nextTextUpdate)
        {
            nextTextUpdate = Time.unscaledTime + 0.1f;
            RefreshText();
        }

    }

    public void ResetStats()
    {
        Combo = 0;
        LastDamage = 0;
        TotalDamage = 0;
        Posture = 0f;
        brokenUntil = -999f;
        samples.Clear();
        RefreshText();
    }

    private float CurrentDps()
    {
        if (samples.Count == 0)
            return 0f;

        int sum = 0;

        for (int i = 0; i < samples.Count; i++)
            sum += samples[i].dmg;

        float span = Mathf.Clamp(Time.time - samples[0].time, 1f, dpsWindow);

        return sum / span;
    }

    // =========================================================
    // HUD (dummy'nin üstünde, dünya uzayında)
    // =========================================================

    // Collider'ın üst ucu (dummy'nin boyutu ne olursa olsun HUD üstünde durur).
    private float TopY
    {
        get
        {
            Collider2D c = GetComponentInChildren<Collider2D>();
            return c != null ? c.bounds.max.y : transform.position.y + 2f;
        }
    }

    private void BuildHud()
    {
        GameObject root = new GameObject("DummyHud");
        root.transform.SetParent(transform, false);
        root.transform.position = new Vector3(transform.position.x, TopY + 0.45f, 0f);

        // Dummy ölçeklense bile HUD sabit kalsın.
        Vector3 s = transform.lossyScale;
        root.transform.localScale = new Vector3(
            s.x != 0f ? 1f / s.x : 1f,
            s.y != 0f ? 1f / s.y : 1f,
            1f
        );

        statsText = CombatCallout.CreateText(root.transform, "", Color.white, 0.07f, 0, 700);
        statsText.transform.localPosition = new Vector3(0f, 0.55f, 0f);

        barRoot = new GameObject("PostureBar").transform;
        barRoot.SetParent(root.transform, false);
        barRoot.localPosition = Vector3.zero;

        Transform back = MakeQuad("Back", barRoot, new Color(0f, 0f, 0f, 0.7f), 1.5f, 0.16f, 690).transform;
        back.localPosition = Vector3.zero;

        SpriteRenderer fill = MakeQuad("Fill", barRoot, new Color(0.95f, 0.8f, 0.3f), 1.5f, 0.16f, 691);
        barFill = fill.transform;
        barFillSr = fill;
        barFill.localPosition = new Vector3(-0.75f, 0f, 0f);
        barFill.localScale = new Vector3(0f, 1f, 1f);
    }

    private SpriteRenderer MakeQuad(string name, Transform parent, Color color, float w, float h, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetWhiteSprite();
        sr.color = color;
        sr.sortingOrder = order;

        // pivot sol-ortada olduğu için ölçek = genişlik/yükseklik
        go.transform.localScale = new Vector3(w, h, 1f);

        return sr;
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null)
            return whiteSprite;

        Texture2D tex = Texture2D.whiteTexture;

        // Pivot SOL-orta: çubuk soldan dolsun.
        whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0f, 0.5f), tex.width);

        return whiteSprite;
    }

    private void UpdateBar()
    {
        if (barFill == null)
            return;

        float t = maxPosture > 0f ? Mathf.Clamp01(Posture / maxPosture) : 0f;

        barFill.localScale = new Vector3(1.5f * t, 0.16f, 1f);

        if (barFillSr != null)
            barFillSr.color = IsBroken
                ? new Color(1f, 0.35f, 0.25f)
                : Color.Lerp(new Color(0.95f, 0.8f, 0.3f), new Color(1f, 0.5f, 0.2f), t);
    }

    private void RefreshText()
    {
        if (statsText == null)
            return;

        if (!showStatsText)
        {
            statsText.text = "";
            return;
        }

        statsText.text =
            "COMBO x" + Combo + "\n" +
            "SON " + LastDamage + "   TOPLAM " + TotalDamage + "\n" +
            "DPS " + CurrentDps().ToString("0.0") + (IsBroken ? "\nKIRIK" : "");
    }

    // Çubuğun yerleşimi için pivot (bar sol-orta).
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(new Vector3(transform.position.x, TopY + 0.45f, 0f), 0.1f);
    }
}
