using UnityEngine;

/// <summary>
/// EFSANEVİ · KAN RİTMİ (risk): her başarılı parry max canın %3'ünü
/// iyileştirir; AMA oda arası ve nöbet temizleme iyileşmeleri KAPANIR.
/// Can tamamen parry'e bağlı.
/// </summary>
[CreateAssetMenu(menuName = "Charms/Effect/Blood Rhythm", fileName = "BloodRhythmEffect")]
public class BloodRhythmEffect : CharmEffect
{
    // RunManager okur: açıkken oda arası / nöbet iyileşmesi yok.
    public static bool Active { get; private set; }

    [Range(0f, 1f)] public float healPercent = 0.03f;

    private CharmContext context;
    private bool subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Active = false;
    }

    public override void Apply(CharmContext ctx, int stacks)
    {
        context = ctx;
        Active = true;

        if (subscribed)
            return;

        CombatEvents.ParrySucceeded += OnParry;
        subscribed = true;
    }

    public override void Remove(CharmContext ctx)
    {
        if (subscribed)
            CombatEvents.ParrySucceeded -= OnParry;

        Active = false;
        subscribed = false;
        context = null;
    }

    private void OnParry(EnemyController enemy, bool brokeBalance)
    {
        Health h = context != null ? context.playerHealth : null;

        if (h == null || h.IsDead)
            return;

        h.Heal(Mathf.Max(1, Mathf.RoundToInt(h.MaxHealth * healPercent)));
    }

    public override string Describe(int stacks)
    {
        return "Her parry +" + CharmUtil.Percent(healPercent) + " can. AMA oda arası ve nöbet iyileşmeleri kapanır.";
    }
}
