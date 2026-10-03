using UnityEngine;

/// <summary>
/// FELÇ EDİCİ ZEHİR (zehir): zehirli düşmanların saldırı uyarısı uzar
/// (daha yavaş saldırırlar; parry/dash zamanlaması kolaylaşır).
/// Ön koşul: bir zehir kaynağı (Zehir ya da Zehirli Parry).
///
/// Not: Uyarı, saldırı BAŞLARKEN zehirliyse uzar. Ritim koordinatörü normal
/// süreyi bildiği için zehirli düşmanın vuruşu sırasından biraz kayabilir.
/// </summary>
[CreateAssetMenu(menuName = "Charms/Effect/Paralyzing Poison", fileName = "ParalyzingPoisonEffect")]
public class ParalyzingPoisonEffect : CharmEffect
{
    [Tooltip("İstif başına uyarı süresi artışı (0.2 = +%20).")]
    public float windupBonusPerStack = 0.2f;

    public override void Apply(CharmContext ctx, int newStacks)
    {
        EnemyStatus.PoisonedWindupMultiplier =
            1f + windupBonusPerStack * newStacks;
    }

    public override void Remove(CharmContext ctx)
    {
        EnemyStatus.PoisonedWindupMultiplier = 1f;
    }

    public override string Describe(int newStacks)
    {
        return
            "Zehirli düşmanların saldırı uyarısı +" +
            CharmUtil.Percent(windupBonusPerStack * newStacks) +
            " uzar";
    }
}
