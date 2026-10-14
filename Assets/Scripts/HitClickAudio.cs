using UnityEngine;

/// <summary>
/// KLAVYE TIKI VURUŞ SESLERİ: Resources/HitClicks içindeki klavye basış kayıtlarından rastgele çalar.
///   hit_XX  : oyuncu düşmana vurunca
///   hurt_XX : oyuncu hasar alınca (daha pes, biraz daha güçlü)
/// Oyun başlayınca kendiliğinden oluşur; sahneye elle eklemen gerekmez.
/// </summary>
public class HitClickAudio : MonoBehaviour
{
    public enum HitSet { Keyboard, Fire, Both }

    [Header("Düşmana vuruş")]
    [Tooltip("Cana vuruş sesi: klavye / ateş çıtırtısı / ikisi karışık.")]
    public HitSet hitSet = HitSet.Fire;
    public bool enableHit = true;
    [Range(0f, 1f)] public float hitVolume = 1f;
    public Vector2 hitPitch = new Vector2(0.78f, 0.9f);

    [Header("Posture (denge) vuruşu - tahta adım sesleri")]
    public bool enableStep = true;
    [Range(0f, 1f)] public float stepVolume = 1f;
    public Vector2 stepPitch = new Vector2(0.8f, 0.95f);

    [Header("Hasar alma (oyuncu canı)")]
    public bool enableHurt = true;
    [Range(0f, 1f)] public float hurtVolume = 1f;
    public Vector2 hurtPitch = new Vector2(0.8f, 0.92f);

    [Tooltip("Tüm klavye seslerine ek çarpan (1 üstü = daha yüksek, klip sınırında kırpılabilir).")]
    [Range(0f, 3f)] public float masterBoost = 1.6f;

    [Tooltip("Aynı karede birden çok düşmana vurulursa sesler üst üste binmesin (sn).")]
    public float minInterval = 0.045f;

    AudioClip[] hit, hurt, fire, both, step;
    AudioSource src;
    int lastStep = -1, lastFire = -1, lastBoth = -1, lastHit = -1, lastHurt = -1;
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
        var u = new System.Collections.Generic.List<AudioClip>();

        foreach (var c in all)
        {
            if (c.name.StartsWith("hit_")) h.Add(c);
            else if (c.name.StartsWith("step_")) st.Add(c);
            else if (c.name.StartsWith("fire_")) f.Add(c);
            else if (c.name.StartsWith("hurt_")) u.Add(c);
        }

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

    /// <summary>Training dummy gibi EnemyController olmayan hedefler için: cana vuruş sesi.</summary>
    public static void PlayEnemyHealthHit()
    {
        if (instance != null) instance.PlayHealthHitSound();
    }

    public static void PlayEnemyPostureHit()
    {
        if (instance != null && instance.enableStep)
            instance.Play(instance.step, ref instance.lastStep, instance.stepVolume, instance.stepPitch);
    }

    void PlayHealthHitSound()
    {
        if (!enableHit) return;
        if (hitSet == HitSet.Fire) Play(fire, ref lastFire, hitVolume, hitPitch);
        else if (hitSet == HitSet.Both) Play(both, ref lastBoth, hitVolume, hitPitch);
        else Play(hit, ref lastHit, hitVolume, hitPitch);
    }

    void OnEnemyHit(EnemyController e, DamageInfo d, HitResult r)
    {
        // Posture (denge) vuruşu: tahta adım sesi.
        if (r.hit && r.onBalance && r.amount > 0)
        {
            if (enableStep) Play(step, ref lastStep, stepVolume, stepPitch);
            return;
        }

        if (!enableHit) return;
        // Sadece CANA vurunca çalsın; posture (denge) vuruşunda çalmasın.
        if (!r.hit || r.onBalance || r.amount <= 0) return;
        if (hitSet == HitSet.Fire) Play(fire, ref lastFire, hitVolume, hitPitch);
        else if (hitSet == HitSet.Both) Play(both, ref lastBoth, hitVolume, hitPitch);
        else Play(hit, ref lastHit, hitVolume, hitPitch);
    }

    void OnPlayerDamaged(PlayerDamageReport rep)
    {
        if (!enableHurt || rep.amount <= 0) return;
        Play(hurt, ref lastHurt, hurtVolume, hurtPitch);
    }

    void Play(AudioClip[] set, ref int last, float vol, Vector2 pitch)
    {
        if (set == null || set.Length == 0 || src == null) return;
        if (Time.unscaledTime - lastT < minInterval) return;
        lastT = Time.unscaledTime;

        int i = Random.Range(0, set.Length);
        if (set.Length > 1 && i == last) i = (i + 1 + Random.Range(0, set.Length - 1)) % set.Length;
        last = i;

        src.pitch = Random.Range(pitch.x, pitch.y);
        src.PlayOneShot(set[i], vol * masterBoost);
    }
}
