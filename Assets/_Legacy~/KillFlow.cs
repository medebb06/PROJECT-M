using UnityEngine;

// Öldürme ivmesi: 4 sn içinde üst üste her kill hasarı +%8 artırır (6 kill = +%48). Kill gelmezse söner.
public class KillFlow : MonoBehaviour
{
    public const float ChainWindow = 4f;
    public const float PerKill = 0.08f;
    public const int MaxStacks = 6;

    public static int Chain { get; private set; }
    public static float LastKillTime { get; private set; }

    public static int Stacks { get { return Time.time - LastKillTime > ChainWindow ? 0 : Mathf.Min(Chain, MaxStacks); } }
    public static float DamageMult { get { return 1f + PerKill * Stacks; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        Chain = 0; LastKillTime = -999f;
        GameObject go = new GameObject("KillFlow");
        DontDestroyOnLoad(go);
        go.AddComponent<KillFlow>();
    }

    private void OnEnable() { CombatEvents.EnemyKilled += OnKilled; }
    private void OnDisable() { CombatEvents.EnemyKilled -= OnKilled; }

    private void OnKilled(EnemyBase e)
    {
        if (Time.time - LastKillTime > ChainWindow) Chain = 0;
        Chain++;
        LastKillTime = Time.time;
    }
}
