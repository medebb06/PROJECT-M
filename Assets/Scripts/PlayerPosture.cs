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

    [Header("Kirilma (blok kilidi)")]
    [Tooltip("Posture kirilinca bar tamamen dolana kadar blok/parry yapilamaz. Bu sure sonra dolmaya baslar (sn).")]
    [SerializeField] private float brokenRecoveryDelay = 0.8f;
    [Tooltip("Kilitliyken dolum hizi (posture/sn). 200 max icin 65 = ~3 sn.")]
    [SerializeField] private float brokenRecoverySpeed = 65f;

    [Header("Can etkisi (Sekiro gibi)")]
    [Tooltip("Normal toparlanma hizi carpani (1 = eski hiz).")]
    [SerializeField] private float recoverySpeedScale = 0.7f;
    [Tooltip("Hasardan sonra toparlanma beklemesine eklenen sure (sn).")]
    [SerializeField] private float extraRecoveryDelay = 0.7f;
    [Tooltip("Can bitmek uzereyken posture toparlanma carpani (tam canda 1).")]
    [Range(0.05f, 1f)] [SerializeField] private float lowHealthRecoveryMult = 0.35f;
    [Tooltip("Can bitmek uzereyken yenen posture hasari carpani (tam canda 1; >1 = daha cabuk kirilir).")]
    [Range(1f, 3f)] [SerializeField] private float lowHealthDamageMult = 1.5f;

    private Health healthRef;

    // 1 = tam can, 0 = canin bittigi an.
    private float HealthFraction()
    {
        if (healthRef == null) healthRef = GetComponent<Health>();
        if (healthRef == null || healthRef.MaxHealth <= 0) return 1f;
        return Mathf.Clamp01(healthRef.CurrentHealth / (float)healthRef.MaxHealth);
    }

    public int CurrentPosture => currentPosture;
    public int MaxPosture => maxPosture;

    public bool IsBroken => currentPosture <= 0;

    /// <summary>Kirildi ve henuz tamamen dolmadi: blok/parry basilamaz.</summary>
    public bool BlockLocked { get; private set; }

    public event Action<int, int> OnPostureChanged;
    public event Action OnPostureBroken;
    public event Action OnPostureRecovered;

    private float recoveryTimer;
    private float recoveryAccumulator;
    private bool wasBroken;

    void Awake()
    {
        maxPosture = 200;
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
            (BlockLocked ? brokenRecoverySpeed : recoverySpeed * recoverySpeedScale)
            * Mathf.Lerp(lowHealthRecoveryMult, 1f, HealthFraction()) * Time.deltaTime;

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
            BlockLocked = false;

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

        damage = Mathf.CeilToInt(damage * Mathf.Lerp(lowHealthDamageMult, 1f, HealthFraction()));

        currentPosture -= damage;

        currentPosture =
            Mathf.Max(
                currentPosture,
                0
            );

        recoveryTimer = BlockLocked ? brokenRecoveryDelay : recoveryDelay + extraRecoveryDelay;
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

        recoveryTimer = BlockLocked ? Mathf.Min(recoveryTimer, 0.2f) : recoveryDelay;
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
            BlockLocked = false;
            OnPostureRecovered?.Invoke();
        }
    }

    public void ResetPosture()
    {
        currentPosture = maxPosture;

        recoveryTimer = 0f;
        recoveryAccumulator = 0f;
        wasBroken = false;
        BlockLocked = false;

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
        BlockLocked = true;

        recoveryTimer = brokenRecoveryDelay;
        recoveryAccumulator = 0f;

        OnPostureBroken?.Invoke();
    }
}