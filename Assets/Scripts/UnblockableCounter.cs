using System;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// ENGELLENEMEZ VURUŞLARI FIRSATA ÇEVİRİR. İki karşılık:
///
///  1) KUSURSUZ KAÇIŞ (yakalamaya karşı):
///     Yakalama (sarı) gelirken SON ANDA ve DÜŞMANA DOĞRU dash at →
///     düşmanın içinden geçersin, düşman büyük denge hasarı yer,
///     kombosu kesilir ve uzun süre açık kalır. (Sekiro "Mikiri" gibi.)
///     Uzaklaşan dash ya da erken dash yine kurtarır ama ödül vermez.
///
///  2) ATLA-VUR (süpürmeye karşı):
///     Süpürmenin (mavi) üstünden zıpla → "VUR!" yazısı çıkar ve kısa bir
///     pencere açılır. Bu pencerede bu düşmana vurduğun İLK vuruş
///     ekstra denge hasarı verir. (Havada basılan saldırı inişte çıkar;
///     ground slam de sayılır.)
///
/// KURULUM YOK: EnemyAttackState gerektiğinde düşmana kendisi ekler.
/// Değerleri düşman başına ayarlamak istersen prefab'a elle ekle.
/// </summary>
public class UnblockableCounter : MonoBehaviour
{
    public enum CounterKind
    {
        Dash,
        Jump
    }

    // İstatistik / charm'lar için.
    public static event Action<EnemyController, CounterKind> CounterLanded;

    [Header("Kusursuz Kaçış (yakalamaya dash)")]
    public bool enableDashCounter = true;

    [Tooltip(
        "Dash, vuruştan en fazla bu kadar ÖNCE başladıysa kusursuz sayılır " +
        "(düşman zamanı, sn). Büyük = kolay.")]
    [Min(0.05f)]
    public float perfectDodgeWindow = 0.35f;

    [Tooltip("Dash düşmana DOĞRU atılmalı (içinden geçmek). Kapalıysa her yön sayılır.")]
    public bool requireDashTowardEnemy = true;

    [Tooltip("Düşmanın azami dengesinin yüzdesi kadar denge hasarı (0.5 = %50).")]
    [Range(0f, 1f)]
    public float dashCounterBalancePercent = 0.5f;

    [Tooltip("Kusursuz kaçıştan sonra düşmanın toparlanma süresi çarpanı.")]
    [Min(1f)]
    public float dashCounterRecoveryMultiplier = 1.5f;

    public string dashCounterText = "KUSURSUZ!";
    public Color dashCounterColor = new Color(1f, 0.85f, 0.1f);

    [Header("Atla-Vur (süpürmenin üstünden zıpla)")]
    public bool enableJumpCounter = true;

    [Tooltip("Süpürmeyi atladıktan sonra bonuslu vuruş için süre (sn).")]
    [Min(0.1f)]
    public float jumpCounterWindow = 1.0f;

    [Tooltip("Pencerede atılan ilk vuruşa eklenen denge hasarı (azami dengenin yüzdesi).")]
    [Range(0f, 1f)]
    public float jumpCounterBalancePercent = 0.35f;

    public string jumpPromptText = "VUR!";
    public string jumpCounterText = "KARŞI VURUŞ!";
    public Color jumpCounterColor = new Color(0.45f, 0.85f, 1f);

    [Header("His")]
    public float hitStopDuration = 0.12f;

    [Range(0f, 1f)]
    public float hitStopTimeScale = 0.05f;

    public float shakeForce = 0.6f;

    private EnemyController enemy;
    private EnemyBalance balance;
    private CinemachineImpulseSource impulse;

    private float jumpWindowUntil = -1f;

    public bool JumpWindowOpen => Time.time <= jumpWindowUntil;

    public float RecoveryMultiplier => dashCounterRecoveryMultiplier;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration
    )]
    private static void ResetStatics()
    {
        CounterLanded = null;
    }

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        balance = GetComponent<EnemyBalance>();
    }

    private void OnEnable()
    {
        CombatEvents.EnemyHit += OnEnemyHit;
    }

    private void OnDisable()
    {
        CombatEvents.EnemyHit -= OnEnemyHit;
    }

    // =========================================================
    // 1) KUSURSUZ KAÇIŞ
    // EnemyAttackState, yakalama dash ile atlatılınca çağırır.
    //   dashStartRemaining: dash başladığında vuruşa kalan süre (-1 = görülmedi)
    //   dashStartX: dash başladığında oyuncunun x'i
    //   cueLead: "şimdi kaç" işaretinin vuruştan ne kadar önce çıktığı.
    //            İşaret anında atılan dash HER ZAMAN kusursuz sayılır.
    // =========================================================

    public bool TryDashCounter(
        PlayerController player,
        float dashStartRemaining,
        float dashStartX,
        float cueLead,
        out bool brokeBalance
    )
    {
        brokeBalance = false;

        if (!enableDashCounter || enemy == null || player == null)
            return false;

        // Dash bu vuruşun uyarısı sırasında başlamalı ve son anda olmalı.
        float window = Mathf.Max(perfectDodgeWindow, cueLead);

        if (dashStartRemaining < 0f || dashStartRemaining > window)
            return false;

        if (requireDashTowardEnemy)
        {
            float toEnemy = enemy.transform.position.x - dashStartX;
            float moved = player.transform.position.x - dashStartX;

            // Düşmana doğru hareket etmemiş: sadece kaçış, ödül yok.
            if (toEnemy * moved <= 0f)
                return false;
        }

        if (balance != null && !balance.IsBroken)
            balance.AddBalanceDamage(Bonus(dashCounterBalancePercent));

        brokeBalance = balance != null && balance.IsBroken;

        enemy.PlayBalanceDamageFlash();

        // Düşmanlar yavaşlar, oyuncu değil: karşı saldırı zamanı.
        enemy.PlayParrySlowMotion(brokeBalance);

        Feel(player);

        CombatCallout.PopupAbove(enemy, dashCounterText, dashCounterColor, 1.25f);

        Debug.Log(
            "KUSURSUZ KAÇIŞ → denge +" +
            Bonus(dashCounterBalancePercent) +
            (brokeBalance ? " (denge kırıldı)" : "")
        );

        Raise(CounterKind.Dash);

        return true;
    }

    // =========================================================
    // 2) ATLA-VUR
    // EnemyAttackState, süpürmenin üstünden zıplanınca çağırır.
    // =========================================================

    public void OpenJumpWindow()
    {
        if (!enableJumpCounter || enemy == null)
            return;

        jumpWindowUntil = Time.time + jumpCounterWindow;

        CombatCallout.PopupAbove(enemy, jumpPromptText, jumpCounterColor, 0.9f);

        Debug.Log("SÜPÜRME ATLANDI → KARŞI VURUŞ PENCERESİ AÇIK");
    }

    private void OnEnemyHit(
        EnemyController hitEnemy,
        DamageInfo info,
        HitResult result
    )
    {
        if (hitEnemy != enemy || !result.hit || !JumpWindowOpen)
            return;

        // Pencere tek kullanımlık.
        jumpWindowUntil = -1f;

        // Vuruş dengeye gittiyse ve denge henüz kırılmadıysa bonus ekle.
        if (result.onBalance && balance != null && !balance.IsBroken)
            balance.AddBalanceDamage(Bonus(jumpCounterBalancePercent));

        enemy.PlayBalanceDamageFlash();

        PlayerController player =
            enemy.target != null
                ? enemy.target.GetComponent<PlayerController>()
                : null;

        Feel(player);

        CombatCallout.PopupAbove(enemy, jumpCounterText, jumpCounterColor, 1.15f);

        Debug.Log(
            "KARŞI VURUŞ (atla-vur) → denge +" +
            Bonus(jumpCounterBalancePercent)
        );

        Raise(CounterKind.Jump);
    }

    // =========================================================
    // YARDIMCILAR
    // =========================================================

    private int Bonus(float percent)
    {
        int max = balance != null ? balance.MaxBalance : 100;

        return Mathf.Max(1, Mathf.RoundToInt(max * percent));
    }

    private void Feel(PlayerController player)
    {
        HitStop.Request(hitStopDuration, hitStopTimeScale);

        if (shakeForce <= 0f)
            return;

        if (impulse == null && player != null)
            impulse = player.impulseSource;

        if (impulse != null)
            impulse.GenerateImpulse(shakeForce);
    }

    private void Raise(CounterKind kind)
    {
        if (CounterLanded == null)
            return;

        try
        {
            CounterLanded(enemy, kind);
        }
        catch (Exception e)
        {
            Debug.LogError("UnblockableCounter: abone hatası → " + e);
        }
    }
}
