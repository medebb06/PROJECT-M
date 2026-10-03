using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SALGIN (zehir): zehirli düşman ölünce zehri çevredeki düşmanlara yayılır
/// (ölenin zehir hızıyla). Zincirleme yayılabilir.
/// Ön koşul: bir zehir kaynağı (Zehir ya da Zehirli Parry).
/// </summary>
[CreateAssetMenu(menuName = "Charms/Effect/Plague", fileName = "PlagueEffect")]
public class PlagueEffect : CharmEffect
{
    public float radius = 4f;
    public float radiusPerExtraStack = 0.75f;

    [Tooltip("Yayılan zehrin süresi (düşman saniyesi).")]
    public float duration = 3f;
    public float durationPerExtraStack = 1f;

    private int stacks;
    private bool subscribed;

    public override void Apply(CharmContext ctx, int newStacks)
    {
        stacks = newStacks;

        if (subscribed)
            return;

        CombatEvents.EnemyKilled += OnKilled;
        subscribed = true;
    }

    public override void Remove(CharmContext ctx)
    {
        if (subscribed)
            CombatEvents.EnemyKilled -= OnKilled;

        subscribed = false;
        stacks = 0;
    }

    private float Radius(int s) =>
        radius + radiusPerExtraStack * Mathf.Max(0, s - 1);

    private float Duration(int s) =>
        duration + durationPerExtraStack * Mathf.Max(0, s - 1);

    private void OnKilled(EnemyController dead)
    {
        if (dead == null)
            return;

        EnemyStatus status = dead.GetComponent<EnemyStatus>();

        if (status == null)
            return;

        if (!status.IsPoisoned && !status.DiedPoisoned)
            return;

        float balanceRate = status.PoisonBalanceRate;
        float healthRate = status.PoisonHealthRate;

        if (balanceRate <= 0f && healthRate <= 0f)
            return;

        List<EnemyController> near =
            CharmUtil.EnemiesInRadius(
                dead.transform.position,
                Radius(stacks),
                dead
            );

        for (int i = 0; i < near.Count; i++)
        {
            EnemyStatus.Get(near[i]).ApplyPoison(
                balanceRate,
                healthRate,
                Duration(stacks)
            );
        }
    }

    public override string Describe(int newStacks)
    {
        return
            "Zehirli düşman ölünce zehir " + Radius(newStacks).ToString("0.#") +
            " birimdeki düşmanlara " + Duration(newStacks).ToString("0.#") +
            " sn yayılır";
    }
}
