using UnityEngine;

/// <summary>
/// AKIŞ: eylemleri birbirine bağlayan ZİNCİR + parry'nin KARŞILIK ödülü.
///
///   Parry başarılı  → zincir +1 ve KARŞILIK penceresi (counterWindow sn) açılır:
///                     bir sonraki düz vuruş "karşılık" olur (büyük hasar çarpanı,
///                     uzun hit stop, ekran sarsıntısı, rüzgâr kesikleri, +Kın yükü).
///   Karşılık vuruşu → zincir +1.
///   Kusursuz kaçış (Dodged), Kın patlaması, Atılım Kesiği → zincir +1.
///   Hasar alınca zincir sıfırlanır; chainTimeout sn eylem yoksa söner.
///
/// Zincir arttıkça karşılık ve düz vuruşlar biraz güçlenir (üst sınırlı).
/// Oyun başında kendiliğinden kurulur.
/// </summary>
public class CombatFlow : MonoBehaviour
{
    public static CombatFlow Instance { get; private set; }

    [Header("Karşılık (parry sonrası ilk vuruş)")]
    public float counterWindow = 1.2f;
    public float counterMultiplier = 1.8f;
    public float counterPerLink = 0.15f;
    public float counterMax = 3f;
    [Tooltip("Karşılık vuruşunun düşmana eklediği Kın yükü.")]
    public int counterCharge = 2;

    [Header("Zincir")]
    public float chainTimeout = 4f;
    [Tooltip("Zincirdeki her halka düz vuruşlara bu kadar bonus (üst sınırlı).")]
    public float hitBonusPerLink = 0.05f;
    public float hitBonusMax = 0.3f;

    public static int Chain { get; private set; }

    private float counterUntil;
    private float lastLinkTime;
    private PlayerController player;

    private static readonly Color Gold = new Color(1f, 0.85f, 0.35f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        Chain = 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null || FindAnyObjectByType<CombatFlow>() != null)
            return;

        GameObject go = new GameObject("CombatFlow");
        DontDestroyOnLoad(go);
        go.AddComponent<CombatFlow>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        CombatEvents.ParrySucceeded += OnParry;
        CombatEvents.Dodged += OnDodged;
        CombatEvents.PlayerDamaged += OnPlayerDamaged;
    }

    private void OnDisable()
    {
        CombatEvents.ParrySucceeded -= OnParry;
        CombatEvents.Dodged -= OnDodged;
        CombatEvents.PlayerDamaged -= OnPlayerDamaged;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (Chain > 0 && Time.unscaledTime - lastLinkTime > chainTimeout)
            Chain = 0;
    }

    private PlayerController Player()
    {
        if (player == null)
            player = FindAnyObjectByType<PlayerController>();

        return player;
    }

    // ---------------------------------------------------------
    // OLAYLAR
    // ---------------------------------------------------------

    private void OnParry(EnemyBase enemy, bool brokeBalance)
    {
        counterUntil = Time.unscaledTime + counterWindow;

        Link();

        PlayerController p = Player();

        if (p != null)
        {
            NoCallout.Popup(
                p.transform.position + Vector3.up * 2.4f,
                "KARŞILIK!",
                Gold,
                0.8f
            );

            if (PlayerAbility.Instance != null)
                PlayerAbility.Instance.SpawnRingFx(p.transform.position + Vector3.up * 0.9f, 2.4f, new Color(1f, 0.9f, 0.5f, 0.8f));
        }
    }

    private void OnDodged(EnemyBase enemy, bool unblockable)
    {
        Link();
    }

    private void OnPlayerDamaged(PlayerDamageReport report)
    {
        if (report.amount <= 0 || report.kind == PlayerHitKind.BlockCost)
            return;

        Chain = 0;
        counterUntil = 0f;
    }

    // ---------------------------------------------------------
    // ZİNCİR
    // ---------------------------------------------------------

    /// <summary>Başka sistemler (Kın patlaması, Atılım Kesiği) zincire halka ekler.</summary>
    public static void AddLink()
    {
        if (Instance != null)
            Instance.Link();
    }

    private void Link()
    {
        Chain++;
        lastLinkTime = Time.unscaledTime;

        if (Chain < 2)
            return;

        PlayerController p = Player();

        if (p == null)
            return;

        // Zincir büyüdükçe sıcak renge kayar.
        float k = Mathf.Clamp01((Chain - 2) / 6f);

        NoCallout.Popup(
            p.transform.position + Vector3.up * 3.1f,
            "AKIŞ x" + Chain,
            Color.Lerp(new Color(0.7f, 0.9f, 1f), new Color(1f, 0.55f, 0.25f), k),
            0.7f
        );
    }

    // ---------------------------------------------------------
    // VURUŞ (PlayerDamage çağırır): hasar çarpanı döner, 1 = etkisiz
    // ---------------------------------------------------------

    public static float OnPlayerHit(EnemyBase enemy)
    {
        return Instance != null ? Instance.Hit(enemy) : 1f;
    }

    private float Hit(EnemyBase enemy)
    {
        int links = Mathf.Max(0, Chain - 1);

        float mult = 1f + Mathf.Min(hitBonusMax, hitBonusPerLink * links);

        if (Time.unscaledTime >= counterUntil || enemy == null)
            return mult;

        // KARŞILIK vuruşu.
        counterUntil = 0f;

        mult = Mathf.Min(counterMax, counterMultiplier + counterPerLink * links);

        Link();

        PlayCounterFx(enemy);

        if (WindCharge.Instance != null)
            WindCharge.Instance.AddCharge(enemy, counterCharge);

        return mult;
    }

    private void PlayCounterFx(EnemyBase enemy)
    {
        Vector3 at = enemy.transform.position + Vector3.up * 1f;

        NoCallout.PopupAbove(enemy, "KARŞILIK", Gold, 0.8f);

        HitStop.Request(0.1f, 0.05f);

        PlayerController p = Player();

        if (p != null && p.impulseSource != null)
            p.impulseSource.GenerateImpulse(0.7f);

        if (WindCharge.Instance != null)
            WindCharge.Instance.PlayBurst(enemy, 3);

        if (PlayerAbility.Instance != null)
            PlayerAbility.Instance.SpawnRingFx(at, 3f, new Color(1f, 0.9f, 0.5f, 0.9f));
    }
}
