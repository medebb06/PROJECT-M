using System;
using UnityEngine;

// Oyuncuya gelen hasarın türü (istatistik / ölüm nedeni için).
public enum PlayerHitKind
{
    Normal,       // düşmanın normal (parry/block edilebilir) vuruşu
    Unblockable,  // engellenemez vuruş
    Other         // kaynağı belirtilmemiş (eski IDamageable yolu vb.)
}

// Oyuncunun aldığı bir hasarın tam kaydı.
public struct PlayerDamageReport
{
    public int amount;              // canından GERÇEKTEN düşen miktar
    public int healthAfter;
    public int maxHealth;
    public bool lethal;             // bu vuruş öldürdü mü
    public PlayerHitKind kind;
    public EnemyController source;  // vuran düşman (yoksa null)
    public Vector2 direction;
}

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

    // Oyuncu bir saldırıdan kaçtı (dash VEYA hasar sonrası korumalı dönem).
    // bool: saldırı engellenemez miydi.
    public static event Action<EnemyController, bool> Dodged;

    // Oyuncu hasar aldı. int: can hasarı, Vector2: vuruş yönü.
    // (Eski olay; ayrıntı için PlayerDamaged kullan.)
    public static event Action<int, Vector2> PlayerHurt;

    // Oyuncu hasar aldı: kaynak, tür, ölümcül mü (istatistik / ölüm nedeni).
    public static event Action<PlayerDamageReport> PlayerDamaged;

    // Oyuncu bir vuruşu block'ladı. bool: bu block posture'ı kırdı mı.
    public static event Action<EnemyController, bool> PlayerBlocked;

    // Düşman saldırısı vuruş anında oyuncuya ulaşamadı (menzil/yön/zıplama).
    // bool: engellenemez miydi.
    public static event Action<EnemyController, bool> AttackMissed;

    // Düşman saldırısı vuruş anından ÖNCE kesildi (oyuncu vurdu, denge
    // kırıldı, zehir...). bool: engellenemez miydi.
    public static event Action<EnemyController, bool> AttackInterrupted;

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
        PlayerDamaged = null;
        PlayerBlocked = null;
        AttackMissed = null;
        AttackInterrupted = null;
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

    public static void RaisePlayerDamaged(PlayerDamageReport report)
    {
        Invoke(PlayerDamaged, d => d(report));
    }

    public static void RaisePlayerBlocked(
        EnemyController enemy,
        bool postureBroken
    )
    {
        Invoke(PlayerBlocked, d => d(enemy, postureBroken));
    }

    public static void RaiseAttackMissed(
        EnemyController enemy,
        bool unblockable
    )
    {
        Invoke(AttackMissed, d => d(enemy, unblockable));
    }

    public static void RaiseAttackInterrupted(
        EnemyController enemy,
        bool unblockable
    )
    {
        Invoke(AttackInterrupted, d => d(enemy, unblockable));
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