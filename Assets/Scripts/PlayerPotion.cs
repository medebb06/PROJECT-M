using UnityEngine;

/// <summary>
/// CAN İKSİRİ. F'ye BASILI TUT: dolum çubuğu dolunca can yenilenir ve 1 hak harcanır.
/// 3 hak vardır; bitince iksir boştur. Hasar alırsan, dash/zıplarsan, saldırır/savunursan
/// ya da tuşu bırakırsan içme İPTAL olur (hak harcanmaz).
/// Kendi kendini kurar; arayüzü yok (UI kaldırıldı).
/// </summary>
public class PlayerPotion : MonoBehaviour
{
    [Header("İksir")]
    public int maxCharges = 3;

    [Tooltip("F'ye kaç saniye basılı tutulacak.")]
    public float drinkTime = 1.1f;

    [Tooltip("Maksimum canın yüzde kaçı yenilenir.")]
    [Range(0.05f, 1f)] public float healPercent = 0.4f;

    [Header("Arayüz")]

    public int Charges { get; private set; }

    /// <summary>Şu an F basılı tutularak iksir içiliyor mu? (Boss tepki verir.)</summary>
    public bool IsDrinking => drinking;

    public static PlayerPotion Instance { get; private set; }

    private PlayerController player;
    private Health health;
    private int lastHealth;
    private bool wasDead;

    private float progress;       // 0..1
    private bool drinking;
    private float flashUntil;     // iç çekme parlaması
    private float deniedUntil;    // boşken / canı doluyken basınca kırmızı sarsıntı

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (FindFirstObjectByType<PlayerPotion>() != null)
            return;

        GameObject go = new GameObject("PlayerPotion");
        DontDestroyOnLoad(go);
        go.AddComponent<PlayerPotion>();
    }

    private void Awake()
    {
        Instance = this;
        Charges = maxCharges;
    }

    private void Bind()
    {
        if (player == null)
            player = FindFirstObjectByType<PlayerController>();

        if (player == null)
            return;

        Health h = player.GetComponent<Health>();

        if (h == health)
            return;

        if (health != null)
            health.OnHealthChanged -= OnHealthChanged;

        health = h;

        if (health != null)
        {
            lastHealth = health.CurrentHealth;
            wasDead = health.IsDead;
            health.OnHealthChanged += OnHealthChanged;
            Charges = maxCharges;
        }
    }

    private void OnDestroy()
    {
        if (health != null)
            health.OnHealthChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(int current, int max)
    {
        // Hasar aldıysan içme bozulur.
        if (current < lastHealth)
            Cancel();

        lastHealth = current;
    }

    private void Cancel()
    {
        drinking = false;
        progress = 0f;
    }

    private bool CanDrink()
    {
        if (player == null || health == null || health.IsDead)
            return false;

        if (!player.canControl || player.inputLocked || player.isDashing)
            return false;

        return true;
    }

    [Header("Arayuz (can cubugunun yaninda kirmizi noktalar)")]
    public Color dotColor = new Color(0.9f, 0.08f, 0.1f, 0.95f);
    public float dotSize = 9f;       // 1080p'ye gore piksel
    public float dotGap = 7f;
    public float dotOffsetX = 16f;

    private void LateUpdate()
    {
        if (health == null || !MinimalHealthBar.HasRect || MainMenu.Active) return;

        float s = HudCanvas.Scale();
        Rect bar = MinimalHealthBar.LastRect;
        float d = dotSize * s, gap = dotGap * s;
        float x = bar.xMax + dotOffsetX * s;
        float y = bar.center.y - d * 0.5f;

        for (int i = 0; i < maxCharges; i++)
        {
            bool full = i < Charges;
            Color c = dotColor;

            if (!full) c.a = 0.16f;
            else if (drinking && i == Charges - 1)
                c.a *= Mathf.Lerp(1f, 0.25f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 14f) * progress);
            else if (Time.unscaledTime < deniedUntil)
                c.a *= 0.5f;

            if (Time.unscaledTime < flashUntil && full) c = Color.Lerp(c, Color.white, 0.5f);

            float px = x + i * (d + gap);
            // Yuvarlak gorunum: iki ust uste binen dikdortgen (kose kirpilmis).
            HudCanvas.Fill(new Rect(px + d * 0.2f, y, d * 0.6f, d), c);
            HudCanvas.Fill(new Rect(px, y + d * 0.2f, d, d * 0.6f), c);
        }
    }

    private void Update()
    {
        Bind();

        if (health == null)
            return;

        // Ölüp dirildiyse iksir yenilenir (yeni deneme).
        if (wasDead && !health.IsDead)
        {
            Charges = maxCharges;
            lastHealth = health.CurrentHealth;
        }

        wasDead = health.IsDead;

        if (Time.timeScale <= 0f)
            return;

        bool held = MobileInput.GetKey(KeyCode.F);

        if (!held)
        {
            Cancel();
            return;
        }

        if (!CanDrink())
        {
            Cancel();
            return;
        }

        // İçmeyi bozan eylemler.
        if (
            MobileInput.GetKeyDown(KeyCode.Space) ||
            MobileInput.GetKeyDown(KeyCode.LeftShift) ||
            MobileInput.GetMouseButton(0) ||
            MobileInput.GetMouseButton(1)
        )
        {
            Cancel();
            return;
        }

        // Başlangıç: boşsa / canı doluysa başlama (geri bildirim ver).
        if (!drinking)
        {
            if (!MobileInput.GetKeyDown(KeyCode.F))
                return; // tuş zaten basılıyken başka bir şeyden dönmüş; yeni basış bekle

            if (Charges <= 0 || health.CurrentHealth >= health.MaxHealth)
            {
                deniedUntil = Time.unscaledTime + 0.35f;
                return;
            }

            drinking = true;
            progress = 0f;
        }

        progress += Time.deltaTime / Mathf.Max(0.1f, drinkTime);

        if (progress >= 1f)
        {
            int amount = Mathf.Max(1, Mathf.RoundToInt(health.MaxHealth * healPercent));

            health.Heal(amount);
            Charges = Mathf.Max(0, Charges - 1);

            flashUntil = Time.unscaledTime + 0.5f;

            Debug.Log("İKSİR → +" + amount + " can | kalan hak: " + Charges);

            drinking = false;
            progress = 0f;
        }
    }
}
