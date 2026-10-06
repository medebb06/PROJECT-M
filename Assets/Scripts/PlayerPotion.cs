using UnityEngine;

/// <summary>
/// CAN İKSİRİ. F'ye BASILI TUT: dolum çubuğu dolunca can yenilenir ve 1 hak harcanır.
/// 3 hak vardır; bitince iksir boştur. Hasar alırsan, dash/zıplarsan, saldırır/savunursan
/// ya da tuşu bırakırsan içme İPTAL olur (hak harcanmaz).
/// Kendi kendini kurar; arayüzü (OnGUI) de kendisi çizer (can barının yanında).
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
    [Min(0.3f)] public float uiScale = 1f;

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

    private GUIStyle keyStyle;
    private GUIStyle countStyle;

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

        if (RunUI.MenuVisible)
            return false;

        if (RunManager.Instance != null && RunManager.Instance.IsPaused)
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

        bool held = Input.GetKey(KeyCode.F);

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
            Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.LeftShift) ||
            Input.GetMouseButton(0) ||
            Input.GetMouseButton(1)
        )
        {
            Cancel();
            return;
        }

        // Başlangıç: boşsa / canı doluysa başlama (geri bildirim ver).
        if (!drinking)
        {
            if (!Input.GetKeyDown(KeyCode.F))
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

    // =========================================================
    // ARAYÜZ
    // =========================================================

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || health == null)
            return;

        if (RunUI.MenuVisible)
            return;

        if (keyStyle == null)
        {
            keyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };

            countStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
        }

        float scale = GameSettings.GuiScale(uiScale);

        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;

        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        float screenH = Screen.height / scale;
        float now = Time.unscaledTime;

        // Can barı (PlayerHud) ile aynı satır; yetenek yuvasının sağında.
        const float margin = 18f;
        const float bottomExtra = 18f;
        const float barWidth = 230f;
        const float size = 38f;

        float x = margin + barWidth + 92f + 38f + 14f;
        float y = screenH - margin - bottomExtra - size + 9f;

        // Reddedilince sağa sola titre.
        if (now < deniedUntil)
            x += Mathf.Sin(now * 70f) * 2.5f;

        bool empty = Charges <= 0;
        bool full = health.CurrentHealth >= health.MaxHealth;

        Color liquid = new Color(0.4f, 1f, 0.5f);

        // Arka plan
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(x - 2f, y - 2f, size + 4f, size + 4f), Texture2D.whiteTexture);

        GUI.color = new Color(0.12f, 0.14f, 0.12f, 0.9f);
        GUI.DrawTexture(new Rect(x, y, size, size), Texture2D.whiteTexture);

        // Sıvı: içerken dolum, dururken hak oranı kadar sabit seviye.
        float level = drinking ? progress : (Charges / (float)Mathf.Max(1, maxCharges));

        Color fill = empty
            ? new Color(0.35f, 0.35f, 0.35f, 0.8f)
            : (drinking ? liquid : new Color(0.25f, 0.7f, 0.35f, 0.85f));

        GUI.color = fill;
        GUI.DrawTexture(new Rect(x, y + size * (1f - level), size, size * level), Texture2D.whiteTexture);

        // İçme parlaması
        if (now < flashUntil)
        {
            float a = (flashUntil - now) / 0.5f;
            GUI.color = new Color(0.7f, 1f, 0.75f, 0.6f * a);
            GUI.DrawTexture(new Rect(x - 3f, y - 3f, size + 6f, size + 6f), Texture2D.whiteTexture);
        }

        // Çerçeve
        Color border = drinking
            ? liquid
            : (now < deniedUntil ? new Color(1f, 0.3f, 0.3f) : new Color(1f, 1f, 1f, empty ? 0.2f : 0.55f));

        GUI.color = border;
        GUI.DrawTexture(new Rect(x - 2f, y - 2f, size + 4f, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - 2f, y + size, size + 4f, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - 2f, y, 2f, size), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + size, y, 2f, size), Texture2D.whiteTexture);

        // Tuş harfi
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.Label(new Rect(x + 1f, y + 1f, size, size), "F", keyStyle);
        GUI.color = empty ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
        GUI.Label(new Rect(x, y, size, size), "F", keyStyle);

        // Hak sayısı (sağ alt köşe)
        GUI.color = new Color(0f, 0f, 0f, 0.9f);
        GUI.Label(new Rect(x + size - 14f + 1f, y + size - 16f + 1f, 16f, 16f), Charges.ToString(), countStyle);
        GUI.color = empty ? new Color(1f, 0.4f, 0.4f) : Color.white;
        GUI.Label(new Rect(x + size - 14f, y + size - 16f, 16f, 16f), Charges.ToString(), countStyle);

        // Hak noktaları (altta 3 küçük nokta)
        for (int i = 0; i < maxCharges; i++)
        {
            GUI.color = i < Charges ? liquid : new Color(1f, 1f, 1f, 0.2f);
            GUI.DrawTexture(new Rect(x + 4f + i * 9f, y + size + 5f, 6f, 4f), Texture2D.whiteTexture);
        }

        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
    }
}
