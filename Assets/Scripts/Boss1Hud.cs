using UnityEngine;

/// <summary>
/// Boss1 arayuzu: oyuncunun sol alttaki sade cizgisiyle (MinimalHealthBar) ayni stil, ekranin UST ORTASINDA.
/// Ust cizgi = can (dusunce soluk iz kalir), altindaki ince cizgi = posture (denge). Denge kirilinca posture cizgisi tam parlar.
/// Boss1Enemy runtime'da olusturur; sahne/prefab kurulumu gerekmez.
/// </summary>
public class Boss1Hud : MonoBehaviour
{
    [Header("Yerlesim (1080p'ye gore piksel; ekranla olceklenir)")]
    public float width = 640f;
    public float marginTop = 36f;
    public float healthHeight = 2f;
    public float postureHeight = 1.5f;
    public float gap = 6f;
    [Tooltip("Giris sahnesi bitene kadar false: bar gorunmez.")] public bool show;

    [Header("Renk (oyuncu cubuguyla ayni)")]
    public Color fillColor = new Color(1f, 1f, 1f, 0.95f);
    public Color backColor = new Color(1f, 1f, 1f, 0.16f);
    public Color trailColor = new Color(1f, 1f, 1f, 0.38f);
    public Color postureColor = new Color(1f, 1f, 1f, 0.7f);

    [Header("Boss adi (giriste ekranin ortasinda)")]
    [Tooltip("Ekran yuksekligine oranla yazi boyutu.")]
    [Range(0.03f, 0.4f)] public float nameSize = 0.22f;
    [Tooltip("Harf araligi carpani (1 = fontun kendi araligi, kucuk = sik).")]
    [Range(0.5f, 1.2f)] public float nameTracking = 0.82f;
    public Color nameColor = new Color(0.93f, 0.86f, 0.68f, 1f);

    string bossName;
    bool nameOn;
    float nameAlpha, nameAge;
    GUIStyle nameStyle;

    /// <summary>Ekranin ortasinda buyuk boss adini gosterir (yumusakca belirir).</summary>
    public void ShowName(string n)
    {
        bossName = n;
        nameOn = !string.IsNullOrEmpty(n);
        nameAge = 0f;
    }

    public void HideName() { nameOn = false; }

    Health health;
    EnemyBalance balance;
    float shown = 1f, trail = 1f, trailHold;
    float post;
    float alpha = 1f;

    public static Boss1Hud Create(Health h, EnemyBalance b)
    {
        GameObject go = new GameObject("Boss1HUD");
        Boss1Hud hud = go.AddComponent<Boss1Hud>();
        hud.health = h;
        hud.balance = b;
        return hud;
    }

    float HealthPercent()
    {
        if (health == null || health.MaxHealth <= 0) return 0f;
        return Mathf.Clamp01((float)health.CurrentHealth / health.MaxHealth);
    }

    void Update()
    {
        if (health == null) return;

        float dt = Time.unscaledDeltaTime;
        nameAlpha = Mathf.MoveTowards(nameAlpha, nameOn ? 1f : 0f, dt * (nameOn ? 2.2f : 1.4f));
        if (nameOn) nameAge += dt;
        float target = HealthPercent();

        if (target < shown - 0.0005f)
        {
            shown = target;
            trailHold = Time.unscaledTime + 0.4f;
        }
        else
        {
            shown = Mathf.MoveTowards(shown, target, dt * 1.2f);
        }

        if (trail < shown) trail = shown;
        else if (Time.unscaledTime >= trailHold) trail = Mathf.MoveTowards(trail, shown, dt * 0.7f);

        if (balance != null && balance.MaxBalance > 0)
        {
            float p = balance.IsBroken ? 1f : Mathf.Clamp01((float)balance.CurrentBalance / balance.MaxBalance);
            post = Mathf.MoveTowards(post, p, dt * 4f);
        }

        float a = health.IsDead || !show ? 0f : 1f;
        if (MainMenu.Active) a = 0f;
        alpha = Mathf.MoveTowards(alpha, a, dt * 1.5f);
    }

    Texture2D bandTex;

    // Elden Ring "ENEMY FELLED" tarzi: arkada yumusak koyu serit, cok buyuk, sik harfli (tracking < 1),
    // yavasca buyuyen krem renkli yazi. OnGUI harf araligi desteklemez: harfler tek tek yerlestirilir.
    void DrawName()
    {
        if (nameAlpha <= 0.01f || string.IsNullOrEmpty(bossName)) return;

        if (nameStyle == null)
        {
            nameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Normal, wordWrap = false,
                clipping = TextClipping.Overflow, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0)
            };
        }
        Font f = GameFont.Get();
        if (f != null) nameStyle.font = f;

        if (bandTex == null)
        {
            bandTex = new Texture2D(1, 64, TextureFormat.RGBA32, false);
            bandTex.wrapMode = TextureWrapMode.Clamp;
            for (int i = 0; i < 64; i++)
            {
                float v = Mathf.Sin(i / 63f * Mathf.PI);
                bandTex.SetPixel(0, i, new Color(0f, 0f, 0f, Mathf.Pow(v, 0.7f)));
            }
            bandTex.Apply();
        }

        string txt = bossName.ToUpper(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
        float grow = 1f + 0.07f * Mathf.Clamp01(nameAge / 4f) + 0.12f * Mathf.Exp(-nameAge * 4f);
        float fs = Screen.height * nameSize * grow;

        // Genislik siginmazsa kucult.
        float[] w = new float[txt.Length];
        float total = MeasureName(txt, fs, w);
        float limit = Screen.width * 0.94f;
        if (total > limit) { float k = limit / total; fs *= k; total = MeasureName(txt, fs, w); }

        float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
        float h = fs * 1.35f;

        Color old = GUI.color;

        // Arka serit
        float bandH = h * 1.9f;
        GUI.color = new Color(1f, 1f, 1f, 0.72f * nameAlpha);
        GUI.DrawTexture(new Rect(0f, cy - bandH * 0.5f, Screen.width, bandH), bandTex);

        // Harfler
        float x = cx - total * 0.5f;
        float o = Mathf.Max(2f, fs * 0.025f);
        for (int i = 0; i < txt.Length; i++)
        {
            string ch = txt[i].ToString();
            if (txt[i] != ' ')
            {
                Rect r = new Rect(x, cy - h * 0.5f, w[i] + 4f, h);
                GUI.color = new Color(0f, 0f, 0f, 0.9f * nameAlpha);
                GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), ch, nameStyle);
                GUI.color = new Color(nameColor.r, nameColor.g, nameColor.b, nameAlpha);
                GUI.Label(r, ch, nameStyle);
            }
            x += w[i] * nameTracking;
        }
        GUI.color = old;
    }

    float MeasureName(string txt, float fs, float[] w)
    {
        nameStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(fs));
        float total = 0f;
        for (int i = 0; i < txt.Length; i++)
        {
            w[i] = txt[i] == ' ' ? fs * 0.35f : nameStyle.CalcSize(new GUIContent(txt[i].ToString())).x;
            total += w[i] * nameTracking;
        }
        return total;
    }

    void OnGUI()
    {
        if (Event.current.type == EventType.Repaint) DrawName();
        if (Event.current.type != EventType.Repaint || health == null || alpha <= 0.01f) return;

        float s = Screen.height / 1080f * MobileInput.HudMul;
        float w = width * s;
        float hh = Mathf.Max(1f, healthHeight * s);
        float ph = Mathf.Max(1f, postureHeight * s);
        float x = (Screen.width - w) * 0.5f;
        float y = marginTop * s + RunCameraZoom.TopBarPixels;
        Texture2D t = Texture2D.whiteTexture;

        Color a(Color c) { c.a *= alpha; return c; }

        GUI.color = a(backColor);  GUI.DrawTexture(new Rect(x, y, w, hh), t);
        GUI.color = a(trailColor); GUI.DrawTexture(new Rect(x, y, w * trail, hh), t);
        GUI.color = a(fillColor);  GUI.DrawTexture(new Rect(x, y, w * shown, hh), t);

        float y2 = y + hh + gap * s;
        bool broken = balance != null && balance.IsBroken;
        GUI.color = a(backColor); GUI.DrawTexture(new Rect(x, y2, w, ph), t);
        GUI.color = a(broken ? fillColor : postureColor); GUI.DrawTexture(new Rect(x, y2, w * post, ph), t);

        GUI.color = Color.white;
    }
}
