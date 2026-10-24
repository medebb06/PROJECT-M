using UnityEngine;

// DEMİRCİ MAĞAZASI
// Demirci'nin yanına gidince "[E]" belirir; E ile mağaza açılır (oyun durur).
// Ruh (Souls) harcayarak 4 charm'ın seviyesi alınır (her seviye +%1, sınırsız yığılır, bedel artar).
// Aynı anda 2 charm takılabilir; sadece takılı charm'ın seviyesi etki eder. Her şey kalıcıdır.
public class BlacksmithShop : MonoBehaviour
{
    public static bool IsOpen { get { return open || closedFrame == Time.frameCount; } }
    /// <summary>E tuşu demirci yakınında / mağazadayken diğer sistemlere (KIN) gitmesin.</summary>
    public static bool BlocksKey { get { return near || IsOpen; } }

    private static bool open;
    private static bool near;
    private static int closedFrame = -1;

    public float interactRangeX = 3.6f;
    public float interactRangeY = 4.5f;

    private Font font;
    private Transform npc;
    private Transform playerTf;
    private float findTimer;
    private float openedAt;
    private bool prevCanControl = true;
    private bool prevCursorVisible;
    private CursorLockMode prevLock;
    private PlayerController player;

    private string toast = "";
    private float toastUntil;
    private bool toastBad;
    private readonly float[] flash = new float[4];
    private float resetArmedUntil;
    private int hoverId = -1;
    private int lastHover = -1;

    private static Texture2D gradTex;
    private static Texture2D orbTex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { open = false; near = false; closedFrame = -1; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        // Mağaza kapalıyken her oyun başında alınmış charm'lar sıfırlanır (ruh iadesi yok).
        if (!Charms.ShopEnabled) Charms.ShopReset();

        GameObject go = new GameObject("BlacksmithShop");
        DontDestroyOnLoad(go);
        go.AddComponent<BlacksmithShop>();
    }

    private void OnEnable()
    {
        font = GameFont.Or(Resources.Load<Font>("Pixeltype"));
    }

    private void OnDisable()
    {
        if (open) Close();
        near = false;
    }

    // ------------------------------------------------------------------
    // GİRDİ / YAKINLIK
    // ------------------------------------------------------------------
    private void FindRefs()
    {
        if (npc == null)
        {
            GameObject root = GameObject.Find("[NPC]");
            if (root != null)
            {
                Transform t = root.transform.Find("Demirci");
                npc = t != null ? t : root.transform;
            }
            if (npc == null)
            {
                GameObject d = GameObject.Find("Demirci");
                if (d != null) npc = d.transform;
            }
        }
        if (playerTf == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) { playerTf = p.transform; player = p.GetComponent<PlayerController>(); }
        }
    }

    private bool CanInteract()
    {
        if (MainMenu.Active || KuronamiPicker.IsOpen) return false;
        if (Time.timeScale < 0.01f && !open) return false;
        if (player == null) return false;
        if (!player.canControl && !open) return false;
        Health h = player.GetComponent<Health>();
        if (h != null && h.IsDead) return false;
        return true;
    }

    private void Update()
    {
        if (npc == null || playerTf == null)
        {
            findTimer -= Time.unscaledDeltaTime;
            if (findTimer <= 0f) { findTimer = 1f; FindRefs(); }
        }

        near = false;
        if (npc != null && playerTf != null)
        {
            Vector3 d = playerTf.position - npc.position;
            near = Charms.ShopEnabled && Mathf.Abs(d.x) < interactRangeX && Mathf.Abs(d.y) < interactRangeY && CanInteract();
        }

        if (open)
        {
            Time.timeScale = 0f;   // hit-stop / slow-mo zamanı geri açmasın
            if (Time.unscaledTime - openedAt < 0.15f) return;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Tab)) Close();
            else if (Input.GetKeyDown(KeyCode.Alpha1)) Buy(CharmType.Poison);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) Buy(CharmType.Explosion);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) Buy(CharmType.Blood);
            else if (Input.GetKeyDown(KeyCode.Alpha4)) Buy(CharmType.Storm);
            return;
        }

        if (near && MobileInput.GetKeyDown(KeyCode.E)) Open();
    }

    private void Open()
    {
        open = true;
        openedAt = Time.unscaledTime;
        Time.timeScale = 0f;
        if (player != null) { prevCanControl = player.canControl; player.canControl = false; }
        prevCursorVisible = Cursor.visible;
        prevLock = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        toast = "";
        MenuSfx.Select();
    }

    private void Close()
    {
        open = false;
        closedFrame = Time.frameCount;
        Time.timeScale = 1f;
        if (player != null) player.canControl = prevCanControl;
        Cursor.visible = prevCursorVisible;
        Cursor.lockState = prevLock;
        MenuSfx.Nav();
    }

    private void Say(string s, bool bad)
    {
        toast = s;
        toastBad = bad;
        toastUntil = Time.unscaledTime + 2.2f;
    }

    private void Buy(CharmType t)
    {
        if (Charms.ShopMaxed(t)) { Say("Bu charm en üst seviyede", true); return; }
        int price = Charms.ShopPrice(t);
        if (Souls.Count < price) { Say("Yetersiz ruh  (" + price + " gerekli)", true); return; }
        if (Charms.ShopBuy(t))
        {
            flash[(int)t] = Time.unscaledTime;
            Say(Charms.Name(t) + " +%1  →  seviye " + Charms.ShopLevel(t), false);
            MenuSfx.Select();
        }
    }

    private void Equip(CharmType t)
    {
        if (Charms.ShopLevel(t) <= 0) { Say("Önce satın al", true); return; }
        bool was = Charms.ShopEquipped(t);
        if (!Charms.ShopToggleEquip(t)) { Say("İki slot da dolu: önce birini çıkar", true); return; }
        Say(was ? Charms.Name(t) + " çıkarıldı" : Charms.Name(t) + " takıldı", false);
        MenuSfx.Nav();
    }

    // ------------------------------------------------------------------
    // ÇİZİM YARDIMCILARI
    // ------------------------------------------------------------------
    private static void EnsureTex()
    {
        if (gradTex == null)
        {
            gradTex = new Texture2D(1, 64, TextureFormat.RGBA32, false);
            gradTex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 64; y++)
            {
                float k = y / 63f;   // 0 alt, 1 üst
                Color top = new Color(0.13f, 0.11f, 0.15f, 0.98f);
                Color bot = new Color(0.05f, 0.04f, 0.06f, 0.98f);
                gradTex.SetPixel(0, y, Color.Lerp(bot, top, k));
            }
            gradTex.Apply();
        }
        if (orbTex == null)
        {
            int n = 64;
            orbTex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            orbTex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f - 0.5f, n / 2f - 0.5f)) / (n / 2f);
                    float a;
                    if (d > 1f) a = 0f;
                    else if (d > 0.86f) a = Mathf.Clamp01((1f - d) * 9f);          // keskin dış halka
                    else if (d > 0.7f) a = 0.25f;                                  // boşluk
                    else a = Mathf.Lerp(1f, 0.55f, d / 0.7f);                      // iç çekirdek, parlak merkez
                    orbTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            orbTex.Apply();
        }
    }

    private static void Fill(Rect r, Color c)
    {
        Color o = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = o;
    }

    private static void Border(Rect r, Color c, float w)
    {
        Fill(new Rect(r.x, r.y, r.width, w), c);
        Fill(new Rect(r.x, r.yMax - w, r.width, w), c);
        Fill(new Rect(r.x, r.y, w, r.height), c);
        Fill(new Rect(r.xMax - w, r.y, w, r.height), c);
    }

    private static void Orb(Rect r, Color c)
    {
        Color o = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, orbTex);
        GUI.color = o;
    }

    private GUIStyle st;

    private void Text(Rect r, string s, int size, Color c, TextAnchor a, bool shadow = true, bool wrap = false)
    {
        if (st == null) st = new GUIStyle(GUI.skin.label);
        st.fontSize = size;
        st.alignment = a;
        st.wordWrap = wrap;
        st.richText = false;
        if (font != null) st.font = font;
        if (shadow)
        {
            st.normal.textColor = new Color(0f, 0f, 0f, c.a * 0.85f);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), s, st);
        }
        st.normal.textColor = c;
        GUI.Label(r, s, st);
    }

    private bool Hover(Rect r) { return r.Contains(Event.current.mousePosition); }

    // ------------------------------------------------------------------
    // ARAYÜZ
    // ------------------------------------------------------------------
    private void OnGUI()
    {
        if (!open)
        {
            DrawPrompt();
            return;
        }

        EnsureTex();
        if (font == null) font = GameFont.Or(Resources.Load<Font>("Pixeltype"));

        float scale = Screen.height / 1080f;
        Matrix4x4 oldM = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
        float vw = Screen.width / scale;
        float vh = 1080f;

        float fade = Mathf.Clamp01((Time.unscaledTime - openedAt) / 0.18f);
        Color old = GUI.color;

        // Arka plan karartması
        Fill(new Rect(0f, 0f, vw, vh), new Color(0f, 0f, 0f, 0.72f * fade));

        float pw = 1280f, ph = 800f;
        float slide = (1f - fade) * 24f;
        Rect panel = new Rect((vw - pw) / 2f, (vh - ph) / 2f + slide, pw, ph);

        GUI.color = new Color(1f, 1f, 1f, fade);
        GUI.DrawTexture(panel, gradTex);
        GUI.color = old;

        Color gold = new Color(0.86f, 0.66f, 0.3f, fade);
        Color goldDim = new Color(0.86f, 0.66f, 0.3f, 0.35f * fade);
        Border(panel, gold, 3f);
        Border(new Rect(panel.x + 9f, panel.y + 9f, panel.width - 18f, panel.height - 18f), goldDim, 1f);

        // köşe süsleri
        float cs = 18f;
        Fill(new Rect(panel.x - 3f, panel.y - 3f, cs, 6f), gold); Fill(new Rect(panel.x - 3f, panel.y - 3f, 6f, cs), gold);
        Fill(new Rect(panel.xMax - cs + 3f, panel.y - 3f, cs, 6f), gold); Fill(new Rect(panel.xMax - 3f, panel.y - 3f, 6f, cs), gold);
        Fill(new Rect(panel.x - 3f, panel.yMax - 3f, cs, 6f), gold); Fill(new Rect(panel.x - 3f, panel.yMax - cs + 3f, 6f, cs), gold);
        Fill(new Rect(panel.xMax - cs + 3f, panel.yMax - 3f, cs, 6f), gold); Fill(new Rect(panel.xMax - 3f, panel.yMax - cs + 3f, 6f, cs), gold);

        // Başlık
        Color white = new Color(1f, 1f, 1f, fade);
        Text(new Rect(panel.x + 40f, panel.y + 24f, 600f, 64f), "DEMİRCİ", 64, new Color(1f, 0.88f, 0.6f, fade), TextAnchor.MiddleLeft);
        Text(new Rect(panel.x + 42f, panel.y + 84f, 700f, 30f), "Charm'ları güçlendir • her seviye +%1 • iki slota tak", 24, new Color(1f, 1f, 1f, 0.55f * fade), TextAnchor.MiddleLeft);

        // Ruh bakiyesi
        Rect bal = new Rect(panel.xMax - 380f, panel.y + 28f, 340f, 70f);
        Fill(bal, new Color(0f, 0f, 0f, 0.45f * fade));
        Border(bal, goldDim, 2f);
        Orb(new Rect(bal.x + 14f, bal.y + 13f, 44f, 44f), new Color(0.55f, 0.9f, 1f, fade));
        Text(new Rect(bal.x + 68f, bal.y + 4f, 260f, 26f), "RUH", 20, new Color(1f, 1f, 1f, 0.55f * fade), TextAnchor.MiddleLeft, false);
        Text(new Rect(bal.x + 68f, bal.y + 24f, 260f, 44f), Souls.Count.ToString(), 44, new Color(0.7f, 0.95f, 1f, fade), TextAnchor.MiddleLeft);

        Fill(new Rect(panel.x + 40f, panel.y + 128f, panel.width - 80f, 2f), goldDim);

        // --- SLOTLAR ---
        float sy = panel.y + 148f;
        Text(new Rect(panel.x + 40f, sy, 400f, 28f), "TAKILI CHARM'LAR", 22, new Color(1f, 1f, 1f, 0.6f * fade), TextAnchor.MiddleLeft, false);
        float slotW = (pw - 80f - 30f) / 2f;
        for (int i = 0; i < Charms.ShopSlots; i++)
        {
            Rect r = new Rect(panel.x + 40f + i * (slotW + 30f), sy + 34f, slotW, 104f);
            int ci = Charms.ShopSlotCharm(i);
            bool hv = Hover(r) && ci >= 0;
            if (ci >= 0)
            {
                CharmType t = (CharmType)ci;
                Color tc = Charms.Tint(t);
                Fill(r, new Color(tc.r * 0.3f, tc.g * 0.3f, tc.b * 0.3f, (hv ? 0.9f : 0.7f) * fade));
                Border(r, new Color(tc.r, tc.g, tc.b, fade), 2f);
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 3f + i);
                Orb(new Rect(r.x + 16f, r.y + 18f, 68f, 68f), new Color(tc.r, tc.g, tc.b, fade * pulse));
                Text(new Rect(r.x + 100f, r.y + 8f, r.width - 120f, 40f), Charms.Name(t), 36, new Color(tc.r, tc.g, tc.b, fade), TextAnchor.MiddleLeft);
                Text(new Rect(r.x + 100f, r.y + 48f, r.width - 120f, 30f), "Seviye " + Charms.ShopLevel(t) + "   •   +%" + Mathf.RoundToInt(Charms.ShopLevel(t) * Charms.ShopPerLevel * 100f), 24, white, TextAnchor.MiddleLeft, false);
                Text(new Rect(r.x + 100f, r.y + 74f, r.width - 120f, 24f), hv ? "çıkarmak için tıkla" : "SLOT " + (i + 1), 18, new Color(1f, 1f, 1f, 0.45f * fade), TextAnchor.MiddleLeft, false);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { Charms.ShopUnequipSlot(i); Say(Charms.Name(t) + " çıkarıldı", false); MenuSfx.Nav(); }
            }
            else
            {
                Fill(r, new Color(1f, 1f, 1f, 0.04f * fade));
                Border(r, new Color(1f, 1f, 1f, 0.22f * fade), 2f);
                Text(new Rect(r.x, r.y + 18f, r.width, 40f), "BOŞ SLOT " + (i + 1), 32, new Color(1f, 1f, 1f, 0.35f * fade), TextAnchor.MiddleCenter, false);
                Text(new Rect(r.x, r.y + 58f, r.width, 28f), "aşağıdan bir charm tak", 20, new Color(1f, 1f, 1f, 0.28f * fade), TextAnchor.MiddleCenter, false);
            }
        }

        // --- KARTLAR ---
        float cy = sy + 164f;
        float gap = 22f;
        float cw = (pw - 80f - gap * 3f) / 4f;
        float chh = 420f;
        hoverId = -1;
        for (int i = 0; i < 4; i++)
        {
            CharmType t = (CharmType)i;
            Color tc = Charms.Tint(t);
            Rect r = new Rect(panel.x + 40f + i * (cw + gap), cy, cw, chh);
            int lvl = Charms.ShopLevel(t);
            bool eq = Charms.ShopEquipped(t);
            bool maxed = Charms.ShopMaxed(t);
            int price = Charms.ShopPrice(t);
            bool afford = Souls.Count >= price && !maxed;

            float fl = Mathf.Clamp01(1f - (Time.unscaledTime - flash[i]) / 0.45f);

            Fill(r, new Color(0.02f, 0.02f, 0.03f, 0.55f * fade));
            Fill(new Rect(r.x, r.y, r.width, 5f), new Color(tc.r, tc.g, tc.b, fade));
            Fill(new Rect(r.x, r.y + 5f, r.width, 120f), new Color(tc.r * 0.22f, tc.g * 0.22f, tc.b * 0.22f, 0.8f * fade));
            Border(r, eq ? new Color(tc.r, tc.g, tc.b, fade) : new Color(1f, 1f, 1f, 0.14f * fade), eq ? 3f : 1f);
            if (fl > 0f) Fill(r, new Color(1f, 1f, 1f, 0.28f * fl));

            // simge
            float bob = Mathf.Sin(Time.unscaledTime * 2.2f + i) * 3f;
            Orb(new Rect(r.x + cw / 2f - 46f - 6f * fl, r.y + 22f + bob - 6f * fl, 92f + 12f * fl, 92f + 12f * fl), new Color(tc.r, tc.g, tc.b, fade));
            Text(new Rect(r.x + 8f, r.y + 8f, 40f, 28f), (i + 1).ToString(), 22, new Color(1f, 1f, 1f, 0.4f * fade), TextAnchor.MiddleLeft, false);
            if (eq) Text(new Rect(r.xMax - 108f, r.y + 8f, 100f, 28f), "TAKILI", 20, new Color(tc.r, tc.g, tc.b, fade), TextAnchor.MiddleRight, false);

            Text(new Rect(r.x, r.y + 130f, r.width, 40f), Charms.Name(t), 38, new Color(tc.r, tc.g, tc.b, fade), TextAnchor.MiddleCenter);
            Text(new Rect(r.x, r.y + 168f, r.width, 28f), lvl > 0 ? "Seviye " + lvl + "   (+%" + Mathf.RoundToInt(lvl * Charms.ShopPerLevel * 100f) + ")" : "Satın alınmadı", 22, new Color(1f, 1f, 1f, (lvl > 0 ? 0.9f : 0.45f) * fade), TextAnchor.MiddleCenter, false);

            int pctNext = Mathf.RoundToInt((lvl + 1) * Charms.ShopPerLevel * 100f);
            string d = string.Format(Charms.ShopDesc(t), maxed ? Mathf.RoundToInt(lvl * 100f * Charms.ShopPerLevel) : pctNext);
            Text(new Rect(r.x + 14f, r.y + 200f, r.width - 28f, 86f), d, 20, new Color(1f, 1f, 1f, 0.72f * fade), TextAnchor.UpperCenter, false, true);

            // SATIN AL
            Rect b1 = new Rect(r.x + 14f, r.y + 296f, r.width - 28f, 56f);
            bool h1 = Hover(b1);
            if (h1) hoverId = i * 2;
            Color bc = maxed ? new Color(0.4f, 0.4f, 0.4f) : (afford ? tc : new Color(0.75f, 0.25f, 0.25f));
            Fill(b1, new Color(bc.r * (h1 && afford ? 0.55f : 0.32f), bc.g * (h1 && afford ? 0.55f : 0.32f), bc.b * (h1 && afford ? 0.55f : 0.32f), 0.95f * fade));
            Border(b1, new Color(bc.r, bc.g, bc.b, (afford ? 1f : 0.6f) * fade), 2f);
            if (maxed) Text(b1, "EN ÜST SEVİYE", 24, new Color(1f, 1f, 1f, 0.6f * fade), TextAnchor.MiddleCenter, false);
            else
            {
                Orb(new Rect(b1.x + 16f, b1.y + 14f, 28f, 28f), new Color(0.55f, 0.9f, 1f, fade));
                Text(new Rect(b1.x + 50f, b1.y, 90f, b1.height), price.ToString(), 30, afford ? new Color(0.75f, 0.97f, 1f, fade) : new Color(1f, 0.55f, 0.5f, fade), TextAnchor.MiddleLeft);
                Text(new Rect(b1.xMax - 120f, b1.y, 106f, b1.height), "+%1", 28, white, TextAnchor.MiddleRight);
            }
            if (GUI.Button(b1, GUIContent.none, GUIStyle.none)) Buy(t);

            // TAK / ÇIKAR
            Rect b2 = new Rect(r.x + 14f, r.y + 360f, r.width - 28f, 44f);
            bool h2 = Hover(b2);
            if (h2) hoverId = i * 2 + 1;
            bool can = lvl > 0;
            Color ec = eq ? new Color(0.85f, 0.35f, 0.3f) : (can ? new Color(0.85f, 0.85f, 0.85f) : new Color(0.4f, 0.4f, 0.4f));
            Fill(b2, new Color(ec.r * (h2 && can ? 0.4f : 0.18f), ec.g * (h2 && can ? 0.4f : 0.18f), ec.b * (h2 && can ? 0.4f : 0.18f), 0.9f * fade));
            Border(b2, new Color(ec.r, ec.g, ec.b, (can ? 0.9f : 0.4f) * fade), 1f);
            Text(b2, eq ? "ÇIKAR" : "TAK", 24, new Color(1f, 1f, 1f, (can ? 1f : 0.4f) * fade), TextAnchor.MiddleCenter, false);
            if (GUI.Button(b2, GUIContent.none, GUIStyle.none)) Equip(t);
        }

        if (hoverId != lastHover && hoverId >= 0) MenuSfx.Nav();
        lastHover = hoverId;

        // Bildirim
        if (Time.unscaledTime < toastUntil && toast.Length > 0)
        {
            float a = Mathf.Clamp01((toastUntil - Time.unscaledTime) / 0.4f);
            Color tcol = toastBad ? new Color(1f, 0.5f, 0.45f, a) : new Color(0.7f, 1f, 0.75f, a);
            Text(new Rect(panel.x, panel.yMax - 92f, pw, 36f), toast, 28, tcol, TextAnchor.MiddleCenter);
        }

        // Alt ipucu + kapat
        Text(new Rect(panel.x + 40f, panel.yMax - 54f, pw - 460f, 30f), "1-4: satın al   •   kalıcıdır   •   Kuronami'de etkisiz", 20, new Color(1f, 1f, 1f, 0.4f * fade), TextAnchor.MiddleLeft, false);
        // SIFIRLA (iki tıkla onay, ruh iadesi yok)
        Rect rs = new Rect(panel.xMax - 400f, panel.yMax - 62f, 190f, 42f);
        bool armed = Time.unscaledTime < resetArmedUntil;
        bool hr = Hover(rs);
        Color rc = new Color(0.9f, 0.3f, 0.28f);
        Fill(rs, new Color(rc.r, rc.g, rc.b, (armed ? 0.55f : (hr ? 0.3f : 0.14f)) * fade));
        Border(rs, new Color(rc.r, rc.g, rc.b, fade), armed ? 2f : 1f);
        Text(rs, armed ? "EMİN MİSİN?" : "SIFIRLA", 22, white, TextAnchor.MiddleCenter, false);
        if (GUI.Button(rs, GUIContent.none, GUIStyle.none))
        {
            if (armed)
            {
                Charms.ShopReset();
                resetArmedUntil = 0f;
                Say("Charm'lar sıfırlandı (ruh iadesi yok)", false);
                MenuSfx.Select();
            }
            else
            {
                resetArmedUntil = Time.unscaledTime + 3f;
                Say("Tüm seviyeler silinir, ruh iade edilmez. Onay için tekrar tıkla", true);
                MenuSfx.Nav();
            }
        }

        Rect cl = new Rect(panel.xMax - 190f, panel.yMax - 62f, 150f, 42f);
        bool hc = Hover(cl);
        Fill(cl, new Color(1f, 1f, 1f, hc ? 0.16f : 0.07f));
        Border(cl, new Color(1f, 1f, 1f, 0.4f), 1f);
        Text(cl, "KAPAT [E]", 22, white, TextAnchor.MiddleCenter, false);
        if (GUI.Button(cl, GUIContent.none, GUIStyle.none)) Close();

        GUI.matrix = oldM;
        GUI.color = old;
    }

    private void DrawPrompt()
    {
        if (!near || npc == null || MainMenu.Active) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 sp = cam.WorldToScreenPoint(npc.position + Vector3.up * 3.4f);
        if (sp.z < 0f) return;

        if (font == null) font = GameFont.Or(Resources.Load<Font>("Pixeltype"));
        EnsureTex();
        float bob = Mathf.Sin(Time.unscaledTime * 3f) * 5f;
        float s = Screen.height / 1080f;
        float w = 230f * s, h = 54f * s;
        Rect r = new Rect(sp.x - w / 2f, Screen.height - sp.y - h / 2f + bob * s, w, h);
        if (st == null) st = new GUIStyle(GUI.skin.label);
        st.alignment = TextAnchor.MiddleCenter;
        st.fontSize = Mathf.RoundToInt(28f * s);
        st.wordWrap = false;
        if (font != null) st.font = font;
        st.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
        GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), "[E]  Demirci", st);
        st.normal.textColor = Color.white;
        GUI.Label(r, "[E]  Demirci", st);
    }
}
