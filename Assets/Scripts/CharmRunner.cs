using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Davranış charm'larının yardımcısı (oyuncuya kendiliğinden eklenir).
/// Charm efektleri ScriptableObject olduğu için Update alamaz; zamana bağlı
/// işler için CharmRunner.Tick olayına abone olurlar.
///
/// Ayrıca son dash zamanını tutar: bir kaçışın DASH ile mi yoksa hasar
/// sonrası korumalı dönemle mi olduğunu ayırmak için.
/// </summary>
public class CharmRunner : MonoBehaviour
{
    public static CharmRunner Instance { get; private set; }

    // Her karede (ölçekli deltaTime ile). Oyun duraklatılmışken dt = 0.
    public static event Action<float> Tick;

    public static float LastDashTime { get; private set; } = -999f;

    public PlayerController Player { get; private set; }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration
    )]
    private static void ResetStatics()
    {
        Instance = null;
        Tick = null;
        LastDashTime = -999f;
    }

    public static CharmRunner Ensure(GameObject player)
    {
        CharmRunner runner = player.GetComponent<CharmRunner>();

        if (runner == null)
            runner = player.AddComponent<CharmRunner>();

        return runner;
    }

    // Kaçış dash'le mi oldu? (Dash sürüyor ya da az önce bitti.)
    public static bool IsDashDodge(PlayerController player, float window = 0.6f)
    {
        if (player != null && player.isDashing)
            return true;

        return Time.time - LastDashTime <= window;
    }

    private void Awake()
    {
        Instance = this;
        Player = GetComponent<PlayerController>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        CombatEvents.PlayerDashed += OnDashed;
    }

    private void OnDisable()
    {
        CombatEvents.PlayerDashed -= OnDashed;
    }

    private void OnDashed()
    {
        LastDashTime = Time.time;
    }

    private void Update()
    {
        if (Tick == null)
            return;

        float dt = Time.deltaTime;

        Delegate[] list = Tick.GetInvocationList();

        for (int i = 0; i < list.Length; i++)
        {
            try
            {
                ((Action<float>)list[i])(dt);
            }
            catch (Exception e)
            {
                Debug.LogError("CharmRunner: tick hatası → " + e);
            }
        }
    }
}

/// <summary>
/// Charm'ların ortak işleri. Bu yardımcılar PlayerDamage hattından GEÇMEZ
/// (zehir gibi): kritik, riposte ve vuruş tepkisi üretmez.
/// </summary>
public static class CharmUtil
{
    // Düşmanın dengesine MAX dengesinin yüzdesi kadar hasar.
    // Denge kırılırsa stagger EnemyBalance olayıyla kendiliğinden başlar.
    public static bool AddBalancePercent(EnemyController enemy, float percent)
    {
        if (enemy == null || enemy.IsDead || percent <= 0f)
            return false;

        EnemyBalance balance = enemy.GetComponent<EnemyBalance>();

        if (balance == null || balance.IsBroken)
            return false;

        int amount =
            Mathf.Max(1, Mathf.RoundToInt(balance.MaxBalance * percent));

        if (!balance.AddBalanceDamage(amount))
            return false;

        if (!balance.IsBroken)
            enemy.PlayBalanceDamageFlash();

        return true;
    }

    // Merkeze 'radius' mesafedeki canlı düşmanlar (hariç tutulan dışında).
    // Liste kopyalanır: döngü sırasında stagger/ölüm listeyi bozmasın.
    public static List<EnemyController> EnemiesInRadius(
        Vector2 center,
        float radius,
        EnemyController exclude = null
    )
    {
        List<EnemyController> result = new List<EnemyController>();

        EnemyController[] all = EnemyController.All.ToArray();

        float sqr = radius * radius;

        for (int i = 0; i < all.Length; i++)
        {
            EnemyController e = all[i];

            if (e == null || e == exclude || e.IsDead)
                continue;

            Vector2 d = (Vector2)e.transform.position - center;

            if (d.sqrMagnitude <= sqr)
                result.Add(e);
        }

        return result;
    }

    public static string Percent(float value)
    {
        return "%" + Mathf.RoundToInt(value * 100f);
    }
}
