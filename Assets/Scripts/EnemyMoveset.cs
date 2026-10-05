using System;
using System.Collections.Generic;
using UnityEngine;

// Bir vuruşun türü: oyuncudan hangi cevabı istediğini belirler.
public enum MoveHitType
{
    Normal, // kırmızı uyarı: parry / block
    Sweep,  // sarı uyarı + ALÇAK mavi kutu: ZIPLA (dash işe yaramaz)
    Grab,   // sarı uyarı + "!" + UZUN sarı kutu: KAÇ (dash / geri çekil)
    Shot    // OK (mermi): parry = geri yansıt, block = posture, dash = içinden geç
}

// Bir hamledeki tek vuruş.
[Serializable]
public class MoveHit
{
    public MoveHitType type = MoveHitType.Normal;

    [Tooltip(
        "Bu vuruştan önceki uyarı (sn, düşman zamanı). İlk vuruşta hazırlık, " +
        "sonrakilerde kombo arası.")]
    [Min(0.05f)]
    public float windup = 0.8f;

    [Tooltip(
        "Uyarıya eklenen RASTGELE süre (0..bu). Gecikmeli vuruş: ezbere " +
        "parry'i cezalandırır.")]
    [Min(0f)]
    public float windupRandom = 0f;

    [Tooltip("Hasar çarpanı (Normal/Sweep: Attack Damage, Grab: Unblockable Damage).")]
    [Min(0f)]
    public float damageMultiplier = 1f;

    [Tooltip("Menzil çarpanı.")]
    [Min(0.1f)]
    public float reachMultiplier = 1f;

    [Tooltip("Bu vuruş PARRY'lenince düşmanın dengesine giden hasar çarpanı (imza saldırısının son vuruşu büyük ödül).")]
    [Min(0f)]
    public float parryBalanceMultiplier = 1f;

    public MoveHit() { }

    // Zincirle: new MoveHit(...).ParryReward(3f)
    public MoveHit ParryReward(float multiplier)
    {
        parryBalanceMultiplier = multiplier;
        return this;
    }

    public MoveHit(
        MoveHitType type,
        float windup,
        float windupRandom = 0f,
        float damageMultiplier = 1f,
        float reachMultiplier = 1f
    )
    {
        this.type = type;
        this.windup = windup;
        this.windupRandom = windupRandom;
        this.damageMultiplier = damageMultiplier;
        this.reachMultiplier = reachMultiplier;
    }
}

// Bir hamle: bir ya da birkaç vuruşun dizisi (kombo).
[Serializable]
public class AttackMove
{
    public string name = "Hamle";

    [Min(0f)]
    public float weight = 1f;

    [Tooltip("Oyuncu bu mesafeden YAKINSA seçilmez (ör. uzun menzilli hamle).")]
    [Min(0f)]
    public float minDistance = 0f;

    [Tooltip("Oyuncu bu mesafeden UZAKSA seçilmez.")]
    [Min(0f)]
    public float maxDistance = 99f;

    [Tooltip("Kullanıldıktan sonra bu kadar süre (düşman sn) tekrar seçilmez.")]
    [Min(0f)]
    public float cooldown = 0f;

    [Tooltip("Hamle bitince recovery (açık kalma) çarpanı. Uzun kombo = uzun açıklık.")]
    [Min(0.1f)]
    public float recoveryMultiplier = 1f;

    public List<MoveHit> hits = new List<MoveHit>();

    [Tooltip("İMZA SALDIRISI: başlarken düşmanın üstünde adı yazar; son vuruşunu parry'lemek dengeyi büyük ölçüde kırar.")]
    public bool signature;

    [NonSerialized] public float lastUsedTime = -999f;
}

/// <summary>
/// DÜŞMAN HAMLE SETİ. Bir düşmana eklenince EnemyAttackState tek vuruş yerine
/// buradaki hamlelerden birini seçer: kombo, gecikmeli vuruş, süpürme,
/// yakalama... Her vuruş türü oyuncudan FARKLI bir cevap ister:
///
///   Normal  (kırmızı)             → parry / block
///   Sweep   (sarı + alçak kutu)   → zıpla
///   Grab    (sarı + "!" + kutu)   → dash / geri çekil
///
/// Kombonun 2. ve sonraki vuruşları KESİLEMEZ (vurarak bozamazsın) ve
/// block'lanan ara vuruşlar düşmanı geri itmez: her vuruşu karşılaman gerekir.
///
/// KURULUM: Düşman prefab'ına ekle. 'Moves' boşsa Düellocu'nun varsayılan
/// hamleleri yüklenir (Inspector'da sağ tık > "Varsayılan Düellocu Hamleleri"
/// ile de doldurulup düzenlenebilir). Çevik / Ağır setleri: EnemyArchetype.
/// </summary>
[RequireComponent(typeof(EnemyController))]
public class EnemyMoveset : MonoBehaviour
{
    [Header("Hamleler (boşsa varsayılan Düellocu seti)")]
    public List<AttackMove> moves = new List<AttackMove>();

    [Tooltip("Az önce kullanılan hamlenin tekrar seçilme ağırlığı çarpanı.")]
    [Range(0f, 1f)]
    public float repeatPenalty = 0.35f;

    [Header("Kombo")]
    [Tooltip(
        "Açık: kombonun ARA vuruşlarında parry slow-mo yok (ritim bozulmaz). " +
        "Slow-mo son vuruşta ya da denge kırılınca gelir.")]
    public bool noParrySlowMoMidCombo = true;

    [Tooltip("Kombo başlarken başın üstünde vuruş noktaları göster.")]
    public bool showComboIndicator = true;

    [Tooltip(
        "Kombo vuruşları block'lanınca yenen posture çarpanı " +
        "(0.6 = %60). 3 vuruşluk kombo tam block'la posture'ı kırmasın.")]
    [Range(0.1f, 1f)]
    public float comboBlockPostureMultiplier = 0.6f;

    [Tooltip("Kombonun İLK vuruşunun uyarısına eklenen süre: noktaları okuma zamanı.")]
    [Min(0f)]
    public float comboFirstWindupBonus = 0.25f;

    [Header("Süpürme (Sweep)")]
    [Tooltip("Vuruş kutusunun yüksekliği: oyuncunun ayakları bunun üstündeyse ıskalar.")]
    [Min(0.1f)]
    public float sweepHitHeight = 0.6f;

    [Tooltip("Süpürmenin ileri menzili = Attack Range × bu.")]
    [Min(0.5f)]
    public float sweepReachMultiplier = 1.4f;

    [Tooltip("Açık: dash'in dokunulmazlığı süpürmeyi geçemez, ZIPLAMAK gerekir.")]
    public bool sweepIgnoresDash = true;

    [Tooltip("Süpürme uyarısında zemindeki kutunun rengi.")]
    public Color sweepColor = new Color(0.3f, 0.75f, 1f);

    private AttackMove lastMove;

    private void Awake()
    {
        if (moves == null || moves.Count == 0)
            moves = CreateDuelistMoves();
    }

    [ContextMenu("Varsayılan Düellocu Hamleleri")]
    private void FillDefaults()
    {
        moves = CreateDuelistMoves();
    }

    [ContextMenu("Çevik Hamleleri")]
    private void FillQuick()
    {
        moves = CreateQuickMoves();
    }

    [ContextMenu("Ağır Hamleleri")]
    private void FillHeavy()
    {
        moves = CreateHeavyMoves();
    }

    [ContextMenu("Okçu Hamleleri")]
    private void FillArcher()
    {
        moves = CreateArcherMoves();
    }

    // Oyuncu uzaklığına göre ağırlıklı rastgele hamle. Uygun yoksa null
    // (EnemyAttackState klasik tek vuruşa döner).
    public AttackMove PickMove(float distance)
    {
        float now = EnemyTime.Now;
        float total = 0f;

        for (int i = 0; i < moves.Count; i++)
            total += WeightOf(moves[i], distance, now);

        if (total <= 0f)
            return null;

        float roll = UnityEngine.Random.value * total;

        for (int i = 0; i < moves.Count; i++)
        {
            float w = WeightOf(moves[i], distance, now);

            if (w <= 0f)
                continue;

            roll -= w;

            if (roll <= 0f)
                return Use(moves[i], now);
        }

        // Kayan nokta payı: son uygun hamle.
        for (int i = moves.Count - 1; i >= 0; i--)
        {
            if (WeightOf(moves[i], distance, now) > 0f)
                return Use(moves[i], now);
        }

        return null;
    }

    private AttackMove Use(AttackMove move, float now)
    {
        move.lastUsedTime = now;
        lastMove = move;

        // İmza saldırısı duyurusu: oyuncu ne geldiğini bilsin.
        if (move.signature)
        {
            CombatCallout.PopupAbove(
                this,
                move.name.ToUpperInvariant(),
                new Color(1f, 0.6f, 0.2f),
                0.9f,
                0.7f
            );
        }

        return move;
    }

    // =========================================================
    // İMZA SALDIRILARI (48. adım): her tipin kendine özgü, uzun ama
    // okunur hamlesi. Son vuruşu parry'lemek dengeyi ×3 vurur.
    // =========================================================

    public static AttackMove Signature(EnemyArchetypeType type)
    {
        switch (type)
        {
            case EnemyArchetypeType.Quick:
                return new AttackMove
                {
                    name = "Fırtına",
                    signature = true,
                    weight = 0.6f,
                    cooldown = 9f,
                    recoveryMultiplier = 2f,
                    hits =
                    {
                        new MoveHit(MoveHitType.Normal, 0.75f),
                        new MoveHit(MoveHitType.Normal, 0.28f),
                        new MoveHit(MoveHitType.Normal, 0.28f),
                        new MoveHit(MoveHitType.Normal, 0.28f),
                        new MoveHit(MoveHitType.Normal, 0.5f, 0.35f),
                        new MoveHit(MoveHitType.Normal, 0.3f, 0f, 1.3f).ParryReward(3f)
                    }
                };

            case EnemyArchetypeType.Heavy:
                return new AttackMove
                {
                    name = "Deprem",
                    signature = true,
                    weight = 0.6f,
                    cooldown = 10f,
                    recoveryMultiplier = 2.2f,
                    hits =
                    {
                        new MoveHit(MoveHitType.Normal, 1.1f),
                        new MoveHit(MoveHitType.Sweep, 0.65f, 0f, 1.2f),
                        new MoveHit(MoveHitType.Grab, 0.75f),
                        new MoveHit(MoveHitType.Normal, 0.9f, 0.4f, 1.6f).ParryReward(3f)
                    }
                };

            case EnemyArchetypeType.Archer:
                return new AttackMove
                {
                    name = "Ok Yağmuru",
                    signature = true,
                    weight = 0.5f,
                    minDistance = 5f,
                    cooldown = 10f,
                    recoveryMultiplier = 2f,
                    hits =
                    {
                        new MoveHit(MoveHitType.Shot, 0.9f),
                        new MoveHit(MoveHitType.Shot, 0.3f),
                        new MoveHit(MoveHitType.Shot, 0.3f),
                        new MoveHit(MoveHitType.Shot, 0.55f, 0.3f),
                        new MoveHit(MoveHitType.Shot, 0.3f)
                    }
                };

            case EnemyArchetypeType.Shielded:
                return new AttackMove
                {
                    name = "Kalkan Hücumu",
                    signature = true,
                    weight = 0.6f,
                    cooldown = 9f,
                    recoveryMultiplier = 2f,
                    hits =
                    {
                        new MoveHit(MoveHitType.Grab, 1.0f, 0f, 1f, 1.3f),
                        new MoveHit(MoveHitType.Normal, 0.45f),
                        new MoveHit(MoveHitType.Normal, 0.7f, 0.3f, 1.4f).ParryReward(3f)
                    }
                };

            case EnemyArchetypeType.Duelist:
                return new AttackMove
                {
                    name = "Kılıç Dansı",
                    signature = true,
                    weight = 0.6f,
                    cooldown = 9f,
                    recoveryMultiplier = 2f,
                    hits =
                    {
                        new MoveHit(MoveHitType.Normal, 0.75f),
                        new MoveHit(MoveHitType.Normal, 0.4f),
                        new MoveHit(MoveHitType.Normal, 0.55f, 0.45f),
                        new MoveHit(MoveHitType.Sweep, 0.5f, 0f, 1.2f),
                        new MoveHit(MoveHitType.Normal, 0.55f, 0f, 1.5f).ParryReward(3f)
                    }
                };

            default:
                return null;
        }
    }

    /// <summary>Setin sonuna tipin imza saldırısını ekler (yoksa).</summary>
    public static List<AttackMove> WithSignature(List<AttackMove> moves, EnemyArchetypeType type)
    {
        AttackMove sig = Signature(type);

        if (sig != null)
            moves.Add(sig);

        return moves;
    }

    // =========================================================
    // PERDE BOSS'LARI (48. adım)
    //   1 Kılıç Ustası   : dengeli (eski boss seti)
    //   2 Kızıl Düellocu : hızlı seriler, aldatmaca, Fırtına
    //   3 Gölge Efendisi : ağır + karışık, süpürme/yakalama zincirleri
    // =========================================================

    public static List<AttackMove> CreateBossMoves(int act, bool phase2)
    {
        if (act <= 1)
            return phase2 ? CreateBossPhase2Moves() : CreateBossPhase1Moves();

        if (act == 2)
        {
            List<AttackMove> m = CreateQuickMoves();

            m.Add(Signature(EnemyArchetypeType.Quick));

            if (phase2)
            {
                m.Add(
                    new AttackMove
                    {
                        name = "Kızıl Kasırga",
                        signature = true,
                        weight = 0.9f,
                        cooldown = 7f,
                        recoveryMultiplier = 2.2f,
                        hits =
                        {
                            new MoveHit(MoveHitType.Normal, 0.6f),
                            new MoveHit(MoveHitType.Normal, 0.26f),
                            new MoveHit(MoveHitType.Normal, 0.26f),
                            new MoveHit(MoveHitType.Normal, 0.6f, 0.4f),
                            new MoveHit(MoveHitType.Normal, 0.26f),
                            new MoveHit(MoveHitType.Grab, 0.6f),
                            new MoveHit(MoveHitType.Normal, 0.45f, 0f, 1.5f).ParryReward(3f)
                        }
                    }
                );

                ScaleWindups(m, 0.88f);
            }
            else
            {
                ScaleWindups(m, 0.95f);
            }

            return m;
        }

        // Perde 3+
        List<AttackMove> h = CreateHeavyMoves();

        h.Add(Signature(EnemyArchetypeType.Heavy));
        h.Add(Signature(EnemyArchetypeType.Duelist));

        if (phase2)
        {
            h.Add(
                new AttackMove
                {
                    name = "Gölge Zinciri",
                    signature = true,
                    weight = 1f,
                    cooldown = 7f,
                    recoveryMultiplier = 2.4f,
                    hits =
                    {
                        new MoveHit(MoveHitType.Normal, 0.8f),
                        new MoveHit(MoveHitType.Normal, 0.4f),
                        new MoveHit(MoveHitType.Sweep, 0.5f, 0f, 1.3f),
                        new MoveHit(MoveHitType.Normal, 0.7f, 0.5f),
                        new MoveHit(MoveHitType.Grab, 0.6f),
                        new MoveHit(MoveHitType.Normal, 0.6f, 0f, 1.7f).ParryReward(3.5f)
                    }
                }
            );

            ScaleWindups(h, 0.85f);
        }
        else
        {
            ScaleWindups(h, 0.92f);
        }

        return h;
    }

    private float WeightOf(AttackMove move, float distance, float now)
    {
        if (
            move == null ||
            move.hits == null ||
            move.hits.Count == 0 ||
            move.weight <= 0f
        )
        {
            return 0f;
        }

        if (distance < move.minDistance || distance > move.maxDistance)
            return 0f;

        if (now - move.lastUsedTime < move.cooldown)
            return 0f;

        return move == lastMove
            ? move.weight * repeatPenalty
            : move.weight;
    }

    // =========================================================
    // BOSS
    // =========================================================

    // Faz 1: Düellocu seti + dörtlü kombo. Uyarılar biraz daha kısa.
    public static List<AttackMove> CreateBossPhase1Moves()
    {
        List<AttackMove> moves = CreateDuelistMoves();

        moves.Add(
            new AttackMove
            {
                name = "Dörtlü Kombo",
                weight = 1f,
                cooldown = 3f,
                recoveryMultiplier = 1.6f,
                hits =
                {
                    new MoveHit(MoveHitType.Normal, 0.7f),
                    new MoveHit(MoveHitType.Normal, 0.4f),
                    new MoveHit(MoveHitType.Normal, 0.4f),
                    new MoveHit(MoveHitType.Normal, 0.65f, 0f, 1.4f)
                }
            }
        );

        ScaleWindups(moves, 0.92f);

        return moves;
    }

    // Faz 2: daha uzun ve karışık kombolar, daha hızlı.
    public static List<AttackMove> CreateBossPhase2Moves()
    {
        List<AttackMove> moves = new List<AttackMove>
        {
            new AttackMove
            {
                name = "Hızlı Vuruş",
                weight = 0.8f,
                hits = { new MoveHit(MoveHitType.Normal, 0.6f) }
            },

            new AttackMove
            {
                name = "Beşli Kombo",
                weight = 1.2f,
                cooldown = 3f,
                recoveryMultiplier = 1.8f,
                hits =
                {
                    new MoveHit(MoveHitType.Normal, 0.65f),
                    new MoveHit(MoveHitType.Normal, 0.38f),
                    new MoveHit(MoveHitType.Normal, 0.38f),
                    new MoveHit(MoveHitType.Normal, 0.5f),
                    new MoveHit(MoveHitType.Normal, 0.6f, 0f, 1.5f)
                }
            },

            new AttackMove
            {
                name = "Gecikmeli Kombo",
                weight = 0.9f,
                cooldown = 2f,
                recoveryMultiplier = 1.4f,
                hits =
                {
                    new MoveHit(MoveHitType.Normal, 0.9f, 0.7f, 1.2f),
                    new MoveHit(MoveHitType.Normal, 0.4f),
                    new MoveHit(MoveHitType.Normal, 0.45f)
                }
            },

            new AttackMove
            {
                name = "Çifte Süpürme",
                weight = 0.7f,
                cooldown = 4f,
                recoveryMultiplier = 1.5f,
                hits =
                {
                    new MoveHit(MoveHitType.Sweep, 0.85f, 0f, 1.4f),
                    new MoveHit(MoveHitType.Sweep, 0.7f, 0f, 1.4f)
                }
            },

            new AttackMove
            {
                name = "Kombo + Yakalama",
                weight = 0.7f,
                cooldown = 5f,
                recoveryMultiplier = 1.8f,
                hits =
                {
                    new MoveHit(MoveHitType.Normal, 0.65f),
                    new MoveHit(MoveHitType.Normal, 0.4f),
                    new MoveHit(MoveHitType.Grab, 0.9f)
                }
            },

            new AttackMove
            {
                name = "Kombo + Süpürme",
                weight = 0.8f,
                cooldown = 3f,
                recoveryMultiplier = 1.6f,
                hits =
                {
                    new MoveHit(MoveHitType.Normal, 0.65f),
                    new MoveHit(MoveHitType.Normal, 0.4f),
                    new MoveHit(MoveHitType.Sweep, 0.55f, 0f, 1.5f)
                }
            }
        };

        return moves;
    }

    private static void ScaleWindups(List<AttackMove> moves, float factor)
    {
        for (int i = 0; i < moves.Count; i++)
        {
            for (int h = 0; h < moves[i].hits.Count; h++)
                moves[i].hits[h].windup *= factor;
        }
    }

    // =========================================================
    // ÇEVİK: kısa uyarılı SERİ kombolar (parry ritmi)
    // Kombo arası ~0.3 sn: zincir penceresiyle (×1.5) ritme basılabilir,
    // abanmak (mash) boşa basma cezasına takılır.
    // =========================================================

    // KALABALIK: zayıf, basit; tek / ikili pençe ve kısa atılma.
    public static List<AttackMove> CreateSwarmMoves()
    {
        return new List<AttackMove>
        {
            new AttackMove
            {
                name = "Pençe",
                weight = 1.2f,
                hits = { new MoveHit(MoveHitType.Normal, 0.6f) }
            },
            new AttackMove
            {
                name = "İkili Pençe",
                weight = 0.8f,
                recoveryMultiplier = 1.2f,
                hits =
                {
                    new MoveHit(MoveHitType.Normal, 0.65f),
                    new MoveHit(MoveHitType.Normal, 0.38f)
                }
            },
            new AttackMove
            {
                name = "Atılma",
                weight = 0.5f,
                minDistance = 1.5f,
                cooldown = 3f,
                recoveryMultiplier = 1.4f,
                hits = { new MoveHit(MoveHitType.Normal, 0.75f, 0.2f, 1f, 1.5f) }
            }
        };
    }

    public static List<AttackMove> CreateQuickMoves()
    {
        return new List<AttackMove>
        {
            new AttackMove
            {
                name = "Dürtme",
                weight = 0.8f,
                hits = { new MoveHit(MoveHitType.Normal, 0.55f) }
            },

            new AttackMove
            {
                name = "Dörtlü Seri",
                weight = 1.3f,
                recoveryMultiplier = 1.5f,
                hits =
                {
                    new MoveHit(MoveHitType.Normal, 0.6f),
                    new MoveHit(MoveHitType.Normal, 0.32f),
                    new MoveHit(MoveHitType.Normal, 0.32f),
                    new MoveHit(MoveHitType.Normal, 0.45f, 0f, 1.2f)
                }
            },

            new AttackMove
            {
                name = "Beşli Seri",
                weight = 0.8f,
                cooldown = 4f,
                recoveryMultiplier = 1.8f,
                hits =
                {
                    new MoveHit(MoveHitType.Normal, 0.6f),
                    new MoveHit(MoveHitType.Normal, 0.3f),
                    new MoveHit(MoveHitType.Normal, 0.35f),
                    new MoveHit(MoveHitType.Normal, 0.3f),
                    new MoveHit(MoveHitType.Normal, 0.5f, 0f, 1.3f)
                }
            },

            new AttackMove
            {
                // Ritmi bozan kombo: 2. vuruş rastgele gecikir.
                name = "Aldatmaca",
                weight = 0.8f,
                cooldown = 3f,
                recoveryMultiplier = 1.4f,
                hits =
                {
                    new MoveHit(MoveHitType.Normal, 0.6f),
                    new MoveHit(MoveHitType.Normal, 0.6f, 0.4f),
                    new MoveHit(MoveHitType.Normal, 0.3f)
                }
            },

            new AttackMove
            {
                name = "Seri + Süpürme",
                weight = 0.6f,
                cooldown = 4f,
                recoveryMultiplier = 1.6f,
                hits =
                {
                    new MoveHit(MoveHitType.Normal, 0.55f),
                    new MoveHit(MoveHitType.Normal, 0.32f),
                    new MoveHit(MoveHitType.Sweep, 0.5f, 0f, 1.2f)
                }
            },

            new AttackMove
            {
                name = "Kapma",
                weight = 0.3f,
                cooldown = 7f,
                hits = { new MoveHit(MoveHitType.Grab, 0.9f) }
            }
        };
    }

    // =========================================================
    // AĞIR: yavaş, sert; sık yakalama ve süpürme
    // Uzun uyarılar okunur ama ceza büyük; block posture'ı eritir.
    // =========================================================

    public static List<AttackMove> CreateHeavyMoves()
    {
        return new List<AttackMove>
        {
            new AttackMove
            {
                name = "Ağır Darbe",
                weight = 1f,
                hits = { new MoveHit(MoveHitType.Normal, 1.1f) }
            },

            new AttackMove
            {
                name = "Gecikmeli Ezme",
                weight = 0.8f,
                cooldown = 2f,
                hits = { new MoveHit(MoveHitType.Normal, 1.3f, 0.6f, 1.3f) }
            },

            new AttackMove
            {
                name = "İkili Savuruş",
                weight = 0.9f,
                recoveryMultiplier = 1.5f,
                hits =
                {
                    new MoveHit(MoveHitType.Normal, 1.0f),
                    new MoveHit(MoveHitType.Normal, 0.7f, 0f, 1.2f)
                }
            },

            new AttackMove
            {
                name = "Yakalama",
                weight = 0.9f,
                cooldown = 4f,
                hits = { new MoveHit(MoveHitType.Grab, 1.2f) }
            },

            new AttackMove
            {
                name = "Yer Süpürme",
                weight = 0.9f,
                cooldown = 3f,
                hits = { new MoveHit(MoveHitType.Sweep, 1.1f, 0f, 1.3f, 1.2f) }
            },

            new AttackMove
            {
                name = "Darbe + Süpürme",
                weight = 0.6f,
                cooldown = 4f,
                recoveryMultiplier = 1.6f,
                hits =
                {
                    new MoveHit(MoveHitType.Normal, 1.0f),
                    new MoveHit(MoveHitType.Sweep, 0.7f, 0f, 1.2f)
                }
            },

            new AttackMove
            {
                name = "Süpürme + Yakalama",
                weight = 0.6f,
                cooldown = 6f,
                recoveryMultiplier = 1.8f,
                hits =
                {
                    new MoveHit(MoveHitType.Sweep, 1.0f),
                    new MoveHit(MoveHitType.Grab, 0.85f)
                }
            }
        };
    }

    // =========================================================
    // OKÇU: mesafeden ok (Shot). Yakına gelinirse hançer / tekme.
    // Okçunun Attack Range'i büyük (EnemyArchetype: 12); yakın dövüş
    // vuruşlarının menzili bu yüzden küçük çarpanla (0.4 × 12 ≈ 4.8).
    // =========================================================

    public static List<AttackMove> CreateArcherMoves()
    {
        return new List<AttackMove>
        {
            new AttackMove
            {
                name = "Tek Ok",
                weight = 1.2f,
                minDistance = 4.5f,
                hits = { new MoveHit(MoveHitType.Shot, 0.9f) }
            },

            new AttackMove
            {
                name = "Çifte Ok",
                weight = 0.9f,
                minDistance = 4.5f,
                recoveryMultiplier = 1.3f,
                hits =
                {
                    new MoveHit(MoveHitType.Shot, 0.8f),
                    new MoveHit(MoveHitType.Shot, 0.45f)
                }
            },

            new AttackMove
            {
                name = "Gecikmeli Ok",
                weight = 0.8f,
                minDistance = 4.5f,
                hits = { new MoveHit(MoveHitType.Shot, 1.0f, 0.6f, 1.2f) }
            },

            new AttackMove
            {
                name = "Üçlü Yaylım",
                weight = 0.6f,
                minDistance = 5f,
                cooldown = 4f,
                recoveryMultiplier = 1.6f,
                hits =
                {
                    new MoveHit(MoveHitType.Shot, 0.75f),
                    new MoveHit(MoveHitType.Shot, 0.35f),
                    new MoveHit(MoveHitType.Shot, 0.35f)
                }
            },

            new AttackMove
            {
                // Yakına gelen oyuncuya hızlı hançer.
                name = "Hançer",
                weight = 1.5f,
                maxDistance = 4.5f,
                hits = { new MoveHit(MoveHitType.Normal, 0.55f, 0f, 1f, 0.4f) }
            },

            new AttackMove
            {
                // Yakındaki oyuncuyu süpürüp geri itme: zıpla.
                name = "Tekme Süpürme",
                weight = 0.6f,
                maxDistance = 4.5f,
                cooldown = 3f,
                hits = { new MoveHit(MoveHitType.Sweep, 0.7f, 0f, 1f, 0.3f) }
            }
        };
    }

    // =========================================================
    // VARSAYILAN: DÜELLOCU
    // =========================================================

    public static List<AttackMove> CreateDuelistMoves()
    {
        return new List<AttackMove>
        {
            new AttackMove
            {
                name = "Tek Vuruş",
                weight = 1f,
                hits = { new MoveHit(MoveHitType.Normal, 0.8f) }
            },

            new AttackMove
            {
                name = "Üçlü Kombo",
                weight = 1.2f,
                recoveryMultiplier = 1.4f,
                hits =
                {
                    new MoveHit(MoveHitType.Normal, 0.7f),
                    new MoveHit(MoveHitType.Normal, 0.45f),
                    new MoveHit(MoveHitType.Normal, 0.6f, 0f, 1.3f)
                }
            },

            new AttackMove
            {
                name = "Gecikmeli Vuruş",
                weight = 0.9f,
                hits = { new MoveHit(MoveHitType.Normal, 1.0f, 0.8f, 1.2f) }
            },

            new AttackMove
            {
                name = "Süpürme",
                weight = 0.8f,
                cooldown = 2.5f,
                hits = { new MoveHit(MoveHitType.Sweep, 1.0f, 0f, 1.5f) }
            },

            new AttackMove
            {
                name = "Kombo + Süpürme",
                weight = 0.7f,
                cooldown = 4f,
                recoveryMultiplier = 1.6f,
                hits =
                {
                    new MoveHit(MoveHitType.Normal, 0.7f),
                    new MoveHit(MoveHitType.Normal, 0.45f),
                    new MoveHit(MoveHitType.Sweep, 0.6f, 0f, 1.5f)
                }
            },

            new AttackMove
            {
                name = "Yakalama",
                weight = 0.6f,
                cooldown = 6f,
                hits = { new MoveHit(MoveHitType.Grab, 1.2f) }
            }
        };
    }
}
