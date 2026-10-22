using UnityEngine;

// Öldürme akışı: kill can verir; zincir (3 sn içinde üst üste kill) ödülü büyütür.
// Kısa zincir = 4 can, her ek kill +2, tavan 14. Zincir kopunca sıfırlanır.
public class KillFlow : MonoBehaviour
{
    public static int Chain { get; private set; }
    public static float LastKillTime { get; private set; }

    public int baseHeal = 4;
    public int perChain = 2;
    public int maxHeal = 14;
    public float chainWindow = 3f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        GameObject go = new GameObject("KillFlow");
        DontDestroyOnLoad(go);
        go.AddComponent<KillFlow>();
    }

    private void OnEnable() { CombatEvents.EnemyKilled += OnKilled; Chain = 0; }
    private void OnDisable() { CombatEvents.EnemyKilled -= OnKilled; }

    private void OnKilled(EnemyBase e)
    {
        if (Time.time - LastKillTime > chainWindow) Chain = 0;
        Chain++;
        LastKillTime = Time.time;

        GameObject p = GameObject.FindWithTag("Player");
        if (p == null) return;
        Health h = p.GetComponent<Health>();
        if (h == null || h.IsDead) return;
        h.Heal(Mathf.Min(maxHeal, baseHeal + perChain * (Chain - 1)));
    }
}
