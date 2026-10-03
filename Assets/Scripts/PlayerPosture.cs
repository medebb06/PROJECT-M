using UnityEngine;
using System;

public class PlayerPosture : MonoBehaviour
{
    [Header("Posture")]
    [SerializeField] private int maxPosture = 100;
    [SerializeField] private int currentPosture;

    [Header("Recovery")]
    [SerializeField] private float recoveryDelay = 1f;
    [SerializeField] private float recoverySpeed = 25f;

    public int CurrentPosture => currentPosture;
    public int MaxPosture => maxPosture;

    public bool IsBroken => currentPosture <= 0;

    public event Action<int, int> OnPostureChanged;
    public event Action OnPostureBroken;
    public event Action OnPostureRecovered;

    private float recoveryTimer;
    private float recoveryAccumulator;
    private bool wasBroken;

    void Awake()
    {
        currentPosture = maxPosture;
        recoveryTimer = 0f;
        recoveryAccumulator = 0f;
        wasBroken = false;

        OnPostureChanged?.Invoke(
            currentPosture,
            maxPosture
        );
    }

    void Update()
    {
        if (currentPosture >= maxPosture)
            return;

        if (recoveryTimer > 0f)
        {
            recoveryTimer -= Time.deltaTime;

            if (recoveryTimer < 0f)
                recoveryTimer = 0f;

            return;
        }

        recoveryAccumulator +=
            recoverySpeed * Time.deltaTime;

        int recoveryAmount =
            Mathf.FloorToInt(
                recoveryAccumulator
            );

        if (recoveryAmount <= 0)
            return;

        recoveryAccumulator -= recoveryAmount;

        int oldPosture = currentPosture;

        currentPosture =
            Mathf.Min(
                maxPosture,
                currentPosture + recoveryAmount
            );

        if (currentPosture != oldPosture)
        {
            OnPostureChanged?.Invoke(
                currentPosture,
                maxPosture
            );
        }

        if (wasBroken &&
            currentPosture > 0)
        {
            wasBroken = false;
        }

        if (currentPosture >= maxPosture)
        {
            currentPosture = maxPosture;

            recoveryAccumulator = 0f;

            OnPostureRecovered?.Invoke();

            OnPostureChanged?.Invoke(
                currentPosture,
                maxPosture
            );
        }
    }

    public void TakePostureDamage(int damage)
    {
        if (damage <= 0)
            return;

        if (IsBroken)
            return;

        currentPosture -= damage;

        currentPosture =
            Mathf.Max(
                currentPosture,
                0
            );

        recoveryTimer = recoveryDelay;
        recoveryAccumulator = 0f;

        OnPostureChanged?.Invoke(
            currentPosture,
            maxPosture
        );

        if (currentPosture <= 0)
        {
            BreakPosture();
        }
    }

    public void RecoverPosture(int amount)
    {
        if (amount <= 0)
            return;

        if (currentPosture >= maxPosture)
            return;

        currentPosture =
            Mathf.Min(
                currentPosture + amount,
                maxPosture
            );

        recoveryTimer = recoveryDelay;
        recoveryAccumulator = 0f;

        if (wasBroken &&
            currentPosture > 0)
        {
            wasBroken = false;
        }

        OnPostureChanged?.Invoke(
            currentPosture,
            maxPosture
        );

        if (currentPosture >= maxPosture)
        {
            OnPostureRecovered?.Invoke();
        }
    }

    public void ResetPosture()
    {
        currentPosture = maxPosture;

        recoveryTimer = 0f;
        recoveryAccumulator = 0f;
        wasBroken = false;

        OnPostureChanged?.Invoke(
            currentPosture,
            maxPosture
        );
    }

    private void BreakPosture()
    {
        if (wasBroken)
            return;

        wasBroken = true;

        recoveryTimer = recoveryDelay;
        recoveryAccumulator = 0f;

        OnPostureBroken?.Invoke();
    }
}