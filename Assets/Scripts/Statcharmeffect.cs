using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// İstatistik charm'ı: PlayerStats'a değiştirici ekler.
/// Her istif için: ekleme = addPerStack × istif,  çarpan = multPerStack ^ istif.
/// </summary>
[CreateAssetMenu(menuName = "Charms/Effect/Stat", fileName = "StatEffect")]
public class StatCharmEffect : CharmEffect
{
    [Serializable]
    public class Entry
    {
        public StatType type;
        public float addPerStack;
        public float multPerStack = 1f;
    }

    public List<Entry> entries = new List<Entry>();

    public override void Apply(CharmContext context, int stacks)
    {
        if (context == null || context.stats == null)
            return;

        // Her seferinde baştan kur: istif artınca değerler güncellensin.
        context.stats.RemoveModifiers(context.owner);

        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];

            context.stats.AddModifier(
                context.owner,
                e.type,
                e.addPerStack * stacks,
                Mathf.Pow(e.multPerStack, stacks)
            );
        }
    }

    public override void Remove(CharmContext context)
    {
        if (context == null || context.stats == null)
            return;

        context.stats.RemoveModifiers(context.owner);
    }

    public override string Describe(int stacks)
    {
        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];

            if (i > 0)
                sb.Append(", ");

            float add = e.addPerStack * stacks;
            float mult = Mathf.Pow(e.multPerStack, stacks);

            switch (e.type)
            {
                case StatType.CritChance:
                    sb.Append("Kritik şansı +%" + Mathf.RoundToInt(add * 100f));
                    break;

                case StatType.CritMultiplier:
                    sb.Append("Kritik çarpanı +" + add.ToString("0.0") + "x");
                    break;

                case StatType.BalanceDamage:
                    sb.Append(Format("Denge hasarı", add, mult));
                    break;

                case StatType.HealthDamage:
                    sb.Append(Format("Can hasarı", add, mult));
                    break;
            }
        }

        return sb.ToString();
    }

    private static string Format(string label, float add, float mult)
    {
        if (!Mathf.Approximately(mult, 1f))
            return label + " +%" + Mathf.RoundToInt((mult - 1f) * 100f);

        return label + " +" + add.ToString("0.#");
    }
}