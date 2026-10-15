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
