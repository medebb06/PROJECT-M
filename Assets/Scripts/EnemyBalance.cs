using UnityEngine;
using System;

public class EnemyBalance : MonoBehaviour
{
    [Header("Balance")]
    [SerializeField] private int maxBalance = 100;
    [SerializeField] private int currentBalance = 0;

    [Header("Recovery")]
    [SerializeField] private bool canRecover = true;
    [SerializeField] private float recoveryDelay = 1.5f;
    [SerializeField] private float recoverySpeed = 20f;

    [Header("Can oranına göre (canı çoksa denge zor kırılır, azsa kolay)")]
    [SerializeField] private bool scaleByHealth = true;

    [Tooltip("Tam canda gelen denge hasarı bu kata çarpılır (<1 = zor kırılır).")]
    [SerializeField] private float fullHealthDamageMultiplier = 0.6f;

    [Tooltip("Canı bitmek üzereyken gelen denge hasarı bu kata çarpılır (>1 = kolay kırılır).")]
    [SerializeField] private float lowHealthDamageMultiplier = 1.6f;

    [Tooltip("Tam canda denge yenilenme hızı çarpanı (hızlı toparlanır).")]
    [SerializeField] private float fullHealthRecoveryMultiplier = 1.6f;

    [Tooltip("Canı bitmek üzereyken yenilenme çarpanı (yavaş toparlanır).")]
    [SerializeField] private float lowHealthRecoveryMultiplier = 0.4f;

    [Header("Posture kırılınca can hasarı")]
    [Tooltip("Posture her kırıldığında (vuruş, parry, ability - fark etmez) max canın bu oranı kadar can gider. " +
             "Düşman en az 1 canla kalır; öldürmez.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float breakHealthPercent = 0.15f;

    private Health healthRef;

    /// <summary>Düz vuruşların posture'a bağlı can hasarından artan kesir (PlayerDamage kullanır).</summary>
    [System.NonSerialized] public float chipCarry;

    /// <summary>Düz vuruş denge çarpanının kesir artığı (PlayerDamage kullanır).</summary>
    [System.NonSerialized] public float hitCarry;

    // Kesirli denge hasarı birikir (aksi halde 1 × 0.6 gibi küçük hasarlar yuvarlanıp etkisiz kalır).
    private float damageRemainder;

    private float recoveryTimer;
    private float recoveryAccumulator;

    private bool isBroken;

    public int MaxBalance => maxBalance;
    public int CurrentBalance => currentBalance;
    public bool IsBroken => isBroken;

    public float BalancePercent =>
        maxBalance > 0
            ? (float)currentBalance / maxBalance
            : 0f;

    public event Action<int, int> OnBalanceChanged;
    public event Action OnBalanceBroken;
    public event Action OnBalanceRecovered;

    // 0..1 can oranı (Health yoksa tam can sayılır).
    private float HealthFraction()
    {
        if (healthRef == null || healthRef.MaxHealth <= 0)
            return 1f;

        return Mathf.Clamp01((float)healthRef.CurrentHealth / healthRef.MaxHealth);
    }

    private float DamageMultiplier =>
        scaleByHealth
            ? Mathf.Lerp(lowHealthDamageMultiplier, fullHealthDamageMultiplier, HealthFraction())
            : 1f;

    private float RecoveryMultiplier =>
        scaleByHealth
            ? Mathf.Lerp(lowHealthRecoveryMultiplier, fullHealthRecoveryMultiplier, HealthFraction())
            : 1f;

    private void Awake()
    {
        healthRef = GetComponent<Health>();

        currentBalance = 0;
        isBroken = false;

        recoveryTimer = 0f;
        recoveryAccumulator = 0f;
    }

    private void Update()
    {
        if (!canRecover)
            return;

        if (isBroken)
            return;

        if (currentBalance <= 0)
            return;

        // Oyuncu baskıyı bıraktıktan sonra
        // recovery delay bekle.
        recoveryTimer -= EnemyTime.DeltaTime;

        if (recoveryTimer > 0f)
            return;

        // Frame-rate bağımsız recovery.
        recoveryAccumulator +=
            recoverySpeed * RecoveryMultiplier * EnemyTime.DeltaTime;

        int recoveryAmount =
            Mathf.FloorToInt(recoveryAccumulator);

        if (recoveryAmount <= 0)
            return;

        recoveryAccumulator -= recoveryAmount;

        int previousBalance =
            currentBalance;

        currentBalance -= recoveryAmount;

        currentBalance =
            Mathf.Clamp(
                currentBalance,
                0,
                maxBalance
            );

        if (currentBalance != previousBalance)
        {
            OnBalanceChanged?.Invoke(
                currentBalance,
                maxBalance
            );
        }
    }

    // Çalışma anında azami dengeyi ayarlar (ör. düşman tipi: Ağır dengesi
    // geç kırılır, Çevik erken). Mevcut denge yeni sınıra kırpılır.
    // Calisma anında denge toparlanmasını ayarlar (boss: cok yavas, Sekiro gibi).
    public void SetRecovery(float delay, float speed)
    {
        recoveryDelay = Mathf.Max(0f, delay);
        recoverySpeed = Mathf.Max(0f, speed);
    }

    /// <summary>Denge kirilinca giden can orani (max can). 0 = kirilma hasar vermez, sadece acik birakir (boss).</summary>
    /// <summary>>=0 ise PlayerDamage.PostureToHealthRatio yerine bu oran kullanilir (duz vurusun can hasari; boss icin).</summary>
    [System.NonSerialized] public float chipRatioOverride = -1f;

    /// <summary>>0 ise: can bu oranin USTUNDEYKEN posture kirilamaz (cubuk 'breakLockCapFraction'ta durur).</summary>
    [System.NonSerialized] public float breakLockBelowHealth = 0f;
    [System.NonSerialized] public float breakLockCapFraction = 0.75f;

    /// <summary>Son AddBalanceDamage'de kilit yuzunden cubuga SIGMAYAN miktar (chip can hasari icin sayilir).</summary>
    [System.NonSerialized] public int lastOverflow;

    public bool IsBreakLocked => breakLockBelowHealth > 0f && HealthFraction() > breakLockBelowHealth;

    public void SetBreakHealthPercent(float percent)
    {
        breakHealthPercent = Mathf.Clamp(percent, 0f, 0.5f);
    }

    public void SetMaxBalance(int value)
    {
        maxBalance = Mathf.Max(1, value);

        if (!isBroken)
            currentBalance = Mathf.Clamp(currentBalance, 0, maxBalance - 1);
        else
            currentBalance = maxBalance;

        OnBalanceChanged?.Invoke(
            currentBalance,
            maxBalance
        );
    }

    /// <summary>Parry'nin denge hasarı çarpanı (parry çok güçlü olmasın). Kesir birikir.</summary>
    public static float ParryScale = 0.5f;

    [System.NonSerialized] public float parryCarry;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetParryScale()
    {
        ParryScale = 0.5f;
    }

    /// <summary>Parry / yansıyan ok denge hasarı: ParryScale ile ölçeklenir.</summary>
    public bool AddParryBalanceDamage(int amount)
    {
        float scaled = Mathf.Max(0, amount) * ParryScale + parryCarry;

        int whole = Mathf.FloorToInt(scaled);

        parryCarry = scaled - whole;

        if (whole <= 0)
        {
            // Bu parry sadece kesir biriktirdi: baskı sürüyor (toparlanma beklesin).
            if (!isBroken)
            {
                recoveryTimer = recoveryDelay;
                recoveryAccumulator = 0f;
            }

            return true;
        }

        return AddBalanceDamage(whole);
    }

    public bool AddBalanceDamage(int amount, bool useHealthScaling = true)
    {
        lastOverflow = 0;

        if (isBroken)
            return false;

        if (amount <= 0)
            return false;

        if (useHealthScaling)
        {
            float scaled = amount * DamageMultiplier + damageRemainder;

            amount = Mathf.FloorToInt(scaled);
            damageRemainder = scaled - amount;

            // Bu vuruş sadece kesir biriktirdi: baskı sürüyor (yenilenme beklesin).
            if (amount <= 0)
            {
                recoveryTimer = recoveryDelay;
                recoveryAccumulator = 0f;

                return true;
            }
        }

        int previousBalance =
            currentBalance;

        currentBalance += amount;

        if (IsBreakLocked)
        {
            int lockCap = Mathf.Max(
                Mathf.RoundToInt(maxBalance * Mathf.Clamp01(breakLockCapFraction)),
                previousBalance);

            if (currentBalance > lockCap)
            {
                lastOverflow = currentBalance - lockCap;
                currentBalance = lockCap;
            }
        }

        currentBalance =
            Mathf.Clamp(
                currentBalance,
                0,
                maxBalance
            );

        // Oyuncu tekrar baskı uyguladı.
        // Recovery baştan beklemeli.
        recoveryTimer =
            recoveryDelay;

        recoveryAccumulator = 0f;

        OnBalanceChanged?.Invoke(
            currentBalance,
            maxBalance
        );

        if (previousBalance < maxBalance &&
            currentBalance >= maxBalance)
        {
            BreakBalance();
        }

        // Kilit yuzunden cubuga sigmayan vurus da GECERLI sayilir (yoksa PlayerDamage vurusu yok sayar, boss dokunulmaz olur).
        return currentBalance != previousBalance || lastOverflow > 0;
    }

    private void BreakBalance()
    {
        isBroken = true;

        currentBalance = maxBalance;

        recoveryTimer = 0f;
        recoveryAccumulator = 0f;

        // Her kırılışta sabit bir can dilimi gider (parry tek seçenek olmasın).
        if (healthRef != null && !healthRef.IsDead && breakHealthPercent > 0f)
        {
            int chip = Mathf.RoundToInt(healthRef.MaxHealth * breakHealthPercent);
            chip = Mathf.Min(chip, healthRef.CurrentHealth - 1);

            if (chip > 0)
            {
                healthRef.TakeDamage(chip);

                // GÖRÜNÜR olsun: kan efekti + büyük hasar sayısı.
                Vector3 top = transform.position;
                Collider2D col = GetComponent<Collider2D>();

                if (col != null)
                    top = new Vector3(col.bounds.center.x, col.bounds.center.y, transform.position.z);

                if (CombatVFXManager.Instance != null)
                    CombatVFXManager.Instance.PlayHealthHit(top, Vector2.up);

                CombatCallout.Popup(
                    top + new Vector3(0f, 0.8f, 0f),
                    "-" + chip,
                    new Color(1f, 0.35f, 0.25f),
                    1.5f
                );
            }
        }

        OnBalanceBroken?.Invoke();

        OnBalanceChanged?.Invoke(
            currentBalance,
            maxBalance
        );
    }

    public void RecoverBalance()
    {
        if (!isBroken)
            return;

        currentBalance = 0;
        isBroken = false;

        recoveryTimer = 0f;
        recoveryAccumulator = 0f;

        OnBalanceChanged?.Invoke(
            currentBalance,
            maxBalance
        );

        OnBalanceRecovered?.Invoke();
    }

    public void ResetBalance()
    {
        currentBalance = 0;
        isBroken = false;

        recoveryTimer = 0f;
        recoveryAccumulator = 0f;

        OnBalanceChanged?.Invoke(
            currentBalance,
            maxBalance
        );
    }
}
