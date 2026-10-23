using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// iOS / mobil dokunmatik kontroller. Sahneye eklemek GEREKMEZ: oyun açılınca kendini kurar.
///
///  - Sol yarıda yüzen sanal joystick (yön + aşağı = slam/çömelme), sağda butonlar:
///    VUR (sol tık) · PARRY (sağ tık, basılı tut = block) · DASH (Shift) · ZIPLA (Space)
///    Q yetenek · E odak/infaz · F iksir · II duraklat · R (basılı tut) yeniden başlat.
///  - Oyun kodu Input.* yerine bu sınıfın aynı isimli fonksiyonlarını çağırır; klavye/fare
///    masaüstünde AYNEN çalışır, mobilde sanal butonlar eklenir.
///  - Editörde test: F10 = mobil modu aç/kapa (fare parmak gibi davranır) ya da Device Simulator.
///  - Ayrıca mobilde: 60 FPS, ekran uyumasın, yatay yön, UI/yazı ölçeği (UiMul / HudMul / WorldMul).
/// </summary>
public enum MBtn { Attack = 0, Parry, Dash, Jump, Ability, Execute, Potion, Pause, Restart }

[DefaultExecutionOrder(-1000)]
public class MobileInput : MonoBehaviour
{
    private const int BtnCount = 9;

    /// <summary>Editörde / masaüstünde mobil modu zorla (F10).</summary>
    public static bool ForceMobile;

    /// <summary>Butonların saydamlığı (0..1).</summary>
    public static float Opacity = 0.55f;

    /// <summary>Ana menü canvas'ının mobildeki büyütme çarpanı (taşarsa küçült).</summary>
    public static float MenuMul = 1.4f;

    private static MobileInput instance;
    private static readonly bool[] held = new bool[BtnCount];
    private static readonly int[] downFrame = new int[BtnCount];
    private static readonly int[] upFrame = new int[BtnCount];
    private static float stickX, stickY;
    private static float uiMulCache;

    public static bool Active
    {
        get { return ForceMobile || Application.isMobilePlatform; }
    }

    // =========================================================
    // ÖLÇEK (font / HUD)
    // =========================================================

    /// <summary>Yazılar için çarpan: masaüstü 1, telefon ~2.2, tablet ~1.1 (fiziksel ekran boyuna göre).</summary>
    public static float UiMul
    {
        get
        {
            if (!Active)
                return 1f;

            if (uiMulCache > 0f)
                return uiMulCache;

            float m = 1.9f;

            if (Application.isMobilePlatform && Screen.dpi > 40f)
            {
                float inches = Mathf.Min(Screen.width, Screen.height) / Screen.dpi;
                m = Mathf.Clamp(6f / Mathf.Max(inches, 0.5f), 1.1f, 2.2f);
            }

            uiMulCache = m;
            return m;
        }
    }

    /// <summary>Çubuk / panel gibi yazı olmayan HUD için daha yumuşak çarpan.</summary>
    public static float HudMul
    {
        get { return 1f + (UiMul - 1f) * 0.55f; }
    }

    /// <summary>Dünyadaki yazılar (İNFAZ!, DASH!, hasar sayıları).</summary>
    public static float WorldMul
    {
        get { return Active ? 1.3f : 1f; }
    }

    /// <summary>Platforma göre metin seç (ör. "PRESS LMB" / "TAP VUR").</summary>
    public static string Txt(string pc, string mobile)
    {
        return Active ? mobile : pc;
    }

    // =========================================================
    // Input.* YERİNE KULLANILAN FONKSİYONLAR
    // =========================================================

    private static int Map(KeyCode k)
    {
        switch (k)
        {
            case KeyCode.Space: return (int)MBtn.Jump;
            case KeyCode.LeftShift: return (int)MBtn.Dash;
            case KeyCode.E: return (int)MBtn.Execute;
            case KeyCode.Q: return (int)MBtn.Ability;
            case KeyCode.F: return (int)MBtn.Potion;
            case KeyCode.Escape: return (int)MBtn.Pause;
            case KeyCode.R: return (int)MBtn.Restart;
        }

        return -1;
    }

    public static bool GetKey(KeyCode k)
    {
        if (Input.GetKey(k))
            return true;

        if (!Active)
            return false;

        int b = Map(k);
        return b >= 0 && held[b];
    }

    public static bool GetKeyDown(KeyCode k)
    {
        if (Input.GetKeyDown(k))
            return true;

        if (!Active)
            return false;

        int b = Map(k);
        return b >= 0 && downFrame[b] == Time.frameCount;
    }

    public static bool GetKeyUp(KeyCode k)
    {
        if (Input.GetKeyUp(k))
            return true;

        if (!Active)
            return false;

        int b = Map(k);
        return b >= 0 && upFrame[b] == Time.frameCount;
    }

    // Mobilde dokunuş "fare tıklaması"na dönüştüğü için sol tık (saldırı) SADECE sanal butondan okunur.
    public static bool GetMouseButton(int button)
    {
        if (button == 0)
            return Active ? held[(int)MBtn.Attack] : Input.GetMouseButton(0);

        if (button == 1)
            return Input.GetMouseButton(1) || (Active && held[(int)MBtn.Parry]);

        return Input.GetMouseButton(button);
    }

    public static bool GetMouseButtonDown(int button)
    {
        if (button == 0)
            return Active ? downFrame[(int)MBtn.Attack] == Time.frameCount : Input.GetMouseButtonDown(0);

        if (button == 1)
            return Input.GetMouseButtonDown(1) || (Active && downFrame[(int)MBtn.Parry] == Time.frameCount);

        return Input.GetMouseButtonDown(button);
    }

    public static bool GetMouseButtonUp(int button)
    {
        if (button == 0)
            return Active ? upFrame[(int)MBtn.Attack] == Time.frameCount : Input.GetMouseButtonUp(0);

        if (button == 1)
            return Input.GetMouseButtonUp(1) || (Active && upFrame[(int)MBtn.Parry] == Time.frameCount);

        return Input.GetMouseButtonUp(button);
    }

    public static float GetAxisRaw(string axis)
    {
        if (Active)
        {
            if (axis == "Horizontal" && stickX != 0f)
                return stickX;

            if (axis == "Vertical" && stickY != 0f)
                return stickY;
        }

        return Input.GetAxisRaw(axis);
    }

    /// <summary>Ekrana dokunuluyor mu? (Input.anyKey yerine.)</summary>
    public static bool AnyTouch
    {
        get { return Input.touchCount > 0; }
    }

    // =========================================================
    // KURULUM
    // =========================================================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        ForceMobile = false;
        uiMulCache = 0f;
        stickX = stickY = 0f;

        for (int i = 0; i < BtnCount; i++)
        {
            held[i] = false;
            downFrame[i] = -1;
            upFrame[i] = -1;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject("~MobileInput");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<MobileInput>();

        if (Application.isMobilePlatform)
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
            Input.multiTouchEnabled = true;
        }
    }

    // =========================================================
    // DÜZEN (ekran koordinatı, sol-alt orijin)
    // =========================================================

    private static readonly Vector2[] center = new Vector2[BtnCount];
    private static readonly float[] radius = new float[BtnCount];

    private static readonly string[] labels =
        { "VUR", "PARRY", "DASH", "ZIPLA", "Q", "E", "F", "II", "R" };

    // Sağ-alt köşeye göre (ekran yüksekliği biriminde): dx, dy, r
    private static readonly float[] dx = { -0.15f, -0.385f, -0.34f, -0.12f, -0.27f, -0.54f, -0.50f };
    private static readonly float[] dy = { 0.15f, 0.27f, 0.09f, 0.40f, 0.50f, 0.42f, 0.12f };
    private static readonly float[] dr = { 0.11f, 0.09f, 0.065f, 0.08f, 0.055f, 0.055f, 0.05f };

    private static Vector2 stickIdle;
    private static float stickRadius;

    private static void Layout()
    {
        Rect sa = Screen.safeArea;
        float H = Screen.height;
        float m = 0.035f * H;
        float ax = sa.xMax - m;
        float ay = sa.yMin + m;

        for (int i = 0; i < dx.Length; i++)
        {
            center[i] = new Vector2(ax + dx[i] * H, ay + dy[i] * H);
            radius[i] = dr[i] * H;
        }

        // Duraklat: sol üst. Yeniden başlat (basılı tut): yanında.
        radius[(int)MBtn.Pause] = 0.04f * H;
        center[(int)MBtn.Pause] = new Vector2(sa.xMin + m + 0.05f * H, sa.yMax - m - 0.05f * H);
        radius[(int)MBtn.Restart] = 0.035f * H;
        center[(int)MBtn.Restart] = new Vector2(center[(int)MBtn.Pause].x + 0.11f * H, center[(int)MBtn.Pause].y);

        stickRadius = 0.11f * H;
        stickIdle = new Vector2(sa.xMin + 0.28f * H, sa.yMin + 0.30f * H);
    }

    private static int HitButton(Vector2 p)
    {
        int best = -1;
        float bestD = float.MaxValue;

        for (int i = 0; i < BtnCount; i++)
        {
            float d = (p - center[i]).magnitude;

            if (d <= radius[i] * 1.25f && d < bestD)
            {
                bestD = d;
                best = i;
            }
        }

        return best;
    }

    // =========================================================
    // GİRİŞ İŞLEME
    // =========================================================

    private struct Ptr
    {
        public int id;
        public Vector2 pos;
        public bool began;
        public bool ended;
    }

    private readonly List<Ptr> ptrs = new List<Ptr>();
    private readonly Dictionary<int, int> roles = new Dictionary<int, int>(); // -1 joystick, -2 yok, >=0 buton
    private readonly HashSet<int> seen = new HashSet<int>();
    private readonly List<int> stale = new List<int>();

    private bool stickActive;
    private Vector2 stickOrigin, stickPos;
    private float restartTimer;

    private const float RestartHold = 0.7f;

    private static bool Visible()
    {
        return !MainMenu.Active;
    }

    private void Update()
    {
        if (!Active || !Visible())
        {
            ReleaseAll();
            return;
        }

        Layout();
        CollectPointers();

        seen.Clear();

        for (int i = 0; i < ptrs.Count; i++)
        {
            Ptr p = ptrs[i];
            seen.Add(p.id);

            int role;

            if (p.began && !roles.ContainsKey(p.id))
            {
                role = AssignRole(p.pos);
                roles[p.id] = role;
            }
            else if (!roles.TryGetValue(p.id, out role))
            {
                continue;
            }

            if (p.ended)
            {
                ReleaseRole(role);
                roles.Remove(p.id);
                continue;
            }

            if (role == -1)
                stickPos = p.pos;
        }

        // Listede olmayan parmakları bırak (ör. uygulama arka plana gitti).
        stale.Clear();

        foreach (KeyValuePair<int, int> kv in roles)
        {
            if (!seen.Contains(kv.Key))
                stale.Add(kv.Key);
        }

        for (int i = 0; i < stale.Count; i++)
        {
            ReleaseRole(roles[stale[i]]);
            roles.Remove(stale[i]);
        }

        // Yeniden başlat: basılı tut.
        int rb = (int)MBtn.Restart;

        if (held[rb])
        {
            restartTimer += Time.unscaledDeltaTime;

            if (restartTimer >= RestartHold)
            {
                downFrame[rb] = Time.frameCount;
                held[rb] = false;
                restartTimer = 0f;
            }
        }
        else
        {
            restartTimer = 0f;
        }

        UpdateStick();
    }

    private void CollectPointers()
    {
        ptrs.Clear();

        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);

                ptrs.Add(new Ptr
                {
                    id = t.fingerId,
                    pos = t.position,
                    began = t.phase == TouchPhase.Began,
                    ended = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled
                });
            }
        }
        else if (!Application.isMobilePlatform)
        {
            // Editör / F10 testi: fare tek parmak gibi.
            Vector2 mp = Input.mousePosition;

            if (Input.GetMouseButtonDown(0))
                ptrs.Add(new Ptr { id = -100, pos = mp, began = true });
            else if (Input.GetMouseButton(0))
                ptrs.Add(new Ptr { id = -100, pos = mp });
            else if (Input.GetMouseButtonUp(0))
                ptrs.Add(new Ptr { id = -100, pos = mp, ended = true });
        }
    }

    private int AssignRole(Vector2 pos)
    {
        int b = HitButton(pos);

        if (b >= 0)
        {
            if (!held[b])
            {
                held[b] = true;
                downFrame[b] = Time.frameCount;
            }

            return b;
        }

        if (!stickActive && pos.x < Screen.width * 0.5f)
        {
            stickActive = true;
            stickOrigin = pos;
            stickPos = pos;
            return -1;
        }

        return -2;
    }

    private void ReleaseRole(int role)
    {
        if (role == -1)
        {
            stickActive = false;
            stickX = stickY = 0f;
            return;
        }

        if (role >= 0 && held[role])
        {
            held[role] = false;
            upFrame[role] = Time.frameCount;
        }
    }

    private void ReleaseAll()
    {
        if (roles.Count > 0)
            roles.Clear();

        stickActive = false;
        stickX = stickY = 0f;
        restartTimer = 0f;

        for (int i = 0; i < BtnCount; i++)
        {
            if (held[i])
            {
                held[i] = false;
                upFrame[i] = Time.frameCount;
            }
        }
    }

    private void UpdateStick()
    {
        if (!stickActive)
        {
            stickX = stickY = 0f;
            return;
        }

        float R = Mathf.Max(1f, stickRadius);
        Vector2 d = stickPos - stickOrigin;
        float mag = d.magnitude;

        // Parmak sınırı aşarsa taban parmağı takip eder (rahat sürükleme).
        if (mag > R)
        {
            stickOrigin += d / mag * (mag - R);
            d = stickPos - stickOrigin;
        }

        float nx = d.x / R;
        float ny = d.y / R;

        stickX = Mathf.Abs(nx) > 0.35f ? Mathf.Sign(nx) : 0f;

        if (ny < -0.6f && -ny > Mathf.Abs(nx) * 0.75f)
            stickY = -1f;
        else if (ny > 0.65f && ny > Mathf.Abs(nx) * 0.7f)
            stickY = 1f;
        else
            stickY = 0f;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            ReleaseAll();
    }

    private void OnApplicationFocus(bool focus)
    {
        if (!focus)
            ReleaseAll();
    }

    // =========================================================
    // ÇİZİM
    // =========================================================

    private static Texture2D disc, ring;
    private static GUIStyle labelStyle;

    private static void EnsureTextures()
    {
        if (disc != null && ring != null)
            return;

        const int N = 128;
        disc = new Texture2D(N, N, TextureFormat.RGBA32, false);
        ring = new Texture2D(N, N, TextureFormat.RGBA32, false);
        disc.hideFlags = ring.hideFlags = HideFlags.HideAndDontSave;
        disc.wrapMode = ring.wrapMode = TextureWrapMode.Clamp;
        disc.filterMode = ring.filterMode = FilterMode.Bilinear;

        Color32[] dp = new Color32[N * N];
        Color32[] rp = new Color32[N * N];

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float fx = (x + 0.5f) / N * 2f - 1f;
                float fy = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(fx * fx + fy * fy);

                float edge = Mathf.Clamp01((1f - r) * N * 0.5f);
                float inner = Mathf.Clamp01((r - 0.86f) * N * 0.5f);
                float a = edge;
                byte da = (byte)(a * 255f);
                byte ra = (byte)(Mathf.Min(edge, inner) * 255f);

                dp[y * N + x] = new Color32(255, 255, 255, da);
                rp[y * N + x] = new Color32(255, 255, 255, ra);
            }
        }

        disc.SetPixels32(dp);
        ring.SetPixels32(rp);
        disc.Apply();
        ring.Apply();
    }

    private static void Draw(Texture2D tex, Vector2 c, float r, Color col)
    {
        GUI.color = col;
        GUI.DrawTexture(new Rect(c.x - r, Screen.height - c.y - r, r * 2f, r * 2f), tex);
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || !Active || !Visible())
            return;

        EnsureTextures();
        Layout();

        GUI.depth = -1000;

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Overflow,
                wordWrap = false
            };
        }

        float op = Opacity;
        Color old = GUI.color;

        // Joystick
        Vector2 bc = stickActive ? stickOrigin : stickIdle;
        Draw(disc, bc, stickRadius, new Color(1f, 1f, 1f, op * (stickActive ? 0.28f : 0.14f)));
        Draw(ring, bc, stickRadius, new Color(1f, 1f, 1f, op * (stickActive ? 0.9f : 0.5f)));

        Vector2 knob = bc;

        if (stickActive)
        {
            Vector2 d = stickPos - stickOrigin;
            if (d.magnitude > stickRadius) d = d.normalized * stickRadius;
            knob = stickOrigin + d;
        }

        Draw(disc, knob, stickRadius * 0.42f, new Color(1f, 1f, 1f, op * (stickActive ? 0.75f : 0.4f)));

        // Butonlar
        for (int i = 0; i < BtnCount; i++)
        {
            bool down = held[i];
            Color tint = ButtonColor(i);
            float r = radius[i] * (down ? 0.94f : 1f);

            Draw(disc, center[i], r, new Color(tint.r, tint.g, tint.b, op * (down ? 0.75f : 0.32f)));
            Draw(ring, center[i], r, new Color(1f, 1f, 1f, op * (down ? 1f : 0.7f)));

            // Yeniden başlat: basılı tutma ilerlemesi.
            if (i == (int)MBtn.Restart && held[i])
            {
                float k = Mathf.Clamp01(restartTimer / RestartHold);
                Draw(disc, center[i], r * k, new Color(1f, 0.4f, 0.3f, 0.7f));
            }

            string txt = labels[i];
            float fs = radius[i] * (txt.Length <= 2 ? 0.95f : (txt.Length <= 4 ? 0.52f : 0.42f));
            labelStyle.fontSize = Mathf.Max(12, Mathf.RoundToInt(fs));

            Rect lr = new Rect(center[i].x - radius[i], Screen.height - center[i].y - radius[i], radius[i] * 2f, radius[i] * 2f);

            GUI.color = new Color(0f, 0f, 0f, op * 0.9f);
            GUI.Label(new Rect(lr.x + 1.5f, lr.y + 1.5f, lr.width, lr.height), txt, labelStyle);
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(op + 0.3f));
            GUI.Label(lr, txt, labelStyle);
        }

        GUI.color = old;
    }

    private static Color ButtonColor(int i)
    {
        switch ((MBtn)i)
        {
            case MBtn.Attack: return new Color(1f, 0.45f, 0.35f);
            case MBtn.Parry: return new Color(0.45f, 0.75f, 1f);
            case MBtn.Dash: return new Color(0.8f, 0.8f, 0.85f);
            case MBtn.Jump: return new Color(0.6f, 1f, 0.7f);
            case MBtn.Ability: return new Color(0.85f, 0.6f, 1f);
            case MBtn.Execute: return new Color(1f, 0.82f, 0.3f);
            case MBtn.Potion: return new Color(0.5f, 1f, 0.55f);
        }

        return new Color(0.8f, 0.8f, 0.8f);
    }
}
