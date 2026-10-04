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
/// VURUŞ HİZALAMA (uyarı sırasında, 'Windup Style'):
///   STRETCH (varsayılan): saldırı klibi uyarı süresine YAYILIR, yani yavaş
///     oynar (en fazla 'Max Windup Slowdown' kat). Vuruş karesi tam hasar
///     anına gelir. Sadece 'Attack Animation Hit Time' girmen yeter.
///   HOLD POSE: klip 'Hold Pose Time' karesinde DONAR, vuruşa
///     'hit time − hold pose time' kala akmaya başlar (gecikmeli vuruş =
///     silah havada bekler).
/// Uyarı, hit time'dan kısaysa (hızlı kombo) animasyon ortasından başlar.
///
/// HURT: kısa darbelerde de en az 'Min Hurt Time' görünür (tek karelik
/// titreme olmaz).
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

    public enum WindupStyle
    {
        Stretch,
        HoldPose
    }

    [Header("Uyarı animasyonu")]
    [Tooltip(
        "Stretch: saldırı klibi uyarı boyunca yavaş oynar, vuruş karesi hasarla " +
        "çakışır (önerilen). HoldPose: 'Hold Pose Time' karesinde donup bekler.")]
    [SerializeField] private WindupStyle windupStyle = WindupStyle.Stretch;

    [Tooltip("Stretch: klip en fazla bu kadar kat yavaşlatılır; uyarı daha uzunsa başta bekler.")]
    [Min(1f)]
    [SerializeField] private float maxWindupSlowdown = 3f;

    [Tooltip(
        "Stretch: uyarı kısaysa (seri vuruş / hızlı ok) klip en fazla bu kadar " +
        "kat HIZLANDIRILIR; hazırlık yine baştan görünür. 1 = hızlanmaz " +
        "(kısa uyarıda animasyon ortadan başlar).")]
    [Min(1f)]
    [SerializeField] private float maxWindupSpeedup = 3f;

    [Tooltip("Hurt en az bu kadar (sn) görünür; çok kısa darbede tek kare titremesin.")]
    [Min(0f)]
    [SerializeField] private float minHurtTime = 0.25f;

    [Header("Vuruş zamanlaması")]
    [Tooltip(
        "SADECE HoldPose stilinde: uyarı sırasında klibin donduğu an (sn). " +
        "0 = ilk kare. Silahın havada olduğu kareyi seç (12 fps'de 3. kare → 0.25). " +
        "Stretch'te kullanılmaz.")]
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

    [Tooltip(
        "Idle / Walk klibi döngüsüz (Loop Time kapalı) olsa bile baştan " +
        "oynat. (Klip ayarı unutulursa düşman donmasın.)")]
    [SerializeField] private bool forceLoopLocomotion = true;

    [Header("Debug")]
    [Tooltip(
        "Düşmanın üstünde o an oynayan state'i, klip zamanını ve klibin " +
        "döngülü olup olmadığını yazar (animasyon sorunu teşhisi).")]
    [SerializeField] private bool showDebug = false;

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
    private float windupTotal;
    private float hurtUntil;
    private bool warnedHitTime;

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

        // Kısa darbede Hurt hemen kesilmesin.
        if (current == hurtHash && Time.time < hurtUntil)
            return;

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

        // Bu vuruşun toplam uyarı süresi (Stretch hızı için).
        windupTotal = Mathf.Max(0.01f, attack.RemainingWindup);

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
            // Vuruş anı klipten uzun girildiyse (klip son karede donar):
            // gerçek klip uzunluğuna kırp ve bir kez uyar.
            AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);

            if (
                st.shortNameHash == attackClipHash &&
                st.length > 0.01f &&
                attackHitTime >= st.length
            )
            {
                if (!warnedHitTime)
                {
                    warnedHitTime = true;

                    Debug.LogWarning(
                        "EnemyAnimationDriver (" + name + "): vuruş anı (" +
                        attackHitTime.ToString("0.00") + " sn) saldırı klibinden (" +
                        st.length.ToString("0.00") + " sn) uzun. 'Attack Animation " +
                        "Hit Time' değerini klipteki vuruş karesine göre düzelt."
                    );
                }

                attackHitTime = st.length * 0.85f;
            }

            if (attack.IsWindingUp)
            {
                // Klip hızı: Stretch'te uyarıya yayılır (yavaş), HoldPose'ta normal.
                // Stretch: hazırlık (klibin başından vuruş karesine) uyarıya
                // tam sığar. Uzun uyarıda yavaşlar, kısa uyarıda (seri) hızlanır.
                float rate = 1f;

                if (windupStyle == WindupStyle.Stretch && attackHitTime > 0f)
                {
                    rate =
                        Mathf.Clamp(
                            attackHitTime / windupTotal,
                            1f / Mathf.Max(1f, maxWindupSlowdown),
                            Mathf.Max(1f, maxWindupSpeedup)
                        );
                }

                // Vuruşa kalan süreye göre klipte olması gereken an.
                float clipTime = attackHitTime - attack.RemainingWindup * rate;

                float minTime =
                    windupStyle == WindupStyle.HoldPose
                        ? Mathf.Min(holdPoseTime, attackHitTime)
                        : 0f;

                PlayAt(attackClipHash, Mathf.Max(minTime, clipTime));
                return true;
            }

            // Vuruş anı: vuruş karesinden normal hızla akmaya devam.
            holding = false;
            attackStartFrame = Time.frameCount;
            PlayAt(attackClipHash, attackHitTime);
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
        hurtUntil = Time.time + minHurtTime;

        // Darbe tepkisi anında başlasın (geçiş yok).
        PlayAt(hurtHash, 0f);
    }

    private void Play(int hash, float blend)
    {
        if (current == hash)
        {
            // Döngüsüz Idle/Walk bittiyse baştan al (zorunlu döngü).
            if (forceLoopLocomotion && !animator.IsInTransition(0))
            {
                AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);

                if (
                    info.shortNameHash == hash &&
                    !info.loop &&
                    info.normalizedTime >= 1f
                )
                {
                    animator.PlayInFixedTime(hash, 0, 0f);
                }
            }

            return;
        }

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

    // =========================================================
    // DEBUG
    // =========================================================

    private void OnGUI()
    {
        if (!showDebug || animator == null || Camera.main == null)
            return;

        Vector3 screen =
            Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 0.2f);

        if (screen.z < 0f)
            return;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);

        string state =
            info.shortNameHash == idleHash ? idleState :
            info.shortNameHash == walkHash ? walkState :
            info.shortNameHash == attackHash ? attackState :
            info.shortNameHash == hurtHash ? hurtState :
            info.shortNameHash == sweepHash ? sweepState :
            info.shortNameHash == grabHash ? grabState :
            info.shortNameHash == shootHash ? shootState :
            "?";

        string text =
            state +
            "  " + (info.normalizedTime * info.length).ToString("0.00") +
            "/" + info.length.ToString("0.00") + " sn" +
            (info.loop ? "  LOOP" : "  tek sefer") +
            "\nhit " + attackHitTime.ToString("0.00") +
            "  hız " + animator.speed.ToString("0.00") +
            (enemy != null && enemy.CurrentState != null
                ? "\n" + enemy.CurrentState.GetType().Name
                : "");

        Rect r = new Rect(screen.x - 90f, Screen.height - screen.y, 180f, 48f);

        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);

        GUI.color = Color.white;
        GUI.Label(new Rect(r.x + 4f, r.y + 2f, r.width - 8f, r.height), text);
    }
}