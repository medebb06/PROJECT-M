using System;
using UnityEngine;

/// <summary>
/// Dövüşün olay merkezi. Charm'lar ve diğer sistemler (arayüz, ses, istatistik)
/// oyun kodunu DEĞİŞTİRMEDEN buraya abone olur:
///
///   void OnEnable()  { CombatEvents.EnemyHit += OnHit; }
///   void OnDisable() { CombatEvents.EnemyHit -= OnHit; }
///
/// Bir abonenin hatası dövüşü bozmasın diye her abone ayrı ayrı çağrılır
/// ve hata yakalanır (konsola yazılır).
/// </summary>
public static class CombatEvents
{
    // Oyuncu bir düşmana vurdu (pipeline'dan geçti).
    public static event Action<EnemyController, DamageInfo, HitResult> EnemyHit;

    // Düşman öldü (kaynak ne olursa olsun: vuruş, slam, execute, zehir).
    public static event Action<EnemyController> EnemyKilled;

    // Düşmanın dengesi kırıldı (stagger başladı).
    public static event Action<EnemyController> BalanceBroken;

    // Başarılı parry. bool: parry dengeyi kırdı mı.
    public static event Action<EnemyController, bool> ParrySucceeded;

    // Oyuncu bir saldırıdan kaçtı (dash). bool: saldırı engellenemez miydi.
    public static event Action<EnemyController, bool> Dodged;

    // Oyuncu hasar aldı. int: can hasarı, Vector2: vuruş yönü.
    public static event Action<int, Vector2> PlayerHurt;

    // Oyuncu dash attı.
    public static event Action PlayerDashed;

    // Editor'de "Enter Play Mode" (domain reload kapalı) ayarında aboneler
    // oyunlar arasında kalmasın.
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration
    )]
    private static void ResetStatics()
    {
        EnemyHit = null;
        EnemyKilled = null;
        BalanceBroken = null;
        ParrySucceeded = null;
        Dodged = null;
        PlayerHurt = null;
        PlayerDashed = null;
    }

    // ---------------------------------------------------------
    // Tetikleyiciler (oyun kodu çağırır)
    // ---------------------------------------------------------

    public static void RaiseEnemyHit(
        EnemyController enemy,
        DamageInfo info,
        HitResult result
    )
    {
        Invoke(EnemyHit, d => d(enemy, info, result));
    }

    public static void RaiseEnemyKilled(EnemyController enemy)
    {
        Invoke(EnemyKilled, d => d(enemy));
    }

    public static void RaiseBalanceBroken(EnemyController enemy)
    {
        Invoke(BalanceBroken, d => d(enemy));
    }

    public static void RaiseParry(EnemyController enemy, bool brokeBalance)
    {
        Invoke(ParrySucceeded, d => d(enemy, brokeBalance));
    }

    public static void RaiseDodge(EnemyController enemy, bool unblockable)
    {
        Invoke(Dodged, d => d(enemy, unblockable));
    }

    public static void RaisePlayerHurt(int damage, Vector2 direction)
    {
        Invoke(PlayerHurt, d => d(damage, direction));
    }

    public static void RaisePlayerDash()
    {
        Invoke(PlayerDashed, d => d());
    }

    // Her aboneyi ayrı çağır; birinin hatası diğerlerini ve dövüşü bozmasın.
    private static void Invoke<T>(T handlers, Action<T> call)
        where T : Delegate
    {
        if (handlers == null)
            return;

        Delegate[] list = handlers.GetInvocationList();

        for (int i = 0; i < list.Length; i++)
        {
            try
            {
                call((T)(object)list[i]);
            }
            catch (Exception e)
            {
                Debug.LogError(
                    "CombatEvents: abone hatası → " + e
                );
            }
        }
    }
}