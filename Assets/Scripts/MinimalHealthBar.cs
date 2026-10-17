using UnityEngine;

/// <summary>
/// ÇOK SADE CAN ÇUBUĞU: sol üstte incecik beyaz bir çubuk. Hasar alınca kısa süre soluk bir iz kalır.
/// Oyun başlayınca kendiliğinden kurulur.
/// </summary>
public class MinimalHealthBar : MonoBehaviour
{
    [Header("Yerleşim (1080p'ye göre piksel; ekranla ölçeklenir)")]
    public float marginX = 28f;
    public float marginY = 36f;   // ekranin altindan
    public float width = 280f;
    public float height = 4f;

    [Header("Renk")]
    public Color fillColor = new Color(1f, 1f, 1f, 0.95f);
    public Color backColor = new Color(1f, 1f, 1f, 0.16f);
    public Color trailColor = new Color(1f, 1f, 1f, 0.38f);

    /// <summary>Son cizilen can cubugu dikdortgeni (IMGUI koordinati). Iksir noktalari bunun yanina cizilir.</summary>
    public static Rect LastRect;
    public static bool HasRect;

    Health health;
    float shown = 1f, trail = 1f, trailHold;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindAnyObjectByType<MinimalHealthBar>() != null)
            return;

        var go = new GameObject("MinimalHealthBar");
        DontDestroyOnLoad(go);
        go.AddComponent<MinimalHealthBar>();
    }

    void Update()
    {
        if (health == null)
        {
            var p = FindFirstObjectByType<PlayerController>();
            if (p != null) health = p.GetComponent<Health>();
            if (health == null) return;
            shown = trail = Percent();
        }

        float target = Percent();
        float dt = Time.unscaledDeltaTime;

        if (target < shown - 0.0005f)
        {
            shown = target;
            trailHold = Time.unscaledTime + 0.4f;
        }
        else
        {
            shown = Mathf.MoveTowards(shown, target, dt * 1.2f);   // iyileşme yumuşak dolsun
        }

        if (trail < shown) trail = shown;
        else if (Time.unscaledTime >= trailHold) trail = Mathf.MoveTowards(trail, shown, dt * 0.7f);
    }

    float Percent()
    {
        if (health == null || health.MaxHealth <= 0) return 0f;
        return Mathf.Clamp01((float)health.CurrentHealth / health.MaxHealth);
    }

    // IMGUI yerine HudCanvas (overlay canvas): Game view'da UI'nin altinda kalmaz, ekrana gore her karede konumlanir.
    void LateUpdate()
    {
        if (health == null || MainMenu.Active) return;

        float s = HudCanvas.Scale();
        float w = width * s, h = Mathf.Max(2f, height * s);
        // SOL ALT: marginY = ekranin altindan uzaklik; sinematik serit alttan da yukseldigi icin bar yukari kayar.
        Rect sa = HudCanvas.SafeArea();
        float x = sa.x + marginX * s, y = sa.yMax - marginY * s - h - RunCameraZoom.TopBarPixels;
        w = Mathf.Min(w, sa.width - marginX * s * 2f);

        LastRect = new Rect(x, y, w, h);
        HasRect = true;

        HudCanvas.Fill(new Rect(x, y, w, h), backColor);
        HudCanvas.Fill(new Rect(x, y, w * trail, h), trailColor);
        HudCanvas.Fill(new Rect(x, y, w * shown, h), fillColor);
    }
}
