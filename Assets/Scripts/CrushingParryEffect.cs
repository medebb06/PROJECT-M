using UnityEngine;

/// <summary>
/// EZİCİ PARRY (parry): dengeyi KIRAN parry düşmanı daha uzun sersemletir
/// ve o sersemlemede yapılan execute daha çok can hasarı verir.
/// </summary>
[CreateAssetMenu(menuName = "Charms/Effect/Crushing Parry", fileName = "CrushingParryEffect")]
public class CrushingParryEffect : CharmEffect
{
    [Tooltip("İstif başına ek sersemleme (düşman saniyesi).")]
    public float extraStaggerPerStack = 0.5f;

    [Tooltip("İstif başına execute hasarı bonusu (0.5 = +%50).")]
    public float executeBonusPerStack = 0.5f;

    private int stacks;
    private bool subscribed;

    public override void Apply(CharmContext ctx, int newStacks)
    {
        stacks = newStacks;

        if (subscribed)
            return;

        CombatEvents.ParrySucceeded += OnParry;
        subscribed = true;
    }

    public override void Remove(CharmContext ctx)
    {
        if (subscribed)
            CombatEvents.ParrySucceeded -= OnParry;

        subscribed = false;
        stacks = 0;
    }

    private void OnParry(EnemyController enemy, bool brokeBalance)
    {
        if (!brokeBalance || enemy == null || enemy.IsDead)
            return;

        // Stagger, parry olayından ÖNCE başladı (EnemyBalance olayı).
        if (enemy.CurrentState is EnemyStaggerState stagger)
            stagger.Extend(extraStaggerPerStack * stacks);

        EnemyStatus status = EnemyStatus.Get(enemy);

        status.ExecuteMultiplier =
            Mathf.Max(
                status.ExecuteMultiplier,
                1f + executeBonusPerStack * stacks
            );
    }

    public override string Describe(int newStacks)
    {
        return
            "Dengeyi kıran parry: +" +
            (extraStaggerPerStack * newStacks).ToString("0.#") +
            " sn sersemleme, execute hasarı +" +
            CharmUtil.Percent(executeBonusPerStack * newStacks);
    }
}
