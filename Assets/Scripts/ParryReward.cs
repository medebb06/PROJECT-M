using UnityEngine;

/// <summary>
/// Basarili parry odulu (kalabalikta parry oynanabilsin diye):
///  - kisa sureli dokunulmazlik (zincir kopmasin),
///  - yakindaki diger dusmanlar geri itilir,
///  - bir sonraki dusman vurusu biraz ertelenir (nefes alma).
/// Oyun baslayinca kendiliginden kurulur.
/// </summary>
public class ParryReward : MonoBehaviour
{
    [Tooltip("Parry sonrasi dokunulmazlik (sn).")] public float invulnerable = 0.45f;
    [Tooltip("Bu mesafedeki diger dusmanlar geri itilir.")] public float pushRadius = 6f;
    [Tooltip("Parry sonrasi siradaki dusman vurusu en az bu kadar sonra gelir (sn).")] public float breather = 0.55f;

    PlayerController player;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindAnyObjectByType<ParryReward>() != null) return;
        var go = new GameObject("ParryReward");
        DontDestroyOnLoad(go);
        go.AddComponent<ParryReward>();
    }

    void OnEnable() { CombatEvents.ParrySucceeded += OnParry; }
    void OnDisable() { CombatEvents.ParrySucceeded -= OnParry; }

    void OnParry(EnemyBase parried, bool brokeBalance)
    {
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (player == null) return;

        player.hitInvincibilityTimer = Mathf.Max(player.hitInvincibilityTimer, invulnerable);

        Vector2 pp = player.transform.position;

        for (int i = 0; i < EnemyBase.All.Count; i++)
        {
            var e = EnemyBase.All[i];
            if (e == null || e == parried || e.IsDead) continue;

            Vector2 d = (Vector2)e.transform.position - pp;
            if (d.sqrMagnitude > pushRadius * pushRadius) continue;

            e.ApplyBlockKnockback(d.sqrMagnitude < 0.0001f ? Vector2.right : d.normalized);
        }

        EnemyAttackCoordinator.DelayNextHit(breather);
    }
}
