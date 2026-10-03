using UnityEngine;
using System;

public class EnemyPosture : MonoBehaviour
{
    [Header("Posture")]
    [SerializeField] private int maxPosture = 100;
    [SerializeField] private int currentPosture;

    [Header("Recovery")]
    [SerializeField] private bool canRecover = true;
    [SerializeField] private float recoveryDelay = 1.5f;
    [SerializeField] private float recoverySpeed = 20f;

    private float recoveryTimer;
    private bool isBroken;

    public int MaxPosture => maxPosture;
    public int CurrentPosture => currentPosture;
    public bool IsBroken => isBroken;

    public float PosturePercent =>
        maxPosture > 0
            ? (float)currentPosture / maxPosture
            : 0f;

    public event Action<int, int> OnPostureChanged;
    public event Action OnPostureBroken;
    public event Action OnPostureRecovered;

    private void Awake()
    {
        currentPosture = maxPosture;
        isBroken = false;
    }

    private void Update()
    {
        if (!canRecover)
            return;

        if (isBroken)
            return;

        if (currentPosture >= maxPosture)
            return;

        recoveryTimer -= Time.deltaTime;

        if (recoveryTimer > 0f)
            return;

        currentPosture += Mathf.RoundToInt(
            recoverySpeed * Time.deltaTime
        );

        currentPosture = Mathf.Clamp(
            currentPosture,
            0,
            maxPosture
        );

        OnPostureChanged?.Invoke(
            currentPosture,
            maxPosture
        );
    }

    public bool TakeDamage(int damage)
    {
        if (isBroken)
            return false;

        if (damage <= 0)
            return false;

        int previousPosture = currentPosture;

        currentPosture -= damage;

        currentPosture = Mathf.Clamp(
            currentPosture,
            0,
            maxPosture
        );

        recoveryTimer = recoveryDelay;

        OnPostureChanged?.Invoke(
            currentPosture,
            maxPosture
        );

        if (previousPosture > 0 && currentPosture <= 0)
        {
            BreakPosture();
        }

        return currentPosture != previousPosture;
    }

    private void BreakPosture()
    {
        isBroken = true;

        currentPosture = 0;

        OnPostureBroken?.Invoke();
        OnPostureChanged?.Invoke(
            currentPosture,
            maxPosture
        );
    }

    public void RecoverPosture()
    {
        if (!isBroken)
            return;

        currentPosture = maxPosture;
        isBroken = false;

        recoveryTimer = 0f;

        OnPostureChanged?.Invoke(
            currentPosture,
            maxPosture
        );

        OnPostureRecovered?.Invoke();
    }

    public void ResetPosture()
    {
        currentPosture = maxPosture;
        isBroken = false;
        recoveryTimer = 0f;

        OnPostureChanged?.Invoke(
            currentPosture,
            maxPosture
        );
    }
}