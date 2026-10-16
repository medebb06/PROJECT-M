using UnityEngine;

/// <summary>
/// Dengesi (blok) kirilan dusman canindan %20-30 hasar yer (boss: %5).
/// Oyun baslayinca kendiliginden kurulur.
/// </summary>
public class BlockBreakDamage : MonoBehaviour
{
    [Range(0f, 1f)] public float minPercent = 0.20f;
    [Range(0f, 1f)] public float maxPercent = 0.30f;
    [Range(0f, 1f)] public float bossPercent = 0.05f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindAnyObjectByType<BlockBreakDamage>() != null) return;
        var go = new GameObject("BlockBreakDamage");
        DontDestroyOnLoad(go);
        go.AddComponent<BlockBreakDamage>();
    }

    void OnEnable() { CombatEvents.BalanceBroken += OnBroken; }
    void OnDisable() { CombatEvents.BalanceBroken -= OnBroken; }

    void OnBroken(EnemyController e)
    {
        if (e == null || e.IsDead) return;
        Health h = e.GetComponent<Health>();
        if (h == null || h.IsDead || h.MaxHealth <= 0) return;

        bool boss = e.GetComponent<BossController>() != null || e.GetComponent<Boss1Enemy>() != null;
        float p = boss ? bossPercent : Random.Range(minPercent, maxPercent);
        int dmg = Mathf.Max(1, Mathf.RoundToInt(h.MaxHealth * p));
        h.TakeDamage(dmg);
    }
}
