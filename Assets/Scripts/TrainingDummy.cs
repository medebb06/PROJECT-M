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

    // Collider'ı ve "Body" görselini 'size' değerine göre ayarlar.
    [ContextMenu("Boyutu Uygula")]
    public void ApplySize()
    {
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

        HitStop.Request(heavy ? hitStopHeavy : hitStopNormal);

        // Su varsa vuruşta halka + damla.
        if (WaterSurface.Instance != null)
            WaterSurface.Instance.Impact(transform.position.x, heavy ? 1f : 0.5f);

        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(heavy ? cameraShake * 1.6f : cameraShake);

        RefreshText();
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

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            ResetStats();
#endif
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
