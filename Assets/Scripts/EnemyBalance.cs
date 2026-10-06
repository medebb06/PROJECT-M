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

        return currentBalance != previousBalance;
    }

    private void BreakBalance()
    {
        isBroken = true;

        currentBalance = maxBalance;

        recoveryTimer = 0f;
        recoveryAccumulator = 0f;

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
