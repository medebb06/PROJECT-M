using UnityEngine;

/// <summary>
/// İSTEĞE BAĞLI: Sahnede bir objeye ekle, tüm dövüş olaylarını konsola yazar.
/// Olayların (CombatEvents) doğru tetiklendiğini görmek için.
/// </summary>
public class CombatDebugLogger : MonoBehaviour
{
    private void OnEnable()
    {
        CombatEvents.EnemyHit += OnEnemyHit;
        CombatEvents.EnemyKilled += OnEnemyKilled;
        CombatEvents.BalanceBroken += OnBalanceBroken;
        CombatEvents.ParrySucceeded += OnParry;
        CombatEvents.Dodged += OnDodged;
        CombatEvents.PlayerHurt += OnPlayerHurt;
        CombatEvents.PlayerDashed += OnPlayerDashed;
    }

    private void OnDisable()
    {
        CombatEvents.EnemyHit -= OnEnemyHit;
        CombatEvents.EnemyKilled -= OnEnemyKilled;
        CombatEvents.BalanceBroken -= OnBalanceBroken;
        CombatEvents.ParrySucceeded -= OnParry;
        CombatEvents.Dodged -= OnDodged;
        CombatEvents.PlayerHurt -= OnPlayerHurt;
        CombatEvents.PlayerDashed -= OnPlayerDashed;
    }

    private void OnEnemyHit(EnemyController e, DamageInfo i, HitResult r)
    {
        Debug.Log(
            "[EVENT] EnemyHit " + e.name + " " + i.source +
            (r.onBalance ? " denge " : " can ") + r.amount +
            (r.critical ? " KRİTİK" : "")
        );
    }

    private void OnEnemyKilled(EnemyController e) =>
        Debug.Log("[EVENT] EnemyKilled " + e.name);

    private void OnBalanceBroken(EnemyController e) =>
        Debug.Log("[EVENT] BalanceBroken " + e.name);

    private void OnParry(EnemyController e, bool broke) =>
        Debug.Log("[EVENT] Parry " + e.name + (broke ? " (denge kırıldı)" : ""));

    private void OnDodged(EnemyController e, bool unblockable) =>
        Debug.Log("[EVENT] Dodged " + e.name + (unblockable ? " (engellenemez)" : ""));

    private void OnPlayerHurt(int damage, Vector2 dir) =>
        Debug.Log("[EVENT] PlayerHurt -" + damage);

    private void OnPlayerDashed() =>
        Debug.Log("[EVENT] PlayerDashed");
}