using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Varsayılan charm'lar (kodla üretilir, asset gerekmez) ve seçim ekranı
/// için ağırlıklı rastgele seçim.
///
/// Eksenler:
///   TEMEL  : Zehir, Keskin Gözler, Ölümcül Darbe, Keskin Uçlar, Ağır Darbe,
///            Parry Şifası, Hasat
///   PARRY  : Yankı, Kusursuz Zamanlama, Kesintisiz Ritim, Ezici Parry
///   DASH   : Gölge Adım, Rüzgar Kesiği, Fırsat Penceresi
///   RİSK   : Cam Top, Son Nefes, Kan Bedeli, Kusursuzluk, Odak
///   ZEHİR  : Salgın*, Zehirli Parry, Felç Edici Zehir*
///   (* bir zehir kaynağı gerektirir: Zehir ya da Zehirli Parry)
/// </summary>
public static class CharmCatalog
{
    public static List<CharmDefinition> CreateDefaults()
    {
        List<CharmDefinition> list = new List<CharmDefinition>();

        // =====================================================
        // TEMEL
        // =====================================================

        // ---------------- ZEHİR ----------------

        PoisonCharmEffect poison =
            ScriptableObject.CreateInstance<PoisonCharmEffect>();

        CharmDefinition poisonDef =
            Make(
                "Zehir",
                "Vuruşların düşmanı zehirler. Zehir önce dengeyi eritir, " +
                "denge kırıksa canı eritir. Süre her vuruşta yenilenir.",
                5,
                poison
            );

        list.Add(poisonDef);

        // ---------------- KRİTİK ŞANSI ----------------

        list.Add(
            Make(
                "Keskin Gözler",
                "Vuruşların kritik olma şansı artar. Kritik vuruş hasarı katlar.",
                5,
                Stat(StatType.CritChance, add: 0.15f)
            )
        );

        // ---------------- KRİTİK GÜCÜ ----------------

        list.Add(
            Make(
                "Ölümcül Darbe",
                "Kritik vuruşlar çok daha fazla hasar verir.",
                5,
                Stat(StatType.CritMultiplier, add: 0.5f)
            )
        );

        // ---------------- DENGE HASARI ----------------

        list.Add(
            Make(
                "Keskin Uçlar",
                "Vuruşların düşmanın dengesini daha çok bozar: " +
                "daha çabuk sersemler.",
                5,
                Stat(StatType.BalanceDamage, mult: 1.25f)
            )
        );

        // ---------------- CAN HASARI ----------------

        list.Add(
            Make(
                "Ağır Darbe",
                "Sersemlemiş düşmana daha çok can hasarı verirsin.",
                5,
                Stat(StatType.HealthDamage, mult: 1.35f)
            )
        );

        // ---------------- PARRY ŞİFASI ----------------

        HealCharmEffect parryHeal =
            ScriptableObject.CreateInstance<HealCharmEffect>();

        parryHeal.trigger = HealTrigger.OnParry;
        parryHeal.chancePerStack = 0.2f;
        parryHeal.healPercent = 0.08f;

        list.Add(
            Make(
                "Parry Şifası",
                "Başarılı parry bazen seni iyileştirir.",
                5,
                parryHeal
            )
        );

        // ---------------- HASAT ----------------

        HealCharmEffect killHeal =
            ScriptableObject.CreateInstance<HealCharmEffect>();

        killHeal.trigger = HealTrigger.OnKill;
        killHeal.chancePerStack = 0.15f;
        killHeal.healPercent = 0.06f;

        list.Add(
            Make(
                "Hasat",
                "Düşman öldürmek bazen seni iyileştirir.",
                5,
                killHeal
            )
        );

        // =====================================================
        // PARRY EKSENİ
        // =====================================================

        list.Add(
            Make(
                "Yankı",
                "Parry'in yankılanır: çevrendeki diğer düşmanların " +
                "dengesi de sarsılır.",
                3,
                ScriptableObject.CreateInstance<EchoParryEffect>()
            )
        );

        list.Add(
            Make(
                "Kusursuz Zamanlama",
                "Parry penceresi genişler, riposte daha uzun sürer.",
                3,
                Stat(
                    (StatType.ParryWindow, 0.03f, 1f),
                    (StatType.RiposteHits, 1f, 1f),
                    (StatType.RiposteDuration, 0.5f, 1f)
                )
            )
        );

        list.Add(
            Make(
                "Kesintisiz Ritim",
                "Hasar yemeden yaptığın her parry bir sonrakini güçlendirir.",
                3,
                ScriptableObject.CreateInstance<ParryRhythmEffect>()
            )
        );

        list.Add(
            Make(
                "Ezici Parry",
                "Dengeyi kıran parry düşmanı daha uzun sersemletir ve " +
                "execute'unu güçlendirir.",
                3,
                ScriptableObject.CreateInstance<CrushingParryEffect>()
            )
        );

        // =====================================================
        // DASH EKSENİ
        // =====================================================

        list.Add(
            Make(
                "Gölge Adım",
                "Dash'le bir saldırıdan kaçmak saldıranı sarsar ve " +
                "duruşunu toparlar.",
                3,
                ScriptableObject.CreateInstance<ShadowStepEffect>()
            )
        );

        list.Add(
            Make(
                "Rüzgar Kesiği",
                "Dash'le içinden geçtiğin düşmanların dengesi aşınır.",
                3,
                ScriptableObject.CreateInstance<WindSlashEffect>()
            )
        );

        list.Add(
            Make(
                "Fırsat Penceresi",
                "Dash'le kaçtıktan hemen sonraki vuruşların güçlenir.",
                3,
                ScriptableObject.CreateInstance<OpportunityEffect>()
            )
        );

        // =====================================================
        // RİSK EKSENİ
        // =====================================================

        list.Add(
            Make(
                "Cam Top",
                "Tüm hasarın artar, ama sen de daha çok hasar alırsın.",
                2,
                Stat(
                    (StatType.BalanceDamage, 0f, 1.4f),
                    (StatType.HealthDamage, 0f, 1.4f),
                    (StatType.DamageTaken, 0f, 1.3f)
                )
            )
        );

        list.Add(
            Make(
                "Son Nefes",
                "Canın azaldığında çok daha sert vurursun.",
                3,
                ScriptableObject.CreateInstance<LastBreathEffect>()
            )
        );

        list.Add(
            Make(
                "Kan Bedeli",
                "Block artık duruş değil KAN ister (seni öldürmez). " +
                "Karşılığında parry ödülün (riposte) çok güçlenir.",
                2,
                Stat(
                    (StatType.BlockHealthCost, 0.06f, 1f),
                    (StatType.RiposteStrength, 0f, 1.5f)
                )
            )
        );

        list.Add(
            Make(
                "Kusursuzluk",
                "Bir bölümü hiç vuruş yemeden bitirirsen büyük iyileşme " +
                "ve ekstra charm seçimi kazanırsın.",
                3,
                ScriptableObject.CreateInstance<PerfectionEffect>()
            )
        );

        list.Add(
            Make(
                "Odak",
                "Hasar yemeden vurdukça vuruşların keskinleşir: kritik " +
                "şansın birikir. Bir darbe yersen odağın dağılır.",
                3,
                ScriptableObject.CreateInstance<FocusCritEffect>()
            )
        );

        // =====================================================
        // ZEHİR EKSENİ
        // =====================================================

        CharmDefinition poisonParryDef =
            Make(
                "Zehirli Parry",
                "Parry'lediğin düşman zehirlenir.",
                3,
                ScriptableObject.CreateInstance<PoisonParryEffect>()
            );

        list.Add(poisonParryDef);

        CharmDefinition plagueDef =
            Make(
                "Salgın",
                "Zehirli düşman ölünce zehir çevresine yayılır.",
                3,
                ScriptableObject.CreateInstance<PlagueEffect>()
            );

        plagueDef.requiresAnyOf.Add(poisonDef);
        plagueDef.requiresAnyOf.Add(poisonParryDef);

        list.Add(plagueDef);

        CharmDefinition paralyzeDef =
            Make(
                "Felç Edici Zehir",
                "Zehirli düşmanlar daha yavaş saldırır.",
                3,
                ScriptableObject.CreateInstance<ParalyzingPoisonEffect>()
            );

        paralyzeDef.requiresAnyOf.Add(poisonDef);
        paralyzeDef.requiresAnyOf.Add(poisonParryDef);

        list.Add(paralyzeDef);

        return list;
    }

    private static CharmDefinition Make(
        string name,
        string description,
        int maxStacks,
        CharmEffect effect
    )
    {
        CharmDefinition def =
            ScriptableObject.CreateInstance<CharmDefinition>();

        def.displayName = name;
        def.description = description;
        def.maxStacks = maxStacks;
        def.weight = 1f;
        def.effect = effect;

        return def;
    }

    private static StatCharmEffect Stat(
        StatType type,
        float add = 0f,
        float mult = 1f
    )
    {
        return Stat((type, add, mult));
    }

    // Birden çok stat'lı charm: (tip, istif başına ekleme, istif başına çarpan)
    private static StatCharmEffect Stat(
        params (StatType type, float add, float mult)[] values
    )
    {
        StatCharmEffect effect =
            ScriptableObject.CreateInstance<StatCharmEffect>();

        for (int i = 0; i < values.Length; i++)
        {
            effect.entries.Add(
                new StatCharmEffect.Entry
                {
                    type = values[i].type,
                    addPerStack = values[i].add,
                    multPerStack = values[i].mult
                }
            );
        }

        return effect;
    }

    // Havuzdan, envantere hâlâ eklenebilen charm'lar arasından
    // ağırlıklı, TEKRARSIZ 'count' tane seç. (CanAdd ön koşulları da
    // kontrol eder: zehir kaynağı yoksa Salgın çıkmaz.)
    public static List<CharmDefinition> Roll(
        List<CharmDefinition> pool,
        CharmInventory inventory,
        int count
    )
    {
        List<CharmDefinition> candidates =
            new List<CharmDefinition>();

        for (int i = 0; i < pool.Count; i++)
        {
            CharmDefinition def = pool[i];

            if (
                def != null &&
                def.weight > 0f &&
                inventory.CanAdd(def)
            )
            {
                candidates.Add(def);
            }
        }

        List<CharmDefinition> result =
            new List<CharmDefinition>();

        while (result.Count < count && candidates.Count > 0)
        {
            float total = 0f;

            for (int i = 0; i < candidates.Count; i++)
                total += candidates[i].weight;

            float roll = Random.value * total;

            int picked = candidates.Count - 1;

            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= candidates[i].weight;

                if (roll <= 0f)
                {
                    picked = i;
                    break;
                }
            }

            result.Add(candidates[picked]);

            candidates.RemoveAt(picked);
        }

        return result;
    }
}