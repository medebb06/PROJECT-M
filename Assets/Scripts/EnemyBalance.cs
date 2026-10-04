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

    private void Awake()
    {
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
            recoverySpeed * EnemyTime.DeltaTime;

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

    public bool AddBalanceDamage(int amount)
    {
        if (isBroken)
            return false;

        if (amount <= 0)
            return false;

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
