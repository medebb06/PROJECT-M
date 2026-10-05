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

            case EnemyArchetypeType.Swarm:
                return "Kalabalık";

            case EnemyArchetypeType.Shielded:
                return "Kalkanlı";

            case EnemyArchetypeType.Flyer:
                return "Uçan";

            case EnemyArchetypeType.Bomber:
                return "Patlayan";

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
                    unblockableDamage = 0.85f,   // 1.15 idi: Perde 3'te tek vuruşta çok götürüyordu
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

            case EnemyArchetypeType.Swarm:
                // Zayıf, çok sayıda: dengesi çabuk kırılır, 2-3 vuruşta ölür.
                return new ArchetypeProfile
                {
                    health = 0.45f,
                    maxBalance = 0.4f,
                    chaseSpeed = 1.15f,
                    attackDamage = 0.6f,
                    unblockableDamage = 0.6f,
                    attackRecovery = 1.2f,
                    parryBalanceDamage = 1.5f,
                    blockPostureDamage = 0.6f,
                    attackKnockback = 0.7f,
                    knockbackTaken = 1.4f,
                    scale = 0.8f
                };

            case EnemyArchetypeType.Shielded:
                // Önden kapalı: arkasına geç / yukarıdan vur / parry.
                return new ArchetypeProfile
                {
                    health = 1.1f,
                    maxBalance = 1.2f,
                    chaseSpeed = 0.85f,
                    attackDamage = 1.1f,
                    unblockableDamage = 0.9f,
                    attackRecovery = 1.15f,
                    parryBalanceDamage = 1.25f,
                    blockPostureDamage = 1.2f,
                    attackKnockback = 1.1f,
                    knockbackTaken = 0.6f,
                    scale = 1.05f
                };

            case EnemyArchetypeType.Flyer:
                // Havada: pogo / havada vuruş / parry ile düşür.
                return new ArchetypeProfile
                {
                    health = 0.6f,
                    maxBalance = 0.6f,
                    chaseSpeed = 1.2f,
                    attackDamage = 0.8f,
                    unblockableDamage = 0.7f,
                    attackRecovery = 1f,
                    parryBalanceDamage = 1.4f,
                    blockPostureDamage = 0.8f,
                    attackKnockback = 0.8f,
                    knockbackTaken = 1.2f,
                    scale = 0.85f
                };

            case EnemyArchetypeType.Bomber:
                // Çok zayıf, hızlı; yaklaşınca patlar.
                return new ArchetypeProfile
                {
                    health = 0.35f,
                    maxBalance = 0.4f,
                    chaseSpeed = 1.35f,
                    attackDamage = 0.5f,
                    unblockableDamage = 0.5f,
                    attackRecovery = 1.3f,
                    parryBalanceDamage = 1.5f,
                    blockPostureDamage = 0.5f,
                    attackKnockback = 0.6f,
                    knockbackTaken = 1.6f,
                    scale = 0.75f
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

        // ---------------- ÖZEL TİPLER ----------------

        if (type == EnemyArchetypeType.Shielded && GetComponent<EnemyShield>() == null)
            gameObject.AddComponent<EnemyShield>();

        if (type == EnemyArchetypeType.Flyer && GetComponent<EnemyFlyer>() == null)
        {
            gameObject.AddComponent<EnemyFlyer>();

            // Havada sıra beklemez; standby halkası yere göre.
            enemy.useStandby = false;
        }

        if (type == EnemyArchetypeType.Bomber && GetComponent<EnemyBomber>() == null)
            gameObject.AddComponent<EnemyBomber>();

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

        // ---------------- BASİT GÖVDE (sprite'ı olmayan tipler) ----------------
        // Kendi prefab'ı yoksa (renklendirilmiş kopya) kapsül / daire çizilir.

        if (applyScale && type == EnemyArchetypeType.Flyer)
            EnemyShape.Apply(enemy, EnemyShape.Kind.Capsule, new Color(0.6f, 0.95f, 1f), false);

        if (applyScale && type == EnemyArchetypeType.Bomber)
            EnemyShape.Apply(enemy, EnemyShape.Kind.Circle, new Color(1f, 0.55f, 0.4f), true);

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

                case EnemyArchetypeType.Swarm:
                case EnemyArchetypeType.Flyer:
                case EnemyArchetypeType.Bomber:
                    moveset.moves = EnemyMoveset.CreateSwarmMoves();
                    break;

                case EnemyArchetypeType.Shielded:
                    moveset.moves = EnemyMoveset.CreateDuelistMoves();
                    break;
            }
        }

        // ---------------- İMZA SALDIRISI (48. adım) ----------------
        // Her tipin kendine özgü uzun hamlesi (Kalabalık hariç).

        if (applyMoveset)
        {
            EnemyMoveset ms = GetComponent<EnemyMoveset>();

            if (ms != null && ms.moves != null && !ms.moves.Exists(m => m != null && m.signature))
            {
                AttackMove sig = EnemyMoveset.Signature(type);

                if (sig != null)
                    ms.moves.Add(sig);
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
    Archer,
    Swarm,      // SONA eklendi (kayıtlı değerler kaymasın)
    Shielded,   // 40. adım: önden vuruşu kalkanla engeller
    Flyer,      // 40. adım: uçar, dalış saldırısı
    Bomber      // 40. adım: yaklaşınca fitil + patlama
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
