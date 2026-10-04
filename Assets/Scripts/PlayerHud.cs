using UnityEngine;

/// <summary>
/// OYUNCU ARAYÜZÜ: ekranın SOL ALT köşesinde sabit CAN ve İNFAZ barları.
///   CAN   : kırmızı; kaybedilen kısım beyaz iz bırakıp yetişir, iyileşme yeşil
///           parlar, can azken yanıp söner. Üstünde "73 / 100".
///   İNFAZ : altın; 3 bölmeli (≈ 3 öldürme). Dolunca parlar + "[E] İNFAZ".
///
/// KURULUM YOK: kendiliğinden oluşur. Eski can barlarını (HealthBarUI /
/// HealthUnitsUI) gizler ('Hide Old Health UI').
/// </summary>
public class PlayerHud : MonoBehaviour
{
    [Header("Yerleşim")]
    [Tooltip("Ekran yüksekliği 720'ye göre ölçek.")]
    [Min(0.3f)] public float uiScale = 1f;

    public float margin = 18f;

    [Tooltip("Alt kenardan ek boşluk (alttaki bilgi satırı için).")]
    public float bottomExtra = 18f;

    public float barWidth = 230f;
    public float healthHeight = 16f;
    public float executeHeight = 9f;

    [Header("Renkler")]
    public Color healthColor = new Color(0.86f, 0.2f, 0.22f);
    public Color lowHealthColor = new Color(1f, 0.35f, 0.3f);
    public Color trailColor = new Color(1f, 0.95f, 0.9f, 0.9f);
    public Color healColor = new Color(0.45f, 1f, 0.5f);
    public Color executeColor = new Color(1f, 0.78f, 0.25f);
    public Color backColor = new Color(0f, 0f, 0f, 0.6f);

    [Tooltip("Sahnedeki eski can barlarını gizle.")]
    public bool hideOldHealthUI = true;

    private Health health;
    private float shownHealth = 1f;   // yumuşak
    private float trail = 1f;
    private float trailHold;
    private float healFlashUntil;
    private bool oldHidden;

    private GUIStyle numberStyle;
    private GUIStyle labelStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<PlayerHud>() != null)
            return;

        new GameObject("PlayerHud").AddComponent<PlayerHud>();
    }

    private void Update()
    {
        if (health == null)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();

            if (player != null)
                health = player.GetComponent<Health>();

            if (health == null)
                return;

            shownHealth = trail = Percent();
        }

        if (hideOldHealthUI && !oldHidden)
            HideOld();

        float p = Percent();
        float dt = Time.unscaledDeltaTime;

        if (p > shownHealth + 0.001f)
        {
            healFlashUntil = Time.unscaledTime + 0.5f;
            shownHealth = Mathf.MoveTowards(shownHealth, p, dt * 1.5f);
            trail = shownHealth;
        }
        else if (p < shownHealth - 0.001f)
        {
            shownHealth = p;
            trailHold = Time.unscaledTime + 0.45f;
        }

        if (trail < shownHealth)
            trail = shownHealth;
        else if (Time.unscaledTime >= trailHold)
            trail = Mathf.MoveTowards(trail, shownHealth, dt * 0.8f);
    }

    private float Percent()
    {
        if (health == null || health.MaxHealth <= 0)
            return 0f;

        return Mathf.Clamp01((float)health.CurrentHealth / health.MaxHealth);
    }

    private void HideOld()
    {
        oldHidden = true;

        foreach (HealthBarUI ui in FindObjectsByType<HealthBarUI>(FindObjectsSortMode.None))
        {
            if (ui != null)
                ui.gameObject.SetActive(false);
        }

        foreach (HealthUnitsUI ui in FindObjectsByType<HealthUnitsUI>(FindObjectsSortMode.None))
        {
            if (ui != null)
                ui.gameObject.SetActive(false);
        }
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || health == null)
            return;

        // Menülerin (RunUI) arkasında, düşman çubuklarının önünde.
        GUI.depth = 8;

        if (numberStyle == null)
        {
            numberStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
        }

        float scale = Screen.height / 720f * uiScale;

        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;

        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        float screenH = Screen.height / scale;

        float x = margin;
        float total = healthHeight + 6f + executeHeight;
        float y = screenH - margin - bottomExtra - total;

        float now = Time.unscaledTime;

        // ---------------- PANEL ----------------

        GUI.color = new Color(0f, 0f, 0f, 0.35f);
        GUI.DrawTexture(new Rect(x - 6f, y - 6f, barWidth + 12f, total + 12f), Texture2D.whiteTexture);

        // ---------------- CAN ----------------

        GUI.color = backColor;
        GUI.DrawTexture(new Rect(x - 1f, y - 1f, barWidth + 2f, healthHeight + 2f), Texture2D.whiteTexture);

        if (trail > shownHealth)
        {
            GUI.color = trailColor;
            GUI.DrawTexture(new Rect(x, y, barWidth * trail, healthHeight), Texture2D.whiteTexture);
        }

        bool low = shownHealth <= 0.25f;
        bool blink = low && Mathf.Repeat(now * 3f, 1f) < 0.5f;

        Color hc = now < healFlashUntil ? healColor : (blink ? lowHealthColor : healthColor);

        GUI.color = hc;
        GUI.DrawTexture(new Rect(x, y, barWidth * shownHealth, healthHeight), Texture2D.whiteTexture);

        // Üst parlaklık şeridi.
        GUI.color = new Color(1f, 1f, 1f, 0.18f);
        GUI.DrawTexture(new Rect(x, y, barWidth * shownHealth, healthHeight * 0.35f), Texture2D.whiteTexture);

        string text = Mathf.Max(0, health.CurrentHealth) + " / " + health.MaxHealth;

        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(x + 1f, y + 1f, barWidth, healthHeight), text, numberStyle);
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, barWidth, healthHeight), text, numberStyle);

        // ---------------- İNFAZ ----------------

        y += healthHeight + 6f;

        ExecuteMeter meter = ExecuteMeter.Instance;

        float fill = meter != null ? meter.Fill : 0f;
        bool full = meter != null && meter.IsFull;

        GUI.color = backColor;
        GUI.DrawTexture(new Rect(x - 1f, y - 1f, barWidth + 2f, executeHeight + 2f), Texture2D.whiteTexture);

        Color ec = executeColor;

        if (full)
        {
            float pulse = 0.75f + 0.25f * Mathf.Sin(now * 6f);
            ec = Color.Lerp(executeColor, Color.white, 0.35f * pulse);
        }
        else
        {
            ec.a = 0.85f;
        }

        GUI.color = ec;
        GUI.DrawTexture(new Rect(x, y, barWidth * fill, executeHeight), Texture2D.whiteTexture);

        // Bölme çizgileri (≈ öldürme başına bir bölme).
        GUI.color = new Color(0f, 0f, 0f, 0.7f);

        for (int i = 1; i < 3; i++)
            GUI.DrawTexture(new Rect(x + barWidth * i / 3f - 1f, y, 2f, executeHeight), Texture2D.whiteTexture);

        // Dolunca kısa parlama.
        if (meter != null && now - meter.LastFilledTime < 0.4f)
        {
            float a = 1f - (now - meter.LastFilledTime) / 0.4f;

            GUI.color = new Color(1f, 1f, 1f, 0.6f * a);
            GUI.DrawTexture(new Rect(x - 3f, y - 3f, barWidth + 6f, executeHeight + 6f), Texture2D.whiteTexture);
        }

        // Etiket.
        GUI.color = full ? executeColor : new Color(0.8f, 0.8f, 0.8f, 0.7f);
        GUI.Label(new Rect(x + barWidth + 10f, y - 4f, 140f, executeHeight + 8f), full ? "[E] İNFAZ" : "İNFAZ", labelStyle);

        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
    }
}
