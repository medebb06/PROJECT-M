using UnityEngine;

/// <summary>
/// ODAK BARI: can çubuğunun hemen üstünde ince mor çubuk. Parry / öldürme / vuruşla dolar,
/// dolunca parlar (Q = Odak Kesiği). Oyun başlayınca kendiliğinden kurulur.
/// </summary>
public class FocusBar : MonoBehaviour
{
    [Header("Yerleşim (1080p'ye göre piksel)")]
    public float widthFactor = 0.6f;   // can çubuğunun genişliğine oranı
    public float height = 4f;
    public float gap = 8f;

    public Color fillColor = new Color(0.78f, 0.5f, 1f, 0.95f);
    public Color readyColor = new Color(1f, 0.85f, 1f, 1f);
    public Color backColor = new Color(0.78f, 0.5f, 1f, 0.16f);

    PlayerAbility ability;
    float shown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindAnyObjectByType<FocusBar>() != null)
            return;

        var go = new GameObject("FocusBar");
        DontDestroyOnLoad(go);
        go.AddComponent<FocusBar>();
    }

void LateUpdate()
    {
        if (MainMenu.Active || !MinimalHealthBar.HasRect)
            return;

        if (ability == null)
        {
            ability = PlayerAbility.Instance;

            if (ability == null)
            {
                var p = FindFirstObjectByType<PlayerController>();
                if (p != null) ability = PlayerAbility.Ensure(p.gameObject);
            }

            if (ability == null) return;
            shown = ability.FocusCharge01;
        }

        if (!ability.focusEnabled)
            return;

        shown = Mathf.MoveTowards(shown, ability.FocusCharge01, Time.unscaledDeltaTime * 2f);

        float s = HudCanvas.Scale();
        Rect hb = MinimalHealthBar.LastRect;
        float h = Mathf.Max(2f, height * s);
        float totalW = hb.width * widthFactor;
        float seg = 3;
        float gapPx = 4f * s;
        float w = (totalW - gapPx * (seg - 1)) / seg;
        float y = hb.y - gap * s - h;

        bool full = ability.FocusFull;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);

        for (int i = 0; i < 3; i++)
        {
            float x = hb.x + i * (w + gapPx);
            float k = Mathf.Clamp01(shown * seg - i);

            Color fc = fillColor;
            if (full) fc = Color.Lerp(fillColor, readyColor, pulse);
            else if (k >= 0.999f) fc = Color.Lerp(fillColor, readyColor, 0.35f);

            HudCanvas.Fill(new Rect(x, y, w, h), backColor);
            HudCanvas.Fill(new Rect(x, y, w * k, h), fc);
        }
    }
}
