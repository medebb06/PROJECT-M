using UnityEngine;

/// <summary>
/// OYUNCU ARAYÜZÜ: ekranın SOL ALT köşesinde sabit CAN ve İNFAZ barları.
///   CAN   : kırmızı; kaybedilen kısım beyaz iz bırakıp yetişir, iyileşme yeşil
///           parlar, can azken yanıp söner. Üstünde "73 / 100".
///   İNFAZ : altın; 3 bölmeli (≈ 3 öldürme). Dolunca parlar + "[E] İNFAZ".
///   YETENEK: barların sağında [Q] yuvası; bekleme süresi aşağıdan dolar,
///           hazırken yeteneğin renginde parlar. Seviye noktaları altta.
/// Ana menüde (Lobi) görünmez. Boyut: Ayarlar → Arayüz boyutu.
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
    private GUIStyle glyphStyle;
    private GUIStyle keyStyle;

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

        // Ana menü açıkken gizle (RunManager olsa da olmasa da).
        if (RunUI.MenuVisible)
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

            glyphStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            keyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }

        float scale = GameSettings.GuiScale(uiScale);

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

        // ---------------- SERİ ----------------

        KillStreak streak = KillStreak.Instance;

        if (streak != null && streak.Count >= 2)
        {
            float sy = y - 30f;
            float pop = Mathf.Clamp01(streak.TimeLeft01);

            string st = "SERİ ×" + streak.Count;

            if (streak.DamageBonus > 0f)
                st += "   +" + Mathf.RoundToInt(streak.DamageBonus * 100f) + "% HASAR";

            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(x + 1f, sy + 1f, barWidth + 120f, 16f), st, labelStyle);
            GUI.color = Color.Lerp(new Color(1f, 0.55f, 0.25f), new Color(1f, 0.9f, 0.4f), pop);
            GUI.Label(new Rect(x, sy, barWidth + 120f, 16f), st, labelStyle);

            // Kalan süre çizgisi.
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(x, sy + 16f, barWidth * 0.5f, 3f), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.75f, 0.3f, 0.9f);
            GUI.DrawTexture(new Rect(x, sy + 16f, barWidth * 0.5f * pop, 3f), Texture2D.whiteTexture);
        }

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
        bool full = meter != null && meter.FullSegments >= 1;

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

        // Odakta harcanacak parçalar yanıp söner (önizleme).
        int spending = PlayerFinisher.ChargingSegments;

        if (spending > 0 && meter != null)
        {
            float from = Mathf.Max(0f, fill - spending / (float)ExecuteMeter.Segments);

            GUI.color = new Color(1f, 1f, 1f, 0.4f + 0.4f * Mathf.Sin(now * 18f));
            GUI.DrawTexture(new Rect(x + barWidth * from, y, barWidth * (fill - from), executeHeight), Texture2D.whiteTexture);
        }

        // Bölme çizgileri (her parça bir infaz hakkı).
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
        GUI.Label(new Rect(x + barWidth + 10f, y - 4f, 140f, executeHeight + 8f), full ? "[E] İNFAZ ×" + meter.FullSegments : "İNFAZ", labelStyle);

        // ---------------- YETENEK YUVASI ----------------

        DrawAbilitySlot(x + barWidth + 92f, y + executeHeight, now);

        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
    }

    // bottom: yuvanın alt kenarı (infaz barının altıyla hizalı).
    private void DrawAbilitySlot(float x, float bottom, float now)
    {
        PlayerAbility ability = PlayerAbility.Instance;

        if (ability == null || !ability.HasAbility)
            return;

        const float size = 38f;

        float y = bottom - size;

        Color c = AbilityInfo.Color(ability.Type);
        bool ready = ability.Ready;
        float charge = ability.Charge01;

        // Hazır olunca kısa büyüme.
        float pop = Mathf.Clamp01(1f - (now - ability.LastReadyTime) / 0.35f);
        float grow = pop * 4f;

        Rect r = new Rect(x - grow * 0.5f, y - grow * 0.5f, size + grow, size + grow);

        // Arka plan + çerçeve.
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), Texture2D.whiteTexture);

        // Dolum (aşağıdan yukarı).
        Color fillColor = c;
        fillColor.a = ready ? 0.9f : 0.45f;

        GUI.color = fillColor;
        GUI.DrawTexture(new Rect(r.x, r.yMax - r.height * charge, r.width, r.height * charge), Texture2D.whiteTexture);

        // Hazırken nabız gibi parlayan çerçeve.
        if (ready)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(now * 5f);

            GUI.color = new Color(1f, 1f, 1f, 0.25f + 0.35f * pulse);

            GUI.DrawTexture(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x - 2f, r.yMax, r.width + 4f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x - 2f, r.y, 2f, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax, r.y, 2f, r.height), Texture2D.whiteTexture);
        }

        // Kullanınca kısa beyaz flaş.
        float used = Mathf.Clamp01(1f - (now - ability.LastUsedTime) / 0.25f);

        if (used > 0f)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.7f * used);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
        }

        // İkon yazısı.
        string glyph = ready ? AbilityInfo.Glyph(ability.Type) : Mathf.CeilToInt(ability.CooldownLeft).ToString();

        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), glyph, glyphStyle);
        GUI.color = ready ? Color.white : new Color(0.85f, 0.85f, 0.85f);
        GUI.Label(r, glyph, glyphStyle);

        // [Q] üstte.
        GUI.color = ready ? c : new Color(0.7f, 0.7f, 0.7f, 0.8f);
        GUI.Label(new Rect(x, y - 15f, size, 14f), "[Q]", keyStyle);

        // Seviye noktaları altta.
        for (int i = 0; i < AbilityInfo.MaxLevel; i++)
        {
            GUI.color = i < ability.Level ? c : new Color(1f, 1f, 1f, 0.2f);
            GUI.DrawTexture(new Rect(x + size * 0.5f - 13f + i * 10f, y + size + 4f, 6f, 3f), Texture2D.whiteTexture);
        }
    }
}
