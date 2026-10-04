using UnityEngine;

/// <summary>
/// DÜŞMAN ANİMASYON SÜRÜCÜSÜ. Animator'da geçiş (ok) ÇİZMEDEN Idle / Walk /
/// Attack / Hurt (ve isteğe bağlı Sweep / Grab) animasyonlarını düşmanın o
/// anki durumuna göre oynatır.
///
/// Öncelik (her karede):
///   Ölü                      → Hurt (son karede kalır)
///   Stagger / Execute        → Hurt (son karede kalır = sersem duruş)
///   Hit (vurulup savruldu)   → Hurt (her YENİ vuruşta baştan)
///   Attack                   → vuruş türüne göre Attack / Sweep / Grab
///                              (komboda HER vuruşta baştan)
///   Hareket ediyor           → Walk
///   Duruyor                  → Idle
///
/// VURUŞ HİZALAMA: Uyarı sırasında saldırı animasyonu 'Hold Pose Time'
/// anındaki karede (hazırlık/kalkmış silah pozu) DONAR; vuruşa tam
/// 'hit time − hold pose time' kala akmaya başlar. Vuruş karesi hasarla
/// aynı anda gelir: gecikmeli vuruş = silah havada bekler.
/// Uyarı, hit time'dan kısaysa (hızlı kombo) animasyon ortasından başlar.
///
/// KURULUM:
///  1) Düşman prefab'ına ekle.
///  2) Animator'da state'ler: Idle, Walk, Attack, Hurt (+ isteğe bağlı
///     Sweep, Grab, Shoot; yoksa Attack kullanılır). Default = Idle. OK ÇİZME.
///  3) Parameters'a 'Attack' adında Trigger ekle (sadece uyarı susturur).
///  4) EnemyController > Attack Animation Hit Time = klibin başından vuruş
///     karesine kadar süre (12 fps'de 5. kare → 5/12 = 0.42).
/// </summary>
[RequireComponent(typeof(EnemyController))]
public class EnemyAnimationDriver : MonoBehaviour
{
    [Header("Animator state adları (Animator'dakiyle birebir aynı)")]
    [SerializeField] private string idleState = "Idle";
    [SerializeField] private string walkState = "Walk";
    [SerializeField] private string attackState = "Attack";
    [SerializeField] private string hurtState = "Hurt";

    [Tooltip("Süpürme animasyonu. Animator'da yoksa Attack oynar.")]
    [SerializeField] private string sweepState = "Sweep";

    [Tooltip("Yakalama/engellenemez animasyonu. Animator'da yoksa Attack oynar.")]
    [SerializeField] private string grabState = "Grab";

    [Tooltip("Ok atma animasyonu (Okçu). Animator'da yoksa Attack oynar.")]
    [SerializeField] private string shootState = "Shoot";

    [Header("Vuruş zamanlaması")]
    [Tooltip(
        "Uyarı sırasında saldırı klibinin donduğu an (sn). 0 = ilk kare. " +
        "Silahın havada olduğu kareyi seç (ör. 12 fps'de 3. kare → 0.25).")]
    [Min(0f)]
    [SerializeField] private float holdPoseTime = 0f;

    [Tooltip("Sweep klibinin vuruş karesi (sn). 0 = Attack Animation Hit Time.")]
    [Min(0f)]
    [SerializeField] private float sweepHitTime = 0f;

    [Tooltip("Grab klibinin vuruş karesi (sn). 0 = Attack Animation Hit Time.")]
    [Min(0f)]
    [SerializeField] private float grabHitTime = 0f;

    [Tooltip("Shoot klibinde okun bırakıldığı kare (sn). 0 = Attack Animation Hit Time.")]
    [Min(0f)]
    [SerializeField] private float shootHitTime = 0f;

    [Header("Geçişler")]
    [Tooltip("Idle ↔ Walk yumuşak geçiş süresi (sn). Pixel art için 0.")]
    [Min(0f)]
    [SerializeField] private float locomotionBlend = 0f;

    [Tooltip("Bu yatay hızın üstünde Walk oynar.")]
    [Min(0f)]
    [SerializeField] private float walkSpeedThreshold = 0.15f;

    [Header("Davranış")]
    [Tooltip(
        "Vuruş karesini hasar anına hizala (önerilen). Kapalı: animasyon " +
        "EnemyController'ın çağırdığı anda baştan oynar.")]
    [SerializeField] private bool alignHitFrameToDamage = true;

    [Tooltip("Sersemleme ve execute sırasında Hurt oynasın (son karede kalır).")]
    [SerializeField] private bool hurtDuringStagger = true;

    [Tooltip("Ölünce Hurt oynasın (fade-out bitene kadar).")]
    [SerializeField] private bool hurtOnDeath = true;

    [Tooltip("Her yeni darbe Hurt'ü baştan başlatsın.")]
    [SerializeField] private bool restartHurtOnNewHit = true;

    private EnemyController enemy;
    private Animator animator;
    private Rigidbody2D rb;

    private int idleHash;
    private int walkHash;
    private int attackHash;
    private int hurtHash;
    private int sweepHash;
    private int grabHash;
    private int shootHash;

    private bool hasSweep;
    private bool hasGrab;
    private bool hasShoot;

    private int current;
    private object lastStateObject;
    private object hurtSource;

    // Saldırı
    private bool attackActive;
    private bool holding;
    private int attackClipHash;
    private float attackHitTime;
    private int lastStep = -1;
    private int attackStartFrame;

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        animator = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();

        idleHash = Animator.StringToHash(idleState);
        walkHash = Animator.StringToHash(walkState);
        attackHash = Animator.StringToHash(attackState);
        hurtHash = Animator.StringToHash(hurtState);
        sweepHash = Animator.StringToHash(sweepState);
        grabHash = Animator.StringToHash(grabState);
        shootHash = Animator.StringToHash(shootState);

        if (animator == null)
        {
            Debug.LogWarning(
                "EnemyAnimationDriver: " + name + " üzerinde (ya da " +
                "çocuklarında) Animator yok."
            );

            enabled = false;
            return;
        }

        CheckState(idleState, idleHash, true);
        CheckState(walkState, walkHash, true);
        CheckState(attackState, attackHash, true);
        CheckState(hurtState, hurtHash, true);

        hasSweep = CheckState(sweepState, sweepHash, false);
        hasGrab = CheckState(grabState, grabHash, false);
        hasShoot = CheckState(shootState, shootHash, false);
    }

    private void OnEnable()
    {
        current = 0;
        lastStateObject = null;
        hurtSource = null;
        attackActive = false;
        holding = false;
        lastStep = -1;
    }

    // Durumlar EnemyController.Update'te değişir; biz ondan SONRA bakarız.
    private void LateUpdate()
    {
        if (animator == null || !animator.isActiveAndEnabled)
            return;

        IEnemyState state = enemy.CurrentState;

        bool newState = !ReferenceEquals(state, lastStateObject);

        lastStateObject = state;

        // ---------------- ÖLÜ ----------------

        if (enemy.IsDead)
        {
            attackActive = false;

            if (hurtOnDeath)
                PlayHurt(hurtSource, false);

            return;
        }

        // ---------------- SERSEM / EXECUTE ----------------

        if (
            hurtDuringStagger &&
            (state is EnemyStaggerState || state is EnemyExecuteState)
        )
        {
            attackActive = false;
            PlayHurt(state, false);
            return;
        }

        // ---------------- VURULDU ----------------

        if (state is EnemyHitState)
        {
            attackActive = false;
            PlayHurt(state, restartHurtOnNewHit && newState);
            return;
        }

        // ---------------- SALDIRI ----------------

        if (state is EnemyAttackState attack)
        {
            // Yeni saldırı ya da kombonun yeni vuruşu: baştan.
            if (newState || attack.StepIndex != lastStep)
                BeginAttack(attack);

            if (attackActive && UpdateAttack(attack))
                return;

            // Vuruş bitti (recovery) ya da animasyon kapalı.
            Play(idleHash, locomotionBlend);
            return;
        }

        attackActive = false;
        holding = false;
        lastStep = -1;

        // ---------------- HAREKET ----------------

        float speedX =
            rb != null ? Mathf.Abs(rb.linearVelocity.x) : 0f;

        Play(
            speedX > walkSpeedThreshold ? walkHash : idleHash,
            locomotionBlend
        );
    }

    // =========================================================
    // SALDIRI
    // =========================================================

    private void BeginAttack(EnemyAttackState attack)
    {
        lastStep = attack.StepIndex;
        attackActive = true;

        // EnemyController'ın tetiklediği trigger birikmesin.
        animator.ResetTrigger(attackState);

        SelectClip(attack.CurrentHitType);

        if (!alignHitFrameToDamage)
        {
            holding = false;
            attackStartFrame = Time.frameCount;
            PlayAt(attackClipHash, 0f);
            return;
        }

        holding = true;
        UpdateAttack(attack);
    }

    private void SelectClip(MoveHitType type)
    {
        float baseHit = enemy.AttackAnimationHitTime;

        switch (type)
        {
            case MoveHitType.Sweep when hasSweep:
                attackClipHash = sweepHash;
                attackHitTime = sweepHitTime > 0f ? sweepHitTime : baseHit;
                break;

            case MoveHitType.Grab when hasGrab:
                attackClipHash = grabHash;
                attackHitTime = grabHitTime > 0f ? grabHitTime : baseHit;
                break;

            case MoveHitType.Shot when hasShoot:
                attackClipHash = shootHash;
                attackHitTime = shootHitTime > 0f ? shootHitTime : baseHit;
                break;

            default:
                attackClipHash = attackHash;
                attackHitTime = baseHit;
                break;
        }
    }

    // true: saldırı animasyonu hâlâ sürüyor (dokunma).
    private bool UpdateAttack(EnemyAttackState attack)
    {
        if (holding)
        {
            float hold = Mathf.Min(holdPoseTime, attackHitTime);

            // Vuruşa kalan süreye göre klipte olması gereken an.
            float clipTime =
                attack.IsWindingUp
                    ? attackHitTime - attack.RemainingWindup
                    : attackHitTime;

            if (clipTime < hold)
            {
                // Hazırlık pozunda bekle.
                PlayAt(attackClipHash, hold);
                return true;
            }

            // Bırak: tam olması gereken yerden akmaya başlar.
            holding = false;
            attackStartFrame = Time.frameCount;
            PlayAt(attackClipHash, clipTime);
            return true;
        }

        // Play'in etkisi bir sonraki karede görünür; ilk kareleri bekle.
        if (Time.frameCount <= attackStartFrame + 1)
            return true;

        if (animator.IsInTransition(0))
            return true;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);

        if (info.shortNameHash == attackClipHash && info.normalizedTime < 1f)
            return true;

        attackActive = false;
        return false;
    }

    // =========================================================
    // YARDIMCILAR
    // =========================================================

    private void PlayHurt(object source, bool restart)
    {
        if (current == hurtHash && !restart)
            return;

        hurtSource = source;

        // Darbe tepkisi anında başlasın (geçiş yok).
        PlayAt(hurtHash, 0f);
    }

    private void Play(int hash, float blend)
    {
        if (current == hash)
            return;

        current = hash;

        if (blend > 0f)
            animator.CrossFadeInFixedTime(hash, blend, 0, 0f);
        else
            animator.PlayInFixedTime(hash, 0, 0f);
    }

    // Klibin belirli SANİYESİNDEN oynat (anında).
    private void PlayAt(int hash, float seconds)
    {
        current = hash;
        animator.PlayInFixedTime(hash, 0, Mathf.Max(0f, seconds));
    }

    private bool CheckState(string stateName, int hash, bool required)
    {
        if (animator.runtimeAnimatorController == null)
            return false;

        bool exists =
            animator.HasState(0, hash) ||
            animator.HasState(0, Animator.StringToHash("Base Layer." + stateName));

        if (!exists && required)
        {
            Debug.LogWarning(
                "EnemyAnimationDriver: Animator'da '" + stateName + "' adında " +
                "bir state yok (" + name + "). State adını ya da bu bileşendeki " +
                "alanı düzelt."
            );
        }

        return exists;
    }
}
