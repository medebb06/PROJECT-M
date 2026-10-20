using UnityEngine;

// EnemyUnit durumlarını standart Animator durum adlarına çevirir.
// Standart adlar: Idle, Walk, Windup, Attack, Recover, Hurt, Stagger, Death.
// Eksik durum varsa sessizce atlanır (Stagger yoksa Hurt, Hurt yoksa Idle).
// Windup/Attack/Recover klibi, AttackDef süresine sığacak şekilde hızlandırılır/yavaşlatılır.
[RequireComponent(typeof(EnemyUnit))]
public class EnemyAnimBridge : MonoBehaviour
{
    public Animator animator;
    [Tooltip("Walk klibinin doğal hızına denk gelen yürüme hızı (birim/sn).")]
    public float walkReferenceSpeed = 4.5f;
    public float crossFade = 0.05f;

    private EnemyUnit unit;
    private EnemyUnit.UnitState last = (EnemyUnit.UnitState)(-1);
    private bool walking;

    private void Awake()
    {
        unit = GetComponent<EnemyUnit>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        unit.StateChanged += OnState;
    }

    private void OnDisable()
    {
        unit.StateChanged -= OnState;
    }

    private bool Has(string n)
    {
        return animator != null && !string.IsNullOrEmpty(n) && animator.HasState(0, Animator.StringToHash(n));
    }

    private void Play(string n)
    {
        if (!Has(n)) return;
        animator.CrossFadeInFixedTime(n, crossFade, 0);
    }

    private void PlayFirst(params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
            if (Has(names[i])) { Play(names[i]); return; }
    }

    // Klip uzunluğu: durum adıyla aynı isimli klip.
    private float ClipLength(string n)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return 0f;
        foreach (AnimationClip c in animator.runtimeAnimatorController.animationClips)
            if (c != null && c.name == n) return c.length;
        return 0f;
    }

    private void OnState(EnemyUnit.UnitState s, AttackDef a)
    {
        if (animator == null) return;
        last = s;
        walking = false;
        switch (s)
        {
            case EnemyUnit.UnitState.Idle: PlayFirst("Idle"); break;
            case EnemyUnit.UnitState.Chase: break; // Walk/Idle LateUpdate'te hıza göre
            case EnemyUnit.UnitState.Windup: PlayFirst(a != null ? a.animWindup : "", "Windup"); break;
            case EnemyUnit.UnitState.Active: PlayFirst(a != null ? a.animAttack : "", "Attack"); break;
            case EnemyUnit.UnitState.Recover: PlayFirst(a != null ? a.animRecover : "", "Recover"); break;
            case EnemyUnit.UnitState.Hurt: PlayFirst("Hurt", "Idle"); break;
            case EnemyUnit.UnitState.Stagger: PlayFirst("Stagger", "Hurt", "Idle"); break;
            case EnemyUnit.UnitState.Dead: PlayFirst("Death", "Hurt"); break;
        }
    }

    private void LateUpdate()
    {
        if (animator == null || unit == null) return;

        float scale = EnemyTime.Scale;
        AttackDef a = unit.CurrentAttack;

        switch (unit.State)
        {
            case EnemyUnit.UnitState.Windup: Fit(a != null ? a.animWindup : "", "Windup", a != null ? a.windup : 0f, scale); break;
            case EnemyUnit.UnitState.Active: Fit(a != null ? a.animAttack : "", "Attack", a != null ? a.active : 0f, scale); break;
            case EnemyUnit.UnitState.Recover: Fit(a != null ? a.animRecover : "", "Recover", a != null ? a.recovery : 0f, scale); break;
            case EnemyUnit.UnitState.Idle:
            case EnemyUnit.UnitState.Chase:
                bool moving = unit.HorizontalSpeed > 0.3f;
                if (moving != walking || last != unit.State)
                {
                    walking = moving;
                    last = unit.State;
                    if (moving) PlayFirst("Walk", "Idle"); else PlayFirst("Idle");
                }
                animator.speed = moving
                    ? Mathf.Clamp(unit.HorizontalSpeed / Mathf.Max(0.1f, walkReferenceSpeed), 0.4f, 2f) * scale
                    : scale;
                break;
            default:
                animator.speed = scale;
                break;
        }
    }

    // Klibi AttackDef süresine uydur: speed = klipUzunluğu / süre.
    private void Fit(string custom, string standard, float duration, float scale)
    {
        string n = Has(custom) ? custom : standard;
        float len = ClipLength(n);
        if (len > 0.01f && duration > 0.01f) animator.speed = (len / duration) * scale;
        else animator.speed = scale;
    }
}
