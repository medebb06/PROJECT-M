using UnityEngine;

/// <summary>
/// Oyuncu savunması: parry (bas) ve block (basılı tut).
///
/// AKIŞ (parry + parry + parry):
///  - Parry penceresi tuşu bıraksan da tam süre açık kalır.
///  - Giriş tamponu: kilitliyken (hasar sersemlemesi, saldırı...) basılan
///    parry kaybolmaz, kısa süre içinde ilk fırsatta başlar.
///  - Havada da savunma yapılabilir (vuruş seni hafif kaldırınca parry
///    yutulmasın).
///  - Saldırı sırasında basınca saldırı iptal olur, parry hemen başlar.
///  - ZİNCİR: başarılı parry'den sonra kısa süre pencere biraz GENİŞLER
///    (kombonun sonraki vuruşları biraz daha affedici). Ama HER vuruş için
///    yine ZAMANLAMALI basmak gerekir: basılı tutmak = BLOCK (posture yer).
///  - BOŞA BASMA CEZASI (zincirde de geçerli): boşa giden parry'nin hemen
///    ardından basılan pencere daralır. Abanmak (mash) işe yaramaz,
///    ritme basmak yarar.
///
/// Zorluk ayarı (Inspector): Chain Parry Window Multiplier (büyük = kolay),
/// Whiff Window Multiplier (küçük = mash daha çok cezalanır).
/// </summary>
public class PlayerDefenseController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController player;
    [SerializeField] private GameObject defenseVisual;
    [SerializeField] private CombatImpactFeedback combatFeedback;

    [Header("Parry")]
    [SerializeField] private float parryWindow = 0.12f;
    [SerializeField] private GameObject parryVisual;

    [Header("Akış")]
    [Tooltip("Savunma tuşu bu kadar süre 'hatırlanır' (kilit bitince parry başlar).")]
    [Min(0f)]
    [SerializeField] private float inputBuffer = 0.15f;

    [Tooltip("Havadayken de parry/block yapılabilsin.")]
    [SerializeField] private bool allowAirDefense = true;

    [Tooltip("Saldırı sırasında savunmaya basınca saldırı iptal olsun.")]
    [SerializeField] private bool cancelAttackOnDefense = true;

    [Header("Parry Zinciri (kombo akışı)")]
    [Tooltip(
        "Son başarılı parry'den sonra zincir bu kadar sürer (sn). Her başarılı " +
        "parry zinciri tazeler; kombo arası boşluktan uzun olmalı.")]
    [Min(0f)]
    [SerializeField] private float chainDuration = 1.0f;

    // NOT: alan adları bilerek değişti (eski: chainWindowMultiplier 2.5,
    // holdToParryInChain). Prefab'a kayıtlı eski kolay değerler yeni
    // varsayılanları ezmesin.
    [Tooltip(
        "Zincirdeki parry penceresinin çarpanı. 1 = ilk parry kadar zor, " +
        "1.5 = biraz affedici (önerilen), 2.5 = eski çok kolay hali.")]
    [Min(1f)]
    [SerializeField] private float chainParryWindowMultiplier = 1.5f;

    [Header("Parry sonrası tepki")]
    [Tooltip("Başarılı parry'den sonra pencere bu kadar sn içinde kapanır (aynı anda gelen ikinci vuruş için kısa pay).")]
    [Min(0f)]
    [SerializeField] private float postParryGrace = 0.06f;

    [Tooltip("Başarılı parry'den sonra bu süre içinde saldırı / zıplama savunmayı keser.")]
    [Min(0f)]
    [SerializeField] private float parryCancelTime = 0.45f;

    [Header("Boşa Basma Cezası (mash'e karşı)")]
    [Tooltip(
        "Boşa giden parry penceresi bittikten sonra bu süre içinde tekrar " +
        "basılırsa ceza uygulanır (zincirde de).")]
    [Min(0f)]
    [SerializeField] private float whiffPenaltyTime = 0.3f;

    [Tooltip("Cezalı parry penceresinin çarpanı. 1 = ceza yok, 0.5 = yarı pencere.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float whiffWindowMultiplier = 0.5f;

    [Header("Debug")]
    [Tooltip("Ekranda savunma durumunu gösterir (takılma teşhisi için).")]
    [SerializeField] private bool showDebug = false;

    private IPlayerDefenseState currentState;
    private PlayerCombatController combat;

    private float bufferTimer;

    // Zıplama/dash savunmayı iptal ettiyse, tuş bırakılana kadar
    // 'basılı tutuluyor' savunma sayılmaz (hareket kilitlenmesin).
    private bool suppressHeld;

    private float lastSuccessTime = -99f;
    private float lastFailTime = -99f;

    public bool IsBlocking { get; private set; }
    public bool IsParrying { get; private set; }

    public bool IsDefending
    {
        get
        {
            return IsParrying ||
                   IsBlocking ||
                   (Input.GetMouseButton(1) && !suppressHeld);
        }
    }

    // Charm'lar (Kusursuz Zamanlama) pencereyi genişletebilir.
    public float ParryWindow =>
        Mathf.Max(
            0.02f,
            PlayerStats.GetOr(StatType.ParryWindow, parryWindow)
        );

    // Az önce başarılı parry yapıldı mı? (saldırı savunmayı kesebilir)
    public bool RecentlyParried =>
        Time.time - lastSuccessTime <= parryCancelTime;

    // Zincirde mi? (Arayüz / efekt için.)
    public bool InParryChain =>
        Time.time - lastSuccessTime <= chainDuration;

    void Awake()
    {
        if (player == null)
            player = GetComponent<PlayerController>();

        if (combatFeedback == null)
            combatFeedback =
                GetComponent<CombatImpactFeedback>();

        combat = GetComponent<PlayerCombatController>();

        if (defenseVisual != null)
            defenseVisual.SetActive(false);

        if (parryVisual != null)
            parryVisual.SetActive(false);
    }

    private void OnEnable()
    {
        CombatEvents.ParrySucceeded += OnParrySucceeded;
    }

    private void OnDisable()
    {
        CombatEvents.ParrySucceeded -= OnParrySucceeded;
    }

    void Update()
    {
        HandleInput();
        currentState?.Tick();
    }

    // =========================================================
    // GİRİŞ
    // =========================================================

    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(1))
        {
            suppressHeld = false;
            bufferTimer = inputBuffer > 0f ? inputBuffer : 0.0001f;
        }

        if (Input.GetMouseButtonUp(1))
            suppressHeld = false;

        if (bufferTimer > 0f)
        {
            if (TryStartParry())
                bufferTimer = 0f;
            else
                bufferTimer -= Time.unscaledDeltaTime;
        }

        // Block bırakılınca savunma biter. (Parry penceresi bırakınca
        // KAPANMAZ; süresi dolunca kendisi karar verir.)
        if (
            Input.GetMouseButtonUp(1) &&
            currentState is PlayerBlockState
        )
        {
            ChangeState(null);
        }
    }

    private bool CanStartDefense()
    {
        if (player == null)
            return false;

        // Posture break / hasar sersemlemesi / ölüm sırasında başlatılamaz
        // (tampon bekler).
        if (!player.canControl)
            return false;

        if (player.inputLocked)
            return false;

        if (player.isDashing)
            return false;

        if (!allowAirDefense && !player.IsGrounded())
            return false;

        return true;
    }

    private bool TryStartParry()
    {
        if (!CanStartDefense())
            return false;

        // Zaten açık bir parry penceresi varsa ona dokunma.
        if (currentState is PlayerParryState)
            return true;

        if (cancelAttackOnDefense && combat != null)
            combat.CancelAttack();

        if (player.rb != null && player.IsGrounded())
        {
            player.rb.linearVelocity =
                new Vector2(
                    0f,
                    player.rb.linearVelocity.y
                );
        }

        ChangeState(
            new PlayerParryState(
                this,
                player,
                CurrentParryWindow()
            )
        );

        return true;
    }

    private float CurrentParryWindow()
    {
        float window = ParryWindow;

        // Zincir: biraz affedici.
        if (InParryChain)
            window *= chainParryWindowMultiplier;

        // Boşa basma cezası zincirde de geçerli: abanmak pencereyi
        // daraltır, ritme basmak tam pencere verir.
        if (Time.time - lastFailTime <= whiffPenaltyTime)
            window *= whiffWindowMultiplier;

        return window;
    }

    // =========================================================
    // PARRY SONUCU
    // =========================================================

    private void OnParrySucceeded(EnemyController enemy, bool brokeBalance)
    {
        lastSuccessTime = Time.time;

        // Parry işini yaptı: pencere kısa payla kapanır, oyuncu hemen
        // saldırabilir / hareket edebilir.
        if (currentState is PlayerParryState parry)
            parry.ShortenTo(postParryGrace);
    }

    // PlayerParryState pencere bitince çağırır.
    public void NotifyParryWindowEnded(float windowStartTime)
    {
        // Pencere süresince hiç başarılı parry olmadıysa: boşa gitti.
        if (lastSuccessTime < windowStartTime)
            lastFailTime = Time.time;
    }

    // Zıplama / dash savunmayı keser. Tuş hâlâ basılıysa bırakılana
    // kadar block'a geri düşülmez.
    public void CancelDefense()
    {
        bufferTimer = 0f;

        if (Input.GetMouseButton(1))
            suppressHeld = true;

        if (currentState != null)
            ChangeState(null);
    }

    // =========================================================
    // STATE
    // =========================================================

    public void ChangeState(
        IPlayerDefenseState newState
    )
    {
        currentState?.Exit();

        currentState = newState;

        if (currentState != null)
            currentState.Enter();
    }

    public void SetParrying(bool value)
    {
        IsParrying = value;

        if (parryVisual != null)
            parryVisual.SetActive(value);
    }

    public void SetBlocking(bool value)
    {
        IsBlocking = value;

        if (defenseVisual != null)
            defenseVisual.SetActive(value);

        if (player != null)
            player.SetBlockingAnimation(value);
    }

    // Sadece açık parry penceresi parry'dir. Basılı tutmak (zincirde de)
    // BLOCK'tur: güvenli ama posture yer.
    public bool CanParry()
    {
        return IsParrying;
    }

    public bool CanBlock()
    {
        return IsBlocking;
    }

    public void PlayParryFeedback()
    {
        if (player != null)
            player.PlayParryAnimation();

        if (combatFeedback != null)
            combatFeedback.PlayParryImpact();
    }

    // =========================================================
    // BLOCK
    // =========================================================

    public void HandleBlockHit(
        Vector2 hitDirection,
        int postureDamage
    )
    {
        // Kan Bedeli charm'ı: block posture yerine CAN yer.
        float healthCost =
            PlayerStats.GetOr(StatType.BlockHealthCost, 0f);

        if (healthCost > 0f)
        {
            PayBlockWithHealth(healthCost, hitDirection);
            return;
        }

        PlayerPosture posture =
            GetComponent<PlayerPosture>();

        if (posture == null)
        {
            Debug.LogWarning(
                "PlayerDefenseController: " +
                "PlayerPosture bulunamadı!"
            );

            return;
        }

        posture.TakePostureDamage(
            postureDamage
        );

        Debug.Log(
            "PLAYER BLOCK → POSTURE -" +
            postureDamage +
            " | CURRENT: " +
            posture.CurrentPosture +
            "/" +
            posture.MaxPosture
        );

        if (posture.IsBroken)
        {
            Debug.Log(
                "PLAYER POSTURE BROKEN!"
            );

            // Önce mevcut block/parry'yi kapat.
            ChangeState(null);

            bufferTimer = 0f;

            if (player != null)
            {
                player.ApplyPostureBreak(
                    hitDirection
                );
            }
        }
    }

    // =========================================================
    // DEBUG
    // =========================================================

    private void OnGUI()
    {
        if (!showDebug || player == null)
            return;

        GUI.Label(
            new Rect(10, 120, 520, 90),
            "SAVUNMA  state: " +
            (currentState != null ? currentState.GetType().Name : "-") +
            "  parry: " + IsParrying +
            "  block: " + IsBlocking +
            "  zincir: " + InParryChain +
            "\ncanControl: " + player.canControl +
            "  inputLocked: " + player.inputLocked +
            "  grounded: " + player.IsGrounded() +
            "  tampon: " + (bufferTimer > 0f) +
            "\nplayer state: " +
            (player.stateMachine != null && player.stateMachine.CurrentState != null
                ? player.stateMachine.CurrentState.GetType().Name
                : "-")
        );
    }

    // Block bedeli candan ödenir. ÖLDÜRMEZ: en az 1 can bırakır
    // (block'lamak hiçbir zaman ölüm nedeni olmasın).
    private void PayBlockWithHealth(
        float healthCost,
        Vector2 hitDirection
    )
    {
        Health health = GetComponent<Health>();

        if (health == null || health.IsDead)
            return;

        int amount =
            Mathf.Max(
                1,
                Mathf.RoundToInt(health.MaxHealth * healthCost)
            );

        amount =
            Mathf.Min(amount, health.CurrentHealth - 1);

        if (amount <= 0)
            return;

        health.TakeDamage(amount);

        CombatEvents.RaisePlayerDamaged(
            new PlayerDamageReport
            {
                amount = amount,
                healthAfter = health.CurrentHealth,
                maxHealth = health.MaxHealth,
                lethal = false,
                kind = PlayerHitKind.BlockCost,
                source = null,
                direction = hitDirection
            }
        );

        Debug.Log(
            "PLAYER BLOCK → KAN BEDELİ -" + amount + " can"
        );
    }
}