using System.Collections;
using UnityEngine;

/// <summary>
/// Boss1'i GERCEK bir dusmana cevirir: oyuncuyu kovalar, kilicla vurur, oyuncu parry/block yapabilir,
/// oyuncu boss'a hasar verir (denge -> sersemleme -> can).
///
/// Mevcut dusman sistemini AYNEN kullanir (yeni bir savas sistemi yazmaz):
///   EnemyController (durumlar: Idle/Chase/Attack/Hit/Stagger/Execute) + Health + EnemyBalance.
/// Bu script sadece onlari kurar, boss'a gore ayarlar ve Boss1Rig animasyonuna baglar:
///   - Saldiri uyarisi = Boss1Rig'in gerilme animasyonu; hasar anı = kilicin yere vurdugu an.
///   - Parry / vurus / sersemleme / olum -> Boss1Rig tepkileri.
///
/// KURULUM: Gerekmez. Boss1Rig.makeEnemy acikken (varsayilan) oyun baslayinca otomatik eklenir.
/// Degerleri Inspector'dan ayarlamak istersen Boss1 objesine bu script'i elle ekle (Add Component > Boss1Enemy).
/// </summary>
[DefaultExecutionOrder(-80)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Boss1Rig))]
public class Boss1Enemy : MonoBehaviour
{
    [Header("Can / denge")]
    [Tooltip("Boss'un cani. Normal dusman ~300. Duz vurus cana ~1 hasar verir; asil kayip denge KIRILINCA (%15 can) ve infazla gelir.")]
    [Min(1)] public int maxHealth = 2000;
    [Tooltip("Denge (posture) cubugu. Parry denge doldurur; dolunca boss sersemler (infaz firsati).")]
    [Min(1)] public int maxBalance = 240;
    [Tooltip("Parry'nin boss dengesine hasari (oyun bunu x0.5 ve cana gore x0.6-1.6 ile olcekler: ~18 = denge ~13 parry, can azaldikca daha az).")]
    [Min(1)] public int parryBalanceDamage = 170;
    [Tooltip("Denge kirilinca boss'un sersemleme suresi (sn).")]
    [Min(0.5f)] public float staggerDuration = 3.5f;

    [Header("Hareket (dunya birimi)")]
    [Tooltip("Yurume hizi (birim/sn). Agir olsun diye yavas. Normal dusman: 10.")]
    [Min(0.5f)] public float walkSpeed = 8.5f;
    [Tooltip("Oyuncuyu bu mesafeden once fark eder (0 = hep avlar).")]
    [Min(0f)] public float chaseRange = 1000f;
    [Tooltip("Oyuncuya bu mesafeye kadar yaklasir, sonra vurur. Kilic darbesi gövdeden ~9.5 - 23.5 birim oteye iner.")]
    [Min(1f)] public float stopDistance = 15f;

    [Header("Saldiri")]
    [Tooltip("Vurusun one dogru erisimi (gövde merkezinden, birim). Kilic ucu ~23.5.")]
    [Min(1f)] public float attackReach = 24f;
    [Tooltip("Oyuncunun ayagi boss'un ayagindan bu kadar yukaridaysa vurustan kurtulur (ziplama). Kilic yerde yatiyor: yuksek tutuldu.")]
    [Min(0.5f)] public float hitHeight = 3.5f;
    [Tooltip("Vurustan sonra bir sonraki saldiriya kadar ek bekleme (sn). Animasyon (bekleme + toparlanma) buna eklenir.")]
    [Min(0f)] public float extraCooldown = 2.0f;
    [Tooltip("Sersemlemis (denge kirik) boss'a duz vurus basina max cana gore hasar yuzdesi. 0 = oyunun kendi degeri.")]
    [Range(0f, 0.2f)] public float staggerHitPercent = 0.03f;
    [Tooltip("Duz vuruslar boss'u bu yuzdenin altina dusuremez (infaz bitirsin). 0 = sinir yok.")]
    [Range(0f, 0.5f)] public float staggerHitFloorPercent = 0.12f;
    [Tooltip("Oyuncuya vurus hasari. (Oyuncu hasari oyunda 'Fixed Hit Percent' ile sabit; bu deger istatistik/olum icin.)")]
    [Min(1)] public int attackDamage = 20;
    [Tooltip("Oyuncuyu geri iten kuvvet.")]
    [Min(0f)] public float hitKnockback = 14f;
    [Tooltip("Acik: saldiri baslayinca oyuncu vuruslariyla kesilmez (boss agirligi).")]
    public bool superArmorWhileAttacking = true;

    [Tooltip("Sersemlemis boss'a infazin cani yuzde kac gotursun.")]
    [Range(0.01f, 0.5f)] public float executeHealthPercent = 0.12f;

    [Header("Arayuz")]
    [Tooltip("Ekranin ust ortasinda ince kirmizi can cizgisi + altinda sari posture cizgisi.")]
    public bool showHud = true;

    [Header("Fizik")]
    [Tooltip("Boss kutlesi (oyuncu itemesin).")]
    [Min(1f)] public float mass = 1000f;
    [Tooltip("Yercekimi. Normal dusmanla ayni.")]
    public float gravityScale = 3.5f;
    [Tooltip("Oyuncu vurunca boss'un savrulmasi (normal dusmana gore carpan). 0.1 = neredeyse hic.")]
    [Range(0f, 1f)] public float knockbackScale = 0.12f;
    [Tooltip("Sersemleyen boss'un infaz mesafesi (gövde buyuk oldugu icin normal dusmandan uzak).")]
    [Min(0.5f)] public float executeDistance = 5f;

    EnemyController enemy;
    Boss1Rig rig;
    Health health;
    Rigidbody2D rb;
    SpriteRenderer facingProxy;
    float startY;
    Boss1Hud hud;

    void Awake()
    {
        rig = GetComponent<Boss1Rig>();
        rig.EnsureBuilt();                       // katmanlar var olsun (Health/EnemyController sprite'lari toplar)

        // 1) Antrenman kuklasi: oyuncunun vurusunu yutar, asla olmez. Boss artik gercek -> kaldir.
        TrainingDummy dummy = GetComponent<TrainingDummy>();
        if (dummy != null)
            DestroyImmediate(dummy);

        // 2) Fizik govdesi.
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = gravityScale;
        rb.mass = mass;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;

        // 3) Can + denge (EnemyController.Awake bunlari arar: ondan ONCE eklenir).
        health = GetComponent<Health>();
        if (health == null)
            health = gameObject.AddComponent<Health>();
        health.SetMaxHealth(maxHealth, true);

        EnemyBalance balance = GetComponent<EnemyBalance>();
        if (balance == null)
            balance = gameObject.AddComponent<EnemyBalance>();
        balance.SetMaxBalance(maxBalance);
        balance.SetRecovery(postureRecoveryDelay, postureRecoverySpeed);

        // 4) Yon gostergesi: EnemyController yonu bir SpriteRenderer.flipX'ten okur/yazar.
        //    Gorunmez bir yardimci SpriteRenderer kullaniyoruz; Boss1Rig bunu okuyup aynalar.
        GameObject proxyGo = new GameObject("FacingProxy");
        proxyGo.transform.SetParent(transform, false);
        facingProxy = proxyGo.AddComponent<SpriteRenderer>();
        facingProxy.enabled = false;
        facingProxy.flipX = true;                // rig dogal olarak SOLA bakar
        rig.facingSource = facingProxy;

        // 5) Dusman beyni.
        CloneSoundComponents();                  // EnemyAttackState kurucusu bunlari arar: EnemyController'dan ONCE

        enemy = GetComponent<EnemyController>();
        if (enemy == null)
            enemy = gameObject.AddComponent<EnemyController>();

        CloneEnemyClips();

        EnemyHitFeedback fb = GetComponent<EnemyHitFeedback>();
        parryFallback = fb == null || fb.ParrySound == null;
        Debug.Log("[Boss1] Ses kopyasi: kaynak=" + (GetSoundTemplate() != null ? GetSoundTemplate().name : "YOK") +
                  " hitFeedback=" + (fb != null) + " parrySound=" + (fb != null && fb.ParrySound != null) +
                  " attackAudio=" + (GetComponent<EnemyAttackAudio>() != null) +
                  " vfxManager=" + (CombatVFXManager.Instance != null));

        enemy.SetFacingRenderer(facingProxy);
        Configure();

        enemy.AttackAnimationRequested += OnAttackAnimationRequested;
        enemy.AttackHitGate = rig.ReadyToHit;

        if (roarClips == null || roarClips.Length == 0)
            roarClips = Resources.LoadAll<AudioClip>("Boss1/Sfx");
        rig.AttackHit += OnStrikeLanded;    // kilic inmeden hasar yok

        if (showHud)
            hud = Boss1Hud.Create(health, balance);
    }

    // ---------------------------------------------------------------------
    // SES / EFEKT: parry, blok, vurus, uyari sesleri normal dusmandaki bilesenlerde durur.
    // Boss1 kodla kuruldugu icin bunlari bir ornek dusmandan (Duelist prefab) kopyalariz.
    // ---------------------------------------------------------------------
    [Header("Ses")]
    [Tooltip("Ses/efekt bilesenleri (EnemyHitFeedback, EnemyAttackAudio) buradan kopyalanir. Bos ise Resources/Endless/Duelist.")]
    public GameObject soundSource;

    [Header("Kukreme sesleri")]
    [Tooltip("Bos ise Resources/Boss1/Sfx altindaki tum klipler kullanilir. Her darbede rastgele biri calinir.")]
    public AudioClip[] roarClips;
    [Range(0f, 1f)] public float roarVolume = 0.5f;
    [Range(0f, 0.15f)] public float roarPitchVariance = 0.04f;
    [Header("Saldiri sarsintisi")]
    [Tooltip("Kilic yere indigi anda kamera sarsintisi (0 = kapali).")]
    [Range(0f, 6f)] public float attackShake = 2.5f;
    [Tooltip("Dogrudan kamera titretme carpani (dunya birimi / attackShake). 0 = yalniz Cinemachine impulse.")]
    [Range(0f, 2f)] public float attackShakeWorld = 0.5f;

    AudioSource roarSource;
    int lastRoar = -1;

    void OnStrikeLanded()
    {
        if (attackShake <= 0f) return;
        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(attackShake);
        RunCameraZoom.AddShake(attackShake * attackShakeWorld);
    }

    // Saldiri animasyonu bitene kadar yerinde dur (sersemlemis/vurulmus degilse).
    void LateUpdate()
    {
        if (rb != null && rig != null && rig.IsAttacking && !enemy.IsStaggered && !enemy.IsDead)
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    float PlayRoar()
    {
        if (roarClips == null || roarClips.Length == 0) return 0f;
        int i = Random.Range(0, roarClips.Length);
        if (roarClips.Length > 1 && i == lastRoar) i = (i + 1 + Random.Range(0, roarClips.Length - 1)) % roarClips.Length;
        lastRoar = i;

        // Bagimsiz 2D kaynak: boss yok edilse bile ses sonuna kadar calar.
        GameObject go = new GameObject("Boss1Roar");
        AudioSource src = go.AddComponent<AudioSource>();
        src.spatialBlend = 0f;
        src.playOnAwake = false;
        src.pitch = 1f + Random.Range(-roarPitchVariance, roarPitchVariance);
        src.volume = roarVolume;
        src.clip = roarClips[i];
        src.Play();
        Destroy(go, roarClips[i].length + 0.5f);
        return roarClips[i].length;
    }

    // ---------------------------------------------------------------------
    // GIRIS SAHNESI (Dead Cells tarzi): oyuncu kilitlenir, sinematik seritler + kamera boss'a kayar/yaklasir,
    // boss kukrer, sonra oyun baslar.
    // ---------------------------------------------------------------------
    [Header("Giris sahnesi")]
    public bool introCutscene = true;
    [Tooltip("Oyuncu bu kadar yaklasinca (ya da boss ekrana girince) giris sahnesi baslar.")]
    [Min(1f)] public float introDistance = 45f;
    [Tooltip("Kamera boss'a yerlesirken kukremeden once bekleme (sn).")]
    [Min(0f)] public float introSettle = 1.4f;
    [Tooltip("Kukreme bitince sahnenin devam suresi (sn).")]
    [Min(0f)] public float introHold = 0.9f;
    [Tooltip("Kukremeyi beklerken en fazla sure (sn).")]
    [Min(0.5f)] public float introMaxRoar = 3.5f;
    [Tooltip("Kameranin odaklandigi nokta boss'un ayagindan bu kadar yukarida.")]
    public float introFocusHeight = 7f;

    bool introActive;
    Transform introFocus;
    bool introPrevForce, introPrevCan;
    PlayerController introPlayer;

    IEnumerator IntroRoutine(PlayerController pl)
    {
        introActive = true;
        introPlayer = pl;
        introPrevForce = RunCameraZoom.ForceCinematic;
        introPrevCan = pl.canControl;

        if (introFocus == null)
        {
            GameObject f = new GameObject("IntroFocus");
            f.transform.SetParent(transform, false);
            introFocus = f.transform;
        }
        introFocus.position = transform.position + Vector3.up * introFocusHeight;

        RunCameraZoom.ForceCinematic = true;
        RunCameraZoom.FocusTarget = introFocus;

        yield return IntroWait(introSettle);

        float len = PlayRoar();
        rig.Flinch(1.8f);
        rig.Squash(2.2f);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(1.4f);
        RunCameraZoom.AddShake(1.2f);

        yield return IntroWait(Mathf.Min(len, introMaxRoar) + introHold);

        EndIntro();
    }

    IEnumerator IntroWait(float seconds)
    {
        float end = Time.unscaledTime + seconds;
        while (Time.unscaledTime < end)
        {
            HoldPlayer();
            yield return null;
        }
    }

    void HoldPlayer()
    {
        if (introPlayer == null) return;
        introPlayer.canControl = false;
        introPlayer.moveInput = 0f;
        if (introPlayer.rb != null) introPlayer.rb.linearVelocity = new Vector2(0f, introPlayer.rb.linearVelocity.y);
    }

    void EndIntro()
    {
        if (!introActive) return;
        introActive = false;

        RunCameraZoom.FocusTarget = null;
        RunCameraZoom.ForceCinematic = introPrevForce;

        if (introPlayer != null && !MainMenu.Active)
            introPlayer.canControl = introPrevCan;

        if (introFocus != null)
            Destroy(introFocus.gameObject);
    }

    // Kukreme yalniz iki anda: oyuncunun gorusune ilk girdigi an + oldugu an.
    bool seenRoared, deathRoared;
    float nextSeenCheck;

    void CheckRoarTriggers()
    {
        if (!deathRoared && health != null && health.IsDead)
        {
            deathRoared = true;
            seenRoared = true;
            PlayRoar();
            return;
        }

        if (seenRoared || Time.time < nextSeenCheck) return;
        nextSeenCheck = Time.time + 0.2f;

        if (health != null && health.IsDead) return;

        PlayerController pl = FindFirstObjectByType<PlayerController>();
        if (pl == null) return;

        bool seen = Vector2.Distance(pl.transform.position, transform.position) <= introDistance;

        if (!seen)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                SpriteRenderer[] rs = GetComponentsInChildren<SpriteRenderer>();
                Bounds b = new Bounds(transform.position, Vector3.zero);
                bool any = false;
                foreach (SpriteRenderer r in rs)
                {
                    if (r == null || !r.enabled || r.sprite == null) continue;
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                seen = any && GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(cam), b);
            }
        }

        if (!seen) return;

        seenRoared = true;

        if (introCutscene)
            StartCoroutine(IntroRoutine(pl));
        else
            PlayRoar();
    }

    GameObject soundTemplate;

    [Header("Posture toparlanma (Sekiro gibi cok yavas)")]
    [Tooltip("Son posture hasarindan sonra toparlanma baslamadan bekleme (sn).")]
    [Min(0f)] public float postureRecoveryDelay = 5f;
    [Tooltip("Toparlanma hizi (puan/sn). Oyunun normal degeri 20.")]
    [Min(0f)] public float postureRecoverySpeed = 2f;

    [Header("Parry")]
    [Tooltip("Parry carpisma efektinin buyuklugu (1 = normal dusman).")]
    [Range(1f, 40f)] public float parryEffectScale = 10f;
    [Tooltip("Parry'de govde ezilme siddeti (squash/stretch).")]
    [Range(0f, 6f)] public float parrySquash = 3f;

    bool parryFallback;
    AudioSource parrySrc;

    // Bilesen kopyalanamadiysa parry sesi + carpisma efekti buradan (EnemyHitFeedback.PlayParry ile ayni cagrilar).
    void PlayParryFallback(bool sound, bool vfx)
    {
        GameObject t = GetSoundTemplate();
        EnemyHitFeedback tf = t != null ? t.GetComponent<EnemyHitFeedback>() : null;

        if (sound && tf != null && tf.ParrySound != null)
        {
            if (parrySrc == null)
            {
                parrySrc = gameObject.AddComponent<AudioSource>();
                parrySrc.playOnAwake = false;
                parrySrc.spatialBlend = 0f;
            }
            parrySrc.PlayOneShot(tf.ParrySound);
        }

        if (!vfx) return;

        Vector3 pos = ParryVFX.ContactPoint(transform);
        Vector2 dir = enemy != null && enemy.target != null
            ? ((Vector2)(enemy.target.position - transform.position)).normalized
            : Vector2.right;

        if (CombatVFXManager.Instance != null)
            CombatVFXManager.Instance.PlayParry(pos, dir);
        else
            ParryVFX.Play(pos, dir, "Default", 100);
    }

    GameObject GetSoundTemplate()
    {
        if (soundTemplate != null) return soundTemplate;

        soundTemplate = soundSource != null ? soundSource : Resources.Load<GameObject>("Endless/Duelist");

        // Yedek: yuklu herhangi bir dusman prefab'i / nesnesi (kendimiz haric).
        if (soundTemplate == null)
        {
            foreach (EnemyHitFeedback f in Resources.FindObjectsOfTypeAll<EnemyHitFeedback>())
            {
                if (f != null && f.transform.root != transform.root) { soundTemplate = f.gameObject; break; }
            }
        }

        return soundTemplate;
    }

    void CloneSoundComponents()
    {
        GameObject src = GetSoundTemplate();
        if (src == null)
        {
            Debug.LogWarning("[Boss1] Ses kaynagi bulunamadi (Resources/Endless/Duelist yok). Sound Source alanina bir dusman prefab'i ver.");
            return;
        }

        AudioSource srcAudio = src.GetComponent<AudioSource>();
        AudioSource main = GetComponent<AudioSource>();
        if (main == null) main = gameObject.AddComponent<AudioSource>();
        main.playOnAwake = false;
        if (srcAudio != null)
        {
            main.outputAudioMixerGroup = srcAudio.outputAudioMixerGroup;
            main.volume = srcAudio.volume;
            main.spatialBlend = srcAudio.spatialBlend;
            main.minDistance = srcAudio.minDistance;
            main.maxDistance = srcAudio.maxDistance;
            main.rolloffMode = srcAudio.rolloffMode;
        }

        AudioSource second = gameObject.AddComponent<AudioSource>();
        second.playOnAwake = false;
        second.loop = false;
        if (srcAudio != null)
        {
            second.outputAudioMixerGroup = srcAudio.outputAudioMixerGroup;
            second.spatialBlend = srcAudio.spatialBlend;
        }

        CopyComponent<EnemyHitFeedback>(src, main, second);
        CopyComponent<EnemyAttackAudio>(src, main, second);
    }

    void CopyComponent<T>(GameObject src, AudioSource main, AudioSource second) where T : Component
    {
        T from = src.GetComponent<T>();
        if (from == null || GetComponent<T>() != null) return;

        T to = gameObject.AddComponent<T>();
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(from), to);

        // Prefab'in kendi ses kaynaklarina / alt objelerine giden referanslari boss'unkilerle degistir.
        Transform srcRoot = src.transform.root;
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        foreach (var f in typeof(T).GetFields(flags))
        {
            if (typeof(AudioSource).IsAssignableFrom(f.FieldType))
            {
                bool balance = f.Name.ToLowerInvariant().Contains("balance");
                f.SetValue(to, balance ? second : main);
            }
            else if (typeof(Component).IsAssignableFrom(f.FieldType))
            {
                Component c = f.GetValue(to) as Component;
                if (c != null && c.transform.root == srcRoot) f.SetValue(to, null);
            }
        }
    }

    // EnemyController uzerindeki ses klipleri (committedHitClip, healthHitClip ...).
    void CloneEnemyClips()
    {
        GameObject src = GetSoundTemplate();
        if (src == null) return;

        EnemyController from = src.GetComponent<EnemyController>();
        if (from == null) return;

        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;
        foreach (var f in typeof(EnemyController).GetFields(flags))
        {
            if (f.FieldType == typeof(AudioClip))
            {
                AudioClip clip = f.GetValue(from) as AudioClip;
                if (clip != null && f.GetValue(enemy) == null) f.SetValue(enemy, clip);
            }
        }
    }

    void Configure()
    {
        // Hareket
        enemy.chaseSpeed = walkSpeed;
        enemy.chaseRange = chaseRange;
        enemy.alwaysHunt = chaseRange <= 0f;
        enemy.chaseStopDistance = stopDistance;
        enemy.useStandby = false;
        enemy.suppressFlash = true;              // rig katmanlari tek tek beyaza yanip sonuyordu: EnemyController flaslari kapali
        enemy.solidWhiteFlash = false;           // katmanli rig: duz beyaz silüet flasi garip 'outline' gibi gorunuyordu                // tek boss: sira bekleme yok

        // Saldiri
        enemy.attackRange = attackReach;
        enemy.normalAttackFrontOnly = true;
        enemy.normalAttackHitHeight = hitHeight;
        enemy.useUnblockableAttacks = false;     // tum vuruslar parry/block edilebilir
        enemy.lockFacingDuringAttack = true;     // vurus yonu kilitli: arkasina gecen vurulmaz
        enemy.attackDamage = attackDamage;
        enemy.attackKnockbackForce = hitKnockback;
        enemy.attackWarningTime = rig.ContactTime;
        enemy.attackRecoveryTime = rig.holdTime + rig.recoverTime + extraCooldown;
        enemy.attackCommitPoint = superArmorWhileAttacking ? 0.05f : 0.4f;

        // Denge / sersemleme
        enemy.parryBalanceDamage = parryBalanceDamage;
        enemy.staggerDuration = staggerDuration;
        enemy.executeDistance = executeDistance;
        enemy.executeDamage = Mathf.Max(1, Mathf.RoundToInt(maxHealth * executeHealthPercent));

        // Agir govde: vurulunca savrulmasin / kaymasin.
        enemy.balanceHitKnockbackForce *= knockbackScale;
        enemy.balanceHitKnockbackVerticalForce = 0f;
        enemy.postureKnockbackForce *= knockbackScale;
        enemy.postureKnockbackVerticalForce = 0f;
        enemy.healthKnockbackForce *= knockbackScale;
        enemy.healthKnockbackVerticalForce = 0f;
        enemy.attack1KnockbackForce *= knockbackScale;
        enemy.attack2KnockbackForce *= knockbackScale;
        enemy.attack3KnockbackForce *= knockbackScale;
        enemy.attack4KnockbackForce *= knockbackScale;
        enemy.blockKnockbackForce *= knockbackScale;
        enemy.blockKnockbackVerticalForce = 0f;

        // Kucuk dusman gostergeleri bu boyutta anlamsiz: sadece boss animasyonu uyari verir.
        enemy.showDangerIcon = false;
        enemy.showDangerZone = false;
    }

    void Start()
    {
        startY = transform.position.y;
    }

    void OnEnable()
    {
        CombatEvents.EnemyHit += OnEnemyHit;
        CombatEvents.ParrySucceeded += OnParry;
    }

    void OnDisable()
    {
        EndIntro();
        CombatEvents.EnemyHit -= OnEnemyHit;
        CombatEvents.ParrySucceeded -= OnParry;
    }

    void OnDestroy()
    {
        EndIntro();
        if (enemy != null)
            enemy.AttackAnimationRequested -= OnAttackAnimationRequested;
        if (rig != null)
            rig.AttackHit -= OnStrikeLanded;

        if (hud != null)
            Destroy(hud.gameObject);
    }

    // EnemyAttackState uyariyi baslatti -> kilic gerilme animasyonu.
    void OnAttackAnimationRequested()
    {
        rig.PlayAttack();
    }

    void OnEnemyHit(EnemyController e, DamageInfo info, HitResult result)
    {
        if (e != enemy)
            return;

        // Denge vurusu hafif, can vurusu agir sarsar.
        rig.Flinch(result.onBalance ? 0.6f : 1f);
        rig.Squash(result.onBalance ? 0.7f : 1f);
        rig.Flash();

        // Sersemlemis boss: duz vurus sabit bir yuzde goturur (ne 1 hasar, ne aninda olum).
        if (staggerHitPercent > 0f && enemy.IsStaggered && !result.onBalance && !result.killed
            && health != null && !health.IsDead)
        {
            int want = Mathf.RoundToInt(health.MaxHealth * staggerHitPercent) - result.amount;
            int floor = Mathf.RoundToInt(health.MaxHealth * staggerHitFloorPercent);
            int extra = Mathf.Min(want, health.CurrentHealth - floor);
            if (extra > 0)
                health.TakeDamage(extra);
        }
    }

    void OnParry(EnemyController e, bool brokeBalance)
    {
        if (e != enemy)
            return;

        rig.OnParried(brokeBalance);

        // EnemyHitFeedback.PlayParry bu olaydan hemen ONCE cagrildi; efekt olustuysa tekrar uretme.
        bool vfxMade = FindObjectsByType<ParryVFX>(FindObjectsSortMode.None).Length > 0;
        Debug.Log("[Boss1] Parry olayi: sesYedegi=" + parryFallback + " efektVar=" + vfxMade +
                  " vfxManager=" + (CombatVFXManager.Instance != null));

        if (parryFallback || !vfxMade)
            PlayParryFallback(parryFallback, !vfxMade);

        // Efekti buyut: yeni olusan ParryVFX'ler (olcegi hala 1) carpisma noktasinin yakininda.
        Vector3 contact = ParryVFX.ContactPoint(transform);
        foreach (ParryVFX v in FindObjectsByType<ParryVFX>(FindObjectsSortMode.None))
        {
            if (v.transform.localScale != Vector3.one) continue;
            if ((v.transform.position - contact).sqrMagnitude > 4f) continue;
            v.transform.localScale = Vector3.one * parryEffectScale;
        }

        rig.Squash(brokeBalance ? parrySquash * 1.4f : parrySquash);
    }

    void Update()
    {
        if (enemy == null)
            return;

        if (!MainMenu.Active)
            CheckRoarTriggers();

        if (hud != null)
            hud.show = seenRoared && !introActive;

        // Ana menu aciksa (oyuncu kilitli) boss beklesin: yoksa menu arkasinda oyuncuya vurur.
        if (MainMenu.Active || introActive)
        {
            if (enemy.enabled)
            {
                enemy.enabled = false;
                if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }

            rig.MoveAllowed = false;
            return;
        }

        if (!enemy.enabled && !(health != null && health.IsDead))
            enemy.enabled = true;

        // Uyari suresi = animasyonun darbeye kadar suresi (Inspector'dan degisirse senkron kalsin).
        enemy.attackWarningTime = rig.ContactTime;
        enemy.attackRecoveryTime = rig.holdTime + rig.recoverTime + extraCooldown;

        bool attacking = enemy.CurrentState is EnemyAttackState;

        rig.MoveAllowed = enemy.CurrentState is EnemyChaseState;
        rig.Staggered = enemy.IsStaggered;
        rig.Dead = enemy.IsDead;

        // Saldiri vurustan ONCE bozuldu (sersemledi, baska duruma gecti): animasyonu yumusakca birak.
        if (rig.IsAttacking && !attacking)
            rig.CancelAttack();

        // Guvenlik: zemin yoksa sonsuza kadar dusme.
        if (transform.position.y < startY - 60f && rb != null)
        {
            rb.position = new Vector2(rb.position.x, startY);
            rb.linearVelocity = Vector2.zero;
            Debug.LogWarning("Boss1Enemy: boss zeminden dustu, baslangic yuksekligine geri alindi. Altinda Collider var mi?");
        }
    }
}
