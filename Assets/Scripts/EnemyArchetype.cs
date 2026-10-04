using System;
using UnityEngine;

/// <summary>
/// DÜŞMAN TİPİ. Aynı düşman altyapısını (EnemyController + EnemyMoveset)
/// farklı oynanan bir düşmana çevirir:
///
///   DÜELLOCU : dengeli (varsayılan, değişiklik yok).
///   ÇEVİK    : hızlı, az canlı; kısa uyarılı 4-5 vuruşluk SERİ kombolar.
///              Sınadığı şey: parry RİTMİ. Her parry dengeye az vurur,
///              block'ta vuruş başına az posture yer.
///   AĞIR     : yavaş, dayanıklı, çok sert; sık YAKALAMA ve SÜPÜRME.
///              Block posture'ı hızla eritir, parry çok ödüllü, saldırı
///              sonrası uzun açık kalır. Sınadığı şey: kusursuz kaçış,
///              atla-vur, sabır.
///   OKÇU     : mesafe korur (yaklaşınca geri çekilir), ok atar. Ok parry
///              ile OKÇUYA geri yansır; block posture yer; dash ile içinden
///              geçilir. Yakına gelinirse hançer / tekme süpürme.
///              Sınadığı şey: mesafe kapatma, iki düşmanlı odada öncelik.
///
/// KURULUM:
///  - RunManager kendisi kullanır: 'Quick Prefab' / 'Heavy Prefab' boşsa
///    Enemy Prefab'ın renklendirilmiş kopyasına bu bileşeni ekler.
///  - Kendi prefab'ın varsa (ör. iri karakter = Ağır) bu bileşeni ekle,
///    'Type' seç. Sahneye elle koyulan düşmanda da Start'ta uygulanır.
///  - Değerleri o prefab için değiştirmek istersen 'Override Profile' aç.
/// </summary>
public class EnemyArchetype : MonoBehaviour
{
    public EnemyArchetypeType type = EnemyArchetypeType.Duelist;

    [Tooltip("Açık: hamle setini tipe göre değiştir (EnemyMoveset yoksa ekler).")]
    public bool applyMoveset = true;

    [Tooltip(
        "Açık: tipin boyut çarpanı uygulanır. Kendi sprite'lı prefab'da " +
        "RunManager bunu kapatır (boyutu prefab belirler).")]
    public bool applyScale = true;

    [Tooltip("Açık: aşağıdaki 'Custom' değerleri kullanılır. Kapalı: koddaki varsayılanlar.")]
    public bool overrideProfile = false;

    public ArchetypeProfile custom = new ArchetypeProfile();

    public bool Applied { get; private set; }

    public string DisplayName => NameOf(type);

    public static string NameOf(EnemyArchetypeType type)
    {
        switch (type)
        {
            case EnemyArchetypeType.Quick:
                return "Çevik";

            case EnemyArchetypeType.Heavy:
                return "Ağır";

            case EnemyArchetypeType.Archer:
                return "Okçu";

            default:
                return "Düellocu";
        }
    }

    // =========================================================
    // VARSAYILAN PROFİLLER (yüzde tabanlı çarpanlar)
    // =========================================================

    public static ArchetypeProfile DefaultProfile(EnemyArchetypeType type)
    {
        switch (type)
        {
            case EnemyArchetypeType.Quick:
                return new ArchetypeProfile
                {
                    health = 0.75f,
                    maxBalance = 0.75f,
                    chaseSpeed = 1.3f,
                    attackDamage = 0.7f,
                    unblockableDamage = 0.8f,
                    attackRecovery = 0.8f,
                    parryBalanceDamage = 0.75f,
                    blockPostureDamage = 0.7f,
                    attackKnockback = 0.8f,
                    knockbackTaken = 1.15f,
                    scale = 0.92f
                };

            case EnemyArchetypeType.Heavy:
                return new ArchetypeProfile
                {
                    health = 1.5f,
                    maxBalance = 1.4f,
                    chaseSpeed = 0.75f,
                    attackDamage = 1.5f,
                    unblockableDamage = 1.15f,
                    attackRecovery = 1.35f,
                    parryBalanceDamage = 1.4f,
                    blockPostureDamage = 1.8f,
                    attackKnockback = 1.3f,
                    knockbackTaken = 0.45f,
                    scale = 1.15f
                };

            case EnemyArchetypeType.Archer:
                return new ArchetypeProfile
                {
                    health = 0.7f,
                    maxBalance = 0.7f,
                    chaseSpeed = 1.05f,
                    attackDamage = 0.8f,
                    unblockableDamage = 0.8f,
                    attackRecovery = 1f,
                    parryBalanceDamage = 1f,
                    blockPostureDamage = 0.8f,
                    attackKnockback = 0.8f,
                    knockbackTaken = 1.1f,
                    scale = 0.95f,

                    // Mesafe: bu uzaklıkta durur, yaklaşınca geri çekilir.
                    // (Karakter ölçeği 2 olan sahneye göre: ~3 gövde boyu.)
                    attackRangeOverride = 12f,
                    chaseStopOverride = 11f
                };

            default:
                return new ArchetypeProfile();
        }
    }

    public ArchetypeProfile Profile =>
        overrideProfile && custom != null
            ? custom
            : DefaultProfile(type);

    // =========================================================
    // UYGULA (bir kez)
    // RunManager doğurur doğurmaz çağırır (zorluk ölçeğinden ÖNCE).
    // Sahneye elle koyulan düşmanda Start'ta kendisi çağrılır.
    // =========================================================

    private void Start()
    {
        if (!Applied)
            Apply();
    }

    public void Apply()
    {
        if (Applied)
            return;

        Applied = true;

        EnemyController enemy = GetComponent<EnemyController>();

        if (enemy == null)
        {
            Debug.LogWarning("EnemyArchetype: EnemyController yok → " + name);
            return;
        }

        ArchetypeProfile p = Profile;

        // ---------------- CAN / DENGE ----------------

        Health health = GetComponent<Health>();

        if (health != null && !Mathf.Approximately(p.health, 1f))
        {
            health.SetMaxHealth(
                Mathf.Max(1, Mathf.RoundToInt(health.MaxHealth * p.health)),
                true
            );
        }

        EnemyBalance balance = GetComponent<EnemyBalance>();

        if (balance != null && !Mathf.Approximately(p.maxBalance, 1f))
        {
            balance.SetMaxBalance(
                Mathf.Max(1, Mathf.RoundToInt(balance.MaxBalance * p.maxBalance))
            );
        }

        // ---------------- SALDIRI ----------------

        enemy.chaseSpeed *= p.chaseSpeed;

        enemy.attackDamage = Scale(enemy.attackDamage, p.attackDamage);
        enemy.unblockableDamage = Scale(enemy.unblockableDamage, p.unblockableDamage);

        enemy.attackRecoveryTime *= p.attackRecovery;

        enemy.parryBalanceDamage = Scale(enemy.parryBalanceDamage, p.parryBalanceDamage);
        enemy.blockPostureDamage = Scale(enemy.blockPostureDamage, p.blockPostureDamage);

        enemy.attackKnockbackForce *= p.attackKnockback;

        // ---------------- MESAFE (okçu) ----------------
        // ChaseState, düşmanı min(Chase Stop, Attack Range) uzaklığında
        // tutar; oyuncu yaklaşınca geri çekilir.

        float rangeOverride = p.attackRangeOverride;
        float stopOverride = p.chaseStopOverride;

        // Okçu menzilsiz kalmasın: Override Profile açık ama mesafe 0
        // bırakıldıysa tipin varsayılan mesafesi kullanılır.
        if (type == EnemyArchetypeType.Archer)
        {
            ArchetypeProfile def = DefaultProfile(type);

            if (rangeOverride <= 0f)
                rangeOverride = def.attackRangeOverride;

            if (stopOverride <= 0f)
                stopOverride = def.chaseStopOverride;
        }

        if (rangeOverride > 0f)
            enemy.attackRange = rangeOverride;

        if (stopOverride > 0f)
            enemy.chaseStopDistance = stopOverride;

        if (type == EnemyArchetypeType.Archer)
        {
            // Sıra beklemez: yakın dövüşçü saldırırken arkadan ok atabilir
            // (saldırı ritmi koordinatörü yine geçerli). Geri çekilmeyi
            // EnemyArcher yapar.
            enemy.useStandby = false;

            if (GetComponent<EnemyArcher>() == null)
                gameObject.AddComponent<EnemyArcher>();
        }

        // ---------------- SAVRULMA (ağır az savrulur) ----------------

        enemy.balanceHitKnockbackForce *= p.knockbackTaken;
        enemy.postureKnockbackForce *= p.knockbackTaken;
        enemy.healthKnockbackForce *= p.knockbackTaken;
        enemy.attack1KnockbackForce *= p.knockbackTaken;
        enemy.attack2KnockbackForce *= p.knockbackTaken;
        enemy.attack3KnockbackForce *= p.knockbackTaken;
        enemy.attack4KnockbackForce *= p.knockbackTaken;

        // ---------------- BOYUT ----------------

        if (applyScale && !Mathf.Approximately(p.scale, 1f))
            transform.localScale *= p.scale;

        // ---------------- HAMLE SETİ ----------------

        if (applyMoveset && type != EnemyArchetypeType.Duelist)
        {
            EnemyMoveset moveset = GetComponent<EnemyMoveset>();

            if (moveset == null)
                moveset = gameObject.AddComponent<EnemyMoveset>();

            switch (type)
            {
                case EnemyArchetypeType.Quick:
                    moveset.moves = EnemyMoveset.CreateQuickMoves();
                    break;

                case EnemyArchetypeType.Heavy:
                    moveset.moves = EnemyMoveset.CreateHeavyMoves();
                    break;

                case EnemyArchetypeType.Archer:
                    moveset.moves = EnemyMoveset.CreateArcherMoves();
                    break;
            }
        }

        Debug.Log("DÜŞMAN TİPİ: " + DisplayName + " → " + name);
    }

    private static int Scale(int value, float multiplier)
    {
        if (Mathf.Approximately(multiplier, 1f))
            return value;

        return Mathf.Max(1, Mathf.RoundToInt(value * multiplier));
    }
}

public enum EnemyArchetypeType
{
    Duelist,
    Quick,
    Heavy,
    Archer
}

// Tipin çarpanları (1 = değişiklik yok).
[Serializable]
public class ArchetypeProfile
{
    [Tooltip("Can çarpanı.")]
    public float health = 1f;

    [Tooltip("Azami denge çarpanı (büyük = dengesi geç kırılır).")]
    public float maxBalance = 1f;

    public float chaseSpeed = 1f;

    [Tooltip("Normal vuruş hasarı çarpanı.")]
    public float attackDamage = 1f;

    [Tooltip("Engellenemez (yakalama) hasarı çarpanı.")]
    public float unblockableDamage = 1f;

    [Tooltip("Saldırı sonrası açık kalma süresi çarpanı.")]
    public float attackRecovery = 1f;

    [Tooltip("Parry'nin bu düşmanın dengesine verdiği hasar çarpanı.")]
    public float parryBalanceDamage = 1f;

    [Tooltip("Block'ladığında oyuncunun posture kaybı çarpanı.")]
    public float blockPostureDamage = 1f;

    [Tooltip("Oyuncuya uyguladığı savrulma çarpanı.")]
    public float attackKnockback = 1f;

    [Tooltip("Oyuncunun vuruşlarıyla savrulma çarpanı (küçük = ağır).")]
    public float knockbackTaken = 1f;

    [Tooltip("Boyut çarpanı.")]
    public float scale = 1f;

    [Tooltip("0 = değiştirme. >0: Attack Range bu değer olur (okçu: 7).")]
    public float attackRangeOverride = 0f;

    [Tooltip("0 = değiştirme. >0: Chase Stop Distance bu değer olur (okçu: 7).")]
    public float chaseStopOverride = 0f;
}
