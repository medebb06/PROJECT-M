using UnityEngine;

// KUROYORU modu: her 20 öldürmede oyun durur, 4 charm'ın +%2'lik versiyonundan biri seçilir.
// Aynı charm tekrar tekrar seçilebilir (yığılır). Seçim: 1-4 tuşu ya da tıkla.
public class KuroyoruPicker : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    private static KuroyoruPicker inst;

    private Font font;
    private float openedAt;
    private bool prevCanControl = true;
    private PlayerController player;

    public static void Open()
    {
        if (IsOpen) return;
        if (inst == null)
            inst = new GameObject("~KuroyoruPicker").AddComponent<KuroyoruPicker>();
        inst.DoOpen();
    }

    private void DoOpen()
    {
        font = GameFont.Or(Resources.Load<Font>("Pixeltype"));
        IsOpen = true;
        openedAt = Time.unscaledTime;
        Time.timeScale = 0f;
        player = FindFirstObjectByType<PlayerController>();
        if (player != null) { prevCanControl = player.canControl; player.canControl = false; }
    }

    private void Close()
    {
        IsOpen = false;
        Time.timeScale = 1f;
        if (player != null) player.canControl = prevCanControl;
    }

    private void OnDestroy()
    {
        IsOpen = false;
        if (inst == this) inst = null;
    }

    private void Update()
    {
        if (!IsOpen) return;
        Time.timeScale = 0f;   // hit-stop / slow-mo zamanı geri açmasın
        if (Time.unscaledTime - openedAt < 0.35f) return;
        for (int i = 0; i < 4; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) { Pick((CharmType)i); return; }
    }

    private void Pick(CharmType t)
    {
        Charms.AddStack(t);
        Close();
    }

    private GUIStyle Style(int size, Color c, TextAnchor a)
    {
        GUIStyle st = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = a };
        if (font != null) st.font = font;
        st.normal.textColor = c;
        return st;
    }

    private void OnGUI()
    {
        if (!IsOpen) return;

        float w = 760f, rowH = 84f, h = 130f + rowH * 4f;
        Rect panel = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        Color old = GUI.color;

        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = new Color(0f, 0f, 0f, 0.88f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = old;

        GUI.Label(new Rect(panel.x, panel.y + 12f, w, 40f), "KUROYORU  -  CHARM SEÇ", Style(34, Color.white, TextAnchor.MiddleCenter));
        GUI.Label(new Rect(panel.x, panel.y + 54f, w, 24f), "1-4 ya da tıkla   •   her seçim +%" + Mathf.RoundToInt(Charms.PerPick * 100f) + ", yığılır", Style(18, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter));

        bool ready = Time.unscaledTime - openedAt >= 0.35f;
        for (int i = 0; i < 4; i++)
        {
            CharmType t = (CharmType)i;
            Color tc = Charms.Tint(t);
            Rect r = new Rect(panel.x + 20f, panel.y + 100f + rowH * i, w - 40f, rowH - 8f);
            bool hover = r.Contains(Event.current.mousePosition);

            GUI.color = new Color(tc.r, tc.g, tc.b, hover ? 0.45f : 0.22f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;

            int n = Charms.Stacks(t);
            GUI.Label(new Rect(r.x + 12f, r.y, 40f, r.height), (i + 1).ToString(), Style(30, Color.white, TextAnchor.MiddleLeft));
            GUI.Label(new Rect(r.x + 56f, r.y + 6f, 360f, 30f), Charms.Name(t) + " +%2", Style(28, tc, TextAnchor.MiddleLeft));
            GUI.Label(new Rect(r.x + 56f, r.y + 38f, r.width - 190f, 26f), Charms.KDesc(t), Style(18, new Color(1f, 1f, 1f, 0.78f), TextAnchor.MiddleLeft));
            GUI.Label(new Rect(r.xMax - 150f, r.y, 140f, r.height), "x" + n + "  >  x" + (n + 1), Style(24, Color.white, TextAnchor.MiddleRight));

            if (ready && GUI.Button(r, GUIContent.none, GUIStyle.none)) { Pick(t); return; }
        }
    }
}
