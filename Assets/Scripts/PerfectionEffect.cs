using UnityEngine;

/// <summary>
/// KUSURSUZLUK (risk): bir bölümü HİÇ vuruş yemeden bitirirsen büyük
/// iyileşme ve ekstra bir charm seçimi. Vuruş yiyince (Kan Bedeli'nin
/// block bedeli hariç) o bölümün ödülü kaybolur.
/// </summary>
[CreateAssetMenu(menuName = "Charms/Effect/Perfection", fileName = "PerfectionEffect")]
public class PerfectionEffect : CharmEffect
{
    [Tooltip("İstif başına iyileşme: max canın yüzdesi.")]
    public float healPercentPerStack = 0.2f;

    [Tooltip("Temiz bölüm ekstra charm seçimi de verir.")]
    public bool grantBonusOffer = true;

    private CharmContext context;
    private int stacks;
    private bool subscribed;
    private bool clean = true;

    public override void Apply(CharmContext ctx, int newStacks)
    {
        context = ctx;
        stacks = newStacks;

        if (subscribed)
            return;

        RunManager.StageStarted += OnStageStarted;
        RunManager.StageCleared += OnStageCleared;
        CombatEvents.PlayerDamaged += OnDamaged;
        subscribed = true;
    }

    public override void Remove(CharmContext ctx)
    {
        if (subscribed)
        {
            RunManager.StageStarted -= OnStageStarted;
            RunManager.StageCleared -= OnStageCleared;
            CombatEvents.PlayerDamaged -= OnDamaged;
        }

        subscribed = false;
        stacks = 0;
        context = null;
        clean = true;
    }

    private void OnStageStarted(int stage)
    {
        clean = true;
    }

    private void OnDamaged(PlayerDamageReport report)
    {
        if (report.kind == PlayerHitKind.BlockCost)
            return;

        clean = false;
    }

    private void OnStageCleared(int stage)
    {
        if (!clean || context == null)
            return;

        Health health = context.playerHealth;

        if (health != null && !health.IsDead)
        {
            int amount =
                Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        health.MaxHealth * healPercentPerStack * stacks
                    )
                );

            health.Heal(amount);
        }

        if (grantBonusOffer && RunManager.Instance != null)
            RunManager.Instance.QueueBonusOffer();

        Debug.Log("KUSURSUZLUK: bölüm " + stage + " hasarsız bitti!");
    }

    public override string Status()
    {
        return clean ? "temiz" : "bozuldu";
    }

    public override string Describe(int newStacks)
    {
        return
            "Hasar yemeden biten bölüm: max canın " +
            CharmUtil.Percent(healPercentPerStack * newStacks) +
            "'i iyileşme" +
            (grantBonusOffer ? " + ekstra charm seçimi" : "");
    }
}
