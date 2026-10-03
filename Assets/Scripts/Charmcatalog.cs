using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Varsayılan charm'lar (kodla üretilir, asset gerekmez) ve seçim ekranı
/// için ağırlıklı rastgele seçim.
/// </summary>
public static class CharmCatalog
{
    public static List<CharmDefinition> CreateDefaults()
    {
        List<CharmDefinition> list = new List<CharmDefinition>();

        // ---------------- ZEHİR ----------------

        PoisonCharmEffect poison =
            ScriptableObject.CreateInstance<PoisonCharmEffect>();

        list.Add(
            Make(
                "Zehir",
                "Vuruşların düşmanı zehirler. Zehir önce dengeyi eritir, " +
                "denge kırıksa canı eritir. Süre her vuruşta yenilenir.",
                5,
                poison
            )
        );

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
        StatCharmEffect effect =
            ScriptableObject.CreateInstance<StatCharmEffect>();

        effect.entries.Add(
            new StatCharmEffect.Entry
            {
                type = type,
                addPerStack = add,
                multPerStack = mult
            }
        );

        return effect;
    }

    // Havuzdan, envantere hâlâ eklenebilen charm'lar arasından
    // ağırlıklı, TEKRARSIZ 'count' tane seç.
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