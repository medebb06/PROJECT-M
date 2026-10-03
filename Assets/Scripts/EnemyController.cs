using System.Collections.Generic;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    // Sahnedeki tüm aktif düşmanlar (halka slotu doluluk kontrolü için).
    public static readonly List<EnemyController> All =
        new List<EnemyController>();

    private static int nextSpawnIndex;

    // Her düşmana benzersiz, artan numara (eşitlik bozmak için).
    public int SpawnIndex { get; private set; }

    [Header("Attack")]
    public float attackRange = 1.5f;

    [Header("Chase")]
    public float chaseStopDistance = 1.8f;
    public float chaseSpeed = 4f;

    [Tooltip(
        "Açıkken düşman oyuncuyu HER MESAFEDEN kovalar, Idle'a düşmez. " +
        "(Koşu modunda doğan düşmanlar bunu açar: uzakta takılı kalan " +
        "düşman bölümü kilitlemesin.)")]
    public bool alwaysHunt = false;

    [Header("Standby (sıra bekleme / dağılma)")]
    [Tooltip(
        "Açıkken: saldırıya hazır düşmanlar sıraya girer. Sıradaki oyuncuya " +
        "yaklaşır, diğerleri sıra numarasına göre geride kademeli bekler. " +
        "Kapalıysa eski davranış (hepsi yığılır).")]
    public bool useStandby = true;

    [Tooltip("Sırada 2. olan düşmanın oyuncuya uzaklığı.")]
    public float standbyDistance = 4.5f;

    [Tooltip(
        "Sıradaki her düşman bir öncekinden bu kadar daha geride durur. " +
        "Düşman collider'ı genişse artır.")]
    public float standbySpacing = 1.5f;

    [Tooltip("Bekleme pozisyonuna yürürken hız çarpanı (temkinli görünsün).")]
    [Range(0.1f, 1f)]
    public float standbySpeedMultiplier = 0.7f;

    [Tooltip("Hedef konuma bu kadar yaklaşınca durur (titremeyi önler).")]
    public float standbyArrivalTolerance = 0.2f;

    [Tooltip(
        "Sıradaki düşmanların oyuncudan en fazla ne kadar uzakta " +
        "bekleyebileceği. Kalabalık için artır.")]
    public float standbyMaxDistance = 9f;

    [Tooltip(
        "Düşman genişliğine eklenen boşluk. Slot aralığı hiçbir zaman " +
        "'genişlik + bu değer'den küçük olmaz.")]
    public float ringSlotPadding = 0.15f;

    [Tooltip(
        "Saldırı halkasının iç slotunun oyuncuya en fazla ne kadar " +
        "yaklaşabileceği. İç slot bundan yakına düşüyorsa kullanılmaz " +
        "(ikinci düşman halkanın arkasında bekler).")]
    public float ringInnerMinDistance = 1.3f;

    [Header("Depth Layering (görsel katman)")]
    [Tooltip(
        "Düşmanlar üst üste binince ön/arka düzeni verir (fiziksel değil, " +
        "sadece çizim sırası). Saldırıya hazırlananlar en önde, " +
        "geri kalanlar oyuncuya yakınlığa göre sıralanır.")]
    public bool useDepthSorting = true;

    [Tooltip(
        "En arkadaki düşman en fazla kaç kademe geriye gider. " +
        "Düşmanlar zemin/arka plan arkasına düşerse azalt.")]
    [Range(0, 8)]
    public int depthMaxLevels = 4;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    [Header("Stagger")]
    public float staggerDuration = 1.2f;
    public float attackWarningTime = 1f;

    [Header("Attack Recovery")]
    public float attackRecoveryTime = 0.8f;

    [Header("Attack Commit (Super Armor)")]
    [Tooltip(
        "Uyarı süresinin bu oranından sonra saldırı hasar alınca " +
        "KESİLMEZ ve düşman savrulmaz. 0 = baştan itibaren, " +
        "1 = hiç (eski davranış). Ağır düşman: ~0.15, normal: ~0.4, " +
        "hafif: 1.")]
    [Range(0f, 1f)]
    public float attackCommitPoint = 0.4f;

    public Color committedHitFlashColor =
        new Color(1f, 0.6f, 0.1f);

    public float committedFlashDuration = 0.1f;

    [Tooltip("Kararlı saldırı vurulduğunda ek çalan ses (boşsa sessiz).")]
    public AudioClip committedHitClip;

    [Header("Unblockable Attack (engellenemez vuruş)")]
    [Tooltip(
        "Açıkken: bu düşman ara sıra ENGELLENEMEZ bir vuruş yapar. " +
        "Parry ve block işe yaramaz; tek cevap dash (i-frame) veya geri " +
        "çekilmektir. Uyarısı sarı ve hızlı yanıp söner.")]
    public bool useUnblockableAttacks = true;

    [Tooltip("Her saldırıda engellenemez olma olasılığı.")]
    [Range(0f, 1f)]
    public float unblockableChance = 0.25f;

    [Tooltip(
        "İki engellenemez vuruş arasında en az kaç normal saldırı olsun. " +
        "(Peş peşe gelmesin.)")]
    [Min(0)]
    public int unblockableMinNormalAttacksBetween = 2;

    [Tooltip("Uyarı süresi çarpanı. Uzun uyarı = dash için okunur zaman.")]
    [Min(1f)]
    public float unblockableWindupMultiplier = 1.6f;

    [Tooltip(
        "Engellenemez vuruşun hasar anına bu kadar saniye EKLENİR. " +
        "Negatif = hasar daha erken gelir. (Uyarı süresini ve ritim " +
        "koordinatörünü de etkiler.)")]
    public float unblockableHitOffset = 0f;

    [Tooltip(
        "Saldırı animasyonunun BAŞLANGICINDAN hasar (vuruş) karesine kadar " +
        "geçen süre (saniye). Engellenemez vuruşun uzun uyarısında animasyon " +
        "bu kadar süre kala başlatılır, böylece vuruş karesi hasarla aynı " +
        "ana denk gelir. 0 = animasyon klibinin uzunluğu kullanılır " +
        "(bulunamazsa Attack Warning Time).")]
    [Min(0f)]
    public float attackAnimationHitTime = 0f;

    [Tooltip(
        "Uyarının bu oranından sonra hasar alınca kesilmez. " +
        "0 = baştan itibaren, 1 = hiç. Normal saldırıdan daha erken " +
        "kararlı olması önerilir.")]
    [Range(0f, 1f)]
    public float unblockableCommitPoint = 0.15f;

    [Tooltip(
        "Engellenemez vuruşun oyuncunun CAN birimine verdiği hasar. " +
        "Block ve parry bu vuruşa karşı tamamen etkisizdir. " +
        "DİKKAT: Health 'birim' mantığıyla çalışır (1 = bir can birimi); " +
        "oyuncunun Max Health'inden büyük bir değer anında öldürür.")]
    [Min(1)]
    public int unblockableDamage = 75;

    [Tooltip("Oyuncuya uygulanan savrulma çarpanı.")]
    [Min(0f)]
    public float unblockableKnockbackMultiplier = 1.4f;

    [Tooltip(
        "KAÇIŞ PAYI: Dash bittikten sonra bu kadar süre içinde de hâlâ " +
        "kaçmış sayılırsın. Dash basma penceresi = Dash Time + bu değer.")]
    [Min(0f)]
    public float unblockableDodgeGrace = 0.12f;

    [Tooltip(
        "Vuruş anında erişim mesafesi bu çarpanla küçülür. " +
        "1 altında geri çekilmek kolaylaşır.")]
    [Range(0.5f, 1f)]
    public float unblockableReachMultiplier = 0.9f;

    [Tooltip(
        "Vuruşa bu kadar kala 'ŞİMDİ KAÇ' işareti verilir " +
        "(simge ve bant beyaza döner, ses çalar).")]
    [Min(0.05f)]
    public float unblockableDodgeCueLead = 0.4f;

    [Header("Attack Shape (yönlü / uzun vuruş)")]
    [Tooltip(
        "Açıkken: engellenemez vuruş SADECE düşmanın baktığı tarafa, " +
        "uzun bir kutu içinde isabet eder. Arkasına geçen ya da " +
        "yeterince yükseğe zıplayan oyuncu vurulmaz. Kapalıysa eski " +
        "dairesel erişim.")]
    public bool unblockableFrontOnly = true;

    [Tooltip(
        "Engellenemez vuruşun ileriye doğru uzunluğu (düşman merkezinden). " +
        "Normal Attack Range'den büyük olması 'uzun vuruş' hissini verir.")]
    [Min(0.5f)]
    public float unblockableForwardReach = 5.5f;

    [Tooltip(
        "Vuruş kutusunun yüksekliği (düşmanın ayaklarından yukarı). " +
        "Oyuncunun ayakları bunun üstündeyse vuruş ıskalar (zıplayarak kaçış).")]
    [Min(0.2f)]
    public float unblockableHitHeight = 2.2f;

    [Tooltip(
        "AÇIKKEN: düşman saldırıya BAŞLADIĞI anda baktığı yönü kilitler ve " +
        "saldırı bitene kadar oyuncuya DÖNMEZ. Vuruş sadece o yöne isabet " +
        "eder; oyuncu dash ile arkasına geçerse vuruş boşa gider. " +
        "(Normal ve engellenemez tüm saldırılar için geçerli.)")]
    public bool lockFacingDuringAttack = true;

    [Tooltip(
        "Açıkken yön kilidi vuruş anında açılır (düşman recovery'de oyuncuya " +
        "döner). Kapalıyken saldırı bitene kadar (recovery dahil) kilitli kalır, " +
        "yani arkasına geçen oyuncuyu recovery boyunca göremez.")]
    public bool releaseFacingAtHit = false;

    [Tooltip(
        "Yönlü vuruşlarda düşmanın hemen arkasında kalan pay " +
        "(tam üst üste duran oyuncu da vurulsun diye).")]
    [Min(0f)]
    public float attackBackTolerance = 0.4f;

    [Tooltip(
        "Açıkken NORMAL saldırılar da sadece baktığı tarafa isabet eder " +
        "(menzil = Attack Range). Arkasına geçen oyuncu vurulmaz. " +
        "Kapalıyken eski dairesel erişim: yön kilidi tek başına yetmez.")]
    public bool normalAttackFrontOnly = true;

    [Min(0.2f)]
    public float normalAttackHitHeight = 2.2f;

    [Header("Parry Slow-Mo (sadece düşmanlar)")]
    [Tooltip(
        "Bu düşman parry edilince TÜM düşmanların zamanı yavaşlar " +
        "(oyuncu ve dünya normal). Animasyonlar yavaşlar, sesler normal. " +
        "Ağır başlar, zamanla normale döner. Süre GERÇEK saniye. " +
        "Karşı saldırı için zaman kazandırır.")]
    public HitSlowMotion parrySlowMo =
        new HitSlowMotion(1.6f, 0.25f, 1.5f);

    [Tooltip(
        "Parry bu düşmanın dengesini KIRDIYSA (stagger, execute fırsatı).")]
    public HitSlowMotion parryBreakSlowMo =
        new HitSlowMotion(2.2f, 0.2f, 1.5f);

    [Tooltip(
        "İki normal parry slow-mo'su arasındaki en az süre (gerçek sn). " +
        "0 = her parry yavaşlatır. Dengeyi kıran parry bundan etkilenmez.")]
    [Min(0f)]
    public float parrySlowMoCooldown = 0f;

    [Header("Danger Indicator (okunurluk)")]
    [Tooltip("Engellenemez vuruşta düşmanın başının üstünde '!' simgesi.")]
    public bool showDangerIcon = true;

    [Tooltip("Zeminde vuruşun erişim alanını gösteren bant.")]
    public bool showDangerZone = true;

    public Color dangerColor = new Color(1f, 0.85f, 0.1f);

    public float dangerIconScale = 1.2f;

    [Tooltip("Simgenin sprite'ın üstünden yüksekliği.")]
    public float dangerIconHeightOffset = 0.4f;

    [Header("Unblockable Recovery")]
    [Tooltip(
        "Vuruş sonrası recovery çarpanı. Boşa giden (dash ile kaçılan) " +
        "engellenemez vuruş düşmanı uzun süre açıkta bırakır.")]
    [Min(1f)]
    public float unblockableRecoveryMultiplier = 1.6f;

    [Header("Hit Deceleration")]
    public float knockbackDeceleration = 45f;

    [Header("Hit Flash")]
    public Color hitFlashColor = Color.white;
    public float hitFlashDuration = 0.06f;

    [Header("Stagger Flash")]
    public Color staggerFlashColor = Color.yellow;
    public float staggerFlashDuration = 0.15f;

    [Header("Balance Damage Flash (beyaz flaş)")]
    [Tooltip(
        "Denge (posture) hasarı alınca sprite tamamen beyaz yanıp söner. " +
        "SpriteWhiteFlash.shader dosyası Assets/Resources içinde olmalı; " +
        "yoksa sadece renk tonu değişir (renkli sprite'ta görünmez).")]
    public bool solidWhiteFlash = true;

    public Color balanceHitFlashColor = Color.white;

    public float balanceHitFlashDuration = 0.09f;

    [Header("Enemy Attack Damage (Player'ın can birimine)")]
    [Tooltip(
        "Bu düşmanın normal vuruşunun oyuncunun Health'inden düştüğü miktar. " +
        "Health 'birim' mantığıyla çalışır: 1 = bir can birimi. " +
        "Block/parry'de can hasarı yoktur.")]
    [Min(1)]
    public int attackDamage = 1;

    [Header("Enemy Attack Knockback (Player'a uygulanır)")]
    public float attackKnockbackForce = 7f;
    public float attackKnockbackVerticalForce = 3f;
    public float attackKnockbackDuration = 0.12f;

    [Tooltip(
        "Savrulma sonunda oyuncunun yatay hızı bu ivmeyle sıfırlanır " +
        "(birim/sn²). Küçük = uzun kayar, büyük = çabuk durur, " +
        "0 = ani dur. Ek kayma ≈ kuvvet² / (2 × bu değer).")]
    public float attackKnockbackDeceleration = 40f;

    [Header("Balance Hit Knockback")]
    public float balanceHitKnockbackForce = 1.5f;
    public float balanceHitKnockbackVerticalForce = 0f;
    public float balanceHitKnockbackDuration = 0.10f;
    public float balanceHitKnockbackDeceleration = 15f;

    [Header("Posture Hit Knockback")]
    public float postureKnockbackForce = 6f;
    public float postureKnockbackVerticalForce = 1f;
    public float postureKnockbackDuration = 0.12f;

    [Header("Health Hit Knockback")]
    public float healthKnockbackForce = 8f;
    public float healthKnockbackVerticalForce = 1.5f;
    public float healthKnockbackDuration = 0.12f;

    [Header("Combo Knockback")]
    public float attack1KnockbackForce = 4f;
    public float attack2KnockbackForce = 5.5f;
    public float attack3KnockbackForce = 7f;
    public float attack4KnockbackForce = 9f;

    [Header("Block Knockback")]
    public float blockKnockbackForce = 2.5f;
    public float blockKnockbackVerticalForce = 0.2f;
    public float blockKnockbackDuration = 0.15f;
    public float blockKnockbackDeceleration = 12f;

    [Header("Defense Balance")]
    public int parryBalanceDamage = 2;

    [Header("Player Block Posture")]
    // Oyuncunun block'ladığı her vuruşta PlayerPosture'dan
    // düşen miktar. (PlayerPosture max 100.)
    public int blockPostureDamage = 25;

    [Header("Ground Slam")]
    // Oyuncunun ground slam'i bu düşmanın dengesine
    // ne kadar hasar verir.
    public int slamBalanceDamage = 3;

    [Header("Execute")]
    public int executeDamage = 10;
    public float executeDistance = 1.2f;
    public float executeDuration = 0.08f;

    [Header("Hit Sounds")]
    public AudioSource hitAudioSource;
    public AudioClip healthHitClip;
    public AudioClip postureHitClip;
    public AudioClip knockbackClip;

    public Transform target;
    public float chaseRange = 5f;
    [Header("Facing")]
    [SerializeField] private SpriteRenderer enemySprite;

    private float attackRecoveryTimer;
    private float movementLockTimer;

    private EnemyBalance enemyBalance;
    private IEnemyState currentState;

    private Health health;
    private Health targetHealth;
    private FinisherTargetHighlight finisherHighlight;
    private EnemyAttackTelegraph telegraph;
    private Collider2D bodyCollider;

    // Derinlik katmanı
    private int[] baseSortingOrders;
    private int baseMinSortingOrder;
    private int depthSpan = 1;
    private int currentDepthLevel;
    private SpriteRenderer targetSprite;

    private bool deathHandled;
    private float nextTargetSearchTime;

    // =========================================================
    // HIT FLASH
    // =========================================================

    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;
    private Material[] originalMaterials;

    private static Material sharedFlashMaterial;
    private static bool warnedMissingFlashShader;
    private Coroutine flashRoutine;

    // Stagger boyunca korunan renk tonu.
    // Hit flash bitince bu tona geri dönülür.
    private float staggerTintEndTime;

    public bool CanAttack =>
        attackRecoveryTimer <= 0f;

    public bool IsMovementLocked =>
        movementLockTimer > 0f;

    public bool IsStaggered =>
        currentState is EnemyStaggerState;

    public bool IsDead =>
        health != null &&
        health.IsDead;

    // Saldırının kararlı aşamasında mı?
    public bool IsAttackCommitted =>
        currentState is EnemyAttackState attack &&
        attack.IsCommitted;

    // Hedef (oyuncu) öldü mü?
    public bool IsTargetDead
    {
        get
        {
            if (target == null)
                return false;

            if (
                targetHealth == null ||
                targetHealth.gameObject != target.gameObject
            )
            {
                targetHealth =
                    target.GetComponent<Health>();
            }

            return targetHealth != null &&
                   targetHealth.IsDead;
        }
    }

    public IEnemyState CurrentState =>
        currentState;

    public void PlayAttackAnimation()
    {
        if (animator == null)
            return;

        animator.SetTrigger("Attack");
    }

    private void Awake()
    {
        SpawnIndex = nextSpawnIndex++;

        if (hitAudioSource == null)
        {
            hitAudioSource =
                GetComponent<AudioSource>();

            if (hitAudioSource == null)
            {
                hitAudioSource =
                    gameObject.AddComponent<AudioSource>();
            }
        }

        hitAudioSource.playOnAwake = false;

        health =
            GetComponent<Health>();

        finisherHighlight =
            GetComponent<FinisherTargetHighlight>();

        telegraph =
            GetComponent<EnemyAttackTelegraph>();

        bodyCollider =
            GetComponent<Collider2D>();

        enemyBalance =
            GetComponent<EnemyBalance>();

        if (enemyBalance != null)
        {
            enemyBalance.OnBalanceBroken +=
                HandleBalanceBroken;
        }

        // -----------------------------------------------------
        // HIT FLASH
        // -----------------------------------------------------

        spriteRenderers =
            GetComponentsInChildren<SpriteRenderer>(true);

        originalColors =
            new Color[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null)
                continue;

            originalColors[i] =
                spriteRenderers[i].color;
        }

        originalMaterials =
            new Material[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null)
                continue;

            originalMaterials[i] =
                spriteRenderers[i].sharedMaterial;
        }

        // Derinlik katmanı için temel sorting order'lar.
        // Bir düşmanın çocuk sprite'ları (varsa) birbirine göre
        // sırasını korusun diye kayma miktarı = iç aralığı kadar.
        baseSortingOrders =
            new int[spriteRenderers.Length];

        int minOrder = int.MaxValue;
        int maxOrder = int.MinValue;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null)
                continue;

            int order =
                spriteRenderers[i].sortingOrder;

            baseSortingOrders[i] = order;

            if (order < minOrder) minOrder = order;
            if (order > maxOrder) maxOrder = order;
        }

        if (minOrder != int.MaxValue)
        {
            baseMinSortingOrder = minOrder;
            depthSpan = Mathf.Max(1, maxOrder - minOrder + 1);
        }
    }

    private void OnEnable()
    {
        if (!All.Contains(this))
            All.Add(this);

        if (useDepthSorting)
            EnemyDepthSorter.EnsureExists();
    }

    private void OnDisable()
    {
        All.Remove(this);

        SetDepthLevel(0);
    }

    // =========================================================
    // DERİNLİK KATMANI (görsel ön/arka sıra)
    // level 0 = en önde (orijinal sorting order),
    // her seviye düşmanı bir kademe geriye alır.
    // =========================================================

    public void SetDepthLevel(int level)
    {
        if (
            spriteRenderers == null ||
            baseSortingOrders == null
        )
        {
            return;
        }

        level = Mathf.Max(0, level);

        if (level == currentDepthLevel)
            return;

        currentDepthLevel = level;

        // Oyuncuyla olan sıralama ilişkisini bozma: düşman oyuncunun
        // ÖNÜNDE çiziliyorsa, geriye kaydırırken oyuncunun arkasına geçmesin.
        int shift =
            Mathf.Min(
                level * depthSpan,
                GetMaxDepthShift()
            );

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null)
                continue;

            spriteRenderers[i].sortingOrder =
                baseSortingOrders[i] - shift;
        }
    }

    private int GetMaxDepthShift()
    {
        if (target == null)
            return int.MaxValue;

        if (targetSprite == null)
        {
            targetSprite =
                target.GetComponentInChildren<SpriteRenderer>();
        }

        if (
            targetSprite == null ||
            spriteRenderers.Length == 0 ||
            spriteRenderers[0] == null ||
            targetSprite.sortingLayerID !=
            spriteRenderers[0].sortingLayerID
        )
        {
            return int.MaxValue;
        }

        // Düşman oyuncunun önünde çiziliyorsa (order daha büyük),
        // en fazla oyuncunun hemen üstüne kadar inebilir.
        if (baseMinSortingOrder > targetSprite.sortingOrder)
        {
            return
                baseMinSortingOrder -
                targetSprite.sortingOrder -
                1;
        }

        return int.MaxValue;
    }

    // =========================================================
    // SLOT GEOMETRİSİ (standby / saldırı halkası)
    // =========================================================

    // Düşmanın yatay genişliği (collider'dan). Bulunamazsa 1.
    public float BodyWidth
    {
        get
        {
            if (bodyCollider == null)
                return 1f;

            float width =
                bodyCollider.bounds.size.x;

            return width > 0.05f
                ? width
                : 1f;
        }
    }

    // Slotlar arası mesafe: ayarlanan aralık ya da gerçek genişlik
    // (hangisi büyükse). Geniş sprite'larda üst üste binmeyi önler.
    public float SlotSpacing =>
        Mathf.Max(
            standbySpacing,
            BodyWidth + ringSlotPadding
        );

    // Saldırı halkasının dış slotu: menzilin hemen içi.
    public float RingOuter =>
        Mathf.Min(chaseStopDistance, attackRange) - 0.05f;

    public float RingInner =>
        RingOuter - SlotSpacing;

    private void OnDrawGizmosSelected()
    {
        // Seçili düşmanın hedefi etrafındaki slotları gösterir
        // (Scene görünümünde): kırmızı = halka, sarı = bekleme sırası.
        if (target == null)
            return;

        Vector3 center = target.position;
        float spacing = SlotSpacing;

        for (int side = -1; side <= 1; side += 2)
        {
            Gizmos.color = Color.red;

            Gizmos.DrawWireSphere(
                center + Vector3.right * side * RingOuter,
                0.25f
            );

            if (RingInner >= ringInnerMinDistance)
            {
                Gizmos.DrawWireSphere(
                    center + Vector3.right * side * RingInner,
                    0.2f
                );
            }

            Gizmos.color = Color.yellow;

            float baseDistance =
                Mathf.Max(standbyDistance, RingOuter + spacing);

            for (int rank = 1; rank <= 5; rank++)
            {
                float d =
                    Mathf.Min(
                        baseDistance + (rank - 1) * spacing,
                        standbyMaxDistance
                    );

                Gizmos.DrawWireSphere(
                    center + Vector3.right * side * d,
                    0.15f
                );
            }
        }
    }

    private void OnDestroy()
    {
        if (enemyBalance != null)
        {
            enemyBalance.OnBalanceBroken -=
                HandleBalanceBroken;
        }
    }

    // =========================================================
    // TARGET
    // =========================================================

    // Hedef yoksa Player tag'iyle bulmayı dener.
    // Her karede aramasın diye 0.5 sn'de bir dener.
    public bool TryFindTarget()
    {
        if (target != null)
            return true;

        if (Time.time < nextTargetSearchTime)
            return false;

        nextTargetSearchTime =
            Time.time + 0.5f;

        GameObject playerObj =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObj == null)
            return false;

        target =
            playerObj.transform;

        return true;
    }

    private void FaceTarget()
    {
        // Saldırının uyarısında yön kilitlenmişse oyuncuya dönme.
        if (facingLocked)
            return;

        if (target == null)
            return;

        if (enemySprite == null)
            return;

        if (target.position.x > transform.position.x)
        {
            enemySprite.flipX = false;
        }
        else if (target.position.x < transform.position.x)
        {
            enemySprite.flipX = true;
        }
    }

    private void Update()
    {
        // Düşman zamanı: Animator ve ses aynı ölçekle akar.
        ApplyEnemyTimeScale();

        // =====================================================
        // ÖLÜ DÜŞMAN
        // Health fade-out sırasında düşman hâlâ Update
        // alıyordu ve ölürken bile oyuncuya vurabiliyordu.
        // =====================================================

        if (health != null && health.IsDead)
        {
            if (!deathHandled)
                HandleDeath();

            return;
        }

        if (attackRecoveryTimer > 0f)
        {
            attackRecoveryTimer -=
                EnemyTime.DeltaTime;

            if (attackRecoveryTimer < 0f)
                attackRecoveryTimer = 0f;
        }

        if (movementLockTimer > 0f)
        {
            movementLockTimer -=
                EnemyTime.DeltaTime;

            if (movementLockTimer < 0f)
                movementLockTimer = 0f;
        }

        UpdateStaggerTint();

        FaceTarget();

        currentState?.Tick();
    }

    private void HandleDeath()
    {
        deathHandled = true;

        // Kaynak ne olursa olsun (vuruş, slam, execute, zehir) tek yerden.
        CombatEvents.RaiseEnemyKilled(this);

        // Telegraph kapatılmazsa her karede rengi ezip
        // fade-out'u bozar.
        EnemyAttackTelegraph telegraph =
            GetComponent<EnemyAttackTelegraph>();

        if (telegraph != null)
            telegraph.enabled = false;

        // Ölürken engellenemez vuruş göstergeleri ekranda kalmasın.
        EnemyDangerIndicator danger =
            GetComponent<EnemyDangerIndicator>();

        if (danger != null)
            danger.Hide();

        SetFinisherHighlight(false);
    }

    private void Start()
    {
        TryFindTarget();

        ChangeState(
            new EnemyIdleState(this)
        );
    }

    public void ChangeState(
        IEnemyState newState
    )
    {
        if (newState == null)
            return;

        if (currentState == newState)
            return;

        currentState?.Exit();

        currentState = newState;

        currentState.Enter();
    }

    public void ForceStagger()
    {
        if (IsStaggered)
            return;

        PlayFlash(
            staggerFlashColor,
            staggerFlashDuration
        );

        staggerTintEndTime =
            EnemyTime.Now + staggerFlashDuration;

        ChangeState(
            new EnemyStaggerState(this)
        );
    }

    public void StartAttackRecovery()
    {
        attackRecoveryTimer =
            attackRecoveryTime;
    }

    public void StartAttackRecovery(float duration)
    {
        attackRecoveryTimer =
            Mathf.Max(0f, duration);
    }

    // =========================================================
    // SALDIRI PLANI (normal / engellenemez)
    // Karar, saldırı BAŞLAMADAN verilir ki ritim koordinatörü
    // doğru uyarı süresiyle vuruş anını hesaplayabilsin.
    // =========================================================

    private bool attackPlanned;
    private bool plannedUnblockable;
    private int normalAttacksSinceUnblockable;

    // Animasyonun başlangıcından vuruş karesine kadar süre.
    // Elle verilmediyse Attack klibinin uzunluğu otomatik bulunur.
    private float detectedAttackClipLength = -1f;

    public float AttackAnimationHitTime
    {
        get
        {
            if (attackAnimationHitTime > 0f)
                return attackAnimationHitTime;

            if (detectedAttackClipLength < 0f)
                detectedAttackClipLength = DetectAttackClipLength();

            return detectedAttackClipLength > 0f
                ? detectedAttackClipLength
                : attackWarningTime;
        }
    }

    private float DetectAttackClipLength()
    {
        if (
            animator == null ||
            animator.runtimeAnimatorController == null
        )
        {
            return 0f;
        }

        AnimationClip[] clips =
            animator.runtimeAnimatorController.animationClips;

        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];

            if (
                clip != null &&
                clip.name.IndexOf(
                    "attack",
                    System.StringComparison.OrdinalIgnoreCase
                ) >= 0
            )
            {
                Debug.Log(
                    "EnemyController: saldırı klibi '" + clip.name +
                    "' uzunluğu " + clip.length.ToString("0.00") +
                    " sn bulundu (Attack Animation Hit Time = 0 iken " +
                    "vuruş anı olarak kullanılır)."
                );

                return clip.length;
            }
        }

        return 0f;
    }

    public float WindupFor(bool unblockable)
    {
        return unblockable
            ? Mathf.Max(
                0.1f,
                attackWarningTime * unblockableWindupMultiplier +
                unblockableHitOffset
            )
            : attackWarningTime;
    }

    // Sıradaki saldırının uyarı süresi. ChaseState bunu koordinatöre verir.
    public float PlannedWindup
    {
        get
        {
            EnsureAttackPlanned();

            // Beklerken başka bir düşman engellenemez vuruşa başladıysa
            // bunu normale çevir: aynı anda iki engellenemez vuruş olmasın.
            if (
                plannedUnblockable &&
                AnotherUnblockableIsWindingUp()
            )
            {
                plannedUnblockable = false;
            }

            return WindupFor(plannedUnblockable);
        }
    }

    // Saldırı başlarken çağrılır: planı tüketir, engellenemez mi söyler.
    public bool ConsumePlannedAttack()
    {
        EnsureAttackPlanned();

        bool unblockable =
            plannedUnblockable &&
            !AnotherUnblockableIsWindingUp();

        attackPlanned = false;

        if (unblockable)
            normalAttacksSinceUnblockable = 0;
        else
            normalAttacksSinceUnblockable++;

        return unblockable;
    }

    private void EnsureAttackPlanned()
    {
        if (attackPlanned)
            return;

        plannedUnblockable =
            RollUnblockable();

        attackPlanned = true;
    }

    private bool RollUnblockable()
    {
        if (
            !useUnblockableAttacks ||
            unblockableChance <= 0f
        )
        {
            return false;
        }

        if (
            normalAttacksSinceUnblockable <
            unblockableMinNormalAttacksBetween
        )
        {
            return false;
        }

        if (AnotherUnblockableIsWindingUp())
            return false;

        return Random.value < unblockableChance;
    }

    private bool AnotherUnblockableIsWindingUp()
    {
        for (int i = 0; i < All.Count; i++)
        {
            EnemyController other = All[i];

            if (other == null || other == this || other.IsDead)
                continue;

            EnemyAttackState attack =
                other.CurrentState as EnemyAttackState;

            if (
                attack != null &&
                attack.IsUnblockable &&
                attack.IsWindingUp
            )
            {
                return true;
            }
        }

        return false;
    }

    // =========================================================
    // YÖN (yönlü vuruşlar için)
    // =========================================================

    private bool facingLocked;

    public void LockFacing(bool locked)
    {
        facingLocked = locked;
    }

    // +1 = sağa, -1 = sola bakıyor.
    // (FaceTarget: hedef sağdaysa flipX = false, yani sprite sağa bakar.)
    public float FacingDirection =>
        enemySprite != null && enemySprite.flipX
            ? -1f
            : 1f;

    // Düşmanın ayak hizası (vuruş kutusu buradan yukarı ölçülür).
    public float FeetY =>
        bodyCollider != null
            ? bodyCollider.bounds.min.y
            : transform.position.y;

    // Durum etkileri (zehir vb.) için kısa renk tonu flaşı.
    public void PlayTintFlash(Color color, float duration)
    {
        PlayFlash(color, duration, false);
    }

    // Düşman zamanına göre ölçeklenmiş koşma hızı.
    public float ScaledChaseSpeed =>
        chaseSpeed * EnemyTime.Scale;

    // Düşman zaman ölçeğini Animator'a uygular (animasyon sayaçlarla
    // senkron kalsın). Sesler yavaşlamaz: pitch'e dokunulmaz.
    private void ApplyEnemyTimeScale()
    {
        if (animator != null)
            animator.speed = EnemyTime.Scale;
    }

    // Parry sonrası: tüm düşmanlar yavaşlar, oyuncu normal kalır.
    public void PlayParrySlowMotion(bool brokeBalance)
    {
        HitSlowMotion profile =
            brokeBalance
                ? parryBreakSlowMo
                : parrySlowMo;

        if (profile == null || !profile.enabled)
            return;

        // Dengeyi kıran parry (execute fırsatı) cooldown'a takılmaz.
        if (
            !brokeBalance &&
            Time.unscaledTime - EnemyTime.LastRequestTime <
            parrySlowMoCooldown
        )
        {
            return;
        }

        EnemyTime.RequestRamp(
            profile.duration,
            profile.startTimeScale,
            profile.rampPower
        );
    }

    // Engellenemez vuruşun başlangıcında net bir "dikkat!" işareti.
    public void PlayAlertFlash()
    {
        const float duration = 0.15f;

        // Telegraph rengi her karede sprite'ı ezdiği için kısa süre sustur.
        if (telegraph != null)
            telegraph.Suppress(duration);

        PlayFlash(
            Color.white,
            duration,
            solidWhiteFlash
        );
    }

    // =========================================================
    // FINISHER HIGHLIGHT
    // =========================================================

    public void SetFinisherHighlight(bool highlighted)
    {
        if (finisherHighlight == null)
            return;

        finisherHighlight.SetHighlighted(
            highlighted
        );
    }

    // =========================================================
    // BALANCE HIT
    // =========================================================

    public void ApplyBalanceHit(
        Vector2 hitDirection
    )
    {
        // Kararlı saldırı: kesilmez, savrulmaz.
        if (TryAbsorbCommittedHit())
            return;

        // Denge hasarı: beyaz flaş.
        PlayBalanceDamageFlash();

        Rigidbody2D rb =
            GetComponent<Rigidbody2D>();

        if (rb == null)
            return;

        float direction =
            Mathf.Sign(hitDirection.x);

        if (direction == 0f)
            direction = 1f;

        Vector2 knockback =
            new Vector2(
                direction *
                balanceHitKnockbackForce,
                balanceHitKnockbackVerticalForce
            );

        movementLockTimer =
            balanceHitKnockbackDuration;

        if (!IsStaggered)
        {
            ChangeState(
                new EnemyHitState(
                    this,
                    balanceHitKnockbackDuration,
                    balanceHitKnockbackDeceleration
                )
            );
        }

        // Yavaşlama sırasında savrulma da yavaş (yatay).
        rb.linearVelocity =
            new Vector2(
                knockback.x * EnemyTime.Scale,
                knockback.y
            );
    }

    // =========================================================
    // ATTACK HIT - OLD COMPATIBILITY METHOD
    // =========================================================

    public void ApplyAttackHit(
        Vector2 hitDirection,
        bool healthHit
    )
    {
        ApplyAttackHit(
            hitDirection,
            healthHit,
            1
        );
    }

    // =========================================================
    // ATTACK HIT - COMBO VERSION
    // =========================================================

    public void ApplyAttackHit(
        Vector2 hitDirection,
        bool healthHit,
        int attackStep
    )
    {
        // Kararlı saldırı: kesilmez, savrulmaz.
        if (TryAbsorbCommittedHit())
            return;

        // Can vuruşu: eski renk tonu flaşı.
        // Denge kıran vuruş (healthHit = false): beyaz flaş.
        if (healthHit)
        {
            PlayFlash(
                hitFlashColor,
                hitFlashDuration
            );
        }
        else
        {
            PlayBalanceDamageFlash();
        }

        Rigidbody2D rb =
            GetComponent<Rigidbody2D>();

        if (rb == null)
            return;

        float direction =
            Mathf.Sign(hitDirection.x);

        if (direction == 0f)
            direction = 1f;

        float knockbackForce;

        if (healthHit)
        {
            knockbackForce =
                GetComboKnockbackForce(
                    attackStep
                );
        }
        else
        {
            knockbackForce =
                postureKnockbackForce;
        }

        float knockbackVerticalForce =
            healthHit
                ? healthKnockbackVerticalForce
                : postureKnockbackVerticalForce;

        float knockbackDuration =
            healthHit
                ? healthKnockbackDuration
                : postureKnockbackDuration;

        Vector2 knockback =
            new Vector2(
                direction *
                knockbackForce,
                knockbackVerticalForce
            );

        movementLockTimer =
            knockbackDuration;

        if (!IsStaggered)
        {
            ChangeState(
                new EnemyHitState(
                    this,
                    knockbackDuration
                )
            );
        }

        // Yavaşlama sırasında savrulma da yavaş (yatay).
        rb.linearVelocity =
            new Vector2(
                knockback.x * EnemyTime.Scale,
                knockback.y
            );

        PlayKnockbackSound();
    }

    // =========================================================
    // COMBO KNOCKBACK FORCE
    // =========================================================

    private float GetComboKnockbackForce(
        int attackStep
    )
    {
        switch (attackStep)
        {
            case 1:
                return attack1KnockbackForce;

            case 2:
                return attack2KnockbackForce;

            case 3:
                return attack3KnockbackForce;

            case 4:
                return attack4KnockbackForce;

            default:
                return attack1KnockbackForce;
        }
    }

    // =========================================================
    // HIT FLASH
    // =========================================================

    // Denge (posture) hasarı alınca çağrılır: beyaz flaş.
    // Shader bulunamazsa renk tonuna düşer (eski davranış).
    public void PlayBalanceDamageFlash()
    {
        PlayFlash(
            balanceHitFlashColor,
            balanceHitFlashDuration,
            solidWhiteFlash
        );
    }

    private void PlayFlash(
        Color flashColor,
        float duration,
        bool solid = false
    )
    {
        if (spriteRenderers == null ||
            spriteRenderers.Length == 0)
            return;

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);

            RestoreBaseColors();
        }

        flashRoutine =
            StartCoroutine(
                FlashCoroutine(
                    flashColor,
                    duration,
                    solid
                )
            );
    }

    private System.Collections.IEnumerator FlashCoroutine(
        Color flashColor,
        float duration,
        bool solid
    )
    {
        // Sprite renkliyse "beyaz tint" hiçbir şey değiştirmez.
        // Gerçek flaş için sprite'ı tam dolu silüete çeviren
        // materyale geçici olarak geçiyoruz.
        bool useSolid =
            solid &&
            GetFlashMaterial() != null;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null)
                continue;

            if (useSolid)
            {
                spriteRenderers[i].sharedMaterial =
                    sharedFlashMaterial;
            }

            Color color =
                spriteRenderers[i].color;

            color.r = flashColor.r;
            color.g = flashColor.g;
            color.b = flashColor.b;

            spriteRenderers[i].color =
                color;
        }

        yield return new WaitForSeconds(
            duration
        );

        RestoreBaseColors();

        flashRoutine = null;
    }

    private static Material GetFlashMaterial()
    {
        if (sharedFlashMaterial != null)
            return sharedFlashMaterial;

        Shader shader =
            Shader.Find("Custom/SpriteWhiteFlash");

        if (shader == null)
        {
            if (!warnedMissingFlashShader)
            {
                warnedMissingFlashShader = true;

                Debug.LogWarning(
                    "EnemyController: 'Custom/SpriteWhiteFlash' " +
                    "shader'ı bulunamadı. SpriteWhiteFlash.shader " +
                    "dosyasını Assets/Resources/ klasörüne koy. " +
                    "O zamana kadar flaş sadece renk tonu olarak çalışır."
                );
            }

            return null;
        }

        sharedFlashMaterial =
            new Material(shader);

        return sharedFlashMaterial;
    }

    // Flash bitince dönülecek renk:
    // Stagger tonu hâlâ aktifse o, değilse orijinal renk.
    // (Eskiden stagger sırasında alınan ilk vuruşun flash'ı
    // stagger rengini siliyordu.)
    private void RestoreBaseColors()
    {
        if (spriteRenderers == null)
            return;

        bool staggerTinted =
            IsStaggered &&
            EnemyTime.Now < staggerTintEndTime;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null)
                continue;

            // Flaş için değiştirilen materyali geri ver.
            if (
                originalMaterials != null &&
                originalMaterials[i] != null &&
                spriteRenderers[i].sharedMaterial !=
                originalMaterials[i]
            )
            {
                spriteRenderers[i].sharedMaterial =
                    originalMaterials[i];
            }

            Color baseColor =
                staggerTinted
                    ? staggerFlashColor
                    : originalColors[i];

            Color color =
                spriteRenderers[i].color;

            // Alpha'ya dokunma (ölüm fade-out'u için).
            color.r = baseColor.r;
            color.g = baseColor.g;
            color.b = baseColor.b;

            spriteRenderers[i].color =
                color;
        }
    }

    private void UpdateStaggerTint()
    {
        if (staggerTintEndTime <= 0f)
            return;

        if (EnemyTime.Now < staggerTintEndTime)
            return;

        staggerTintEndTime = 0f;

        if (flashRoutine == null)
            RestoreBaseColors();
    }

    // EnemyStaggerState.Exit tarafından çağrılır.
    public void ClearStaggerTint()
    {
        staggerTintEndTime = 0f;

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);

            flashRoutine = null;
        }

        RestoreBaseColors();
    }

    // =========================================================
    // AUDIO
    // =========================================================

    public void PlayHealthHitSound()
    {
        if (hitAudioSource == null)
            return;

        if (healthHitClip == null)
            return;

        hitAudioSource.PlayOneShot(
            healthHitClip
        );
    }

    public void PlayPostureHitSound()
    {
        if (hitAudioSource == null)
            return;

        if (postureHitClip == null)
            return;

        hitAudioSource.PlayOneShot(
            postureHitClip
        );
    }

    // =========================================================
    // COMMITTED HIT
    // Hasar ve denge hasarı zaten uygulandı (çağıran taraf).
    // Burada sadece state kesilmez, knockback yok, farklı geri bildirim.
    // Denge kırılırsa yine stagger olur ve saldırı iptal edilir.
    // =========================================================

    private bool TryAbsorbCommittedHit()
    {
        if (!IsAttackCommitted)
            return false;

        // Telegraph rengi her karede sprite'ı ezdiği için
        // vuruş flaşı görünmezdi; kısa süre sustur.
        if (telegraph != null)
            telegraph.Suppress(committedFlashDuration);

        PlayFlash(
            committedHitFlashColor,
            committedFlashDuration
        );

        PlayCommittedHitSound();

        return true;
    }

    public void PlayCommittedHitSound()
    {
        if (hitAudioSource == null)
            return;

        if (committedHitClip == null)
            return;

        hitAudioSource.PlayOneShot(
            committedHitClip
        );
    }

    public void PlayKnockbackSound()
    {
        if (hitAudioSource == null)
            return;

        if (knockbackClip == null)
            return;

        hitAudioSource.PlayOneShot(
            knockbackClip
        );
    }

    // =========================================================
    // BLOCK KNOCKBACK
    // =========================================================

    public void ApplyBlockKnockback(
        Vector2 hitDirection
    )
    {
        Rigidbody2D rb =
            GetComponent<Rigidbody2D>();

        if (rb == null)
            return;

        float direction =
            Mathf.Sign(
                hitDirection.x
            );

        if (direction == 0f)
            direction = 1f;

        float knockbackDirection =
            -direction;

        Vector2 knockback =
            new Vector2(
                knockbackDirection *
                blockKnockbackForce,
                blockKnockbackVerticalForce
            );

        movementLockTimer =
            blockKnockbackDuration;

        if (!IsStaggered)
        {
            ChangeState(
                new EnemyHitState(
                    this,
                    blockKnockbackDuration,
                    blockKnockbackDeceleration
                )
            );
        }

        // Yavaşlama sırasında savrulma da yavaş (yatay).
        rb.linearVelocity =
            new Vector2(
                knockback.x * EnemyTime.Scale,
                knockback.y
            );
    }

    // =========================================================
    // EXECUTE
    // =========================================================

    public void Execute()
    {
        if (!IsStaggered)
            return;

        Debug.Log(
            "EXECUTE TARGET: " +
            gameObject.name
        );

        ChangeState(
            new EnemyExecuteState(this)
        );
    }

    // =========================================================
    // BALANCE BROKEN
    // =========================================================

    private void HandleBalanceBroken()
    {
        ForceStagger();

        CombatEvents.RaiseBalanceBroken(this);
    }
}