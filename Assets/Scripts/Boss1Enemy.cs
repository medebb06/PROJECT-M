using System.Collections;
using System.Collections.Generic;
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
    [Min(0.5f)] public float walkSpeed = 15f;
    [Tooltip("Oyuncuyu bu mesafeden once fark eder (0 = hep avlar).")]
    [Min(0f)] public float chaseRange = 5000f;
    [Tooltip("Oyuncuya bu mesafeye kadar yaklasir, sonra vurur. Kilic darbesi gövdeden ~9.5 - 23.5 birim oteye iner.")]
    [Min(1f)] public float stopDistance = 15f;

    [Header("Saldiri")]
    [Tooltip("Vurusun one dogru erisimi (gövde merkezinden, birim). Kilic ucu ~23.5.")]
    [Min(1f)] public float attackReach = 24f;
    [Tooltip("Oyuncunun ayagi boss'un ayagindan bu kadar yukaridaysa vurustan kurtulur (ziplama). Kilic yerde yatiyor: yuksek tutuldu.")]
    [Min(0.5f)] public float hitHeight = 3.5f;
    [Tooltip("Vurustan sonra bir sonraki saldiriya kadar ek bekleme (sn). Animasyon (bekleme + toparlanma) buna eklenir.")]
    [Min(0f)] public float extraCooldown = 1.2f;
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
        nextLeapTime = Time.time + leapFirstDelay;
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
        BuildComboMoveset();

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
        if (comboTinted && comboStepNow >= 2) comboEndAt = Time.time + 0.45f;
        if (attackShake <= 0f) return;
        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(attackShake);
        RunCameraZoom.AddShake(attackShake * attackShakeWorld);
    }

    // Saldiri animasyonu bitene kadar yerinde dur (sersemlemis/vurulmus degilse).
    void LateUpdate()
    {
        bool run = false;
        if (rb != null && rig != null && rig.IsAttacking && !leapActive && !enemy.IsStaggered && !enemy.IsDead)
        {
            // Kombo hazirligi sirasinda uzaktaki oyuncuya KOS (vurus menziline gelince dur).
            EnemyAttackState st = enemy.CurrentState as EnemyAttackState;
            if (comboRunEnabled && st != null && st.MoveName == ComboName && st.RemainingWindup > 0.05f && enemy.target != null)
            {
                float dx = enemy.target.position.x - transform.position.x;
                if (Mathf.Abs(dx) > stopDistance)
                {
                    rb.linearVelocity = new Vector2(Mathf.Sign(dx) * comboRunSpeed * EnemyTime.Scale, rb.linearVelocity.y);
                    run = true;
                }
            }
            if (!run)
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
        if (rig != null) rig.RunWhileAttacking = run;
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
    [Tooltip("Giriste ekranin ortasinda buyuk yazilan boss adi.")]
    public string bossName = "Mustafa Golem";
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

        if (hud != null) hud.ShowName(bossName);
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
        if (hud != null) hud.HideName();
        nextLeapTime = Time.time + leapFirstDelay;

        RunCameraZoom.FocusTarget = null;
        RunCameraZoom.ForceCinematic = introPrevForce;

        if (introPlayer != null && !MainMenu.Active)
            introPlayer.canControl = introPrevCan;

        if (introFocus != null)
            Destroy(introFocus.gameObject);
    }

    // ---------------------------------------------------------------------
    // ZIPLAMA SALDIRISI: gerilir, ekran disina ziplar, oyuncunun ustune isaret dusurur,
    // asiri hizli ustune iner. Parry/blok edilemez: sadece dash ile kacilir.
    // ---------------------------------------------------------------------
    [Header("Ziplama saldirisi")]
    public bool leapEnabled = true;
    [Tooltip("Giris sahnesinden / oyundan sonra ilk ziplamaya kadar en az sure (sn).")]
    [Min(0f)] public float leapFirstDelay = 4f;
    [Tooltip("Iki ziplama arasi en az sure (sn).")]
    [Min(1f)] public float leapCooldown = 6f;
    [Tooltip("Uzaktan ziplama icin en az yatay mesafe (0 = hep).")]
    [Min(0f)] public float leapMinDistance = 0f;
    [Min(1f)] public float leapMaxDistance = 90f;
    [Tooltip("Uygun mesafedeyken saniyede ziplama baslatma olasiligi.")]
    [Range(0f, 1f)] public float leapChancePerSecond = 0.35f;
    [Tooltip("Gerilme/cokme suresi (kilic yukari, govde ezik). Rig gerilmesinden (1.0 sn) kisa olmasin.")]
    [Min(0.3f)] public float leapCrouchTime = 1.1f;
    [Min(0.05f)] public float leapRiseTime = 0.22f;
    [Tooltip("KULLANILMIYOR: ekran disi sure artik izleme + kilit penceresinden (oyuncunun dash degerlerinden) hesaplanir.")]
    [Min(0.3f)] public float leapAirTime = 1.0f;
    [Tooltip("Ilk inis: isaret bu sure oyuncuyu izler, sonra SABITLENIR.")]
    [Min(0.1f)] public float leapTrackTime = 0.55f;
    [Tooltip("Ziplama bir kez odaklandiktan sonra art arda kac kez inis yapsin.")]
    [Range(1, 5)] public int leapSlamCount = 3;
    [Tooltip("2. ve 3. inislerde isaretin oyuncuyu izleme suresi (hizli hizli).")]
    [Min(0.1f)] public float leapChainTrackTime = 0.3f;
    [Tooltip("Iki inis arasi yerde bekleme (sn).")]
    [Min(0f)] public float leapChainGap = 0.08f;
    [Tooltip("Isaret kilitlendikten sonra oyuncunun tepki suresi (sn). Kilit penceresi = bu + dash suresi.")]
    [Min(0f)] public float leapReaction = 0.3f;
    [Tooltip("Yaricap = oyuncunun dash mesafesi x bu. 0.95: kilitlenince hemen dash atarsa ucu ucuna kurtulur.")]
    [Range(0.5f, 1.2f)] public float leapEdgeMargin = 0.95f;
    [Tooltip("Acik: yaricap/kilit suresi oyuncunun dash degerlerinden hesaplanir. Kapali: 'leapRadius' ve 'leapReaction' kullanilir.")]
    public bool leapAutoRadius = true;
    [Tooltip("Isaret serit kalinligi (dunya birimi).")]
    [Min(0.1f)] public float leapStripHeight = 0.9f;
    [Tooltip("Inis suresi: asiri hizli.")]
    [Min(0.04f)] public float leapFallTime = 0.1f;
    [Min(0.1f)] public float leapLandTime = 0.6f;
    [Tooltip("Inis hasar yaricapi (dunya birimi, yatay).")]
    [Min(1f)] public float leapRadius = 14f;
    [Tooltip("Oyuncunun ayagi zeminden bu kadar yukaridaysa inis ona degmez.")]
    [Min(0.5f)] public float leapHitHeight = 6f;
    [Tooltip("Ekran disi yukseklik (oyuncunun ustunde, dunya birimi).")]
    [Min(20f)] public float leapApexAbove = 60f;
    [Range(0f, 10f)] public float leapShake = 4.5f;

    [Tooltip("Oyuncu yakinda, boss normal vurusa baslayacakken bunun yerine ziplama secme olasiligi (ziplama hazirsa).")]
    [Range(0f, 1f)] public float leapCloseChance = 0.4f;

    float nextLeapTime;
    bool leapActive, pendingLeap;

    // ---------------------------------------------------------------------
    // UCLU KOMBO: 2 hizli vurus + 3. GECIKMELI vurus. Parry gerekir (blok posture'u kirar).
    // Kombo sirasinda kilic, koyu kirmizi 'Impact' VFX'ine bürünür.
    // ---------------------------------------------------------------------
    [Header("Uclu kombo (2 hizli + 1 gecikmeli)")]
    public bool comboEnabled = true;
    [Tooltip("Kombo sonrasi tekrar secilmeden onceki sure (sn). Arada normal tek vurus atar.")]
    [Min(0f)] public float comboCooldown = 6f;
    [Tooltip("1. vurus hazirlik suresi (sn): kilic bu surede yavasca kalkar ve kirmizi parlar, kombonun geldigi anlasilsin.")]
    [Min(0.3f)] public float comboFirstWindup = 1.8f;
    [Tooltip("2. vurus: hizli (sn).")]
    [Min(0.2f)] public float comboFastWindup = 0.36f;
    [Tooltip("3. vurus: GECIKMELI (sn). Ezbere parry'i cezalandirir.")]
    [Min(0.5f)] public float comboDelayedWindup = 1.45f;
    [Tooltip("3. vurusun gecikmesine eklenen rastgele sure (0..bu).")]
    [Min(0f)] public float comboDelayedRandom = 0.45f;
    [Range(0.5f, 2.5f)] public float comboLastDamage = 1.3f;
    [Tooltip("3. vurusu parry'lemenin boss dengesine carpani.")]
    [Range(1f, 4f)] public float comboFinalParryReward = 1.5f;
    [Tooltip("Bos ise Resources/Boss1/Vfx/Boss1SwordImpact (Free Slash VFX > Impact kopyasi).")]
    public GameObject comboVfxPrefab;
    [Tooltip("Acik: kilica ek olarak Impact parcacik efekti (kivilcim) baglanir. Kapali: yalniz duz kirmizi bicak.")]
    public bool comboUseParticles = false;
    [Tooltip("Oyuncu uzaktaysa kombo hazirlanirken boss oyuncuya KOSAR ve kosarak vurur.")]
    public bool comboRunEnabled = true;
    [Min(1f)] public float comboRunSpeed = 26f;
    [Tooltip("Oyuncu bu yatay mesafeden yakinsa (ve vurus menzili disindaysa) uzaktan kombo baslatabilir.")]
    [Min(1f)] public float comboRunMaxDistance = 70f;
    [Tooltip("Uygun mesafedeyken saniyede uzaktan kombo baslatma olasiligi.")]
    [Range(0f, 1f)] public float comboRunChancePerSecond = 0.6f;
    [Tooltip("Kilica baglanan efektin dunya boyutu carpani.")]
    [Min(0.1f)] public float comboVfxScale = 5f;
    [Tooltip("Efekt rengi: saf parlak kirmizi (Impact materyalinin HDR parlakligi korunur).")]
    public Color comboColor = new Color(1f, 0f, 0.01f, 1f);

    [Header("Blok")]
    [Tooltip("Oyuncu bu kadar BLOK atinca posture'u kirilir (2 = iki blok). Parry yapmak zorunda.")]
    [Range(1, 6)] public int blocksToBreakPosture = 2;

    const string ComboName = "Boss1 Uclu Kombo";
    EnemyMoveset moveset;
    PlayerPosture playerPosture;
    GameObject comboVfx;
    bool comboTinted, comboVfxWarned;
    int comboStepNow = -1;
    float comboEndAt = -1f;
    float comboLastStart = -999f;

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

        RefreshBlockPosture();
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

        if (comboVfx != null)
            Destroy(comboVfx);
    }

    // ---- kombo kurulumu ----
    void BuildComboMoveset()
    {
        if (!comboEnabled) return;

        moveset = GetComponent<EnemyMoveset>();
        if (moveset == null) moveset = gameObject.AddComponent<EnemyMoveset>();

        AttackMove m = new AttackMove();
        m.name = ComboName;
        m.weight = 1f;
        m.cooldown = comboCooldown;
        m.recoveryMultiplier = 1f;
        m.hits.Add(new MoveHit(MoveHitType.Normal, comboFirstWindup, 0f, 0.85f));
        m.hits.Add(new MoveHit(MoveHitType.Normal, comboFastWindup, 0f, 0.85f).Retarget());
        m.hits.Add(new MoveHit(MoveHitType.Normal, comboDelayedWindup, comboDelayedRandom, comboLastDamage).Retarget().ParryReward(comboFinalParryReward));

        moveset.moves = new List<AttackMove> { m };
        moveset.comboFirstWindupBonus = 0f;       // ilk vurus da hizli baslasin
        moveset.comboBlockPostureMultiplier = 1f; // kombo vuruslari blokta tam posture yer
        moveset.showComboIndicator = false;
        moveset.noParrySlowMoMidCombo = true;
    }

    // Blok posture hasari: oyuncunun posture'unu 'blocksToBreakPosture' blokta kirar.
    void RefreshBlockPosture()
    {
        if (enemy == null) return;
        if (playerPosture == null) playerPosture = FindFirstObjectByType<PlayerPosture>();
        int max = playerPosture != null ? playerPosture.MaxPosture : 200;
        enemy.blockPostureDamage = Mathf.CeilToInt(max / (float)Mathf.Max(1, blocksToBreakPosture)) + 1;
    }

    // ---- kombo efekti: kilic koyu kirmizi Impact VFX'i ----
    void StartComboVfx(int step)
    {
        comboStepNow = step;
        if (step == 0) comboLastStart = Time.time;
        comboEndAt = -1f;
        comboTinted = true;
        rig.SetSwordTint(comboColor, 1f, step == 0 ? 0.9f : 7f);   // 1. vurusta kilic yukari kalkarken yavasca parlar

        if (comboUseParticles && comboVfx == null)
        {
            GameObject prefab = comboVfxPrefab != null ? comboVfxPrefab : Resources.Load<GameObject>("Boss1/Vfx/Boss1SwordImpact");
            Transform sword = rig.SwordBone;
            if (prefab == null || sword == null)
            {
                if (!comboVfxWarned)
                {
                    comboVfxWarned = true;
                    Debug.LogWarning("Boss1Enemy: kombo VFX'i yok (Resources/Boss1/Vfx/Boss1SwordImpact.prefab) ya da kilic kemigi bulunamadi.");
                }
                return;
            }

            comboVfx = Instantiate(prefab, sword);
            comboVfx.name = "Boss1ComboVfx";
            Transform t = comboVfx.transform;
            t.localPosition = rig.SwordBladeCenter;
            t.localRotation = Quaternion.identity;
            float ls = Mathf.Max(0.0001f, Mathf.Abs(sword.lossyScale.y));
            t.localScale = Vector3.one * (comboVfxScale / ls);
            TintVfx(comboVfx, comboColor);
        }

        if (comboVfx == null) return;
        ParticleSystem[] pss = comboVfx.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < pss.Length; i++)
        {
            pss[i].Clear(true);
            pss[i].Play(true);
        }
    }

    void StopComboVfx()
    {
        comboTinted = false;
        comboStepNow = -1;
        comboEndAt = -1f;
        if (rig != null) rig.SetSwordTint(comboColor, 0f);

        if (comboVfx != null)
        {
            ParticleSystem[] pss = comboVfx.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < pss.Length; i++)
                pss[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(comboVfx, 1.5f);
            comboVfx = null;
        }
    }

    void UpdateComboVfx(bool attacking)
    {
        if (!comboTinted) return;
        if (!attacking || enemy.IsStaggered || enemy.IsDead || (comboEndAt > 0f && Time.time >= comboEndAt))
            StopComboVfx();
    }

    // Impact prefab'ini koyu kirmizi tona boyar: parcacik renkleri + materyal renk ozellikleri (alfa korunur).
    static void TintVfx(GameObject root, Color c)
    {
        ParticleSystem[] pss = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < pss.Length; i++)
        {
            ParticleSystem ps = pss[i];

            ParticleSystem.MainModule main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(c);

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            if (col.enabled)
            {
                GradientAlphaKey[] alpha = null;
                if (col.color.mode == ParticleSystemGradientMode.Gradient && col.color.gradient != null)
                    alpha = col.color.gradient.alphaKeys;
                if (alpha == null || alpha.Length == 0)
                    alpha = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };
                Gradient g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) }, alpha);
                col.color = new ParticleSystem.MinMaxGradient(g);
            }
        }

        Renderer[] rs = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rs.Length; i++)
        {
            Renderer r = rs[i];
            r.sortingOrder = Mathf.Max(r.sortingOrder, 60);
            Material[] ms = r.materials;   // kopya: asset materyali bozulmaz
            for (int j = 0; j < ms.Length; j++)
            {
                Material m = ms[j];
                if (m == null || m.shader == null) continue;
                Shader sh = m.shader;
                int n = sh.GetPropertyCount();
                for (int k = 0; k < n; k++)
                {
                    if (sh.GetPropertyType(k) != UnityEngine.Rendering.ShaderPropertyType.Color) continue;
                    string pn = sh.GetPropertyName(k);
                    Color old = m.GetColor(pn);
                    float inten = Mathf.Max(1f, Mathf.Max(old.r, Mathf.Max(old.g, old.b)));
                    m.SetColor(pn, new Color(c.r * inten, c.g * inten, c.b * inten, old.a));
                }
            }
            r.materials = ms;
        }
    }

    // EnemyAttackState uyariyi baslatti -> kilic gerilme animasyonu.
    void OnAttackAnimationRequested()
    {
        RefreshBlockPosture();

        // Kombo vurusu: sure hamlenin adimindan gelir (hizli / gecikmeli); kilic o sureye gore oynar.
        EnemyAttackState st = enemy.CurrentState as EnemyAttackState;
        if (st != null && st.MoveName == ComboName)
        {
            StartComboVfx(st.StepIndex);
            rig.PlayAttack(st.RemainingWindup, st.StepIndex == 0);   // 1. vurus: kilic YAVASCA kalkar (okunurluk)
            return;
        }

        // Yakin mesafede de ziplama: normal vurus baslarken bazen onun yerine ziplama sec (Update'te state degisir).
        if (leapEnabled && !leapActive && !pendingLeap && Time.time >= nextLeapTime && Random.value < leapCloseChance)
        {
            pendingLeap = true;
            return;
        }

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

        if (pendingLeap)
        {
            pendingLeap = false;
            if (enemy.CurrentState is EnemyAttackState && !leapActive && !enemy.IsStaggered && !enemy.IsDead)
                enemy.ChangeState(new Boss1LeapState(this, enemy));
            else
                rig.PlayAttack();
        }

        // Uzaktan kombo: oyuncu vurus menzili disindaysa kosarak baslat.
        if (comboEnabled && comboRunEnabled && !leapActive && enemy.CurrentState is EnemyChaseState
            && Time.time >= comboLastStart + comboCooldown + 1.5f
            && enemy.target != null && !enemy.IsTargetDead && !enemy.IsStaggered && !enemy.IsDead && enemy.CanAttack)
        {
            float cx = Mathf.Abs(enemy.target.position.x - transform.position.x);
            if (cx > attackReach && cx <= comboRunMaxDistance && Random.value < comboRunChancePerSecond * Time.deltaTime
                && EnemyAttackCoordinator.TryRequestAttack(enemy, enemy.PlannedWindup))
            {
                enemy.ChangeState(new EnemyAttackState(enemy));
                EnemyAttackState ns = enemy.CurrentState as EnemyAttackState;
                if (ns == null || ns.MoveName != ComboName)
                {
                    enemy.ChangeState(new EnemyChaseState(enemy));   // kombo secilemedi: geri don
                    comboLastStart = Time.time - comboCooldown;       // kisa sure tekrar denemesin
                }
            }
        }

        if (leapEnabled && !leapActive && Time.time >= nextLeapTime && enemy.CurrentState is EnemyChaseState
            && enemy.target != null && !enemy.IsTargetDead && !enemy.IsStaggered && !enemy.IsDead)
        {
            float lx = Mathf.Abs(enemy.target.position.x - transform.position.x);
            if (lx >= leapMinDistance && lx <= leapMaxDistance && Random.value < leapChancePerSecond * Time.deltaTime)
                enemy.ChangeState(new Boss1LeapState(this, enemy));
        }

        bool attacking = enemy.CurrentState is EnemyAttackState;
        UpdateComboVfx(attacking);

        rig.MoveAllowed = enemy.CurrentState is EnemyChaseState;
        rig.Staggered = enemy.IsStaggered;
        rig.Dead = enemy.IsDead;

        // Saldiri vurustan ONCE bozuldu (sersemledi, baska duruma gecti): animasyonu yumusakca birak.
        if (rig.IsAttacking && !attacking && !leapActive)
            rig.CancelAttack();

        // Guvenlik: zemin yoksa sonsuza kadar dusme.
        if (transform.position.y < startY - 60f && rb != null)
        {
            rb.position = new Vector2(rb.position.x, startY);
            rb.linearVelocity = Vector2.zero;
            Debug.LogWarning("Boss1Enemy: boss zeminden dustu, baslangic yuksekligine geri alindi. Altinda Collider var mi?");
        }
    }

    // =====================================================================
    // Boss1LeapState: Crouch -> Rise -> Air (isaret) -> Fall -> Land
    // =====================================================================
    internal sealed class Boss1LeapState : IEnemyState
    {
        enum Ph { Crouch, Rise, Air, Fall, Gap, Land }

        readonly Boss1Enemy o;
        readonly EnemyController e;
        Ph ph;
        float t, groundY, apexY, markerX;
        bool released, airborne;
        int slam;
        float radius, lockTime, curTrack;
        Rigidbody2D rb;
        Collider2D[] cols;
        bool[] colWas;
        SpriteRenderer marker;
        static Sprite markerSprite;

        public Boss1LeapState(Boss1Enemy owner, EnemyController enemy) { o = owner; e = enemy; }

        public void Enter()
        {
            rb = o.GetComponent<Rigidbody2D>();
            groundY = o.transform.position.y;
            o.leapActive = true;
            e.superArmorOverride = true;
            e.RetargetFacing();
            e.LockFacing(true);
            if (rb != null) rb.linearVelocity = Vector2.zero;
            ComputeWindow();
            o.rig.PlayLeapTense();
            o.rig.SetBaseSquash(0.5f, 1.4f);
            ph = Ph.Crouch; t = 0f;
        }

        public void Tick()
        {
            if (e.target == null || e.IsTargetDead)
            {
                e.ChangeState(new EnemyIdleState(e));
                return;
            }

            float dt = EnemyTime.DeltaTime;
            t += dt;
            Vector3 p = o.transform.position;

            switch (ph)
            {
                case Ph.Crouch:
                    if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                    if (t >= o.leapCrouchTime) BeginRise();
                    break;

                case Ph.Rise:
                {
                    float k = Mathf.Clamp01(t / o.leapRiseTime);
                    SetPos(p.x, Mathf.Lerp(groundY, apexY, k * k));
                    if (k >= 1f)
                    {
                        ph = Ph.Air; t = 0f;
                        markerX = e.target.position.x;
                        ShowMarker();
                    }
                    break;
                }

                case Ph.Air:
                {
                    if (t < curTrack)
                        markerX = Mathf.MoveTowards(markerX, e.target.position.x, 70f * dt);
                    SetPos(markerX, apexY);
                    UpdateMarker();
                    if (t >= curTrack + lockTime) { ph = Ph.Fall; t = 0f; }
                    break;
                }

                case Ph.Fall:
                {
                    float k = Mathf.Clamp01(t / o.leapFallTime);
                    SetPos(markerX, Mathf.Lerp(apexY, groundY, k));
                    if (!released && t >= o.leapFallTime - 0.12f) { released = true; o.rig.ReleaseStrike(); }
                    if (k >= 1f) Land();
                    break;
                }

                case Ph.Gap:
                    if (t >= o.leapChainGap) BeginRise();
                    break;

                case Ph.Land:
                    if (t >= o.leapLandTime && !o.rig.IsAttacking)
                        e.ChangeState(new EnemyChaseState(e));
                    break;
            }
        }

        // Oyuncunun dash degerlerinden kilit penceresi ve yaricap:
        //  pencere = tepki + dash suresi (inis dahil); yaricap = dash mesafesi x kenar payi.
        //  Kilitlenince hemen dash atan oyuncu seridin disina ucu ucuna cikar.
        void ComputeWindow()
        {
            float reaction = o.leapReaction;
            radius = o.leapRadius;
            PlayerController pl = e.target != null ? e.target.GetComponent<PlayerController>() : null;
            if (o.leapAutoRadius && pl != null && pl.dashDistance > 0f)
            {
                float dashDist = Mathf.Abs(pl.dashDistance) * DashState.DistanceScale;
                float dashDur = Mathf.Max(0.05f, pl.dashTime * DashState.DurationScale);
                radius = dashDist * o.leapEdgeMargin;
                lockTime = Mathf.Max(0.15f, reaction + dashDur - o.leapFallTime);
            }
            else
            {
                lockTime = Mathf.Max(0.15f, reaction + 0.33f - o.leapFallTime);
            }
        }

        void BeginRise()
        {
            curTrack = slam == 0 ? o.leapTrackTime : o.leapChainTrackTime;
            released = false;
            if (slam > 0) o.rig.PlayLeapTense();
            airborne = true;
            Camera cam = Camera.main;
            float baseY = e.target.position.y;
            if (cam != null) baseY = Mathf.Max(baseY, cam.transform.position.y);
            apexY = baseY + o.leapApexAbove;

            cols = o.GetComponentsInChildren<Collider2D>();
            colWas = new bool[cols.Length];
            for (int i = 0; i < cols.Length; i++) { colWas[i] = cols[i].enabled; cols[i].enabled = false; }
            if (rb != null) { rb.linearVelocity = Vector2.zero; rb.bodyType = RigidbodyType2D.Kinematic; }

            o.rig.SetBaseSquash(-0.5f, 14f);
            ph = Ph.Rise; t = 0f;
        }

        void SetPos(float x, float y)
        {
            if (rb != null) rb.position = new Vector2(x, y);
            else o.transform.position = new Vector3(x, y, o.transform.position.z);
            o.transform.position = new Vector3(x, y, o.transform.position.z);
        }

        void Land()
        {
            airborne = false;
            slam++;
            bool last = slam >= Mathf.Max(1, o.leapSlamCount);
            Restore();
            SetPos(markerX, groundY);
            HideMarker();

            o.rig.SetBaseSquash(0.5f, 25f);
            o.rig.Squash(0.5f);
            o.rig.SetBaseSquash(0f, 3f);
            float sh = last ? o.leapShake : o.leapShake * 0.7f;
            if (CameraShake.Instance != null) CameraShake.Instance.Shake(sh);
            RunCameraZoom.AddShake(sh * 0.5f);

            DamagePlayer();

            t = 0f;
            if (last)
            {
                ph = Ph.Land;
                o.nextLeapTime = Time.time + o.leapCooldown;
            }
            else
            {
                ph = Ph.Gap;
                markerX = o.transform.position.x;
            }
        }

        void DamagePlayer()
        {
            PlayerController pl = e.target.GetComponent<PlayerController>();
            if (pl == null) return;

            float dx = Mathf.Abs(pl.transform.position.x - markerX);
            float feet = pl.col != null ? pl.col.bounds.min.y : pl.transform.position.y;
            if (dx > radius || feet > groundY + o.leapHitHeight) return;

            if (pl.isInvincible)
            {
                CombatEvents.RaiseDodge(e, true);
                return;
            }

            PlayerDamageReceiver rcv = pl.GetComponent<PlayerDamageReceiver>();
            if (rcv == null) return;

            Vector2 dir = new Vector2(Mathf.Sign(pl.transform.position.x - markerX + 0.001f), 0f);
            float m = e.unblockableKnockbackMultiplier;
            rcv.TakeDamage(
                e.unblockableDamage,
                dir,
                e.attackKnockbackForce * m,
                e.attackKnockbackVerticalForce * m,
                e.attackKnockbackDuration,
                e.attackKnockbackDeceleration,
                e,
                PlayerHitKind.Unblockable,
                true
            );
            EnemyAttackCoordinator.NotifyPlayerHit();
        }

        void Restore()
        {
            if (cols != null)
            {
                for (int i = 0; i < cols.Length; i++)
                    if (cols[i] != null) cols[i].enabled = colWas[i];
                cols = null;
            }
            if (rb != null && rb.bodyType != RigidbodyType2D.Dynamic)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.linearVelocity = Vector2.zero;
            }
        }

        // ---- yer isareti ----
        void ShowMarker()
        {
            if (marker == null)
            {
                if (markerSprite == null) markerSprite = BuildMarkerSprite();
                GameObject go = new GameObject("Boss1LeapMarker");
                marker = go.AddComponent<SpriteRenderer>();
                marker.sprite = markerSprite;
                marker.sortingLayerName = "Default";
                marker.sortingOrder = 40;
            }
            marker.enabled = true;
            UpdateMarker();
        }

        void UpdateMarker()
        {
            if (marker == null) return;
            float w = 2f * radius;
            marker.transform.position = new Vector3(markerX, groundY + 0.1f, 0f);
            marker.transform.localScale = new Vector3(w, o.leapStripHeight, 1f);

            bool locked = t >= curTrack;
            float rate = locked ? 22f : 7f;
            float a = 0.35f + 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * rate));
            Color c = locked ? new Color(1f, 0.12f, 0.08f, a + 0.2f) : new Color(1f, 0.35f, 0.15f, a);
            marker.color = c;
        }

        void HideMarker()
        {
            if (marker != null) { UnityEngine.Object.Destroy(marker.gameObject); marker = null; }
        }

        // Zemine paralel duz serit (izometrik degil): 64x16, uclarda parlak kapak, ust/alt kenar yumusak.
        static Sprite BuildMarkerSprite()
        {
            const int W = 64, H = 16;
            Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color32[] px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float vy = Mathf.Abs((y + 0.5f) / H * 2f - 1f);
                    float edgeY = Mathf.Clamp01((1f - vy) / 0.35f);
                    bool cap = x < 2 || x >= W - 2;
                    float a = (cap ? 1f : 0.7f) * edgeY;
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), W);
        }

        public void Exit()
        {
            Restore();
            HideMarker();

            if (airborne)
                SetPos(o.transform.position.x, groundY);   // yarida kesildi: yere indir

            o.leapActive = false;
            e.superArmorOverride = false;
            e.LockFacing(false);
            o.rig.ReleaseStrike();
            o.rig.SetBaseSquash(0f, 6f);
            if (slam == 0 && o.rig.IsAttacking) o.rig.CancelAttack();
            o.nextLeapTime = Mathf.Max(o.nextLeapTime, Time.time + 3f);
        }
    }
}
