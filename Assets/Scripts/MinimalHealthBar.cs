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

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || health == null) return;

        float s = Screen.height / 1080f * MobileInput.HudMul;
        float w = width * s, h = Mathf.Max(2f, height * s);
        // SOL ALT: marginY = ekranin altindan uzaklik; sinematik serit alttan da yukseldigi icin bar yukari kayar.
        float x = marginX * s, y = Screen.height - marginY * s - h - RunCameraZoom.TopBarPixels;
        Texture2D t = Texture2D.whiteTexture;

        GUI.color = backColor;  GUI.DrawTexture(new Rect(x, y, w, h), t);
        GUI.color = trailColor; GUI.DrawTexture(new Rect(x, y, w * trail, h), t);
        GUI.color = fillColor;  GUI.DrawTexture(new Rect(x, y, w * shown, h), t);
        GUI.color = Color.white;
    }
}
