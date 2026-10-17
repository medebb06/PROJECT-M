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
    [Min(1)] public int maxHealth = 3000;
    [Tooltip("Denge (posture) cubugu. Parry denge doldurur; dolunca boss sersemler (infaz firsati).")]
    [Min(1)] public int maxBalance = 240;
    [Tooltip("Parry'nin boss dengesine hasari (oyun bunu x0.5 ve cana gore x0.6-1.6 ile olcekler: ~18 = denge ~13 parry, can azaldikca daha az).")]
    [Min(1)] public int parryBalanceDamage = 60;
    [Tooltip("Denge kirilinca boss'un sersemleme suresi (sn).")]
    [Min(0.5f)] public float staggerDuration = 3.5f;

    [Header("Hareket (dunya birimi)")]
    [Tooltip("Yurume hizi (birim/sn). Agir olsun diye yavas. Normal dusman: 10.")]
    [Min(0.5f)] public float walkSpeed = 22f;
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
    [Min(0f)] public float extraCooldown = 0.15f;
    [Tooltip("Sersemlemis boss'a ILK vurustan SONRAKI duz vuruslarin ek hasari (max can yuzdesi). 0 = ilk agir darbeden sonra vuruslar normal hasar verir.")]
    [Range(0f, 0.3f)] public float staggerHitPercent = 0f;
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
        nextWaveTime = Time.time + waveFirstDelay; expectedSince = Time.time;
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
        balance.SetBreakHealthPercent(postureBreakHealthPercent);
        balance.chipRatioOverride = hitChipHealthRatio;
        balance.breakLockBelowHealth = postureLockAboveHealth;
        balance.breakLockCapFraction = postureLockCap;
        health.MinHealth = 1;   // faz 1: can 0'a inmez, cubuk bitince faz 2 baslar   // 0 = kirilma hasar vermez (Sekiro gibi: sadece acar)

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

    int lastRoar = -1;

    void OnStrikeLanded()
    {
        EnemyAttackState sst = enemy != null ? enemy.CurrentState as EnemyAttackState : null;
        if (sst != null)
        {
            MoveHitType sht = sst.CurrentHitType;
            float dirX = enemy.FacingDirection >= 0f ? 1f : -1f;
            if (sht == MoveHitType.Sweep && sst.MoveName == SweepName)
            {
                Boss1Shock.Spawn(transform.position + new Vector3(dirX * 4f, 0f, 0f), dirX, attackReach * sweepReachMultiplier, sweepColor);
                rig.SetSwordTint(sweepColor, 0f, 3f);
            }
            else if (sst.MoveName == FakeName)
                rig.SetSwordTint(fakeColor, 0f, 3f);
        }

        if (comboTinted && comboStepNow >= comboLastIndex) { comboEndAt = Time.time + 0.45f; potionWindowPending = potionWindowAfterCombo; }
        else if (comboTinted) rig.SetSwordTint(comboColor, 1f, 12f);   // ipucu parlamasindan normal kirmiziya don
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
    [Tooltip("Kapali: boss sadece oyuncu 'introDistance' kadar yaklasinca uyanir (kukrer + kovalar). Acik: ekrana girer girmez de uyanir.")]
    public bool introSeeOnScreen = false;
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
        if (rig != null) rig.BeatChest(Mathf.Max(1.2f, Mathf.Min(len, introMaxRoar)));   // goril gogus vurusu
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
        nextWaveTime = Time.time + waveFirstDelay; expectedSince = Time.time;

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
    [Min(0f)] public float leapFirstDelay = 2.5f;
    [Tooltip("Iki ziplama arasi en az sure (sn).")]
    [Min(1f)] public float leapCooldown = 3.5f;
    [Tooltip("Uzaktan ziplama icin en az yatay mesafe (0 = hep).")]
    [Min(0f)] public float leapMinDistance = 0f;
    [Min(1f)] public float leapMaxDistance = 90f;
    [Tooltip("Uygun mesafedeyken saniyede ziplama baslatma olasiligi.")]
    [Range(0f, 1f)] public float leapChancePerSecond = 0.6f;
    [Tooltip("Gerilme/cokme suresi (kilic yukari, govde ezik). Rig gerilmesinden (1.0 sn) kisa olmasin.")]
    [Min(0.3f)] public float leapCrouchTime = 0.85f;
    [Min(0.05f)] public float leapRiseTime = 0.22f;
    [Tooltip("Ilk inis: isaret bu sure oyuncuyu izler, sonra SABITLENIR.")]
    [Min(0.1f)] public float leapTrackTime = 0.55f;
    [Tooltip("Ziplama bir kez odaklandiktan sonra art arda kac kez inis yapsin.")]
    [Range(1, 5)] public int leapSlamCount = 1;
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
    [Range(0f, 1f)] public float leapCloseChance = 0.5f;

    float nextLeapTime;
    bool leapActive, pendingLeap, pendingWave;

    // ---------------------------------------------------------------------
    // DALGA SALDIRISI: uzaktan odaklanir (kilic kalkar, kirmizi parlar), sonra RITIMLI 3 dalga firlatir.
    // Dalgalar parry / blok edilebilir; ziplayarak ya da dash ile gecilebilir.
    // ---------------------------------------------------------------------
    [Header("Dalga saldirisi (3 ritimli dalga)")]
    public bool waveEnabled = true;
    [Tooltip("Giristen sonra ilk dalgaya kadar en az sure (sn).")]
    [Min(0f)] public float waveFirstDelay = 2.5f;
    [Tooltip("Dalga saldirisi sonrasi tekrar secilmeden onceki sure (sn).")]
    [Min(1f)] public float waveCooldown = 2.2f;
    [Tooltip("Dalga icin oyuncunun en az uzakligi (yakindaysa normal vurus/kombo kullanir).")]
    [Min(1f)] public float waveMinDistance = 65f;
    [Min(1f)] public float waveMaxDistance = 140f;
    [Range(0f, 1f)] public float waveChancePerSecond = 1.0f;
    [Tooltip("Odaklanma suresi: kilic yavasca kalkar, ilk dalga bu sure sonra cikar.")]
    [Min(0.4f)] public float waveFocusTime = 1.4f;
    [Tooltip("Dalgalar arasi sure (sn): RITIM. Esit aralikli.")]
    [Min(0.4f)] public float waveInterval = 0.8f;
    [Range(1, 5)] public int waveCount = 3;
    [Tooltip("Dalga hizi (birim/sn).")]
    [Min(4f)] public float waveSpeed = 145f;
    [Tooltip("Dalga yuksekligi (ziplayarak gecmek icin ustune cikmak gerekir).")]
    [Min(1f)] public float waveHeight = 9f;
    [Tooltip("Dalganin oyuncuya degme yaricapi (yatay).")]
    [Min(0.5f)] public float waveHitHalfWidth = 3.5f;
    [Tooltip("Dalga bossun onunden bu kadar uzakta dogar.")]
    [Min(1f)] public float waveSpawnForward = 18f;
    [Tooltip("Odaklanirken/dalgalar arasinda oyuncu bu kadar yakina gelirse saldiri iptal olur (boss normal vurusa gecer). Oyuncu bundan yakinsa dalga bossun hemen onunde dogar.")]
    [Min(1f)] public float waveAbortDistance = 8f;
    [Tooltip("Dalga carpinca hasar (normal vurus hasarina carpan).")]
    [Min(0f)] public float waveDamageMultiplier = 1f;
    [Tooltip("Parry'de boss dengesine verilen hasar (normal parry hasarina carpan).")]
    [Min(0f)] public float waveParryBalanceMultiplier = 0.8f;
    public Color waveColor = new Color(1f, 0.1f, 0.02f, 1f);
    [Tooltip("Parry'lenen dalganin rengi (geri seker).")]
    public Color waveReflectColor = new Color(0.25f, 0.85f, 1f, 1f);
    [Tooltip("Geri seken dalganin hizi (dalga hizina carpan).")]
    [Min(0.2f)] public float waveReflectSpeedMultiplier = 0.8f;
    [Tooltip("Geri seken dalga bossa bu kadar yaklasinca carpar.")]
    [Min(0f)] public float waveReflectStopDistance = 7f;
    [Min(0.1f)] public float waveEndDelay = 0.4f;

    [Header("Dalga oncesi geri ziplama")]
    [Tooltip("Acik: oyuncu yakindaysa boss once geriye ziplayip mesafe acar, sonra dalgalari atar.")]
    public bool waveBackHop = true;
    [Tooltip("Oyuncu yakindayken (waveMinDistance'tan az) saniyede geri ziplama baslatma olasiligi.")]
    [Range(0f, 2f)] public float waveCloseChancePerSecond = 2f;
    [Tooltip("Ziplama sonrasi hedeflenen mesafe (oyuncuyla boss arasi).")]
    [Min(10f)] public float waveHopTargetDistance = 82f;
    [Tooltip("Tek ziplamada en fazla kac birim geri gider.")]
    [Min(5f)] public float waveHopMaxDistance = 58f;
    [Min(0.2f)] public float waveHopTime = 0.6f;
    [Min(0f)] public float waveHopHeight = 8f;
    [Tooltip("Ziplamadan once cokme (sn).")]
    [Min(0f)] public float waveHopCrouch = 0.3f;

    [Header("Acik an (denge kirilinca - Sekiro gibi)")]
    [Tooltip("Boss'un dengesi kirilinca ANINDA giden can (max canin yuzdesi). 0 = kirilma hasar VERMEZ; boss sadece saldiriya/infaza acilir, can acikken vurdukca gider (Stagger Hit Percent).")]
    [Range(0f, 0.5f)] public float postureBreakHealthPercent = 0f;
    [Tooltip("Boss saldiriya/infaza ACILDIGI an bir kez calan uyari sesi (tik). Bos birakilabilir; sesi sonra buraya surukle.")]
    public AudioClip openWarnSound;
    [Range(0f, 1f)] public float openWarnVolume = 1f;
    bool openWarnWas;
    [Tooltip("Boss ACILDIKTAN sonraki ILK duz vurus max canin bu yuzdesi kadar can goturur (agir darbe). Sonraki vuruslar staggerHitPercent kullanir.")]
    [Range(0f, 0.5f)] public float staggerFirstHitPercent = 0.2f;
    [Tooltip("Agir darbe CARPTIGI an zaman yavaslar ve yavasca normale doner: toplam sure (gercek sn). 0 = kapali.")]
    [Min(0f)] public float heavyHitSlowDuration = 0.9f;
    [Tooltip("Agir darbede baslangic zaman hizi (0.05 = %5 hiz, cok agir).")]
    [Range(0.02f, 1f)] public float heavyHitSlowScale = 0.08f;
    [Tooltip("1 = duz hizlanma; 2-3 = bir sure agir kalir sonra hizlanir.")]
    [Range(1f, 4f)] public float heavyHitSlowPower = 2.2f;
    bool staggerFirstHitPending;
    bool phase2Pending;
    [Header("Posture kilidi + iksir penceresi")]
    [Tooltip("Boss cani bu oranin USTUNDEYKEN posture kirilamaz (cubuk tavanda durur, vurus yine can goturur). 0 = kilit yok.")]
    [Range(0f, 0.9f)] public float postureLockAboveHealth = 0.5f;
    [Tooltip("Kilitliyken posture cubugunun cikabilecegi en yuksek oran.")]
    [Range(0.3f, 0.99f)] public float postureLockCap = 0.75f;
    [Tooltip("Kombo bitirici vurusundan sonra boss bu kadar sn saldirmaz (oyuncu iksir icsin). 0 = yok.")]
    [Min(0f)] public float potionWindowAfterCombo = 1.5f;
    [Tooltip("Ziplama-ezme sonrasi iksir penceresi (sn).")]
    [Min(0f)] public float potionWindowAfterLeap = 1.3f;
    [Tooltip("Pencere sadece oyuncunun cani bu oranin altindaysa ve iksir hakki varsa acilir.")]
    [Range(0.1f, 1f)] public float potionWindowBelowHealth = 0.75f;
    float potionWindowPending;
    float heavyHitUntil;
    [Tooltip("Faz 1 olumcul darbe: max canin bu orani kadar can goturur (tamamini degil).")]
    [Range(0.05f, 1f)] public float deadlyBlowPercent = 0.34f;
    [Tooltip("Faz 2 olumcul darbe: max canin bu orani kadar can goturur; cani bundan azsa boss olur.")]
    [Range(0.05f, 1f)] public float phase2DeadlyBlowPercent = 0.5f;
    [Header("Can ile oldurme (kacip vurarak)")]
    [Tooltip("Posture cubugunun tamami dolacak kadar duz vurus, max canin bu orani kadar can goturur. 0 = duz vurus can gotürmez.")]
    [Range(0f, 1f)] public float hitChipHealthRatio = 0.5f;
    [Tooltip("true: eski davranis, can %50'nin altina inince de faz 2 baslar. false: faz 2 sadece can cubugu sifirlaninca / olumcul darbede.")]
    public bool phase2AtHalfHealth = false;
    [Tooltip("Faz 2'de posture (denge) cubugu bu kadar katina cikar; olumcul darbe icin daha zor kirilsin.")]
    [Range(1f, 3f)] public float phase2PostureScale = 1.8f;
    [Tooltip("Olumcul darbeden sonra can cubugunun (kukreme sirasinda) tamamen dolma suresi (sn, gercek zaman).")]
    [Min(0.5f)] public float phase2RefillTime = 2.2f;

    // Cani sifirla (olmeden), sendelemesi bitince faz 2'ye gec ve kukrerken cani doldur.
    IEnumerator DeadlyBlowPhase2(float wait)
    {
        phase2Pending = true;
        health.MinHealth = 1;                         // vuruslar bu surede oldurmesin
        int dmg = health.CurrentHealth - 1;
        if (dmg > 0) health.TakeDamage(dmg);          // cubuk sifira iner

        wait = Mathf.Max(0.2f, wait);
        for (float t = 0f; t < wait; t += Time.unscaledDeltaTime) yield return null;

        for (float g = 0f; g < 2f && (leapActive || waveActive); g += Time.unscaledDeltaTime) yield return null;
        if (health != null && !health.IsDead && !phase2) BeginPhase2();

        float dur = Mathf.Max(phase2RefillTime, 0.5f);
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            if (health == null || health.IsDead) break;
            health.SetHealth(Mathf.Max(1, Mathf.RoundToInt(health.MaxHealth * Mathf.SmoothStep(0f, 1f, t / dur))));
            yield return null;
        }
        if (health != null && !health.IsDead) health.SetHealth(health.MaxHealth);
        if (health != null) health.MinHealth = 0;   // faz 2: can kilidi yok, 0 = olum
        phase2Pending = false;
    }
    [Header("Agir darbe tepkisi")]
    [Tooltip("Agir darbeden sonra geriye sendeleme hizi (0 = sendelemez).")]
    [Min(0f)] public float heavyHitStumbleSpeed = 18f;
    [Tooltip("Faz 2 saldiri hizi carpani (1 = degismez). Hazirlik/toparlanma sureleri ve bekleme sureleri bolunur.")]
    [Range(1f, 3f)] public float phase2AttackSpeed = 1.7f;

    void PlayOpenWarn()
    {
        if (openWarnSound == null) return;
        GameObject go = new GameObject("Boss1_OpenWarnSfx");
        AudioSource a = go.AddComponent<AudioSource>();
        a.clip = openWarnSound;
        a.volume = openWarnVolume;
        a.spatialBlend = 0f;     // 2D: uzakliktan bagimsiz, hep duyulur
        a.playOnAwake = false;
        a.Play();
        Destroy(go, openWarnSound.length + 0.2f);
    }

    [Header("Posture kirilma merhameti")]
    [Tooltip("0 = kapali (boss her halukarda saldirisini bitirir). >0: oyuncunun posture'u kirilinca boss bu sure (sn) saldirmaz.")]
    [Min(0f)] public float postureBreakMercy = 0f;
    float mercyUntil;
    bool mercyPending, postureWasBroken;
    internal bool InMercy { get { return Time.time < mercyUntil; } }

    // ---------------------------------------------------------------------
    // YETENEK DONGUSU: ayni yetenek ust uste spamlanmaz; dalga (uzaktan) one cikar.
    // ---------------------------------------------------------------------
    [Header("Yetenek dongusu / hasar")]
    [Tooltip("Acik: ayni yetenek (kombo / ziplama / dalga) arka arkaya iki kez secilmez.")]
    public bool skillRotation = true;
    [Tooltip("Dalga, son kullanimindan beri gecen her yetenek icin bu kadar daha sik secilir.")]
    [Range(0f, 2f)] public float waveStaleBoost = 0.6f;
    [Tooltip("Boss'un her isabeti oyuncunun MAX canindan bu kadar alir (0.34 = 3 hata olum).")]
    [Range(0.05f, 1f)] public float bossHitPercent = 0.34f;

    enum Skill { None, Melee, Combo, Sweep, Leap, Wave }
    Skill lastSkill = Skill.None;
    int skillsSinceWave = 3;
    IEnemyState trackedState;
    AttackMove comboMove, sweepMove, fakeMove;

    [Header("Yetenek sirasi (ogrenilebilir dongu)")]
    [Tooltip("Dongudeki siradaki yetenek 'beklenir'; baska ozel yetenek secilmez. Beklenen yetenek bu kadar sn gerceklesmezse atlanir.")]
    [Min(2f)] public float patternPatience = 9f;
    // Faz 1 dongusu (Melee = duz vurus).
    static readonly Skill[] Pattern1 = { Skill.Melee, Skill.Wave, Skill.Combo, Skill.Sweep, Skill.Wave, Skill.Melee, Skill.Melee, Skill.Leap };
    static readonly Skill[] Pattern2 = { Skill.Wave, Skill.Combo, Skill.Sweep, Skill.Wave, Skill.Melee, Skill.Combo, Skill.Leap, Skill.Wave };
    bool phase2;
    int patIdx;
    float expectedSince;
    bool fakeFlag;

    Skill[] CurPattern { get { return phase2 ? Pattern2 : Pattern1; } }
    Skill Expected { get { Skill[] p = CurPattern; return p[patIdx % p.Length]; } }

    // Dongu aciksa sadece sirasi gelen ozel yetenek secilebilir.
    bool SkillBlocked(Skill s) { return skillRotation && s != Expected; }

    void AdvancePattern()
    {
        patIdx++;
        expectedSince = Time.time;
        fakeFlag = Random.value < fakeChance;
    }

    void NoteSkill(Skill s)
    {
        skillsSinceWave = s == Skill.Wave ? 0 : skillsSinceWave + 1;
        lastSkill = s;
        if (skillRotation && s == Expected)
            AdvancePattern();
    }

    void TrackSkill(IEnemyState st)
    {
        if (st is Boss1WaveState) NoteSkill(Skill.Wave);
        else if (st is Boss1LeapState) NoteSkill(Skill.Leap);
        else
        {
            EnemyAttackState a = st as EnemyAttackState;
            if (a == null) return;
            string n = a.MoveName;
            NoteSkill(n == ComboName ? Skill.Combo : n == SweepName ? Skill.Sweep : Skill.Melee);
        }
    }

    // Moveset agirligi: sadece sirasi gelen hamle secilir (aldatmaca sadece Melee sirasinda, sansla).
    float MoveWeight(AttackMove m)
    {
        if (!skillRotation) return 1f;
        Skill ex = Expected;
        if (m == comboMove) return ex == Skill.Combo ? 1000f : 0f;
        if (m == sweepMove) return ex == Skill.Sweep ? 1000f : 0f;
        if (m == fakeMove) return (ex == Skill.Melee && fakeFlag) ? 1000f : 0f;
        return 0f;
    }

    // ---------------------------------------------------------------------
    // YENI SALDIRILAR: Supurme (ZIPLA), Yakalama (DASH), Aldatmaca (gecikmeli vurus)
    // ---------------------------------------------------------------------
    [Header("Supurme (zipla)")]
    [Tooltip("Uyari suresi (sn). Yerde ilerleyen sok dalgasi: ustunden ZIPLA (dash korumaz).")]
    [Min(0.5f)] public float sweepWindup = 1.15f;
    [Tooltip("Supurmenin boss'a gore menzili (attackReach carpani).")]
    [Range(0.4f, 1.5f)] public float sweepReachMultiplier = 1f;
    [Tooltip("Oyuncunun ayagi boss'un ayagindan bu kadar yukaridaysa supurmeden kurtulur.")]
    [Min(0.5f)] public float sweepHitHeight = 2f;
    public Color sweepColor = new Color(0.3f, 0.8f, 1f, 1f);

    [Header("Uzaktan baslatma (Supurme menzile girmeden de baslar)")]
    [Tooltip("Siradaki yetenek Supurme ise boss oyuncu normal vurus menzilinin disindayken de bu mesafeye (birim) kadar uzaktan baslatir.")]
    [Min(5f)] public float farSkillMaxDistance = 28f;
    [Tooltip("Uzaktan baslatma sansi (saniyede). Buyuk = hemen baslar.")]
    [Min(0.5f)] public float farSkillChancePerSecond = 3f;
    float farSkillRetry;
    public Color fakeColor = new Color(1f, 0.1f, 0.35f, 1f);

    [Header("Aldatmaca (gecikmeli vurus)")]
    [Min(0.3f)] public float fakeWindup = 0.55f;
    [Tooltip("Uyariya eklenen rastgele sure: ezbere parry'i cezalandirir.")]
    [Min(0f)] public float fakeWindupRandom = 0.8f;
    [Tooltip("Duz vurus sirasi geldiginde aldatmaca secilme sansi.")]
    [Range(0f, 1f)] public float fakeChance = 0.45f;

    const string SweepName = "Boss1 Supurme";
    const string FakeName = "Boss1 Aldatmaca";

    [Header("Faz 2 (ofke)")]
    [Tooltip("Can bu oranin altina inince boss ofkelenir: daha hizli, 5 dalga, farkli dongu.")]
    [Range(0.1f, 0.9f)] public float phase2HealthPercent = 0.5f;
    [Tooltip("Faz 2 kukremesinde boss'un hareketsiz kaldigi sure (oyuncu icin firsat).")]
    [Min(0.5f)] public float phase2RoarTime = 1.7f;

    [Header("Vurus agirligi / cezalandirma")]
    [Tooltip("Boss'a her can vurusunda bossun kisa 'takilmasi' (sadece boss yavaslar).")]
    public bool hitWeight = true;
    [Range(0f, 1f)] public float hitThudVolume = 0.55f;
    [Tooltip("Boss saldirisini bitirip toparlanirken (acik an) duz vurus basina EK can hasari (max can orani). 0 = kapali.")]
    [Range(0f, 0.05f)] public float punishHitPercent = 0.01f;

    float nextWaveTime;
    int comboLastIndex = 3;
    float cueComboId = -1f;
    int cueStep = -1;
    AudioSource cueSrc;
    static AudioClip cueClip;
    bool waveActive;
    internal bool waveParryNow;
    float dashSeenTime = -10f, nextRetargetTime;
    PlayerController playerCtl;

    [Header("Dash ile yon degistirme")]
    [Tooltip("Acik: boss vurus hazirlarken oyuncu dash atip arkasina gecerse boss ona doner.")]
    public bool dashRetarget = true;
    [Tooltip("Vurus bu kadar sn'den az kaldiysa donmez (son anda kacis serbest).")]
    [Min(0f)] public float dashRetargetMinWindup = 0.18f;
    [Tooltip("Dash bittikten sonra bu sure icinde de donmeye izin ver (sn).")]
    [Min(0f)] public float dashRetargetGrace = 0.3f;

    // ---------------------------------------------------------------------
    // UCLU KOMBO: 2 hizli vurus + 3. GECIKMELI vurus. Parry gerekir (blok posture'u kirar).
    // Kombo sirasinda kilic, koyu kirmizi 'Impact' VFX'ine bürünür.
    // ---------------------------------------------------------------------
    [Header("Kombo (ritimli vuruslar + 1 gecikmeli son)")]
    public bool comboEnabled = true;
    [Tooltip("Kombo sonrasi tekrar secilmeden onceki sure (sn). Arada normal tek vurus atar.")]
    [Min(0f)] public float comboCooldown = 3f;
    [Tooltip("1. vurus hazirlik suresi (sn): kilic bu surede yavasca kalkar ve kirmizi parlar, kombonun geldigi anlasilsin.")]
    [Min(0.3f)] public float comboFirstWindup = 1.0f;
    [Tooltip("RITIM: 1. vurustan sonraki ritimli vuruslarin arasi (sn). Hepsi ayni aralikta, sabit.")]
    [Min(0.3f)] public float comboFastWindup = 0.5f;
    [Tooltip("Ayni ritimde kac vurus (1. dahil). Sonra 1 tane GECIKMELI son vurus gelir. 3 = 3 ritimli + 1 gecikmeli.")]
    [Range(2, 5)] public int comboBeatHits = 3;
    [Tooltip("Vurustan hemen once kilic parlar + 'tik' sesi: parry tusuna BASMA ani. Darbeden kac sn once (sn).")]
    [Range(0.08f, 0.6f)] public float comboCueLead = 0.22f;
    [Range(0f, 1.5f)] public float comboCueVolume = 0.7f;
    public Color comboCueColor = new Color(1f, 0.92f, 0.55f, 1f);
    [Tooltip("3. vurus: GECIKMELI (sn). Ezbere parry'i cezalandirir.")]
    [Min(0.5f)] public float comboDelayedWindup = 1.0f;
    [Tooltip("3. vurusun gecikmesine eklenen rastgele sure (0..bu).")]
    [Min(0f)] public float comboDelayedRandom = 0f;
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
    [Range(0f, 1f)] public float comboRunChancePerSecond = 1.2f;
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
    [Header("Olum yavas cekimi (Dead Cells tarzi)")]
    [Tooltip("Son vurus yenince oyun hizi (0.15 = cok yavas). 1 = kapali.")]
    [Range(0.02f, 1f)] public float deathSlowScale = 0.15f;
    [Tooltip("En yavas halde kalma suresi (gercek sn).")]
    [Min(0f)] public float deathSlowHold = 0.9f;
    [Tooltip("Normal hiza donus suresi (gercek sn).")]
    [Min(0.05f)] public float deathSlowRelease = 0.8f;

    bool seenRoared, deathRoared;
    float nextSeenCheck;

    void CheckRoarTriggers()
    {
        if (!deathRoared && health != null && health.IsDead)
        {
            deathRoared = true;
            if (deathSlowScale < 0.999f) Boss1SlowMo.Run(deathSlowScale, deathSlowHold, deathSlowRelease);
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

        if (!seen && introSeeOnScreen)
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
        nextLeapTime = Time.time + leapFirstDelay;
        nextWaveTime = Time.time + waveFirstDelay; expectedSince = Time.time;

        if (introCutscene)
            StartCoroutine(IntroRoutine(pl));
        else
            PlayRoar();
    }

    GameObject soundTemplate;

    [Header("Posture toparlanma (Sekiro gibi cok yavas)")]
    [Tooltip("Son posture hasarindan sonra toparlanma baslamadan bekleme (sn).")]
    [Min(0f)] public float postureRecoveryDelay = 2f;
    [Tooltip("Toparlanma hizi (puan/sn). Oyunun normal degeri 20.")]
    [Min(0f)] public float postureRecoverySpeed = 10f;

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
            EnemyHitFeedback.PlayLoud(parrySrc, tf.ParrySound, tf.ParryVolume);
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

    [Header("Ceviklik")]
    [Tooltip("Acik: boss'un saldiri toparlanmasi kisalir, adimlari hizlanir: daha tepkisel, daha az agir.")]
    public bool agile = true;
    [Range(0.4f, 1.5f)] public float agileWindupTime = 0.6f;
    [Range(0.1f, 0.8f)] public float agileTensionTime = 0.24f;
    [Range(0f, 0.8f)] public float agileHoldTime = 0.08f;
    [Range(0.1f, 2f)] public float agileRecoverTime = 0.35f;
    [Range(0.15f, 1f)] public float agileWalkAnimSpeed = 0.4f;

    void Configure()
    {
        // Sahnede kayitli eski (yavas/kolay) degerleri guvenli sekilde yeni dengeye cek.
        waveSpeed = Mathf.Max(waveSpeed, 140f);
        waveMinDistance = Mathf.Max(waveMinDistance, 60f);
        waveHopTargetDistance = Mathf.Max(waveHopTargetDistance, 78f);
        waveHopMaxDistance = Mathf.Max(waveHopMaxDistance, 55f);
        waveInterval = Mathf.Min(waveInterval, 0.85f);
        waveCooldown = Mathf.Min(waveCooldown, 2.5f);
        postureBreakMercy = 0f;                 // oyuncunun blogu kirilsa da boss son vuruslari ATAR
        PlayerDamageReceiver.BossHitPercent = bossHitPercent;

        if (agile && rig != null)
        {
            rig.windupTime = agileWindupTime;
            rig.tensionTime = agileTensionTime;
            rig.holdTime = agileHoldTime;
            rig.recoverTime = agileRecoverTime;
            rig.walkAnimSpeed = agileWalkAnimSpeed;
        }

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
        enemy.useUnblockableAttacks = false;     // rastgele engellenemez yok; Yakalama/Supurme hamle olarak gelir
        enemy.unblockableFrontOnly = true;
        enemy.unblockableForwardReach = attackReach * 0.95f;
        enemy.unblockableHitHeight = hitHeight + 1f;
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

    [Header("Blok itmesi")]
    public bool blockKnockback = true;
    [Min(0f)] public float blockKnockbackDistance = 4.5f;
    [Min(0.05f)] public float blockKnockbackDuration = 0.3f;

    void OnEnable()
    {
        CombatEvents.PlayerBlocked += OnPlayerBlocked;
        CombatEvents.EnemyHit += OnEnemyHit;
        CombatEvents.ParrySucceeded += OnParry;
    }

    void OnDisable()
    {
        EndIntro();
        CombatEvents.PlayerBlocked -= OnPlayerBlocked;
        CombatEvents.EnemyHit -= OnEnemyHit;
        CombatEvents.ParrySucceeded -= OnParry;
    }

    void OnPlayerBlocked(EnemyController e, bool postureBroken)
    {
        if (e == enemy && postureBroken && postureBreakMercy > 0f) mercyPending = true;
        if (!blockKnockback || e != enemy || enemy == null || enemy.target == null) return;
        PlayerController p = enemy.target.GetComponent<PlayerController>();
        if (p == null || p.rb == null || p.isDashing) return;
        float dir = p.rb.position.x >= transform.position.x ? 1f : -1f;
        StartCoroutine(BlockPushRoutine(p, dir));
    }

    IEnumerator BlockPushRoutine(PlayerController p, float dir)
    {
        float dur = blockKnockbackDuration;
        float t = 0f;
        p.inputLocked = true;
        p.inputLockTimer = Mathf.Max(p.inputLockTimer, dur + 0.1f);

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        filter.SetLayerMask(Physics2D.AllLayers);
        filter.useLayerMask = true;
        RaycastHit2D[] hits = new RaycastHit2D[4];

        while (t < dur && p != null && p.rb != null)
        {
            yield return new WaitForFixedUpdate();
            if (p == null || p.rb == null || p.isDashing) yield break;

            float prev = t / dur;
            t += Time.fixedDeltaTime;
            float next = Mathf.Min(1f, t / dur);
            float f0 = 1f - (1f - prev) * (1f - prev);
            float f1 = 1f - (1f - next) * (1f - next);
            float step = blockKnockbackDistance * (f1 - f0);

            Vector2 d = new Vector2(dir, 0f);
            int n = p.rb.Cast(d, filter, hits, step + 0.02f);
            for (int k = 0; k < n; k++)
            {
                Collider2D c = hits[k].collider;
                if (c != null && !c.isTrigger && c.attachedRigidbody != p.rb &&
                    (c.attachedRigidbody == null || c.attachedRigidbody.bodyType == RigidbodyType2D.Static) &&
                    !c.transform.IsChildOf(p.transform) && hits[k].normal.x * dir < -0.5f)
                {
                    step = Mathf.Max(0f, hits[k].distance - 0.02f);
                    break;
                }
            }
            p.rb.position += d * step;
            p.rb.linearVelocity = new Vector2(0f, p.rb.linearVelocity.y);
        }
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

    // ---- hamle kurulumu (kombo + supurme + yakalama + aldatmaca) ----
    void BuildComboMoveset()
    {
        moveset = GetComponent<EnemyMoveset>();
        if (moveset == null) moveset = gameObject.AddComponent<EnemyMoveset>();

        float pf = phase2 ? 0.85f / phase2AttackSpeed : 1f;   // faz 2: uyarilar %15 kisa
        List<AttackMove> list = new List<AttackMove>();
        comboMove = null;

        if (comboEnabled)
        {
            AttackMove m = new AttackMove();
            m.name = ComboName;
            m.weight = 1f;
            m.cooldown = comboCooldown;
            m.recoveryMultiplier = 1f;
            int beats = Mathf.Clamp(comboBeatHits, 2, 5);
            m.hits.Add(new MoveHit(MoveHitType.Normal, comboFirstWindup * pf, 0f, 0.85f));
            for (int bi = 1; bi < beats; bi++)
                m.hits.Add(new MoveHit(MoveHitType.Normal, comboFastWindup * pf, 0f, 0.85f).Retarget());   // hepsi ayni ritim
            comboLastIndex = beats;   // son (gecikmeli) vurusun adim indeksi
            m.hits.Add(new MoveHit(MoveHitType.Normal, comboDelayedWindup * pf, comboDelayedRandom, comboLastDamage).Retarget().ParryReward(comboFinalParryReward));
            comboMove = m;
            list.Add(m);
        }

        float reach = attackReach + 6f;

        sweepMove = new AttackMove { name = SweepName, weight = 1f, cooldown = 0f, maxDistance = reach, recoveryMultiplier = 1.3f };
        sweepMove.hits.Add(new MoveHit(MoveHitType.Sweep, sweepWindup * pf, 0f, 1f, 1f));
        list.Add(sweepMove);


        fakeMove = new AttackMove { name = FakeName, weight = 1f, cooldown = 0f, maxDistance = reach, recoveryMultiplier = 1.1f };
        fakeMove.hits.Add(new MoveHit(MoveHitType.Normal, fakeWindup * pf, fakeWindupRandom, 1f, 1f).ParryReward(1.6f));
        list.Add(fakeMove);

        moveset.moves = list;
        moveset.weightModifier = MoveWeight;
        moveset.comboFirstWindupBonus = 0f;       // ilk vurus da hizli baslasin
        moveset.comboBlockPostureMultiplier = 1f; // kombo vuruslari blokta tam posture yer
        moveset.showComboIndicator = false;
        moveset.noParrySlowMoMidCombo = true;
        moveset.sweepHitHeight = sweepHitHeight;
        moveset.sweepReachMultiplier = sweepReachMultiplier;
        moveset.sweepColor = sweepColor;
        moveset.sweepIgnoresDash = true;          // supurmeyi dash degil ZIPLAMA kurtarir
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

        // Yakin mesafede de ziplama / dalga: sirasi geldiyse normal vurus yerine onu baslat (Update'te state degisir).
        if (skillRotation)
        {
            Skill ex = Expected;
            if (ex == Skill.Leap && leapEnabled && !leapActive && !pendingLeap && Time.time >= nextLeapTime)
            {
                pendingLeap = true;
                return;
            }
            if (ex == Skill.Wave && waveEnabled && !waveActive && !pendingWave && Time.time >= nextWaveTime)
            {
                pendingWave = true;
                return;
            }
        }
        else if (leapEnabled && !leapActive && !pendingLeap && Time.time >= nextLeapTime && Random.value < leapCloseChance)
        {
            pendingLeap = true;
            return;
        }

        // Supurme / Yakalama / Aldatmaca: uyari boyunca kilic yavasca kalkar, renkle ne olduguna dair ipucu.
        if (st != null && (st.MoveName == SweepName || st.MoveName == FakeName))
        {
            MoveHitType ht = st.CurrentHitType;
            Boss1Rig.AttackStyle sty = Boss1Rig.AttackStyle.Fake;
            if (ht == MoveHitType.Sweep) { rig.SetSwordTint(sweepColor, 1f, 0.9f); sty = Boss1Rig.AttackStyle.Sweep; }
            rig.PlayAttack(Mathf.Max(0.3f, st.RemainingWindup), true, sty);
            return;
        }

        rig.PlayAttack();
    }

    // Kombo ipucu: her vurustan 'comboCueLead' sn once kilic parlar + 'tik' (parry'ye basma zamani, ritim).
    void UpdateComboCue()
    {
        EnemyAttackState cst = enemy.CurrentState as EnemyAttackState;
        if (cst == null || cst.MoveName != ComboName || enemy.IsStaggered || enemy.IsDead) return;
        if (cst.RemainingWindup > comboCueLead) return;
        if (cueComboId == comboLastStart && cueStep == cst.StepIndex) return;

        cueComboId = comboLastStart;
        cueStep = cst.StepIndex;
        rig.SetSwordTint(comboCueColor, 1f, 40f);

        if (comboCueVolume > 0f)
        {
            if (cueClip == null) cueClip = BuildCueClip();
            if (cueSrc == null)
            {
                cueSrc = gameObject.AddComponent<AudioSource>();
                cueSrc.playOnAwake = false;
                cueSrc.spatialBlend = 0f;
            }
            cueSrc.PlayOneShot(cueClip, Mathf.Clamp01(comboCueVolume));
        }
    }

    static AudioClip BuildCueClip()
    {
        const int rate = 44100;
        int n = Mathf.RoundToInt(rate * 0.09f);
        float[] d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float env = Mathf.Exp(-t * 55f);
            d[i] = (Mathf.Sin(2f * Mathf.PI * 1250f * t) * 0.7f + Mathf.Sin(2f * Mathf.PI * 2500f * t) * 0.3f) * env;
        }
        AudioClip c = AudioClip.Create("Boss1Cue", n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }

    // Dalga parry'sinin ses/efekti: dalganin carptigi noktada.
    internal void WaveParryFx(Vector3 pos, Vector2 dir)
    {
        PlayParryFallback(true, false);
        if (CombatVFXManager.Instance != null)
            CombatVFXManager.Instance.PlayParry(pos, dir);
        else
            ParryVFX.Play(pos, dir, "Default", 100);
    }

    // Geri seken dalga bossa carpti: kisa sarsinti + kivilcim.
    internal void WaveReflectHit(Vector3 pos, Vector2 dir)
    {
        rig.Flinch(0.6f);
        rig.Squash(0.6f);
        rig.Flash();
        if (CombatVFXManager.Instance != null)
            CombatVFXManager.Instance.PlayParry(pos, dir);
        else
            ParryVFX.Play(pos, dir, "Default", 100);
        RunCameraZoom.AddShake(1.5f);
    }

    // ---- FAZ 2: ofke ----
    void BeginPhase2()
    {
        phase2 = true;
        patIdx = 0;
        expectedSince = Time.time;
        fakeFlag = Random.value < fakeChance;
        pendingLeap = false;
        pendingWave = false;

        walkSpeed *= 1.2f;
        enemy.chaseSpeed = walkSpeed;
        waveCount = 5;
        waveInterval = Mathf.Min(waveInterval, 0.7f);
        waveCooldown *= 0.7f;
        leapCooldown *= 0.8f;
        rig.windupTime *= 0.88f;
        rig.tensionTime *= 0.88f;
        rig.recoverTime *= 0.85f;
        rig.walkAnimSpeed = Mathf.Min(1f, rig.walkAnimSpeed * 1.2f);

        EnemyBalance bal2 = GetComponent<EnemyBalance>();
        if (bal2 != null && phase2PostureScale > 1f)
        {
            bal2.SetMaxBalance(Mathf.RoundToInt(bal2.MaxBalance * phase2PostureScale));
            if (bal2.chipRatioOverride > 0f) bal2.chipRatioOverride *= phase2PostureScale;   // posture buyudu: ayni vurus sayisi ayni can goturur
        }

        // Faz 2 baski: tum saldirilar belirgin hizlanir.
        float ps = Mathf.Max(1f, phase2AttackSpeed);
        rig.windupTime /= ps;
        rig.tensionTime /= ps;
        rig.holdTime /= ps;
        rig.recoverTime /= ps;
        waveCooldown /= ps;
        leapCooldown /= ps;
        comboCooldown /= ps;
        waveInterval /= ps;
        walkSpeed *= 1f + (ps - 1f) * 0.5f;
        enemy.chaseSpeed = walkSpeed;

        BuildComboMoveset();   // faz 2 uyari sureleri ile yeniden kur
        nextWaveTime = Time.time + 1f;

        enemy.ChangeState(new Boss1RoarState(this, enemy));
    }

    AudioClip thudClip;
    AudioSource thudSrc;

    // Alcak 'tok' vurus sesi (kod ile uretilir).
    void PlayThud()
    {
        if (hitThudVolume <= 0f) return;
        if (thudClip == null)
        {
            const int rate = 44100;
            int n = Mathf.RoundToInt(rate * 0.22f);
            float[] d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Exp(-t * 20f);
                lp += (Random.value * 2f - 1f - lp) * 0.08f;
                d[i] = (Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(85f, 42f, Mathf.Clamp01(t * 6f)) * t) * 0.85f + lp * 0.9f) * env;
            }
            thudClip = AudioClip.Create("Boss1Thud", n, 1, rate, false);
            thudClip.SetData(d, 0);
        }
        if (thudSrc == null)
        {
            thudSrc = gameObject.AddComponent<AudioSource>();
            thudSrc.playOnAwake = false;
            thudSrc.spatialBlend = 0f;
        }
        thudSrc.pitch = Random.Range(0.92f, 1.08f);
        thudSrc.PlayOneShot(thudClip, Mathf.Clamp01(hitThudVolume));
    }

    internal float RoarForPhase2() { return PlayRoar(); }

    void OnEnemyHit(EnemyController e, DamageInfo info, HitResult result)
    {
        if (e != enemy)
            return;

        // Denge vurusu hafif, can vurusu agir sarsar.
        rig.Flinch(result.onBalance ? 0.6f : 1.3f);
        rig.Squash(result.onBalance ? 0.7f : 1.1f);
        rig.Flash();

        // AGIRLIK: boss kisa sureligine 'takilir' (sadece boss yavaslar, oyuncu degil) + tok ses + sarsinti.
        if (hitWeight && !result.onBalance && !result.killed)
        {
            EnemyTime.RequestRamp(0.1f, 0.3f, 2f);
            RunCameraZoom.AddShake(0.7f);
            PlayThud();
        }

        // CEZALANDIRMA: boss saldirisini bitirmis, toparlaniyor (acik an) -> duz vurus ek can goturur.
        if (punishHitPercent > 0f && enemy.openUntil > EnemyTime.Now && !enemy.IsStaggered && !result.onBalance
            && !result.killed && health != null && !health.IsDead)
        {
            int bonus = Mathf.Max(1, Mathf.RoundToInt(health.MaxHealth * punishHitPercent));
            bonus = Mathf.Min(bonus, health.CurrentHealth - 1);
            if (bonus > 0)
                health.TakeDamage(bonus);
        }

        // Sersemlemis boss: duz vurus sabit bir yuzde goturur (ne 1 hasar, ne aninda olum).
        // ILK vurus agirdir (buyuk can kaybi + agir cekim), sonrakiler staggerHitPercent (varsayilan 0 = normal).
        if (enemy.IsStaggered && !result.onBalance && !result.killed
            && health != null && !health.IsDead)
        {
            bool first = staggerFirstHitPending;
            staggerFirstHitPending = false;
            // Olumcul darbe canin TAMAMINI bitirmez, bir dilim goturur (kalani kacip vurarak / sonraki darbelerle).
            // Faz 1'de can 1'de kilitli: dilim cani bitirirse Update faz 2'yi baslatir. Faz 2'de dilim cani bitirirse boss olur.
            float pct = first ? (phase2 ? phase2DeadlyBlowPercent : deadlyBlowPercent) : staggerHitPercent;
            if (first) heavyHitUntil = Time.unscaledTime + heavyHitSlowDuration;
            if (pct > 0f && !(first && phase2 && phase2Pending))
            {
                int want = Mathf.RoundToInt(health.MaxHealth * pct) - result.amount;
                int floor = first ? 0 : Mathf.RoundToInt(health.MaxHealth * staggerHitFloorPercent);
                int extra = Mathf.Min(want, health.CurrentHealth - floor);
                if (extra > 0)
                    health.TakeDamage(extra);
            }
            // Agir darbe carptigi an: zaman agir cekime girer, yavasca hizlanir.
            if (first && heavyHitSlowDuration > 0f)
                HitStop.RequestRamp(heavyHitSlowDuration, heavyHitSlowScale, 1f, heavyHitSlowPower, 5);
            if (first)
            {
                // Kirmizi isik soner, boss geriye dogru sendeler ve bagirir.
                rig.OpenGlowSpent = true;
                rig.FlashFor(heavyHitSlowDuration);   // yavas cekim bitene kadar beyaz kal
                rig.Flinch(3f);
                rig.Squash(0.6f);
                if (heavyHitStumbleSpeed > 0f && enemy.target != null)
                {
                    Rigidbody2D brb = GetComponent<Rigidbody2D>();
                    if (brb != null)
                    {
                        float away = transform.position.x >= enemy.target.position.x ? 1f : -1f;
                        brb.linearVelocity = new Vector2(away * heavyHitStumbleSpeed, brb.linearVelocity.y);
                    }
                }
                PlayRoar();
            }
        }
    }

    void OnParry(EnemyController e, bool brokeBalance)
    {
        if (e != enemy)
            return;

        if (waveParryNow)
            return;   // dalga parry'si: boss uzakta, kilic/ezilme tepkisi ve efekti burada uretilmez

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

    // ---------------------------------------------------------------------
    // TEST TUSLARI (sadece Editor / Development build): yetenekleri gormek icin.
    //   Y = Yakalama, U = Supurme, I = Aldatmaca, H = Kombo, J = Dalga, K = Ziplama, N = Duz vurus
    //   L = SECILEN yetenegi SURESIZ tekrarla (kilit) ac/kapat, G = OLUMSUZ mod ac/kapat
    // ---------------------------------------------------------------------
    Skill testLock = Skill.None;
    bool testGod;

    void ForceSkill(Skill s, bool fake)
    {
        Skill[] p = CurPattern;
        for (int i = 0; i < p.Length; i++)
        {
            if (p[i] == s)
            {
                patIdx = i;
                expectedSince = Time.time;
                fakeFlag = fake;
                if (s == Skill.Wave) nextWaveTime = Time.time;
                if (s == Skill.Leap) nextLeapTime = Time.time;
                Debug.Log("[Boss1 TEST] siradaki yetenek: " + s + (fake ? " (aldatmaca)" : ""));
                return;
            }
        }
    }

    void TestKeys()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return;
        if (kb.uKey.wasPressedThisFrame) { testLock = Skill.None; ForceSkill(Skill.Sweep, false); }
        if (kb.iKey.wasPressedThisFrame) { testLock = Skill.None; ForceSkill(Skill.Melee, true); }
        if (kb.hKey.wasPressedThisFrame) { testLock = Skill.None; ForceSkill(Skill.Combo, false); }
        if (kb.jKey.wasPressedThisFrame) { testLock = Skill.None; ForceSkill(Skill.Wave, false); }
        if (kb.kKey.wasPressedThisFrame) { testLock = Skill.None; ForceSkill(Skill.Leap, false); }
        if (kb.nKey.wasPressedThisFrame) { testLock = Skill.None; ForceSkill(Skill.Melee, false); }

        // L: son istenen yetenegi kilitle (her seferinde ayni yetenek) / kilidi ac.
        if (kb.lKey.wasPressedThisFrame)
        {
            if (testLock != Skill.None) { testLock = Skill.None; Debug.Log("[Boss1 TEST] kilit KAPALI"); }
            else { testLock = Expected; Debug.Log("[Boss1 TEST] kilit ACIK: hep " + testLock); }
        }
        if (testLock != Skill.None && Expected != testLock)
            ForceSkill(testLock, false);

        if (kb.gKey.wasPressedThisFrame)
        {
            testGod = !testGod;
            Debug.Log("[Boss1 TEST] olumsuz mod " + (testGod ? "ACIK" : "KAPALI"));
        }
        if (testGod && playerCtl != null)
        {
            Health ph = playerCtl.GetComponent<Health>();
            if (ph != null && !ph.IsDead && ph.CurrentHealth < ph.MaxHealth)
                ph.Heal(ph.MaxHealth);
        }
#endif
    }

    void Update()
    {
        if (enemy == null)
            return;

        TestKeys();

        if (!MainMenu.Active)
            CheckRoarTriggers();

        if (hud != null)
            hud.show = seenRoared && !introActive;

        // Ana menu aciksa (oyuncu kilitli) boss beklesin: yoksa menu arkasinda oyuncuya vurur.
        if (MainMenu.Active || introActive || !seenRoared)
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

        // Faz 2: can esigin altina inince (saldirmiyorken) ofkelen.
        if (!phase2 && !phase2Pending && health != null && !health.IsDead && health.CurrentHealth <= 1)
            StartCoroutine(DeadlyBlowPhase2(Mathf.Max(0.4f, heavyHitUntil - Time.unscaledTime)));   // can cubugu bitti -> faz 2
        if (phase2AtHalfHealth && !phase2 && !phase2Pending && health != null && !health.IsDead && health.CurrentHealth <= health.MaxHealth * phase2HealthPercent
            && enemy.CurrentState is EnemyChaseState && !enemy.IsStaggered && !leapActive && !waveActive)
            BeginPhase2();

        // Siradaki yetenek uzun suredir gerceklesemiyorsa (mesafe vb.) atla: dongu kilitlenmesin.
        if (skillRotation && Time.time > expectedSince + patternPatience && enemy.CurrentState is EnemyChaseState)
            AdvancePattern();

        // Uyari suresi = animasyonun darbeye kadar suresi (Inspector'dan degisirse senkron kalsin).
        enemy.attackWarningTime = rig.ContactTime;
        enemy.attackRecoveryTime = rig.holdTime + rig.recoverTime + extraCooldown;

        // Iksir penceresi: bitirici kombo / ezmeden sonra, oyuncu yaralı ve iksiri varsa boss kisa sure saldirmaz.
        if (potionWindowPending > 0f && !enemy.IsStaggered && !enemy.IsDead && !leapActive && !waveActive
            && !(enemy.CurrentState is EnemyAttackState) && !(enemy.CurrentState is Boss1RoarState))
        {
            float potionW = potionWindowPending;
            potionWindowPending = 0f;
            Health potionHp = enemy.target != null ? enemy.target.GetComponent<Health>() : null;
            if (potionW > 0f && potionHp != null && !potionHp.IsDead && potionHp.MaxHealth > 0
                && potionHp.CurrentHealth < potionHp.MaxHealth * potionWindowBelowHealth
                && PlayerPotion.Instance != null && PlayerPotion.Instance.Charges > 0)
            {
                if (phase2) potionW *= 0.7f;
                enemy.StartAttackRecovery(potionW);
                nextWaveTime = Mathf.Max(nextWaveTime, Time.time + potionW);
                nextLeapTime = Mathf.Max(nextLeapTime, Time.time + potionW);
                comboLastStart = Mathf.Max(comboLastStart, Time.time - comboCooldown + potionW);
            }
        }

        // Oyuncunun posture'u kirildi: boss bir sure saldirmaz (kirik oyuncu tek seferde olmesin).
        if (playerPosture == null) playerPosture = FindFirstObjectByType<PlayerPosture>();
        bool pBroken = playerPosture != null && playerPosture.IsBroken;
        if (pBroken && !postureWasBroken && postureBreakMercy > 0f) mercyPending = true;
        postureWasBroken = pBroken;
        if (mercyPending)
        {
            mercyPending = false;
            mercyUntil = Time.time + postureBreakMercy;
            pendingLeap = false;
            if (!leapActive && !enemy.IsStaggered && !enemy.IsDead
                && (enemy.CurrentState is EnemyAttackState || enemy.CurrentState is Boss1WaveState))
                enemy.ChangeState(new EnemyChaseState(enemy));
            enemy.StartAttackRecovery(postureBreakMercy);
            nextWaveTime = Mathf.Max(nextWaveTime, Time.time + postureBreakMercy);
            nextLeapTime = Mathf.Max(nextLeapTime, Time.time + postureBreakMercy);
            comboLastStart = Mathf.Max(comboLastStart, Time.time - comboCooldown + postureBreakMercy);
        }

        if (pendingWave)
        {
            pendingWave = false;
            if (enemy.CurrentState is EnemyAttackState && !leapActive && !waveActive && !enemy.IsStaggered && !enemy.IsDead)
                enemy.ChangeState(new Boss1WaveState(this, enemy));
            else
                rig.PlayAttack();
        }

        if (pendingLeap)
        {
            pendingLeap = false;
            if (enemy.CurrentState is EnemyAttackState && !leapActive && !enemy.IsStaggered && !enemy.IsDead)
                enemy.ChangeState(new Boss1LeapState(this, enemy));
            else
                rig.PlayAttack();
        }

        // Yetenek dongusu: durum degisince hangi yetenege girildigini kaydet.
        if (!ReferenceEquals(enemy.CurrentState, trackedState))
        {
            trackedState = enemy.CurrentState;
            TrackSkill(trackedState);
        }

        // Uzaktan kombo: oyuncu vurus menzili disindaysa kosarak baslat.
        if (comboEnabled && comboRunEnabled && !SkillBlocked(Skill.Combo) && !leapActive && enemy.CurrentState is EnemyChaseState
            && Time.time >= comboLastStart + comboCooldown + 0.4f
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

        // Uzaktan Supurme: normal vurus menziline girmesini bekleme; siradaki yetenek buysa uzaktan basla.
        if (skillRotation && Expected == Skill.Sweep && !leapActive && !waveActive
            && enemy.CurrentState is EnemyChaseState && Time.time >= farSkillRetry
            && enemy.target != null && !enemy.IsTargetDead && !enemy.IsStaggered && !enemy.IsDead && enemy.CanAttack)
        {
            float fx = Mathf.Abs(enemy.target.position.x - transform.position.x);
            if (fx > enemy.attackRange && fx <= farSkillMaxDistance && Random.value < farSkillChancePerSecond * Time.deltaTime
                && EnemyAttackCoordinator.TryRequestAttack(enemy, enemy.PlannedWindup))
            {
                enemy.ChangeState(new EnemyAttackState(enemy));
                EnemyAttackState fs = enemy.CurrentState as EnemyAttackState;
                if (fs == null || fs.MoveName != SweepName)
                {
                    enemy.ChangeState(new EnemyChaseState(enemy));   // secilemedi: geri don, kisa sure tekrar denemesin
                    farSkillRetry = Time.time + 0.6f;
                }
            }
        }

        // Dash ile yon degistirme: vurus hazirlarken oyuncu dash'le arkaya gecerse boss doner.
        if (dashRetarget && enemy.target != null && !enemy.IsTargetDead)
        {
            if (playerCtl == null) playerCtl = enemy.target.GetComponent<PlayerController>();
            if (playerCtl != null && playerCtl.isDashing) dashSeenTime = Time.time;

            EnemyAttackState ast = enemy.CurrentState as EnemyAttackState;
            if (ast != null && !leapActive && !enemy.IsStaggered && Time.time >= nextRetargetTime
                && Time.time - dashSeenTime <= dashRetargetGrace && ast.RemainingWindup > dashRetargetMinWindup)
            {
                float side = Mathf.Sign(enemy.target.position.x - transform.position.x + 0.0001f);
                if (side != enemy.FacingDirection)
                {
                    enemy.RetargetFacing();
                    nextRetargetTime = Time.time + 0.2f;
                }
            }
        }

        // Dalga saldirisi: uzaktan odaklan, 3 ritimli dalga.
        if (waveEnabled && !leapActive && !waveActive && Time.time >= nextWaveTime && enemy.CurrentState is EnemyChaseState
            && enemy.target != null && !enemy.IsTargetDead && !enemy.IsStaggered && !enemy.IsDead)
        {
            float wx = Mathf.Abs(enemy.target.position.x - transform.position.x);
            float wBoost = 1f + Mathf.Min(4, skillsSinceWave) * waveStaleBoost;   // uzun suredir dalga yok: daha sik
            if (SkillBlocked(Skill.Wave)) { }
            else if (wx >= waveMinDistance && wx <= waveMaxDistance && Random.value < waveChancePerSecond * wBoost * Time.deltaTime)
                enemy.ChangeState(new Boss1WaveState(this, enemy));
            else if (waveBackHop && wx < waveMinDistance && !InMercy && Random.value < waveCloseChancePerSecond * wBoost * Time.deltaTime)
                enemy.ChangeState(new Boss1WaveState(this, enemy));   // yakin: once geri zipla
        }

        if (leapEnabled && !leapActive && !SkillBlocked(Skill.Leap) && Time.time >= nextLeapTime && enemy.CurrentState is EnemyChaseState
            && enemy.target != null && !enemy.IsTargetDead && !enemy.IsStaggered && !enemy.IsDead)
        {
            float lx = Mathf.Abs(enemy.target.position.x - transform.position.x);
            if (lx >= leapMinDistance && lx <= leapMaxDistance && Random.value < leapChancePerSecond * Time.deltaTime)
                enemy.ChangeState(new Boss1LeapState(this, enemy));
        }

        bool attacking = enemy.CurrentState is EnemyAttackState;
        UpdateComboVfx(attacking);
        UpdateComboCue();

        rig.MoveAllowed = enemy.CurrentState is EnemyChaseState;
        rig.Staggered = enemy.IsStaggered;
        rig.Dead = enemy.IsDead;

        // ACILDIGI an (denge kirildi, sersem): uyari sesi bir kez calar.
        bool openNow = enemy.IsStaggered && !enemy.IsDead;
        if (openNow && !openWarnWas) { PlayOpenWarn(); staggerFirstHitPending = true; rig.OpenGlowSpent = false; }
        openWarnWas = openNow;

        // Saldiri vurustan ONCE bozuldu (sersemledi, baska duruma gecti): animasyonu yumusakca birak.
        if (rig.IsAttacking && !attacking && !leapActive && !waveActive)
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
            o.rig.PoundGround();
            o.rig.SetBaseSquash(0f, 3f);
            float sh = last ? o.leapShake : o.leapShake * 0.7f;
            if (CameraShake.Instance != null) CameraShake.Instance.Shake(sh);
            RunCameraZoom.AddShake(sh * 0.5f);

            DamagePlayer();

            // Boss oyuncunun uzerine indiyse govde icinde sikismasin: en yakin kenara al.
            {
                PlayerController lp = e.target != null ? e.target.GetComponent<PlayerController>() : null;
                if (lp != null && lp.col != null)
                    HitboxUtil.SeparateHorizontally(lp.rb, lp.col, o.GetComponent<Collider2D>());
            }

            t = 0f;
            if (last)
            {
                ph = Ph.Land;
                o.nextLeapTime = Time.time + o.leapCooldown;
                e.openUntil = EnemyTime.Now + o.leapLandTime + 0.5f;   // inis sonrasi acik an
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
            if (o.InMercy) return;   // posture kirik oyuncuya ek hasar yok

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
            o.nextLeapTime = Mathf.Max(o.nextLeapTime, Time.time + 1.5f);
            o.potionWindowPending = Mathf.Max(o.potionWindowPending, o.potionWindowAfterLeap);
        }
    }

    // =====================================================================
    // Boss1WaveState: odaklan (kilic yavas kalkar) -> RITIMLI dalgalar (her darbede bir dalga)
    // =====================================================================
    internal sealed class Boss1WaveState : IEnemyState
    {
        enum Ph { Crouch, Hop, Focus }

        readonly Boss1Enemy o;
        readonly EnemyController e;
        Rigidbody2D rb;
        Ph ph;
        int fired;
        bool needNext, subscribed, airborne;
        float t, phT, doneT, groundY, startX, hopDir, hopDist;
        Collider2D[] cols;
        bool[] colWas;

        public Boss1WaveState(Boss1Enemy owner, EnemyController enemy) { o = owner; e = enemy; }

        public void Enter()
        {
            rb = o.GetComponent<Rigidbody2D>();
            o.waveActive = true;
            e.superArmorOverride = true;
            e.RetargetFacing();
            e.LockFacing(true);
            if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            groundY = o.transform.position.y;
            startX = o.transform.position.x;
            o.rig.AttackHit += OnHit;
            subscribed = true;

            hopDist = ComputeHop();
            if (hopDist >= 6f)
            {
                ph = Ph.Crouch; phT = 0f;
                o.rig.SetBaseSquash(0.5f, 14f);               // geri zipla: once coker
            }
            else StartFocus();
        }

        // Oyuncu yakinsa geriye ne kadar ziplanabilir (duvar payi dahil). 0 = ziplama yok.
        float ComputeHop()
        {
            if (!o.waveBackHop || e.target == null) return 0f;
            float dx = e.target.position.x - o.transform.position.x;
            float adx = Mathf.Abs(dx);
            if (adx >= o.waveMinDistance) return 0f;
            hopDir = dx >= 0f ? -1f : 1f;
            float want = Mathf.Clamp(o.waveHopTargetDistance - adx, 8f, o.waveHopMaxDistance);

            if (rb != null)
            {
                ContactFilter2D filter = new ContactFilter2D();
                filter.useTriggers = false;
                filter.SetLayerMask(Physics2D.AllLayers);
                filter.useLayerMask = true;
                RaycastHit2D[] hits = new RaycastHit2D[6];
                int n = rb.Cast(new Vector2(hopDir, 0f), filter, hits, want + 1f);
                for (int k = 0; k < n; k++)
                {
                    Collider2D c = hits[k].collider;
                    if (c != null && !c.isTrigger && (c.attachedRigidbody == null || c.attachedRigidbody.bodyType == RigidbodyType2D.Static)
                        && hits[k].normal.x * hopDir < -0.5f)
                        want = Mathf.Min(want, Mathf.Max(0f, hits[k].distance - 1f));
                }
            }
            // ZEMIN KONTROLU: ziplamanin yolunda ve inecegi yerde zemin yoksa (harita kenari / ucurum)
            // sadece zeminin bittigi yere kadar ziplar.
            float safe = 0f;
            landY = groundY;
            for (float d = 2f; d <= want; d += 2f)
            {
                float gy;
                if (!GroundAt(o.transform.position.x + hopDir * d, out gy))
                    break;
                safe = d;
                landY = gy;
            }
            if (safe < want)
                want = Mathf.Max(0f, safe - 3f);   // kenardan 3 birim pay
            if (want >= 6f)
            {
                float gy2;
                if (GroundAt(o.transform.position.x + hopDir * want, out gy2))
                    landY = gy2;
                else
                    want = 0f;
            }
            return want;
        }

        float landY;

        // x konumunun altinda sabit zemin var mi? (boss ve oyuncu sayilmaz)
        bool GroundAt(float x, out float y)
        {
            y = groundY;
            RaycastHit2D[] hs = Physics2D.RaycastAll(new Vector2(x, groundY + 4f), Vector2.down, 14f);
            for (int i = 0; i < hs.Length; i++)
            {
                Collider2D c = hs[i].collider;
                if (c == null || c.isTrigger) continue;
                if (c.transform.IsChildOf(o.transform)) continue;
                if (c.attachedRigidbody != null && c.attachedRigidbody.bodyType != RigidbodyType2D.Static) continue;
                y = hs[i].point.y;
                return true;
            }
            return false;
        }

        void StartFocus()
        {
            ph = Ph.Focus; phT = 0f;
            o.rig.SetBaseSquash(0f, 6f);
            o.rig.PlayAttack(o.waveFocusTime, true);            // yavas kalkis: odaklandigi anlasilsin
            o.rig.SetSwordTint(o.waveColor, 1f, 0.9f);
            CombatCallout.Popup(o.transform.position + new Vector3(0f, o.waveHeight * 1.3f, 0f), "DALGA!", o.waveColor, 2.4f);
        }

        void BeginHop()
        {
            ph = Ph.Hop; phT = 0f;
            airborne = true;
            cols = o.GetComponentsInChildren<Collider2D>();
            colWas = new bool[cols.Length];
            for (int i = 0; i < cols.Length; i++) { colWas[i] = cols[i].enabled; cols[i].enabled = false; }
            if (rb != null) { rb.linearVelocity = Vector2.zero; rb.bodyType = RigidbodyType2D.Kinematic; }
            o.rig.SetBaseSquash(-0.5f, 14f);
        }

        void SetPos(float x, float y)
        {
            if (rb != null) rb.position = new Vector2(x, y);
            o.transform.position = new Vector3(x, y, o.transform.position.z);
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

        void OnHit()
        {
            if (ph != Ph.Focus || fired >= o.waveCount) return;
            float dir = e.FacingDirection;
            Vector3 bp = o.transform.position;
            float fwd = o.waveSpawnForward;
            if (e.target != null)
            {
                // Tepki payi: dalga oyuncuya en az ~0.3 sn'de ulassin (cok yakinsa bossun hemen onunde dogar).
                float gap = Mathf.Abs(e.target.position.x - bp.x);
                fwd = Mathf.Clamp(gap - o.waveSpeed * 0.3f, 0f, fwd);
            }
            RunCameraZoom.AddShake(2.2f);
            Boss1Wave.Spawn(o, e, new Vector2(bp.x + dir * fwd, bp.y), dir);
            fired++;
            if (fired < o.waveCount) needNext = true;
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
            phT += dt;

            if (ph == Ph.Crouch)
            {
                if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                if (phT >= o.waveHopCrouch) BeginHop();
                return;
            }

            if (ph == Ph.Hop)
            {
                float k = Mathf.Clamp01(phT / o.waveHopTime);
                SetPos(startX + hopDir * hopDist * k, Mathf.Lerp(groundY, landY, k) + 4f * o.waveHopHeight * k * (1f - k));
                if (k >= 1f)
                {
                    SetPos(startX + hopDir * hopDist, landY);
                    airborne = false;
                    Restore();
                    o.rig.Squash(0.8f);
                    RunCameraZoom.AddShake(1.5f);
                    StartFocus();
                }
                return;
            }

            if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            if (needNext)
            {
                needNext = false;
                o.rig.PlayAttack(Mathf.Max(0.3f, o.waveInterval - dt));   // esit aralikli ritim
            }

            if (fired < o.waveCount && Mathf.Abs(e.target.position.x - o.transform.position.x) < o.waveAbortDistance)
            {
                e.ChangeState(new EnemyChaseState(e));   // oyuncu cok yaklasti: iptal
                return;
            }

            if (fired >= o.waveCount && !o.rig.IsAttacking)
            {
                doneT += dt;
                if (doneT >= o.waveEndDelay) { e.ChangeState(new EnemyChaseState(e)); return; }
            }

            if (t > 15f) e.ChangeState(new EnemyChaseState(e));
        }

        public void Exit()
        {
            if (subscribed) { o.rig.AttackHit -= OnHit; subscribed = false; }
            if (airborne) SetPos(o.transform.position.x, groundY);   // yarida kesildi: yere indir
            Restore();
            o.rig.SetBaseSquash(0f, 6f);
            o.waveActive = false;
            e.superArmorOverride = false;
            e.LockFacing(false);
            if (fired < o.waveCount && o.rig.IsAttacking) o.rig.CancelAttack();
            o.rig.SetSwordTint(o.waveColor, 0f, 3f);
            o.nextWaveTime = Time.time + o.waveCooldown;
            e.openUntil = EnemyTime.Now + 0.8f;
        }
    }

    // =====================================================================
    // Boss1RoarState: faz 2 kukremesi. Boss kisa sure hareketsiz kalir (oyuncu icin firsat).
    // =====================================================================
    internal sealed class Boss1RoarState : IEnemyState
    {
        readonly Boss1Enemy o;
        readonly EnemyController e;
        float t, dur;

        public Boss1RoarState(Boss1Enemy owner, EnemyController enemy) { o = owner; e = enemy; }

        public void Enter()
        {
            dur = o.phase2RoarTime;
            e.superArmorOverride = true;
            e.openUntil = EnemyTime.Now + dur;
            if (o.rb != null) o.rb.linearVelocity = new Vector2(0f, o.rb.linearVelocity.y);
            o.rig.Squash(1f);
            o.rig.Flash();
            float len = o.RoarForPhase2();
            dur = Mathf.Max(dur, Mathf.Min(len, 2.5f));
            o.rig.BeatChest(dur);
            Vector3 top = o.transform.position + new Vector3(0f, o.waveHeight * 1.3f, 0f);
            CombatCallout.Popup(top, "OFKE!", new Color(1f, 0.2f, 0.1f), 3f);
            BalanceBreakFx.Play(o.transform.position + new Vector3(0f, 6f, 0f), null);
            RunCameraZoom.AddShake(6f);
            if (CameraShake.Instance != null) CameraShake.Instance.Shake(5f);
        }

        public void Tick()
        {
            t += EnemyTime.DeltaTime;
            if (o.rb != null) o.rb.linearVelocity = new Vector2(0f, o.rb.linearVelocity.y);
            if (t >= dur)
                e.ChangeState(new EnemyChaseState(e));
        }

        public void Exit()
        {
            e.superArmorOverride = false;
        }
    }
}

/// <summary>
/// Boss1'in dalgasi: yerde ilerleyen yarim ay. Oyuncuya degince parry / blok / hasar cozulur.
/// Dash (i-frame) icinden gecer, ziplayarak ustunden asilir.
/// </summary>
public class Boss1Wave : MonoBehaviour
{
    Boss1Enemy o;
    EnemyController e;
    float dir, life, groundY;
    bool resolved;
    SpriteRenderer sr;
    bool reflected;
    static Sprite spr, sprReflect;

    public static Boss1Wave Spawn(Boss1Enemy owner, EnemyController enemy, Vector2 pos, float direction)
    {
        GameObject go = new GameObject("Boss1Wave");
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        Boss1Wave w = go.AddComponent<Boss1Wave>();
        w.o = owner; w.e = enemy; w.dir = direction >= 0f ? 1f : -1f; w.groundY = pos.y;

        if (spr == null) spr = BuildSprite(new Color(0.85f, 0.02f, 0.02f), new Color(1f, 0.82f, 0.55f));
        w.sr = go.AddComponent<SpriteRenderer>();
        w.sr.sprite = spr;
        w.sr.sortingLayerName = "Default";
        w.sr.sortingOrder = 45;
        Color c = owner.waveColor; w.sr.color = new Color(c.r, c.g, c.b, 1f);

        // Sprite 'waveHeight' birim yuksek olacak sekilde olceklenir (ppu = piksel yuksekligi).
        float h = owner.waveHeight;
        float sc = h * (spr.pixelsPerUnit / spr.rect.height);
        go.transform.localScale = new Vector3(w.dir * sc, sc, 1f);
        return w;
    }

    void Update()
    {
        if (o == null || e == null) { Destroy(gameObject); return; }
        float dt = EnemyTime.DeltaTime;
        life += dt;
        if (life > 8f) { Destroy(gameObject); return; }

        Vector3 p = transform.position;
        float prevX = p.x;
        p.x += dir * o.waveSpeed * (reflected ? o.waveReflectSpeedMultiplier : 1f) * dt;
        transform.position = p;

        // Nabiz
        if (sr != null)
        {
            float k = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 22f);
            if (reflected)
                sr.color = new Color(0.8f + 0.2f * k, 0.8f + 0.2f * k, 1f, 1f);
            else
            {
                Color c = o.waveColor;
                sr.color = new Color(Mathf.Min(1f, c.r * (0.8f + 0.3f * k)), c.g + 0.1f * k, c.b + 0.05f * k, 1f);
            }
        }

        // Parry'lenmis dalga: renk degisti, bossa dogru geri seker.
        if (reflected)
        {
            float toBoss = (o.transform.position.x - transform.position.x) * dir;
            if (toBoss <= o.waveReflectStopDistance || life > 6f)
            {
                o.WaveReflectHit(new Vector3(transform.position.x, transform.position.y + o.waveHeight * 0.4f, 0f), new Vector2(dir, 0f));
                Destroy(gameObject);
            }
            return;
        }

        if (resolved || e.target == null || e.IsTargetDead) return;
        if (o.InMercy) return;                                // posture kirik oyuncudan dalga gecer

        PlayerController pl = e.target.GetComponent<PlayerController>();
        if (pl == null) return;

        // SUPURMELI carpisma: bu karede dalganin kat ettigi yol boyunca oyuncu govdesine degdi mi?
        // (Frame takilmasi / yuksek hizda dalga oyuncunun icinden "atlayip" gecmesin.)
        float bodyHalf = pl.col != null ? pl.col.bounds.extents.x * 0.4f : 0f;
        float hw = o.waveHitHalfWidth + bodyHalf;
        float px = e.target.position.x;
        float relNow = (px - p.x) * dir;                     // + : oyuncu dalganin onunde
        float relPrev = (px - prevX) * dir;
        if (relNow > hw || relPrev < -hw * 1.5f) return;
        float feet = pl.col != null ? pl.col.bounds.min.y : pl.transform.position.y;
        if (feet > groundY + o.waveHeight) return;           // ustunden atladi

        Resolve(pl, p);
    }

    void Resolve(PlayerController pl, Vector3 pos)
    {
        resolved = true;

        if (pl.isInvincible)
        {
            CombatEvents.RaiseDodge(e, false);               // dash'le icinden gecti: dalga yoluna devam eder
            return;
        }

        Vector2 hitDir = new Vector2(dir, 0f);
        PlayerDefenseController def = pl.GetComponent<PlayerDefenseController>();

        if (def != null && def.CanParry())
        {
            def.PlayParryFeedback();
            EnemyBalance bal = e.GetComponent<EnemyBalance>();
            bool broke = false;
            if (bal != null)
            {
                bal.AddParryBalanceDamage(Mathf.Max(1, Mathf.RoundToInt(e.parryBalanceDamage * o.waveParryBalanceMultiplier)));
                broke = bal.IsBroken;
            }
            e.PlayBalanceDamageFlash();
            o.waveParryNow = true;
            CombatEvents.RaiseParry(e, broke);
            o.waveParryNow = false;
            o.WaveParryFx(new Vector3(pos.x, pos.y + o.waveHeight * 0.4f, 0f), -hitDir);

            // Geri sek: rengi degis, bossa dogru don.
            reflected = true;
            dir = -dir;
            life = 0f;
            if (sprReflect == null) sprReflect = BuildSprite(o.waveReflectColor, Color.white);
            sr.sprite = sprReflect;
            Vector3 ls = transform.localScale;
            transform.localScale = new Vector3(-ls.x, ls.y, ls.z);
            return;
        }

        // Dalga BLOKLANAMAZ: sadece parry. Blok / pozisyon fark etmez, parry degilse hasar.
        if (def != null && def.IsBlocking)
            def.CancelDefense();

        PlayerDamageReceiver rcv = pl.GetComponent<PlayerDamageReceiver>();
        if (rcv != null)
        {
            rcv.TakeDamage(
                Mathf.Max(1, Mathf.RoundToInt(e.attackDamage * o.waveDamageMultiplier)),
                hitDir,
                e.attackKnockbackForce,
                e.attackKnockbackVerticalForce,
                e.attackKnockbackDuration,
                e.attackKnockbackDeceleration,
                e,
                PlayerHitKind.Unblockable,
                false
            );
            EnemyAttackCoordinator.NotifyPlayerHit();
        }
        Destroy(gameObject);
    }

    // Yarim ay: one dogru (+x) tasan, arkasi oyuk. Pivot: alt orta (yere basar). Parlak cekirdek, kirmizi kenar.
    static Sprite BuildSprite(Color edgeCol, Color coreCol)
    {
        const int W = 96, H = 128;
        Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[W * H];

        float a1 = W * 0.62f, b1 = H * 0.5f, c1x = W * 0.30f, cy = H * 0.5f;      // dis elips
        float a2 = W * 0.58f, b2 = H * 0.46f, c2x = W * 0.02f;                     // ic (oyuk) elips

        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float dx1 = (x - c1x) / a1, dy1 = (y - cy) / b1;
                float d1 = Mathf.Sqrt(dx1 * dx1 + dy1 * dy1);
                float dx2 = (x - c2x) / a2, dy2 = (y - cy) / b2;
                float d2 = Mathf.Sqrt(dx2 * dx2 + dy2 * dy2);

                float inner = 1f - d1;     // dis kenardan iceri
                float outer = d2 - 1f;     // oyuktan disari
                float edge = Mathf.Min(inner, outer);
                float a = Mathf.Clamp01(edge * 14f);
                if (a <= 0f) { px[y * W + x] = new Color32(0, 0, 0, 0); continue; }

                float core = Mathf.SmoothStep(0f, 0.16f, edge);
                Color col = Color.Lerp(edgeCol, coreCol, core);
                px[y * W + x] = new Color32((byte)(col.r * 255), (byte)(col.g * 255), (byte)(col.b * 255), (byte)(a * 255));
            }

        tex.SetPixels32(px);
        tex.Apply();
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), H, 0, SpriteMeshType.FullRect);
        sp.name = "Boss1WaveSprite";
        return sp;
    }
}

/// <summary>
/// Boss olum yavas cekimi: boss yok edilse bile calisir (bagimsiz nesne). Duraklatma menusu acikken zamana dokunmaz.
/// </summary>
public class Boss1SlowMo : MonoBehaviour
{
    static Boss1SlowMo inst;
    float baseFixed;

    public static void Run(float scale, float hold, float release)
    {
        if (inst == null)
        {
            GameObject go = new GameObject("Boss1SlowMo");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<Boss1SlowMo>();
            inst.baseFixed = Time.fixedDeltaTime;
        }
        inst.StopAllCoroutines();
        inst.StartCoroutine(inst.Routine(scale, hold, release));
    }

    IEnumerator Routine(float scale, float hold, float release)
    {
        float t = 0f;
        float total = hold + release;
        while (t < total)
        {
            if (!MainMenu.Active)
            {
                t += Time.unscaledDeltaTime;
                float k = t <= hold ? 0f : Mathf.Clamp01((t - hold) / Mathf.Max(0.01f, release));
                float sc = Mathf.Lerp(scale, 1f, k * k * (3f - 2f * k));
                Time.timeScale = sc;
                Time.fixedDeltaTime = baseFixed * sc;
            }
            yield return null;
        }
        if (!MainMenu.Active) Time.timeScale = 1f;
        Time.fixedDeltaTime = baseFixed;
    }

    void OnDestroy()
    {
        if (!MainMenu.Active) Time.timeScale = 1f;
        if (baseFixed > 0f) Time.fixedDeltaTime = baseFixed;
        if (inst == this) inst = null;
    }
}

/// <summary>
/// Boss1 Supurme gorseli: boss'tan yerde hizla yayilan alcak sok dalgasi (ustunden ZIPLA).
/// Hasari EnemyAttackState verir; bu sadece okunur bir isaret.
/// </summary>
public class Boss1Shock : MonoBehaviour
{
    static Sprite spr;

    SpriteRenderer sr;
    float t, life = 0.34f, len;
    Color col;

    public static Boss1Shock Spawn(Vector3 pos, float dir, float length, Color color)
    {
        if (spr == null)
        {
            const int W = 128, H = 32;
            Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            Color32[] px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                float vy = 1f - Mathf.Abs((y + 0.5f) / H * 2f - 1f);
                for (int x = 0; x < W; x++)
                {
                    float hx = (x + 0.5f) / W;
                    float a = Mathf.Clamp01(vy * 1.4f) * (0.25f + 0.75f * hx);
                    if (hx > 0.94f) a *= Mathf.Clamp01((1f - hx) / 0.06f);
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            spr = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0f, 0.5f), 16f);
        }

        GameObject go = new GameObject("Boss1Shock");
        go.transform.position = pos + new Vector3(0f, 1.1f, 0f);
        Boss1Shock s = go.AddComponent<Boss1Shock>();
        s.sr = go.AddComponent<SpriteRenderer>();
        s.sr.sprite = spr;
        s.sr.sortingLayerName = "Default";
        s.sr.sortingOrder = 46;
        s.len = Mathf.Max(2f, length);
        s.col = color;
        go.transform.localScale = new Vector3(dir >= 0f ? 0.1f : -0.1f, 1.3f, 1f);
        return s;
    }

    void Update()
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / life);
        float grow = 1f - (1f - k) * (1f - k) * (1f - k);
        float sign = transform.localScale.x >= 0f ? 1f : -1f;
        transform.localScale = new Vector3(sign * Mathf.Max(0.1f, len / 8f * grow), Mathf.Lerp(1.4f, 0.3f, k), 1f);
        Color c = Color.Lerp(Color.white, col, k * 0.8f);
        c.a = Mathf.Pow(1f - k, 1.3f);
        sr.color = c;
        if (k >= 1f)
            Destroy(gameObject);
    }
}
