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
    [Tooltip("Boss'un cani. Normal dusman ~300. Oyuncunun duz vurusu can hasarini kismis verir; sersemleyince can gider.")]
    [Min(1)] public int maxHealth = 900;
    [Tooltip("Denge (posture) cubugu. Parry denge doldurur; dolunca boss sersemler (infaz firsati).")]
    [Min(1)] public int maxBalance = 200;
    [Tooltip("Tek parry'nin boss dengesine verdigi hasar. (Normal dusman: 50 / 45 denge)")]
    [Min(1)] public int parryBalanceDamage = 50;
    [Tooltip("Denge kirilinca boss'un sersemleme suresi (sn).")]
    [Min(0.5f)] public float staggerDuration = 3.5f;

    [Header("Hareket (dunya birimi)")]
    [Tooltip("Yurume hizi (birim/sn). Agir olsun diye yavas. Normal dusman: 10.")]
    [Min(0.5f)] public float walkSpeed = 4.5f;
    [Tooltip("Oyuncuyu bu mesafeden once fark eder (0 = hep avlar).")]
    [Min(0f)] public float chaseRange = 120f;
    [Tooltip("Oyuncuya bu mesafeye kadar yaklasir, sonra vurur. Kilic darbesi gövdeden ~9.5 - 23.5 birim oteye iner.")]
    [Min(1f)] public float stopDistance = 15f;

    [Header("Saldiri")]
    [Tooltip("Vurusun one dogru erisimi (gövde merkezinden, birim). Kilic ucu ~23.5.")]
    [Min(1f)] public float attackReach = 24f;
    [Tooltip("Oyuncunun ayagi boss'un ayagindan bu kadar yukaridaysa vurustan kurtulur (ziplama). Kilic yerde yatiyor: yuksek tutuldu.")]
    [Min(0.5f)] public float hitHeight = 3.5f;
    [Tooltip("Vurustan sonra bir sonraki saldiriya kadar ek bekleme (sn). Animasyon (bekleme + toparlanma) buna eklenir.")]
    [Min(0f)] public float extraCooldown = 0.7f;
    [Tooltip("Oyuncuya vurus hasari. (Oyuncu hasari oyunda 'Fixed Hit Percent' ile sabit; bu deger istatistik/olum icin.)")]
    [Min(1)] public int attackDamage = 20;
    [Tooltip("Oyuncuyu geri iten kuvvet.")]
    [Min(0f)] public float hitKnockback = 14f;
    [Tooltip("Acik: saldiri baslayinca oyuncu vuruslariyla kesilmez (boss agirligi).")]
    public bool superArmorWhileAttacking = true;

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

        // 4) Yon gostergesi: EnemyController yonu bir SpriteRenderer.flipX'ten okur/yazar.
        //    Gorunmez bir yardimci SpriteRenderer kullaniyoruz; Boss1Rig bunu okuyup aynalar.
        GameObject proxyGo = new GameObject("FacingProxy");
        proxyGo.transform.SetParent(transform, false);
        facingProxy = proxyGo.AddComponent<SpriteRenderer>();
        facingProxy.enabled = false;
        facingProxy.flipX = true;                // rig dogal olarak SOLA bakar
        rig.facingSource = facingProxy;

        // 5) Dusman beyni.
        enemy = GetComponent<EnemyController>();
        if (enemy == null)
            enemy = gameObject.AddComponent<EnemyController>();

        enemy.SetFacingRenderer(facingProxy);
        Configure();

        enemy.AttackAnimationRequested += OnAttackAnimationRequested;
    }

    void Configure()
    {
        // Hareket
        enemy.chaseSpeed = walkSpeed;
        enemy.chaseRange = chaseRange;
        enemy.alwaysHunt = chaseRange <= 0f;
        enemy.chaseStopDistance = stopDistance;
        enemy.useStandby = false;                // tek boss: sira bekleme yok

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
        CombatEvents.EnemyHit -= OnEnemyHit;
        CombatEvents.ParrySucceeded -= OnParry;
    }

    void OnDestroy()
    {
        if (enemy != null)
            enemy.AttackAnimationRequested -= OnAttackAnimationRequested;
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
    }

    void OnParry(EnemyController e, bool brokeBalance)
    {
        if (e != enemy)
            return;

        rig.OnParried(brokeBalance);
    }

    void Update()
    {
        if (enemy == null)
            return;

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
