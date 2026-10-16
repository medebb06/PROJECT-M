using UnityEngine;

/// <summary>
/// Oyuncunun POSTURE (blok) bari: sol altta, execute (RageMeter) barinin hemen altinda, TURUNCU.
/// Kirilinca bar kirmizi-turuncu yanip soner ve tamamen dolana kadar blok/parry yapilamaz.
/// Oyun baslayinca kendiliginden kurulur.
/// </summary>
public class PlayerPostureBar : MonoBehaviour
{
    public float height = 7f;
    public float gap = 7f;

    PlayerPosture posture;
    float shown = 1f;
    float flash;
    bool wasLocked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindAnyObjectByType<PlayerPostureBar>() != null) return;
        GameObject go = new GameObject("PlayerPostureBar");
        DontDestroyOnLoad(go);
        go.AddComponent<PlayerPostureBar>();
    }

    void Update()
    {
        if (posture == null)
        {
            posture = FindFirstObjectByType<PlayerPosture>();
            if (posture == null) return;
        }

        float target = posture.MaxPosture > 0 ? posture.CurrentPosture / (float)posture.MaxPosture : 0f;
        shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 3f);

        if (posture.BlockLocked && !wasLocked) flash = 1f;
        if (!posture.BlockLocked && wasLocked) flash = 0.8f;   // acildi
        wasLocked = posture.BlockLocked;
        flash = Mathf.Max(0f, flash - Time.unscaledDeltaTime * 2.5f);
    }

    void LateUpdate()
    {
        if (posture == null || MainMenu.Active) return;

        float s = HudCanvas.Scale();
        float mx = 28f, w = 280f, rh = 4f, my = 26f;

        RageMeter rm = RageMeter.Instance;
        if (rm != null) { mx = rm.marginX; w = rm.width; rh = rm.height; my = rm.marginY; }

        float rageH = Mathf.Max(2f, rh * s);
        Rect sa = HudCanvas.SafeArea();
        float x = sa.x + mx * s;
        float rageY = sa.yMax - my * s - rageH - RunCameraZoom.TopBarPixels;
        float h = Mathf.Max(3f, height * s);
        float y = rageY + rageH + gap * s;
        float bw = Mathf.Min(w * s, sa.width - mx * s * 2f);

        bool locked = posture.BlockLocked;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 12f);
        bool full = posture.CurrentPosture >= posture.MaxPosture && !locked;

        // zemin
        HudCanvas.Fill(new Rect(x - 1f * s, y - 1f * s, bw + 2f * s, h + 2f * s), new Color(0.12f, 0.05f, 0.0f, full ? 0.35f : 0.7f));

        // dolgu
        Color fill = locked
            ? Color.Lerp(new Color(0.75f, 0.15f, 0.05f), new Color(1f, 0.45f, 0.1f), pulse)
            : new Color(1f, 0.55f, 0.08f, full ? 0.55f : 1f);
        HudCanvas.Fill(new Rect(x, y, bw * Mathf.Clamp01(shown), h), fill);

        // dusuk posture: ince parlak uc
        if (!full)
        {
            HudCanvas.Fill(new Rect(x + bw * Mathf.Clamp01(shown) - 2f * s, y, 2f * s, h), new Color(1f, 0.85f, 0.5f, locked ? 0.9f : 0.6f));
        }

        // kilit/acilma flasi
        if (flash > 0f)
        {
            HudCanvas.Fill(new Rect(x - 4f * s, y - 4f * s, bw + 8f * s, h + 8f * s), new Color(1f, locked ? 0.3f : 0.9f, locked ? 0.1f : 0.5f, flash * 0.6f));
        }
    }
}
