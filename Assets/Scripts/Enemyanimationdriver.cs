using UnityEngine;

/// <summary>
/// DÜŞMAN ANİMASYON SÜRÜCÜSÜ. Animator'da geçiş (ok) ÇİZMEDEN Idle / Walk /
/// Attack / Hurt animasyonlarını düşmanın o anki durumuna göre oynatır.
///
/// Mantık (öncelik sırasıyla, her karede):
///   Ölü                      → Hurt (son karede kalır)
///   Stagger / Execute        → Hurt (son karede kalır = sersem duruş)
///   Hit (vurulup savruldu)   → Hurt (her YENİ vuruşta baştan)
///   Attack                   → Attack (vuruş karesi HASAR anına hizalanır)
///   Hareket ediyor           → Walk
///   Duruyor                  → Idle
///
/// VURUŞ HİZALAMA: Saldırı başlayınca Attack'in İLK karesi (hazırlık pozu)
/// tutulur; vuruşa 'Attack Animation Hit Time' kadar kala animasyon
/// akmaya başlar. Böylece vuruş karesi hasarla tam aynı anda gelir
/// (uyarı süresi, engellenemez vuruş, zehir uzatması fark etmez).
///
/// KURULUM:
///  1) Düşman prefab'ına bu bileşeni ekle.
///  2) Animator Controller'da 4 state: Idle, Walk, Attack, Hurt
///     (isimler aşağıdaki alanlarla AYNI). Default = Idle. OK ÇİZME.
///  3) Parameters'a 'Attack' adında bir Trigger ekle (EnemyController
///     hâlâ onu çağırıyor; hiçbir geçişte kullanılmaz, sadece uyarıyı susturur).
///  4) EnemyController > Attack Animation Hit Time = animasyonun başından
///     vuruş karesine kadar geçen süre (ör. 12 fps'de 5. kare → 5/12 = 0.42).
///
/// Düşman zamanı (parry slow-mo) EnemyController'ın animator.speed
/// ayarıyla zaten uygulanır; bu bileşen hıza dokunmaz.
/// </summary>
[RequireComponent(typeof(EnemyController))]
public class EnemyAnimationDriver : MonoBehaviour
{
    [Header("Animator state adları (Animator'dakiyle birebir aynı)")]
    [SerializeField] private string idleState = "Idle";
    [SerializeField] private string walkState = "Walk";
    [SerializeField] private string attackState = "Attack";
    [SerializeField] private string hurtState = "Hurt";

    [Header("Geçişler")]
    [Tooltip("Idle ↔ Walk ↔ Idle yumuşak geçiş süresi (sn). Pixel art için 0.")]
    [Min(0f)]
    [SerializeField] private float locomotionBlend = 0f;

    [Tooltip("Bu yatay hızın üstünde Walk oynar.")]
    [Min(0f)]
    [SerializeField] private float walkSpeedThreshold = 0.15f;

    [Header("Davranış")]
    [Tooltip(
        "Saldırı başında Attack'in ilk karesini (hazırlık pozu) tut, vuruş " +
        "karesi hasar anına denk gelecek şekilde bırak. Kapalı: saldırı " +
        "animasyonu EnemyController'ın çağırdığı anda başlar.")]
    [SerializeField] private bool alignHitFrameToDamage = true;

    [Tooltip("Sersemleme ve execute sırasında Hurt oynasın (son karede kalır).")]
    [SerializeField] private bool hurtDuringStagger = true;

    [Tooltip("Ölünce Hurt oynasın (fade-out bitene kadar).")]
    [SerializeField] private bool hurtOnDeath = true;

    [Tooltip("Kombo vuruşlarında her yeni darbe Hurt'ü baştan başlatsın.")]
    [SerializeField] private bool restartHurtOnNewHit = true;

    private EnemyController enemy;
    private Animator animator;
    private Rigidbody2D rb;

    private int idleHash;
    private int walkHash;
    private int attackHash;
    private int hurtHash;

    private int current;            // şu an oynattığımız state
    private object lastStateObject; // yeni state örneği tespiti
    private object hurtSource;      // Hurt'ü başlatan state örneği

    private bool attackActive;
    private bool holdingWindup;
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

        if (animator == null)
        {
            Debug.LogWarning(
                "EnemyAnimationDriver: " + name + " üzerinde (ya da " +
                "çocuklarında) Animator yok."
            );

            enabled = false;
            return;
        }

        CheckState(idleState, idleHash);
        CheckState(walkState, walkHash);
        CheckState(attackState, attackHash);
        CheckState(hurtState, hurtHash);
    }

    private void OnEnable()
    {
        current = 0;
        lastStateObject = null;
        hurtSource = null;
        attackActive = false;
        holdingWindup = false;
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

            // Aynı sersemleme boyunca baştan başlamaz, son karede kalır.
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
            if (newState)
                BeginAttack(attack);

            if (attackActive)
            {
                if (UpdateAttack(attack))
                    return;
            }

            // Saldırı animasyonu bitti (recovery) ya da hiç başlamadı.
            Play(idleHash, locomotionBlend);
            return;
        }

        attackActive = false;
        holdingWindup = false;

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
        attackActive = true;
        attackStartFrame = Time.frameCount;

        // EnemyController'ın tetiklediği trigger birikmesin.
        animator.ResetTrigger(attackState);

        holdingWindup =
            alignHitFrameToDamage &&
            attack.IsWindingUp &&
            attack.RemainingWindup > enemy.AttackAnimationHitTime;

        PlayFromStart(attackHash);
    }

    // true: Attack animasyonu hâlâ sürüyor (dokunma).
    private bool UpdateAttack(EnemyAttackState attack)
    {
        // Hazırlık pozunu tut: vuruşa 'hit time' kalana kadar ilk kare.
        if (holdingWindup)
        {
            if (
                attack.IsWindingUp &&
                attack.RemainingWindup > enemy.AttackAnimationHitTime
            )
            {
                animator.Play(attackHash, 0, 0f);
                return true;
            }

            // Bırak: animasyon şimdi akmaya başlar, vuruş karesi hasarla gelir.
            holdingWindup = false;
            attackStartFrame = Time.frameCount;
            animator.Play(attackHash, 0, 0f);
            return true;
        }

        // Play'in etkisi bir sonraki karede görünür; ilk kareleri bekle.
        if (Time.frameCount <= attackStartFrame + 1)
            return true;

        if (animator.IsInTransition(0))
            return true;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);

        if (info.shortNameHash == attackHash && info.normalizedTime < 1f)
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

        if (
            current == hurtHash &&
            restart &&
            ReferenceEquals(source, hurtSource) &&
            source != null
        )
        {
            return;
        }

        hurtSource = source;

        // Darbe tepkisi anında başlasın (geçiş yok).
        PlayFromStart(hurtHash);
    }

    private void Play(int hash, float blend)
    {
        if (current == hash)
            return;

        current = hash;

        if (blend > 0f)
            animator.CrossFadeInFixedTime(hash, blend, 0, 0f);
        else
            animator.Play(hash, 0, 0f);
    }

    private void PlayFromStart(int hash)
    {
        current = hash;
        animator.Play(hash, 0, 0f);
    }

    private void CheckState(string stateName, int hash)
    {
        if (
            animator.runtimeAnimatorController == null ||
            animator.HasState(0, hash) ||
            animator.HasState(0, Animator.StringToHash("Base Layer." + stateName))
        )
        {
            return;
        }

        Debug.LogWarning(
            "EnemyAnimationDriver: Animator'da '" + stateName + "' adında " +
            "bir state yok (" + name + "). State adını ya da bu bileşendeki " +
            "alanı düzelt."
        );
    }
}