using UnityEngine;

/// <summary>
/// KLAVYE TIKI VURUŞ SESLERİ: Resources/HitClicks içindeki klavye basış kayıtlarından rastgele çalar.
///   hit_XX  : oyuncu düşmana vurunca
///   hurt_XX : oyuncu hasar alınca (daha pes, biraz daha güçlü)
/// Oyun başlayınca kendiliğinden oluşur; sahneye elle eklemen gerekmez.
/// </summary>
public class HitClickAudio : MonoBehaviour
{
    public enum HitSet { Keyboard, Fire, Both, Ice, Impact }

    [Header("Düşmana vuruş")]
    [Tooltip("Cana vuruş sesi: klavye / ateş çıtırtısı / ikisi karışık.")]
    public HitSet hitSet = HitSet.Impact;
    public bool enableHit = true;
    [Range(0f, 1f)] public float hitVolume = 1f;
    public Vector2 hitPitch = new Vector2(0.62f, 0.74f);
    [Tooltip("Buz catirtisi (ice_XX) icin pitch.")]
    public Vector2 icePitch = new Vector2(0.92f, 1.06f);

    [Tooltip("Impact seti: Resources/HitSfx (temizlenmis vurus sesleri) icin pitch.")]
    public Vector2 impactPitch = new Vector2(0.97f, 1.03f);
    [Range(0f, 1.5f)] public float impactVolume = 1f;

    [Header("Posture (denge) vuruşu - tahta adım sesleri")]
    [Tooltip("Acik: armora (posture) vururken tahta adim yerine HitSfx vurus sesi calar.")]
    public bool postureUsesImpact = true;
    public bool enableStep = true;
    [Range(0f, 1f)] public float stepVolume = 1f;
    public Vector2 stepPitch = new Vector2(0.93f, 1.06f);

    [Header("Posture kırılma sesi (luggage drop, kalınlaştırılmış)")]
    public bool enableBreak = true;
    [Range(0f, 1f)] public float breakVolume = 1f;
    public Vector2 breakPitch = new Vector2(0.97f, 1.03f);

    [Header("Hasar alma (oyuncu canı)")]
    public bool enableHurt = false;   // simdilik sessiz
    [Range(0f, 1f)] public float hurtVolume = 1f;
    public Vector2 hurtPitch = new Vector2(0.8f, 0.92f);

    [Tooltip("Tüm klavye seslerine ek çarpan (1 üstü = daha yüksek, klip sınırında kırpılabilir).")]
    [Range(0f, 3f)] public float masterBoost = 1.6f;

    [Tooltip("Aynı karede birden çok düşmana vurulursa sesler üst üste binmesin (sn).")]
    public float minInterval = 0.045f;

    // Ates varyasyonlarindan sadece TOK/KALIN olanlar (500 Hz alti enerjisi >%78); ince olanlar elendi.
    static readonly string[] ThickFire = { "fire_05", "fire_10", "fire_08", "fire_07" };

    AudioClip[] hit, hurt, fire, both, step, brk, ice, impact;
    AudioSource src;
    int lastIce = -1, lastBrk = -1, lastStep = -1, lastFire = -1, lastBoth = -1, lastHit = -1, lastHurt = -1, lastImpact = -1;
    float lastT = -10f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindAnyObjectByType<HitClickAudio>() != null)
            return;

        var go = new GameObject("HitClickAudio");
        DontDestroyOnLoad(go);
        go.AddComponent<HitClickAudio>();
    }

    void Awake()
    {
        instance = this;
        // Klasörün tamamını yükleyip ada göre ayır.
        var all = Resources.LoadAll<AudioClip>("HitClicks");
        var h = new System.Collections.Generic.List<AudioClip>();
        var f = new System.Collections.Generic.List<AudioClip>();
        var st = new System.Collections.Generic.List<AudioClip>();
        var bk = new System.Collections.Generic.List<AudioClip>();
        var u = new System.Collections.Generic.List<AudioClip>();
        var ic = new System.Collections.Generic.List<AudioClip>();

        foreach (var c in all)
        {
            if (c.name.StartsWith("hit_")) h.Add(c);
            else if (c.name.StartsWith("break_")) bk.Add(c);
            else if (c.name.StartsWith("step_")) st.Add(c);
            else if (c.name.StartsWith("fire_") && System.Array.IndexOf(ThickFire, c.name) >= 0) f.Add(c);
            else if (c.name.StartsWith("hurt_")) u.Add(c);
            else if (c.name.StartsWith("ice_")) ic.Add(c);
        }

        impact = Resources.LoadAll<AudioClip>("HitSfx");
        ice = ic.ToArray();
        brk = bk.ToArray();
        step = st.ToArray();
        fire = f.ToArray();
        var b = new System.Collections.Generic.List<AudioClip>(h); b.AddRange(f); both = b.ToArray();
        hit = h.ToArray(); hurt = u.ToArray();

        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
    }

    void OnEnable()
    {
        CombatEvents.EnemyHit += OnEnemyHit;
        CombatEvents.PlayerDamaged += OnPlayerDamaged;
    }

    void OnDisable()
    {
        CombatEvents.EnemyHit -= OnEnemyHit;
        CombatEvents.PlayerDamaged -= OnPlayerDamaged;
    }

    static HitClickAudio instance;

    /// <summary>Training dummy gibi EnemyBase olmayan hedefler için: cana vuruş sesi.</summary>
    public static void PlayEnemyHealthHit()
    {
        if (instance != null) instance.PlayHealthHitSound();
    }

    /// <summary>Posture kırılma anı: düşman ya da dummy.</summary>
    public static void PlayPostureBreak()
    {
        if (instance != null && instance.enableBreak)
            instance.Play(instance.brk, ref instance.lastBrk, instance.breakVolume, instance.breakPitch, true);
    }

    public static void PlayEnemyPostureHit()
    {
        if (instance != null && instance.enableStep)
            instance.PlayPostureHit();
    }

void PlayPostureHit()
    {
        if (postureUsesImpact && impact != null && impact.Length > 0) Play(impact, ref lastImpact, impactVolume, impactPitch, false, false);
        else Play(step, ref lastStep, stepVolume, stepPitch);
    }

void PlayHealthHitSound()
    {
        if (enableHit) PlayHit();
    }

    void PlayHit()
    {
        if (hitSet == HitSet.Impact && impact != null && impact.Length > 0) { Play(impact, ref lastImpact, impactVolume, impactPitch, false, false); return; }
        if (hitSet == HitSet.Ice && ice != null && ice.Length > 0) Play(ice, ref lastIce, hitVolume, icePitch);
        else if (hitSet == HitSet.Fire) Play(fire, ref lastFire, hitVolume, hitPitch);
        else if (hitSet == HitSet.Both) Play(both, ref lastBoth, hitVolume, hitPitch);
        else if (hitSet == HitSet.Ice) Play(fire, ref lastFire, hitVolume, hitPitch);   // ice klipleri yoksa eski ses
        else Play(hit, ref lastHit, hitVolume, hitPitch);
    }

void OnEnemyHit(EnemyBase e, DamageInfo d, HitResult r)
    {
        // Posture (denge) vuruşu: tahta adım sesi.
        if (r.hit && r.onBalance && r.amount > 0)
        {
            if (enableStep && !r.brokeBalance) PlayPostureHit(); // kırılma anında break sesi çalar
            return;
        }

        // CANA vurunca: eski can vuruş sesi (hitSet: ateş / klavye / buz).
        if (!r.hit || r.onBalance || r.amount <= 0) return;
        if (enableHit) PlayHit();
    }

    void OnPlayerDamaged(PlayerDamageReport rep)
    {
        if (!enableHurt || rep.amount <= 0) return;
        Play(hurt, ref lastHurt, hurtVolume, hurtPitch);
    }

    void Play(AudioClip[] set, ref int last, float vol, Vector2 pitch, bool force = false, bool boost = true)
    {
        if (set == null || set.Length == 0 || src == null) return;
        if (!force && Time.unscaledTime - lastT < minInterval) return;
        lastT = Time.unscaledTime;

        int i = Random.Range(0, set.Length);
        if (set.Length > 1 && i == last) i = (i + 1 + Random.Range(0, set.Length - 1)) % set.Length;
        last = i;

        src.pitch = Random.Range(pitch.x, pitch.y);
        src.PlayOneShot(set[i], boost ? vol * masterBoost : vol);
    }
}
