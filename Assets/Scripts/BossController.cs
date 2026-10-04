using System;
using UnityEngine;

/// <summary>
/// BOSS. RunManager perde sonunda doğurduğu düşmana ekler (prefab'a
/// elle eklemene gerek yok; özel bir boss prefab'ı da kullanabilirsin).
///
///  - Faz 1: boss hamle seti (Düellocu + dörtlü kombo).
///  - Canı 'phase2At' oranına inince FAZ 2: daha uzun / karışık kombolar,
///    biraz daha hızlı. Sersem / execute sırasında geçiş beklenir.
///  - Arayüz (RunUI) üstte isim + can + denge çubuğu gösterir.
/// </summary>
[RequireComponent(typeof(EnemyController))]
public class BossController : MonoBehaviour
{
    public static BossController Current { get; private set; }

    public string BossName { get; private set; } = "Boss";
    public bool InPhase2 { get; private set; }

    public event Action PhaseChanged;

    private float phase2At = 0.5f;

    private EnemyController enemy;
    private EnemyMoveset moveset;
    private EnemyBalance balance;

    public Health Health { get; private set; }

    public float HealthPercent =>
        Health != null && Health.MaxHealth > 0
            ? (float)Health.CurrentHealth / Health.MaxHealth
            : 0f;

    public float BalancePercent =>
        balance != null ? balance.BalancePercent : 0f;

    public bool IsAlive => enemy != null && !enemy.IsDead;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration
    )]
    private static void ResetStatics()
    {
        Current = null;
    }

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        Health = GetComponent<Health>();
        balance = GetComponent<EnemyBalance>();
    }

    public void Setup(string bossName, float phase2Threshold)
    {
        BossName = bossName;
        phase2At = Mathf.Clamp01(phase2Threshold);
        InPhase2 = false;

        moveset = GetComponent<EnemyMoveset>();

        if (moveset == null)
            moveset = gameObject.AddComponent<EnemyMoveset>();

        moveset.moves = EnemyMoveset.CreateBossPhase1Moves();

        Current = this;
    }

    private void OnDestroy()
    {
        if (Current == this)
            Current = null;
    }

    private void Update()
    {
        if (InPhase2 || Health == null || enemy == null || enemy.IsDead)
            return;

        if (HealthPercent > phase2At)
            return;

        // Sersem / execute sırasında faz geçişi yapma: execute fırsatı
        // bozulmasın. Sonra geçer.
        if (enemy.IsStaggered || enemy.CurrentState is EnemyExecuteState)
            return;

        EnterPhase2();
    }

    private void EnterPhase2()
    {
        InPhase2 = true;

        if (moveset != null)
            moveset.moves = EnemyMoveset.CreateBossPhase2Moves();

        enemy.chaseSpeed *= 1.15f;
        enemy.attackRecoveryTime *= 0.85f;

        // Kısa nefes: oyuncu ne olduğunu görsün.
        if (balance != null && !balance.IsBroken)
            balance.ResetBalance();

        enemy.PlayAlertFlash();

        HitStop.Request(0.25f, 0.1f);

        if (RunManager.Instance != null)
            RunManager.Instance.ShowBanner("FAZ 2", 1.4f);

        PhaseChanged?.Invoke();

        Debug.Log("BOSS FAZ 2: " + BossName);
    }
}
