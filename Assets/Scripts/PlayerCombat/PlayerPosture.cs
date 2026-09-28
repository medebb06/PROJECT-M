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
    private bool wasBroken;

    void Awake()
    {
        currentPosture = maxPosture;
        recoveryTimer = 0f;
        wasBroken = false;

        OnPostureChanged?.Invoke(
            currentPosture,
            maxPosture
        );
    }

    void Update()
    {
        if (IsBroken)
            return;

        if (recoveryTimer > 0f)
        {
            recoveryTimer -= Time.deltaTime;

            if (recoveryTimer < 0f)
                recoveryTimer = 0f;

            return;
        }

        if (currentPosture < maxPosture)
        {
            float oldPosture = currentPosture;

            currentPosture = Mathf.Min(
                maxPosture,
                currentPosture +
                Mathf.RoundToInt(
                    recoverySpeed *
                    Time.deltaTime
                )
            );

            if (currentPosture != oldPosture)
            {
                OnPostureChanged?.Invoke(
                    currentPosture,
                    maxPosture
                );
            }

            if (currentPosture >= maxPosture)
            {
                OnPostureRecovered?.Invoke();
            }
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

        if (IsBroken)
            return;

        currentPosture =
            Mathf.Min(
                currentPosture + amount,
                maxPosture
            );

        recoveryTimer = recoveryDelay;

        OnPostureChanged?.Invoke(
            currentPosture,
            maxPosture
        );
    }

    public void ResetPosture()
    {
        currentPosture = maxPosture;

        recoveryTimer = 0f;
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

        OnPostureBroken?.Invoke();
    }
}