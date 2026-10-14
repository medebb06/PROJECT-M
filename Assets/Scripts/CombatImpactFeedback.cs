using UnityEngine;
using System.Collections;
using Unity.Cinemachine;

public class CombatImpactFeedback : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CinemachineImpulseSource impulseSource;
    [SerializeField] private Transform punchTarget;

    [Header("Normal Attack Impact")]
    [SerializeField] private float attackHitStopTimeScale = 0.05f;
    [SerializeField] private float attackHitStopDuration = 0.04f;

    [Header("Attack Punch")]
    [SerializeField] private float attackPunchAmount = 0.08f;
    [SerializeField] private float attackPunchDuration = 0.06f;

    [Header("Parry Impact")]
    [SerializeField] private float parryHitStopTimeScale = 0.03f;
    [SerializeField] private float parryHitStopDuration = 0.08f;
    [SerializeField] private float parryShakeForce = 0.35f;

    [Header("Parry Punch")]
    [SerializeField] private float parryPunchAmount = 0.12f;
    [SerializeField] private float parryPunchDuration = 0.08f;

    [Header("Block Impact")]
    [SerializeField] private float blockHitStopTimeScale = 0.035f;
    [SerializeField] private float blockHitStopDuration = 0.035f;
    [SerializeField] private float blockShakeForce = 0.25f;

    [Header("Block Punch")]
    [SerializeField] private float blockPunchAmount = 0.06f;
    [SerializeField] private float blockPunchDuration = 0.05f;

    // Yeni isimli alanlar: eski inspector değerleri bunları ezmesin diye.
    [Header("Parry Hit-Stop (heavy)")]
    [Tooltip("Tam donma süresi (gerçek saniye).")]
    [SerializeField] private float parryFreezeTime = 0.13f;
    [Tooltip("Donmadan sonra ağır çekimden normale dönüş süresi.")]
    [SerializeField] private float parrySlowTime = 0.22f;
    [SerializeField] private float parrySlowScale = 0.15f;

    [Header("Block Hit-Stop (heavy)")]
    [SerializeField] private float blockFreezeTime = 0.08f;
    [SerializeField] private float blockSlowTime = 0.12f;
    [SerializeField] private float blockSlowScale = 0.3f;

    // ---- Vuruş hissi v2 (kombo adımına göre artan, orantılı) ----
    // Dizi sırası: Attack1, Attack2, Attack3, Attack4.
    [Header("Vuruş Hissi v2 (adım başına: A1, A2, A3, A4)")]
    [Tooltip("Vuruşta tam donma süresi (gerçek sn).")]
    [SerializeField] private float[] hitFreezeSharp = { 0.03f, 0.035f, 0.05f, 0.06f };
    [Tooltip("Donmadan sonra kısa ağır çekim (gerçek sn). 0 = yok.")]
    [SerializeField] private float[] hitSlowSharp = { 0f, 0f, 0.025f, 0.03f };
    [Range(0f, 1f)] [SerializeField] private float hitSlowScaleSharp = 0.5f;
    [Tooltip("Kamera sarsıntısı (vuruş yönünde itme).")]
    [SerializeField] private float[] hitShake = { 0.18f, 0.22f, 0.35f, 0.42f };
    [Tooltip("Karakter squash/stretch vuruş darbesi (kare kare çizimde, RonniPlayerBridge uygular).")]
    [SerializeField] private float[] hitKick = { 0.06f, 0.07f, 0.10f, 0.10f };
    [Tooltip("Aynı vuruşta her ek düşman için donma/sarsıntı çarpanı artışı (en çok +%40).")]
    [SerializeField] private float multiHitBonus = 0.15f;
    [Tooltip("Sarsıntının aşağı bileşeni (0 = tam yatay itme).")]
    [Range(0f, 1f)] [SerializeField] private float shakeDown = 0.35f;

    [Header("Parry / Denge Kırma v2")]
    [SerializeField] private float parryShake2 = 0.6f;
    [SerializeField] private float breakFreeze2 = 0.14f;
    [SerializeField] private float breakSlowTime2 = 0.2f;
    [Range(0f, 1f)] [SerializeField] private float breakSlowScale2 = 0.2f;
    [SerializeField] private float breakShake2 = 0.75f;
    [SerializeField] private float breakKick2 = 0.12f;

    /// <summary>Vuruş anı: (kombo adımı, squash darbesi). RonniPlayerBridge dinler.</summary>
    public static System.Action<int, float> AttackImpact;

    [Header("Balance Break Impact")]
    [SerializeField] private float balanceBreakHitStopTimeScale = 0.01f;
    [SerializeField] private float balanceBreakHitStopDuration = 0.12f;
    [SerializeField] private float balanceBreakShakeForce = 0.8f;

    [Header("Balance Break Punch")]
    [SerializeField] private float balanceBreakPunchAmount = 0.18f;
    [SerializeField] private float balanceBreakPunchDuration = 0.1f;

    // HitStop öncelikleri:
    // Balance break aktifken daha düşük öncelikli (normal vuruş,
    // block, parry) hit-stop'lar yok sayılır.
    private const int NormalPriority = 0;
    private const int BalanceBreakPriority = 10;
    private const int ParryPriority = 5;
    private const int BlockPriority = 3;

    private Coroutine punchRoutine;

    private Vector3 originalPunchScale;

    private void Awake()
    {
        if (impulseSource == null)
            impulseSource =
                GetComponent<CinemachineImpulseSource>();

        if (punchTarget != null)
            originalPunchScale =
                punchTarget.localScale;
    }

    // =========================================================
    // NORMAL ATTACK
    // =========================================================

    public void PlayAttackImpact()
    {
        PlayAttackImpact(1, Vector2.right, 1);
    }

    static float Pick(float[] a, int step, float fallback)
    {
        if (a == null || a.Length == 0) return fallback;
        return a[Mathf.Clamp(step - 1, 0, a.Length - 1)];
    }

    /// <summary>Kombo adımına göre ölçeklenen vuruş hissi: donma + kısa ağır çekim + yönlü sarsıntı + squash darbesi.</summary>
    public void PlayAttackImpact(int step, Vector2 dir, int hitCount)
    {
        float m = 1f + Mathf.Min(0.4f, multiHitBonus * Mathf.Max(0, hitCount - 1));

        float freeze = Pick(hitFreezeSharp, step, attackHitStopDuration) * m;
        float slow = Pick(hitSlowSharp, step, 0f);

        HitStop.Request(freeze, 0f, NormalPriority);

        if (slow > 0f)
            HitStop.RequestRamp(freeze + slow, hitSlowScaleSharp, 1f, 2f, NormalPriority);

        PlayDirectionalShake(Pick(hitShake, step, 0f) * m, dir);

        AttackImpact?.Invoke(step, Pick(hitKick, step, 0f));
    }

    private void PlayDirectionalShake(float force, Vector2 dir)
    {
        if (impulseSource == null || force <= 0f)
            return;

        float sx = Mathf.Abs(dir.x) > 0.01f ? Mathf.Sign(dir.x) : 1f;
        Vector3 v = new Vector3(sx, -shakeDown, 0f).normalized * force;

        impulseSource.GenerateImpulseWithVelocity(v);
    }

    // =========================================================
    // PARRY
    // =========================================================

    public void PlayParryImpact()
    {
        // Tam donma + ardından normale yavaşça dönen ağır çekim.
        HitStop.Request(parryFreezeTime, 0f, ParryPriority);
        HitStop.RequestRamp(parryFreezeTime + parrySlowTime, parrySlowScale, 1f, 2f, ParryPriority);

        PlayScreenShake(Mathf.Max(parryShakeForce, parryShake2));

        PlayPunch(
            parryPunchAmount,
            parryPunchDuration
        );
    }

    // =========================================================
    // BLOCK
    // =========================================================

    public void PlayBlockImpact()
    {
        HitStop.Request(blockFreezeTime, 0f, BlockPriority);
        HitStop.RequestRamp(blockFreezeTime + blockSlowTime, blockSlowScale, 1f, 2f, BlockPriority);

        PlayScreenShake(blockShakeForce);

        PlayPunch(
            blockPunchAmount,
            blockPunchDuration
        );
    }

    // =========================================================
    // BALANCE BREAK
    // =========================================================

    public void PlayBalanceBreakImpact()
    {
        // En ağır his: tam donma + ağır çekimden dönüş + güçlü sarsıntı.
        HitStop.Request(breakFreeze2, 0f, BalanceBreakPriority);
        HitStop.RequestRamp(breakFreeze2 + breakSlowTime2, breakSlowScale2, 1f, 2f, BalanceBreakPriority);

        PlayScreenShake(breakShake2);

        AttackImpact?.Invoke(0, breakKick2);
    }

    // =========================================================
    // SCREEN SHAKE
    // =========================================================

    private void PlayScreenShake(
        float force
    )
    {
        if (impulseSource == null)
            return;

        impulseSource.GenerateImpulse(force);
    }

    // =========================================================
    // PUNCH
    // =========================================================

    private void PlayPunch(
        float amount,
        float duration
    )
    {
        if (punchTarget == null)
            return;

        if (punchRoutine != null)
        {
            StopCoroutine(punchRoutine);

            punchTarget.localScale =
                originalPunchScale;
        }

        punchRoutine =
            StartCoroutine(
                PunchCoroutine(
                    amount,
                    duration
                )
            );
    }

    private IEnumerator PunchCoroutine(
        float amount,
        float duration
    )
    {
        Vector3 punchScale =
            originalPunchScale *
            (1f + amount);

        float halfDuration =
            duration * 0.5f;

        float timer = 0f;

        // SCALE UP
        while (timer < halfDuration)
        {
            timer +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    timer / halfDuration
                );

            t =
                Mathf.Sin(
                    t * Mathf.PI * 0.5f
                );

            punchTarget.localScale =
                Vector3.Lerp(
                    originalPunchScale,
                    punchScale,
                    t
                );

            yield return null;
        }

        // SCALE DOWN
        timer = 0f;

        while (timer < halfDuration)
        {
            timer +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    timer / halfDuration
                );

            t =
                1f -
                Mathf.Cos(
                    t * Mathf.PI * 0.5f
                );

            punchTarget.localScale =
                Vector3.Lerp(
                    punchScale,
                    originalPunchScale,
                    t
                );

            yield return null;
        }

        punchTarget.localScale =
            originalPunchScale;

        punchRoutine = null;
    }
}