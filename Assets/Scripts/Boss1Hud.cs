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
    [Range(0.03f, 0.2f)] public float nameSize = 0.1f;
    public Color nameColor = new Color(0.96f, 0.93f, 0.88f, 1f);

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

    void DrawName()
    {
        if (nameAlpha <= 0.01f || string.IsNullOrEmpty(bossName)) return;

        if (nameStyle == null)
            nameStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Normal, wordWrap = false, clipping = TextClipping.Overflow };
        Font f = GameFont.Get();
        if (f != null) nameStyle.font = f;

        float pop = 1f + 0.12f * Mathf.Exp(-nameAge * 3.5f);     // belirirken hafif buyuk -> oturur
        nameStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(Screen.height * nameSize * pop));

        Rect r = new Rect(0f, 0f, Screen.width, Screen.height);       // tam ekran orta
        float o = Mathf.Max(2f, Screen.height * 0.004f);
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.85f * nameAlpha);
        GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), bossName, nameStyle);
        GUI.Label(new Rect(r.x - o, r.y + o, r.width, r.height), bossName, nameStyle);
        GUI.color = new Color(nameColor.r, nameColor.g, nameColor.b, nameAlpha);
        GUI.Label(r, bossName, nameStyle);
        GUI.color = old;
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
