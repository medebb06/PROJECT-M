using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Koşu arayüzü (OnGUI, ek asset yok). Ekranlar:
///   Lobi (zorluk seçimi) · HUD (perde/oda, altın, charm'lar) · Boss çubuğu ·
///   Kapı seçimi · Charm seçimi · Dükkan · Dinlenme · Koşu sonu / Zafer.
///
/// Ana menü (Lobi): BAŞLA / KALICI GELİŞİM (Demirci) / AYARLAR / ÇIKIŞ.
/// Koşu içinde Esc: duraklatma menüsü (Devam / Ayarlar / Ana menü).
///
/// Tuşlar:
///   Menü: W/S ya da ↑/↓ seç, Enter onayla, Esc geri.
///   Yeni koşu: ←/→ zorluk, Enter başla.   Seçimler: 1/2/3 ya da tıkla.
///   Dükkan: 1..4 satın al, R yenile, Enter çık.   Sonuç: Enter.
///   TAB (basılı): canlı istatistik.
/// Boyut: 'UI Scale'. RunManager kendiliğinden ekler.
/// </summary>
public class RunUI : MonoBehaviour
{
    [Header("Boyut")]
    [Range(0.4f, 1.5f)]
    [SerializeField] private float uiScale = 0.7f;

    [Header("Davranış")]
    [SerializeField] private KeyCode liveStatsKey = KeyCode.Tab;
    [SerializeField] private int maxStageRows = 8;
    [SerializeField] private bool showBuildStats = true;

    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle smallStyle;
    private GUIStyle tinyStyle;
    private GUIStyle charmStyle;
    private GUIStyle cardStyle;
    private GUIStyle buttonStyle;
    private GUIStyle bannerStyle;
    private GUIStyle bigTitleStyle;
    private GUIStyle riposteStyle;
    private GUIStyle richCentered;
    private GUIStyle legendStyle;
    private GUIStyle panelStyle;

    private GUIStyle menuTitleStyle;
    private GUIStyle menuItemStyle;
    private GUIStyle menuHintStyle;

    private Texture2D dimTexture;
    private Texture2D sideFadeTexture;
    private Texture2D panelTexture;
    private Texture2D cardTexture;
    private Texture2D cardHoverTexture;

    private static readonly Color Gold = new Color(1f, 0.82f, 0.3f);
    private static readonly Color Soft = new Color(1f, 1f, 1f, 0.75f);

    private static readonly Color ColParry = new Color(1f, 0.82f, 0.2f);
    private static readonly Color ColBlock = new Color(0.4f, 0.6f, 1f);
    private static readonly Color ColDash = new Color(0.3f, 0.9f, 0.9f);
    private static readonly Color ColMissed = new Color(0.62f, 0.62f, 0.62f);
    private static readonly Color ColInterrupted = new Color(1f, 0.5f, 0.2f);
    private static readonly Color ColIFrame = new Color(0.72f, 0.5f, 1f);
    private static readonly Color ColHit = new Color(0.95f, 0.25f, 0.25f);

    private const float Pad = 8f;

    // ---------------- MENÜ DURUMU ----------------

    private enum MenuPage { Main, NewRun, Meta, Settings }

    private MenuPage page = MenuPage.Main;
    private int mainIndex;
    private bool pauseSettings;
    private bool confirmAbandon;
    private float pendingUiScale = -1f;
    private string metaMessage = "";
    private Vector2 lastMenuMouse;
    private float metaMessageUntil;

    private static readonly string[] MainItems =
    {
        "BAŞLA", "KALICI GELİŞİM", "AYARLAR", "ÇIKIŞ"
    };
    private const string GoldIcon = "●";

    // =========================================================
    // GİRİŞ
    // =========================================================

    private void Update()
    {
        RunManager run = RunManager.Instance;

        if (run == null)
            return;

        switch (run.State)
        {
            case RunState.Lobby:
                UpdateLobby(run);
                break;

            case RunState.AbilityOffer:
                for (int i = 0; i < 9; i++)
                {
                    if (NumberPressed(i))
                        run.ChooseAbility(i);
                }
                break;

            case RunState.Fighting:
            case RunState.Cleared:
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    if (run.IsPaused)
                    {
                        if (pauseSettings)
                        {
                            pauseSettings = false;
                            GameSettings.Save();
                        }
                        else
                        {
                            run.SetPaused(false);
                        }
                    }
                    else if (run.CanPause)
                    {
                        pauseSettings = false;
                        confirmAbandon = false;
                        run.SetPaused(true);
                    }
                }
                break;

            case RunState.Offer:
                for (int i = 0; i < 9; i++)
                {
                    if (NumberPressed(i))
                        run.Choose(i);
                }
                break;

            case RunState.ChoosingRoom:
                for (int i = 0; i < 3; i++)
                {
                    if (NumberPressed(i))
                        run.ChooseDoor(i);
                }
                break;

            case RunState.Shop:
                for (int i = 0; i < 9; i++)
                {
                    if (NumberPressed(i))
                        run.BuyShopItem(i);
                }

                if (Input.GetKeyDown(KeyCode.R))
                    run.RerollShop();

                if (EnterPressed() || Input.GetKeyDown(KeyCode.Escape))
                    run.LeaveShop();
                break;

            case RunState.Rest:
                if (NumberPressed(0))
                    run.ChooseRest(true);

                if (NumberPressed(1))
                    run.ChooseRest(false);
                break;

            case RunState.Dead:
            case RunState.Victory:
                if (
                    EnterPressed() ||
                    Input.GetKeyDown(KeyCode.Space) ||
                    Input.GetKeyDown(KeyCode.R)
                )
                {
                    run.RequestRestart();
                }
                break;
        }
    }

    private void UpdateLobby(RunManager run)
    {
        bool back = Input.GetKeyDown(KeyCode.Escape);

        switch (page)
        {
            case MenuPage.Main:
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                    mainIndex = (mainIndex + MainItems.Length - 1) % MainItems.Length;

                if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                    mainIndex = (mainIndex + 1) % MainItems.Length;

                if (EnterPressed() || Input.GetKeyDown(KeyCode.Space))
                    ActivateMain(run, mainIndex);
                break;

            case MenuPage.NewRun:
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                    run.ChangeHeat(-1);

                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                    run.ChangeHeat(1);

                if (EnterPressed() || Input.GetKeyDown(KeyCode.Space))
                    StartRun(run);

                if (back)
                    page = MenuPage.Main;
                break;

            case MenuPage.Meta:
            case MenuPage.Settings:
                if (back)
                {
                    if (page == MenuPage.Settings)
                        GameSettings.Save();

                    page = MenuPage.Main;
                }
                break;
        }
    }

    private void ActivateMain(RunManager run, int index)
    {
        switch (index)
        {
            case 0:
                // Zorluk kademesi açık değilse doğrudan başla.
                if (MetaProgress.HeatUnlocked > 0)
                    page = MenuPage.NewRun;
                else
                    StartRun(run);
                break;

            case 1:
                page = MenuPage.Meta;
                break;

            case 2:
                pendingUiScale = -1f;
                page = MenuPage.Settings;
                break;

            case 3:
                QuitGame();
                break;
        }
    }

    private void StartRun(RunManager run)
    {
        page = MenuPage.Main;
        run.RequestStart();
    }

    private static void QuitGame()
    {
        GameSettings.Save();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static bool EnterPressed()
    {
        return
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    private static bool NumberPressed(int index)
    {
        return
            Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + index)) ||
            Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad1 + index));
    }

    // =========================================================
    // ÇİZİM
    // =========================================================

    private void OnGUI()
    {
        RunManager run = RunManager.Instance;

        if (run == null || run.Inventory == null)
            return;

        float scale = GameSettings.GuiScale(uiScale);

        GUI.matrix =
            Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        float width = Screen.width / scale;
        float height = Screen.height / scale;

        EnsureStyles();

        if (run.State == RunState.Lobby)
        {
            DrawMainMenu(run, width, height);
            return;
        }

        DrawHud(run, width, height);
        DrawBossBar(width);

        switch (run.State)
        {
            case RunState.AbilityOffer:
                DrawAbilityOffer(run, width, height);
                break;

            case RunState.Offer:
                DrawOffer(run, width, height);
                break;

            case RunState.ChoosingRoom:
                DrawDoors(run, width, height);
                break;

            case RunState.Shop:
                DrawShop(run, width, height);
                break;

            case RunState.Rest:
                DrawRest(run, width, height);
                break;

            case RunState.Fighting:
            case RunState.Starting:
                if (Time.unscaledTime < run.BannerUntil)
                    DrawBanner(run.BannerText, width, height);
                break;

            case RunState.Cleared:
                DrawBanner(
                    run.CurrentRoom == RoomType.Boss
                        ? "BOSS YENİLDİ"
                        : "ODA TEMİZLENDİ",
                    width,
                    height
                );
                break;

            case RunState.Dead:
            case RunState.Victory:
                DrawResult(run, width, height);
                break;
        }

        if (
            (run.State == RunState.Fighting || run.State == RunState.Cleared) &&
            Input.GetKey(liveStatsKey) &&
            !run.IsPaused
        )
        {
            DrawLiveStats(run, width);
        }

        if (run.IsPaused)
            DrawPause(run, width, height);
        else
            pauseSettings = false;
    }

    // =========================================================
    // LOBİ
    // =========================================================

    // =========================================================
    // ANA MENÜ
    // =========================================================

    private void DrawMainMenu(RunManager run, float width, float height)
    {
        // Sol tarafta koyu geçiş: arkadaki orman görünür kalsın.
        GUI.DrawTexture(new Rect(0, 0, Mathf.Min(width, 760f), height), sideFadeTexture);

        switch (page)
        {
            case MenuPage.Main:
                DrawMainPage(run, width, height);
                break;

            case MenuPage.NewRun:
                DrawLobby(run, width, height);
                break;

            case MenuPage.Meta:
                DrawMetaPage(width, height);
                break;

            case MenuPage.Settings:
                DrawSettingsPanel(width, height, () =>
                {
                    GameSettings.Save();
                    page = MenuPage.Main;
                });
                break;
        }
    }

    private void DrawMainPage(RunManager run, float width, float height)
    {
        float x = 70f;
        float y = height * 0.2f;

        // Başlık (gölgeli).
        Color old = GUI.color;

        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.Label(new Rect(x + 3f, y + 3f, 600f, 70f), "PROJECT M", menuTitleStyle);
        GUI.color = old;
        GUI.Label(new Rect(x, y, 600f, 70f), "PROJECT M", menuTitleStyle);

        y += 70f;

        GUI.Label(
            new Rect(x + 4f, y, 600f, 18f),
            "<color=#BBBBBB>denge • parry • infaz</color>",
            smallStyle
        );

        y += 60f;

        Event e = Event.current;

        // Fare hareket ettiyse üzerindeki madde seçilir (klavye seçimini ezmesin).
        bool mouseMoved =
            e.type == EventType.Repaint &&
            (e.mousePosition - lastMenuMouse).sqrMagnitude > 1f;

        for (int i = 0; i < MainItems.Length; i++)
        {
            Rect r = new Rect(x, y, 320f, 34f);

            if (mouseMoved && r.Contains(e.mousePosition))
                mainIndex = i;

            bool selected = i == mainIndex;

            string label = MainItems[i];

            if (i == 1)
                label += "   <size=14><color=#C9A0FF>◆ " + MetaProgress.Essence + " öz</color></size>";

            string text =
                selected
                    ? "<color=#FFD54A>›  " + label + "</color>"
                    : "<color=#DDDDDD>    " + label + "</color>";

            if (GUI.Button(r, text, menuItemStyle))
            {
                mainIndex = i;
                ActivateMain(run, i);
            }

            y += 40f;
        }

        if (e.type == EventType.Repaint)
            lastMenuMouse = e.mousePosition;

        // Alt bilgi.
        string stats =
            "Koşu " + MetaProgress.Runs +
            "   •   Zafer " + MetaProgress.Wins +
            "   •   En iyi perde " + MetaProgress.BestAct;

        if (run.LastEssence > 0)
            stats += "   •   <color=#C9A0FF>son koşu +" + run.LastEssence + " öz</color>";

        GUI.Label(new Rect(x, height - 56f, 700f, 18f), stats, smallStyle);

        GUI.Label(
            new Rect(x, height - 36f, 900f, 18f),
            "[Q] yetenek   [E] infaz   [Esc] duraklat   W/S + Enter: menü",
            menuHintStyle
        );
    }

    // ---------------- KALICI GELİŞİM (DEMİRCİ) ----------------

    private void DrawMetaPage(float width, float height)
    {
        MetaUpgrade[] list = MetaUpgrades.All;

        float rowH = 42f;
        float panelWidth = Mathf.Min(660f, width - 2f * Pad);
        float panelHeight = Mathf.Min(height - 2f * Pad, 96f + list.Length * rowH + 50f);

        Rect panel =
            new Rect(
                Mathf.Max(Pad, 60f),
                (height - panelHeight) * 0.5f,
                panelWidth,
                panelHeight
            );

        GUI.Box(panel, GUIContent.none, panelStyle);

        float y = panel.y + 12f;

        GUI.Label(new Rect(panel.x + 16f, y, panelWidth, 30f), "<b>DEMİRCİ</b>  <size=14><color=#BBBBBB>kalıcı gelişim</color></size>", titleStyle);

        GUI.Label(
            new Rect(panel.x, y, panelWidth - 16f, 30f),
            "<size=18><color=#C9A0FF>◆ " + MetaProgress.Essence + " öz</color></size>",
            RightAligned(titleStyle)
        );

        y += 30f;

        GUI.Label(
            new Rect(panel.x + 16f, y, panelWidth - 32f, 32f),
            "<color=#AAAAAA>Öz koşu sonunda kazanılır (öldürme, perde, boss, zafer; ısı arttırır). " +
            "Ana menüye dönmek de koşuyu bitirir.</color>",
            legendStyle
        );

        y += 40f;

        for (int i = 0; i < list.Length; i++)
        {
            MetaUpgrade u = list[i];

            int level = MetaProgress.UpgradeLevel(u.id);
            int max = u.costs.Length;
            int cost = MetaProgress.NextCost(u);

            Rect row = new Rect(panel.x + 12f, y, panelWidth - 24f, rowH - 4f);

            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, i % 2 == 0 ? 0.04f : 0.08f);
            GUI.DrawTexture(row, Texture2D.whiteTexture);
            GUI.color = old;

            string nameColor = u.isAbility ? "#" + ColorUtility.ToHtmlStringRGB(AbilityInfo.Color(u.ability)) : "#FFFFFF";

            string pips = "";

            if (!u.isAbility)
            {
                for (int p = 0; p < max; p++)
                    pips += p < level ? "<color=#FFD54A>■</color>" : "<color=#555555>■</color>";
            }

            GUI.Label(
                new Rect(row.x + 8f, row.y + 2f, row.width - 170f, 18f),
                "<b><color=" + nameColor + ">" + u.name + "</color></b>   " + pips,
                labelStyle
            );

            GUI.Label(
                new Rect(row.x + 8f, row.y + 20f, row.width - 170f, 16f),
                "<color=#BBBBBB>" + u.description + "</color>",
                smallStyle
            );

            Rect button = new Rect(row.xMax - 150f, row.y + 7f, 142f, 24f);

            if (cost < 0)
            {
                GUI.enabled = false;
                GUI.Button(button, u.isAbility ? "AÇIK" : "MAKS", buttonStyle);
                GUI.enabled = true;
            }
            else
            {
                bool afford = MetaProgress.Essence >= cost;

                GUI.enabled = afford;

                string label =
                    (u.isAbility ? "AÇ  " : "AL  ") +
                    "<color=" + (afford ? "#C9A0FF" : "#FF6A50") + ">◆ " + cost + "</color>";

                if (GUI.Button(button, label, buttonStyle))
                {
                    if (MetaProgress.TryBuy(u))
                    {
                        metaMessage = u.name + (u.isAbility ? " açıldı!" : " → seviye " + MetaProgress.UpgradeLevel(u.id));
                        metaMessageUntil = Time.unscaledTime + 2.5f;
                    }
                }

                GUI.enabled = true;
            }

            y += rowH;
        }

        if (Time.unscaledTime < metaMessageUntil)
        {
            GUI.Label(
                new Rect(panel.x, panel.yMax - 62f, panelWidth, 18f),
                "<color=#7CE08A>" + metaMessage + "</color>",
                richCentered
            );
        }

        if (
            GUI.Button(
                new Rect(panel.x + panelWidth * 0.5f - 90f, panel.yMax - 36f, 180f, 26f),
                "GERİ  <color=#888888>[Esc]</color>",
                buttonStyle
            )
        )
        {
            page = MenuPage.Main;
        }
    }

    // ---------------- AYARLAR (menü + duraklatma ortak) ----------------

    private void DrawSettingsPanel(float width, float height, System.Action onBack)
    {
        float panelWidth = 460f;
        float panelHeight = 270f;

        Rect panel =
            new Rect(
                (width - panelWidth) * 0.5f,
                (height - panelHeight) * 0.5f,
                panelWidth,
                panelHeight
            );

        GUI.Box(panel, GUIContent.none, panelStyle);

        float y = panel.y + 14f;

        GUI.Label(new Rect(panel.x, y, panelWidth, 30f), "AYARLAR", Centered(bannerStyle));

        y += 46f;

        float lx = panel.x + 20f;
        float sx = panel.x + 200f;
        float sw = panelWidth - 220f - 60f;

        // Arayüz boyutu: sürüklerken uygulanmaz (kaydırıcı ölçekle kayıp
        // titremesin), bırakınca uygulanır.
        if (pendingUiScale < 0f)
            pendingUiScale = GameSettings.UiScale;

        GUI.Label(new Rect(lx, y, 180f, 20f), "Arayüz boyutu", labelStyle);
        pendingUiScale = GUI.HorizontalSlider(new Rect(sx, y + 5f, sw, 16f), pendingUiScale, GameSettings.MinUiScale, GameSettings.MaxUiScale);
        GUI.Label(new Rect(sx + sw + 8f, y, 60f, 20f), pendingUiScale.ToString("0.00") + "x", smallStyle);

        if (GUIUtility.hotControl == 0 && Mathf.Abs(pendingUiScale - GameSettings.UiScale) > 0.01f)
        {
            GameSettings.UiScale = pendingUiScale;
            pendingUiScale = GameSettings.UiScale;
        }

        y += 32f;

        GUI.Label(new Rect(lx, y, 180f, 20f), "Dünya yazıları", labelStyle);
        float world = GUI.HorizontalSlider(new Rect(sx, y + 5f, sw, 16f), GameSettings.WorldTextScale, GameSettings.MinUiScale, GameSettings.MaxUiScale);
        GUI.Label(new Rect(sx + sw + 8f, y, 60f, 20f), GameSettings.WorldTextScale.ToString("0.00") + "x", smallStyle);

        if (Mathf.Abs(world - GameSettings.WorldTextScale) > 0.001f)
            GameSettings.WorldTextScale = world;

        y += 32f;

        GUI.Label(new Rect(lx, y, 180f, 20f), "Ses", labelStyle);
        float volume = GUI.HorizontalSlider(new Rect(sx, y + 5f, sw, 16f), GameSettings.Volume, 0f, 1f);
        GUI.Label(new Rect(sx + sw + 8f, y, 60f, 20f), "%" + Mathf.RoundToInt(GameSettings.Volume * 100f), smallStyle);

        if (Mathf.Abs(volume - GameSettings.Volume) > 0.001f)
            GameSettings.Volume = volume;

        y += 32f;

        bool full = GameSettings.Fullscreen;

        if (GUI.Button(new Rect(lx, y, 200f, 24f), "Tam ekran: " + (full ? "<color=#7CE08A>AÇIK</color>" : "<color=#888888>KAPALI</color>"), buttonStyle))
            GameSettings.Fullscreen = !full;

        if (GUI.Button(new Rect(lx + 212f, y, 200f, 24f), "Varsayılan boyutlar", buttonStyle))
        {
            GameSettings.UiScale = 1f;
            GameSettings.WorldTextScale = 1f;
            pendingUiScale = 1f;
        }

        if (
            GUI.Button(
                new Rect(panel.x + panelWidth * 0.5f - 90f, panel.yMax - 36f, 180f, 26f),
                "GERİ  <color=#888888>[Esc]</color>",
                buttonStyle
            )
        )
        {
            onBack();
        }
    }

    // ---------------- DURAKLATMA ----------------

    private void DrawPause(RunManager run, float width, float height)
    {
        GUI.DrawTexture(new Rect(0, 0, width, height), dimTexture);

        if (pauseSettings)
        {
            DrawSettingsPanel(width, height, () =>
            {
                GameSettings.Save();
                pauseSettings = false;
            });

            return;
        }

        float panelWidth = 320f;
        float panelHeight = 200f;

        Rect panel =
            new Rect(
                (width - panelWidth) * 0.5f,
                (height - panelHeight) * 0.5f,
                panelWidth,
                panelHeight
            );

        GUI.Box(panel, GUIContent.none, panelStyle);

        GUI.Label(new Rect(panel.x, panel.y + 12f, panelWidth, 30f), "DURAKLATILDI", Centered(bannerStyle));

        float bx = panel.x + 40f;
        float bw = panelWidth - 80f;
        float y = panel.y + 58f;

        if (GUI.Button(new Rect(bx, y, bw, 28f), "DEVAM  <color=#888888>[Esc]</color>", buttonStyle))
            run.SetPaused(false);

        y += 36f;

        if (GUI.Button(new Rect(bx, y, bw, 28f), "AYARLAR", buttonStyle))
        {
            pendingUiScale = -1f;
            pauseSettings = true;
        }

        y += 36f;

        string abandon =
            confirmAbandon
                ? "<color=#FF8A80>EMİN MİSİN? (koşu biter)</color>"
                : "ANA MENÜ";

        if (GUI.Button(new Rect(bx, y, bw, 28f), abandon, buttonStyle))
        {
            if (!confirmAbandon)
            {
                confirmAbandon = true;
            }
            else
            {
                confirmAbandon = false;
                page = MenuPage.Main;
                run.AbandonRun();
            }
        }
    }

    // ---------------- YETENEK SEÇİMİ ----------------

    private void DrawAbilityOffer(RunManager run, float width, float height)
    {
        GUI.DrawTexture(new Rect(0, 0, width, height), dimTexture);

        IReadOnlyList<AbilityType> offers = run.AbilityOffers;

        float cardWidth = 200f;
        float cardHeight = 190f;
        float gap = 12f;

        float total = offers.Count * cardWidth + Mathf.Max(0, offers.Count - 1) * gap;

        if (total > width - 40f)
        {
            float f = (width - 40f) / total;
            cardWidth *= f;
            gap *= f;
            total = width - 40f;
        }

        float x = (width - total) * 0.5f;
        float cardY = (height - cardHeight) * 0.5f;

        GUI.Label(new Rect(0, cardY - 62f, width, 34f), "YETENEĞİNİ SEÇ", bannerStyle);

        GUI.Label(
            new Rect(0, cardY - 28f, width, 18f),
            "<color=#BBBBBB>[Q] ile kullanılır  •  parry ve öldürme bekleme süresini kısaltır  •  dükkanda yükseltilir</color>",
            richCentered
        );

        for (int i = 0; i < offers.Count; i++)
        {
            if (GUI.Button(new Rect(x, cardY, cardWidth, cardHeight), AbilityCardText(offers[i], i + 1), cardStyle))
                run.ChooseAbility(i);

            x += cardWidth + gap;
        }

        GUI.Label(
            new Rect(0, cardY + cardHeight + 10f, width, 18f),
            "1 / 2 / 3 / 4 ya da tıkla   <color=#888888>(yeni yetenekler: ana menü → Kalıcı Gelişim)</color>",
            Centered(tinyStyle)
        );
    }

    private static string AbilityCardText(AbilityType type, int number)
    {
        string color = "#" + ColorUtility.ToHtmlStringRGB(AbilityInfo.Color(type));

        bool last = MetaProgress.SelectedAbility == (int)type;

        return
            "<size=11><color=#888888>" + number + "</color></size>  " +
            "<b><color=" + color + ">" + AbilityInfo.Name(type) + "</color></b>" +
            (last ? "  <size=10><color=#888888>(son seçim)</color></size>" : "") + "\n\n" +
            "<size=12>" + AbilityInfo.Description(type) + "</size>\n\n" +
            "<size=11><color=#FFD54A>Bekleme " + AbilityInfo.BaseCooldown(type).ToString("0") + " sn</color></size>";
    }

    // ---------------- YENİ KOŞU (zorluk) ----------------

    private void DrawLobby(RunManager run, float width, float height)
    {
        float panelWidth = 420f;
        float panelHeight = MetaProgress.HeatUnlocked > 0 ? 250f : 190f;

        Rect panel =
            new Rect(
                (width - panelWidth) * 0.5f,
                (height - panelHeight) * 0.5f,
                panelWidth,
                panelHeight
            );

        GUI.Box(panel, GUIContent.none, panelStyle);

        float y = panel.y + 14f;

        GUI.Label(new Rect(panel.x, y, panelWidth, 34f), "YENİ KOŞU", bigTitleStyle);

        if (GUI.Button(new Rect(panel.x + 8f, panel.y + 8f, 70f, 22f), "‹ GERİ", buttonStyle))
            page = MenuPage.Main;

        y += 40f;

        GUI.Label(
            new Rect(panel.x, y, panelWidth, 18f),
            "Koşu " + MetaProgress.Runs +
            "   •   Zafer " + MetaProgress.Wins +
            "   •   En iyi perde " + MetaProgress.BestAct,
            Centered(smallStyle)
        );

        y += 18f;

        GUI.Label(
            new Rect(panel.x, y, panelWidth, 16f),
            "Toplam execute " + MetaProgress.TotalExecutes +
            "   •   Toplam parry " + MetaProgress.TotalParries,
            Centered(tinyStyle)
        );

        y += 30f;

        if (MetaProgress.HeatUnlocked > 0)
        {
            int heat = run.Heat;

            GUI.Label(
                new Rect(panel.x, y, panelWidth, 24f),
                "<color=#888888>[A]</color>   <b>ISI " + heat + "</b> / " +
                MetaProgress.HeatUnlocked + "   <color=#888888>[D]</color>",
                Centered(titleStyle)
            );

            y += 26f;

            string desc = "";

            for (int i = 1; i <= heat; i++)
                desc += RunManager.DescribeHeat(i) + "\n";

            if (heat == 0)
                desc = RunManager.DescribeHeat(0);

            GUI.Label(
                new Rect(panel.x + 20f, y, panelWidth - 40f, 60f),
                "<color=#FFB080>" + desc.TrimEnd() + "</color>",
                Centered(tinyStyle)
            );

            y += 62f;
        }
        else
        {
            GUI.Label(
                new Rect(panel.x, y, panelWidth, 16f),
                "<color=#888888>Bir koşuyu kazan: zorluk kademeleri açılır</color>",
                Centered(tinyStyle)
            );

            y += 26f;
        }

        if (
            GUI.Button(
                new Rect(panel.x + panelWidth * 0.5f - 90f, panel.yMax - 40f, 180f, 28f),
                "BAŞLA  <color=#888888>[Enter]</color>",
                buttonStyle
            )
        )
        {
            StartRun(run);
        }
    }

    // =========================================================
    // HUD
    // =========================================================

    private void DrawHud(RunManager run, float width, float height)
    {
        IReadOnlyList<CharmInventory.Entry> entries =
            run.Inventory.Entries;

        float panelWidth = 220f;
        float lineH = 15f;
        float panelHeight = 46f + entries.Count * lineH + (entries.Count > 0 ? 6f : 0f);

        Rect panel = new Rect(Pad, Pad, panelWidth, panelHeight);

        GUI.Box(panel, GUIContent.none, panelStyle);

        string where =
            "PERDE " + Mathf.Max(1, run.Act) + "  <size=11><color=#BBBBBB>" +
            (run.RoomInAct > run.RoomsPerAct
                ? "BOSS"
                : "oda " + Mathf.Max(1, run.RoomInAct) + "/" + run.RoomsPerAct) +
            (run.Heat > 0 ? "  •  ısı " + run.Heat : "") +
            "</color></size>";

        GUI.Label(
            new Rect(panel.x + 8f, panel.y + 4f, panelWidth - 16f, 20f),
            where,
            titleStyle
        );

        string goldLine =
            "<color=#FFD54A>" + GoldIcon + " " + run.Gold + "</color>";

        // Son kazanılan altın: kısa süre görünür.
        float sinceGold = Time.unscaledTime - run.LastGoldTime;

        if (sinceGold < 1.6f)
        {
            float a = 1f - Mathf.Clamp01((sinceGold - 1.0f) / 0.6f);

            goldLine +=
                "  <color=#FFE08A" + Mathf.RoundToInt(a * 255).ToString("X2") + ">+" +
                run.LastGoldGain + " " + run.LastGoldReason + "</color>";
        }

        if (run.State == RunState.Fighting)
            goldLine += "   <color=#BBBBBB>düşman " + run.AliveEnemies + "</color>";

        GUI.Label(
            new Rect(panel.x + 8f, panel.y + 24f, panelWidth - 12f, 18f),
            goldLine,
            smallStyle
        );

        float y = panel.y + 46f;

        for (int i = 0; i < entries.Count; i++)
        {
            CharmInventory.Entry e = entries[i];

            string line = "• " + e.definition.displayName;

            if (e.stacks > 1)
                line += " <color=#9AD1FF>x" + e.stacks + "</color>";

            string status =
                e.effect != null ? e.effect.Status() : "";

            if (!string.IsNullOrEmpty(status))
                line += "  <color=#FFD54A>" + status + "</color>";

            GUI.Label(
                new Rect(panel.x + 8f, y, panelWidth - 12f, lineH),
                line,
                charmStyle
            );

            y += lineH;
        }

        if (showBuildStats)
            DrawBuildStats(height);

        DrawRiposte(width);
    }

    private void DrawBuildStats(float height)
    {
        PlayerStats stats = PlayerStats.Current;

        if (stats == null)
            return;

        string text =
            "Kritik %" + Mathf.RoundToInt(stats.CritChance * 100f) +
            " (x" + stats.CritMultiplier.ToString("0.0") + ")" +
            "   Denge x" +
            stats.Get(StatType.BalanceDamage, 1f).ToString("0.00") +
            "   Can x" +
            stats.Get(StatType.HealthDamage, 1f).ToString("0.00") +
            "   <color=#888888>[TAB] istatistik</color>";

        GUI.Label(
            new Rect(Pad + 2f, height - 20f, 600f, 16f),
            text,
            tinyStyle
        );
    }

    private void DrawRiposte(float width)
    {
        if (!ParryRiposte.IsActive)
            return;

        GUI.Label(
            new Rect(0, RiposteY(), width, 24f),
            "RİPOSTE " + new string('●', Mathf.Max(0, ParryRiposte.HitsLeft)),
            riposteStyle
        );
    }

    // Boss çubuğu varsa riposte onun altında.
    private float RiposteY()
    {
        BossController boss = BossController.Current;

        return boss != null && boss.IsAlive ? 60f : 10f;
    }

    // =========================================================
    // BOSS ÇUBUĞU
    // =========================================================

    private void DrawBossBar(float width)
    {
        BossController boss = BossController.Current;

        if (boss == null || !boss.IsAlive)
            return;

        float barWidth = Mathf.Min(460f, width - 2f * 240f);
        barWidth = Mathf.Max(260f, barWidth);

        float x = (width - barWidth) * 0.5f;
        float y = 10f;

        GUI.Label(
            new Rect(x, y, barWidth, 18f),
            "<b>" + boss.BossName.ToUpperInvariant() + "</b>" +
            (boss.InPhase2 ? "   <color=#FF6A50>FAZ 2</color>" : ""),
            Centered(smallStyle)
        );

        y += 19f;

        Color old = GUI.color;

        // Can
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(x - 1f, y - 1f, barWidth + 2f, 10f), Texture2D.whiteTexture);

        GUI.color = new Color(0.85f, 0.18f, 0.15f);
        GUI.DrawTexture(new Rect(x, y, barWidth * boss.HealthPercent, 8f), Texture2D.whiteTexture);

        y += 11f;

        // Denge
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(new Rect(x - 1f, y - 1f, barWidth + 2f, 5f), Texture2D.whiteTexture);

        GUI.color = new Color(1f, 0.8f, 0.25f);
        GUI.DrawTexture(new Rect(x, y, barWidth * boss.BalancePercent, 3f), Texture2D.whiteTexture);

        GUI.color = old;
    }

    // =========================================================
    // CANLI İSTATİSTİK (TAB)
    // =========================================================

    private void DrawLiveStats(RunManager run, float width)
    {
        RunStats s = run.Stats;

        if (s == null)
            return;

        float panelWidth = 360f;

        Rect panel =
            new Rect(width - panelWidth - Pad, Pad, panelWidth, 220f);

        GUI.Box(panel, GUIContent.none, panelStyle);

        float x = panel.x + 10f;
        float w = panelWidth - 20f;
        float y = panel.y + 6f;

        GUI.Label(new Rect(x, y, w, 18f), s.HeaderLine(), smallStyle);

        y += 22f;

        y = DrawOutcomeBlock(new Rect(x, y, w, 0f), "NORMAL SALDIRI", s.Normal, false);
        y += 4f;
        y = DrawOutcomeBlock(new Rect(x, y, w, 0f), "ENGELLENEMEZ", s.Unblockable, true);
        y += 4f;

        GUI.Label(
            new Rect(x, y, w, 32f),
            s.DefenseLine() + "\n" + s.KillsLine(),
            tinyStyle
        );
    }

    // =========================================================
    // CHARM SEÇİMİ
    // =========================================================

    private void DrawOffer(RunManager run, float width, float height)
    {
        GUI.DrawTexture(new Rect(0, 0, width, height), dimTexture);

        IReadOnlyList<CharmDefinition> offers = run.Offers;

        float cardWidth = 210f;
        float cardHeight = 190f;
        float gap = 12f;

        float total =
            offers.Count * cardWidth +
            Mathf.Max(0, offers.Count - 1) * gap;

        // Çok kart varsa küçült.
        if (total > width - 40f)
        {
            float f = (width - 40f) / total;
            cardWidth *= f;
            gap *= f;
            total = width - 40f;
        }

        float x = (width - total) * 0.5f;
        float cardY = (height - cardHeight) * 0.5f;

        GUI.Label(
            new Rect(0, cardY - 46f, width, 34f),
            string.IsNullOrEmpty(run.OfferTitle) ? "BİR CHARM SEÇ" : run.OfferTitle,
            bannerStyle
        );

        for (int i = 0; i < offers.Count; i++)
        {
            if (
                GUI.Button(
                    new Rect(x, cardY, cardWidth, cardHeight),
                    CharmCardText(run, offers[i], i + 1, ""),
                    cardStyle
                )
            )
            {
                run.Choose(i);
            }

            x += cardWidth + gap;
        }

        GUI.Label(
            new Rect(0, cardY + cardHeight + 10f, width, 18f),
            "1 / 2 / 3 ya da tıkla",
            Centered(tinyStyle)
        );
    }

    private static string CharmCardText(
        RunManager run,
        CharmDefinition def,
        int number,
        string footer
    )
    {
        int owned = run.Inventory.GetStacks(def);
        int next = owned + 1;

        string level =
            owned == 0
                ? "Yeni"
                : "Seviye " + owned + " → " + next;

        string effect =
            def.effect != null
                ? def.effect.Describe(next)
                : "";

        return
            "<size=11><color=#888888>" + number + "</color></size>  " +
            "<b>" + def.displayName + "</b>\n" +
            "<size=11><color=#9AD1FF>" + level + "</color></size>\n\n" +
            "<size=12>" + def.description + "</size>\n\n" +
            "<size=11><color=#FFD54A>" + effect + "</color></size>" +
            footer;
    }

    // =========================================================
    // KAPI SEÇİMİ
    // =========================================================

    private void DrawDoors(RunManager run, float width, float height)
    {
        GUI.DrawTexture(new Rect(0, 0, width, height), dimTexture);

        IReadOnlyList<RoomType> doors = run.DoorOptions;

        float cardWidth = 180f;
        float cardHeight = 150f;
        float gap = 14f;

        float total =
            doors.Count * cardWidth +
            Mathf.Max(0, doors.Count - 1) * gap;

        float x = (width - total) * 0.5f;
        float cardY = (height - cardHeight) * 0.5f;

        GUI.Label(
            new Rect(0, cardY - 62f, width, 30f),
            "SIRADAKİ ODA",
            bannerStyle
        );

        Health hp = PlayerHealth();

        GUI.Label(
            new Rect(0, cardY - 30f, width, 18f),
            "Perde " + run.Act + "  •  oda " + run.RoomInAct + "/" + run.RoomsPerAct +
            (run.RoomInAct == run.RoomsPerAct ? "  <color=#FF8A80>(sonra BOSS)</color>" : "") +
            "      Can " + (hp != null ? hp.CurrentHealth + "/" + hp.MaxHealth : "-") +
            "      <color=#FFD54A>" + GoldIcon + " " + run.Gold + "</color>",
            richCentered
        );

        for (int i = 0; i < doors.Count; i++)
        {
            if (
                GUI.Button(
                    new Rect(x, cardY, cardWidth, cardHeight),
                    DoorText(doors[i], i + 1, run),
                    cardStyle
                )
            )
            {
                run.ChooseDoor(i);
            }

            x += cardWidth + gap;
        }
    }

    private static string DoorText(RoomType type, int number, RunManager run)
    {
        string title;
        string color;
        string desc;

        switch (type)
        {
            case RoomType.Elite:
                title = "ELİT";
                color = "#FF6A50";
                desc = "Güçlü düşman.\n\nÖdül: <b>2 charm</b> + bol altın.";
                break;

            case RoomType.Shop:
                title = "DÜKKAN";
                color = "#FFD54A";
                desc = "Altınla charm al, iyileş ya da charm sil.";
                break;

            case RoomType.Rest:
                title = "DİNLENME";
                color = "#7CE08A";
                desc =
                    "%" + run.RestHealPercentDisplay + " iyileş\nya da\nbir charm'ı güçlendir.";
                break;

            default:
                title = "DÖVÜŞ";
                color = "#DDDDDD";
                desc = "Normal düşman.\n\nÖdül: charm seçimi.";
                break;
        }

        return
            "<size=11><color=#888888>" + number + "</color></size>  " +
            "<size=17><b><color=" + color + ">" + title + "</color></b></size>\n\n" +
            "<size=12>" + desc + "</size>";
    }

    // =========================================================
    // DÜKKAN
    // =========================================================

    private void DrawShop(RunManager run, float width, float height)
    {
        GUI.DrawTexture(new Rect(0, 0, width, height), dimTexture);

        float panelWidth = Mathf.Min(720f, width - 2f * Pad);
        float panelHeight = Mathf.Min(height - 2f * Pad, 530f);

        Rect panel =
            new Rect(
                (width - panelWidth) * 0.5f,
                (height - panelHeight) * 0.5f,
                panelWidth,
                panelHeight
            );

        GUI.Box(panel, GUIContent.none, panelStyle);

        float y = panel.y + 10f;

        GUI.Label(
            new Rect(panel.x, y, panelWidth, 30f),
            "DÜKKAN   <color=#FFD54A>" + GoldIcon + " " + run.Gold + "</color>",
            Centered(bannerStyle)
        );

        y += 40f;

        IReadOnlyList<ShopItem> items = run.ShopItems;

        // ---------------- CHARM KARTLARI ----------------

        int charmCount = 0;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].kind == ShopItemKind.Charm)
                charmCount++;
        }

        float cardWidth = 200f;
        float cardHeight = 180f;
        float gap = 12f;

        float total =
            charmCount * cardWidth +
            Mathf.Max(0, charmCount - 1) * gap;

        float x = panel.x + (panelWidth - total) * 0.5f;

        int number = 1;

        for (int i = 0; i < items.Count; i++)
        {
            ShopItem item = items[i];

            if (item.kind != ShopItemKind.Charm || item.charm == null)
                continue;

            bool canBuy =
                !item.sold &&
                run.CanAfford(item.price) &&
                run.Inventory.CanAdd(item.charm);

            string footer =
                item.sold
                    ? "\n\n<b><color=#888888>SATILDI</color></b>"
                    : "\n\n<b><color=" + (run.CanAfford(item.price) ? "#FFD54A" : "#FF6A50") +
                      ">" + GoldIcon + " " + item.price + "</color></b>";

            GUI.enabled = canBuy;

            if (
                GUI.Button(
                    new Rect(x, y, cardWidth, cardHeight),
                    CharmCardText(run, item.charm, number, footer),
                    cardStyle
                )
            )
            {
                run.BuyShopItem(i);
            }

            GUI.enabled = true;

            x += cardWidth + gap;
            number++;
        }

        y += cardHeight + 14f;

        // ---------------- İYİLEŞME / YENİLE ----------------

        float bx = panel.x + (panelWidth - (2f * 200f + 12f)) * 0.5f;

        for (int i = 0; i < items.Count; i++)
        {
            ShopItem item = items[i];

            if (item.kind != ShopItemKind.Heal)
                continue;

            Health hp = PlayerHealth();

            bool full = hp != null && hp.CurrentHealth >= hp.MaxHealth;

            GUI.enabled = !item.sold && !full && run.CanAfford(item.price);

            string label =
                item.sold
                    ? "İyileşme  <color=#888888>(alındı)</color>"
                    : number + "  İyileş   <color=#FFD54A>" + GoldIcon + " " + item.price + "</color>";

            if (GUI.Button(new Rect(bx, y, 200f, 26f), label, buttonStyle))
                run.BuyShopItem(i);

            GUI.enabled = true;

            number++;
        }

        GUI.enabled = run.CanAfford(run.CurrentRerollPrice);

        if (
            GUI.Button(
                new Rect(bx + 212f, y, 200f, 26f),
                "R  Yenile   <color=#FFD54A>" + GoldIcon + " " + run.CurrentRerollPrice + "</color>",
                buttonStyle
            )
        )
        {
            run.RerollShop();
        }

        GUI.enabled = true;

        y += 36f;

        // ---------------- YETENEK ----------------

        float ax = panel.x + (panelWidth - (2f * 300f + 12f)) * 0.5f;

        for (int i = 0; i < items.Count; i++)
        {
            ShopItem item = items[i];

            if (item.kind != ShopItemKind.Ability)
                continue;

            string name = AbilityInfo.Name(item.ability);
            string color = "#" + ColorUtility.ToHtmlStringRGB(AbilityInfo.Color(item.ability));

            PlayerAbility ability = run.Ability;

            string label;

            if (item.abilityUpgrade)
            {
                int lv = ability != null ? ability.Level : 1;

                label =
                    (i + 1) + "  Yükselt: <color=" + color + ">" + name + "</color> " +
                    lv + "→" + (lv + 1);
            }
            else
            {
                label =
                    (i + 1) + "  " + (ability != null && ability.HasAbility ? "Değiştir: " : "Al: ") +
                    "<color=" + color + ">" + name + "</color>";
            }

            label +=
                item.sold
                    ? "  <color=#888888>(alındı)</color>"
                    : "   <color=" + (run.CanAfford(item.price) ? "#FFD54A" : "#FF6A50") + ">" +
                      GoldIcon + " " + item.price + "</color>";

            GUI.enabled = !item.sold && run.CanAfford(item.price);

            if (GUI.Button(new Rect(ax, y, 300f, 26f), new GUIContent(label, AbilityInfo.Description(item.ability)), buttonStyle))
                run.BuyShopItem(i);

            GUI.enabled = true;

            ax += 312f;
        }

        if (!string.IsNullOrEmpty(GUI.tooltip))
        {
            GUI.Label(new Rect(panel.x + 16f, y + 28f, panelWidth - 32f, 16f), "<color=#BBBBBB>" + GUI.tooltip + "</color>", Centered(tinyStyle));
        }

        y += 50f;

        // ---------------- CHARM SİL ----------------

        IReadOnlyList<CharmInventory.Entry> owned = run.Inventory.Entries;

        if (owned.Count > 0)
        {
            GUI.Label(
                new Rect(panel.x + 16f, y, panelWidth - 32f, 16f),
                "Charm sil  <color=#FFD54A>" + GoldIcon + " " + run.RemovePriceNow + "</color>" +
                "  <color=#888888>(istemediğin charm'dan kurtul)</color>",
                smallStyle
            );

            y += 18f;

            float cx = panel.x + 16f;

            // Döngü sırasında silme listeyi değiştirebilir: kopya üzerinden.
            List<CharmDefinition> defs = new List<CharmDefinition>();

            for (int i = 0; i < owned.Count; i++)
                defs.Add(owned[i].definition);

            GUI.enabled = run.CanAfford(run.RemovePriceNow);

            for (int i = 0; i < defs.Count; i++)
            {
                string label = "x " + defs[i].displayName;

                float w = Mathf.Max(70f, buttonStyle.CalcSize(new GUIContent(label)).x + 12f);

                if (cx + w > panel.xMax - 16f)
                {
                    cx = panel.x + 16f;
                    y += 24f;
                }

                if (GUI.Button(new Rect(cx, y, w, 20f), label, buttonStyle))
                {
                    run.RemoveCharm(defs[i]);
                    break;
                }

                cx += w + 6f;
            }

            GUI.enabled = true;
        }

        // ---------------- ÇIK ----------------

        if (
            GUI.Button(
                new Rect(panel.x + panelWidth * 0.5f - 90f, panel.yMax - 36f, 180f, 26f),
                "DEVAM ET  <color=#888888>[Enter]</color>",
                buttonStyle
            )
        )
        {
            run.LeaveShop();
        }
    }

    // =========================================================
    // DİNLENME
    // =========================================================

    private void DrawRest(RunManager run, float width, float height)
    {
        GUI.DrawTexture(new Rect(0, 0, width, height), dimTexture);

        float cardWidth = 200f;
        float cardHeight = 130f;
        float gap = 16f;

        float x = (width - (2f * cardWidth + gap)) * 0.5f;
        float cardY = (height - cardHeight) * 0.5f;

        GUI.Label(new Rect(0, cardY - 60f, width, 30f), "DİNLENME", bannerStyle);

        Health hp = PlayerHealth();

        GUI.Label(
            new Rect(0, cardY - 28f, width, 18f),
            "Can " + (hp != null ? hp.CurrentHealth + "/" + hp.MaxHealth : "-"),
            richCentered
        );

        if (
            GUI.Button(
                new Rect(x, cardY, cardWidth, cardHeight),
                "<size=11><color=#888888>1</color></size>  " +
                "<size=17><b><color=#7CE08A>DİNLEN</color></b></size>\n\n" +
                "<size=12>Max canın %" + run.RestHealPercentDisplay + "'i kadar iyileş.</size>",
                cardStyle
            )
        )
        {
            run.ChooseRest(true);
        }

        GUI.enabled = run.CanUpgradeAnyCharm;

        if (
            GUI.Button(
                new Rect(x + cardWidth + gap, cardY, cardWidth, cardHeight),
                "<size=11><color=#888888>2</color></size>  " +
                "<size=17><b><color=#9AD1FF>ANTRENMAN</color></b></size>\n\n" +
                "<size=12>" +
                (run.CanUpgradeAnyCharm
                    ? "Sahip olduğun bir charm'ı bir seviye güçlendir."
                    : "Güçlendirilecek charm yok.") +
                "</size>",
                cardStyle
            )
        )
        {
            run.ChooseRest(false);
        }

        GUI.enabled = true;
    }

    // =========================================================
    // BANNER
    // =========================================================

    private void DrawBanner(string text, float width, float height)
    {
        if (string.IsNullOrEmpty(text))
            return;

        float bannerH = 40f;
        float y = height * 0.28f;

        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.DrawTexture(new Rect(0, y, width, bannerH), Texture2D.whiteTexture);
        GUI.color = old;

        GUI.Label(new Rect(0, y, width, bannerH), text, bannerStyle);
    }

    // =========================================================
    // KOŞU SONU / ZAFER
    // =========================================================

    private void DrawResult(RunManager run, float width, float height)
    {
        GUI.DrawTexture(new Rect(0, 0, width, height), dimTexture);

        RunStats s = run.Stats;

        IReadOnlyList<string> unlocks = run.NewUnlocks;

        float panelWidth = Mathf.Min(680f, width - 2f * Pad);
        float panelHeight = Mathf.Min(height - 2f * Pad, 580f);

        Rect panel =
            new Rect(
                (width - panelWidth) * 0.5f,
                (height - panelHeight) * 0.5f,
                panelWidth,
                panelHeight
            );

        GUI.Box(panel, GUIContent.none, panelStyle);

        float x = panel.x + 16f;
        float w = panelWidth - 32f;
        float y = panel.y + 10f;

        GUI.Label(
            new Rect(panel.x, y, panelWidth, 34f),
            run.State == RunState.Victory
                ? "<color=#FFD54A>ZAFER!</color>"
                : "KOŞU BİTTİ",
            bannerStyle
        );

        y += 38f;

        if (s == null || !s.HasResult)
        {
            DrawRestartHint(panel);
            return;
        }

        GUI.Label(new Rect(panel.x, y, panelWidth, 18f), s.HeaderLine(), Centered(smallStyle));

        y += 18f;

        GUI.Label(
            new Rect(panel.x, y, panelWidth, 18f),
            (s.Victory ? "<color=#FFD54A>" : "<color=#FF8A80>") + s.DeathText() + "</color>",
            richCentered
        );

        y += 20f;

        // Öz (kalıcı gelişim)
        if (run.LastEssence > 0)
        {
            GUI.Label(
                new Rect(panel.x, y, panelWidth, 18f),
                "<color=#C9A0FF><b>+" + run.LastEssence + " ÖZ</b></color>  <size=11><color=#888888>(" +
                run.LastEssenceBreakdown + ")  •  toplam " + MetaProgress.Essence + "</color></size>",
                richCentered
            );

            y += 18f;
        }

        // Yeni açılımlar
        for (int i = 0; i < unlocks.Count; i++)
        {
            GUI.Label(
                new Rect(panel.x, y, panelWidth, 16f),
                "<color=#7CE08A>+ " + unlocks[i] + "</color>",
                richCentered
            );

            y += 16f;
        }

        y += 8f;

        y = DrawOutcomeBlock(new Rect(x, y, w, 0f), "NORMAL SALDIRILARA CEVAP", s.Normal, false);
        y += 4f;
        y = DrawOutcomeBlock(new Rect(x, y, w, 0f), "ENGELLENEMEZ SALDIRILARA CEVAP", s.Unblockable, true);
        y += 6f;

        GUIStyle line = Centered(tinyStyle);

        GUI.Label(new Rect(panel.x, y, panelWidth, 15f), s.KillsLine() + "     " + s.DefenseLine(), line);
        y += 15f;
        GUI.Label(new Rect(panel.x, y, panelWidth, 15f), s.DamageTakenLine(), line);
        y += 15f;
        GUI.Label(new Rect(panel.x, y, panelWidth, 15f), s.DamageDealtLine(), line);
        y += 15f;
        GUI.Label(
            new Rect(panel.x, y, panelWidth, 15f),
            "Build: " + (s.BuildText.Length > 0 ? s.BuildText : "(charm yok)"),
            line
        );

        y += 22f;

        DrawStageTable(s, panel, y);

        DrawRestartHint(panel);
    }

    // Tıklanabilir "YENİ KOŞU" düğmesi (Enter / Space / R de çalışır).
    private void DrawRestartHint(Rect panel)
    {
        const float buttonWidth = 220f;
        const float buttonHeight = 24f;

        Rect button =
            new Rect(
                panel.x + (panel.width - buttonWidth) * 0.5f,
                panel.yMax - buttonHeight - 8f,
                buttonWidth,
                buttonHeight
            );

        if (GUI.Button(button, "ANA MENÜ  <color=#888888>[Enter]</color>", buttonStyle))
        {
            RunManager run = RunManager.Instance;

            if (run != null)
                run.RequestRestart();
        }
    }

    private float DrawOutcomeBlock(
        Rect area,
        string title,
        RunStats.AttackOutcomes o,
        bool unblockable
    )
    {
        float y = area.y;
        int total = o.Total;

        GUI.Label(
            new Rect(area.x, y, area.width, 16f),
            title + "  <color=#888888>(" + total + ")</color>",
            smallStyle
        );

        y += 17f;

        Rect bar = new Rect(area.x, y, area.width, 8f);

        Color old = GUI.color;

        GUI.color = new Color(1f, 1f, 1f, 0.1f);
        GUI.DrawTexture(bar, Texture2D.whiteTexture);

        if (total > 0)
        {
            float bx = bar.x;

            bx = Segment(bx, bar, o.parried, total, ColParry);
            bx = Segment(bx, bar, o.blocked, total, ColBlock);
            bx = Segment(bx, bar, o.dashed, total, ColDash);
            bx = Segment(bx, bar, o.missed, total, ColMissed);
            bx = Segment(bx, bar, o.interrupted, total, ColInterrupted);
            bx = Segment(bx, bar, o.iFrame, total, ColIFrame);
            Segment(bx, bar, o.hit, total, ColHit);
        }

        GUI.color = old;

        y += 11f;

        string legend =
            (unblockable
                ? ""
                : Legend("parry", o.parried, total, ColParry) +
                  Legend("block", o.blocked, total, ColBlock)) +
            Legend("dash", o.dashed, total, ColDash) +
            Legend("kaçtı", o.missed, total, ColMissed) +
            Legend("kesildi", o.interrupted, total, ColInterrupted) +
            Legend("i-frame", o.iFrame, total, ColIFrame) +
            Legend("YENDİ", o.hit, total, ColHit);

        float legendHeight =
            legendStyle.CalcHeight(new GUIContent(legend), area.width);

        GUI.Label(
            new Rect(area.x, y, area.width, legendHeight),
            legend,
            legendStyle
        );

        return y + legendHeight + 2f;
    }

    private static float Segment(
        float x,
        Rect bar,
        int count,
        int total,
        Color color
    )
    {
        if (count <= 0)
            return x;

        float w = bar.width * count / total;

        GUI.color = color;
        GUI.DrawTexture(new Rect(x, bar.y, w, bar.height), Texture2D.whiteTexture);

        return x + w;
    }

    private static string Legend(
        string name,
        int count,
        int total,
        Color color
    )
    {
        return
            "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">● " +
            name + " " + count + " " +
            RunStats.Pct(count, total) + "</color>   ";
    }

    private void DrawStageTable(RunStats s, Rect panel, float y)
    {
        string[] headers =
        {
            "Dövüş", "Süre", "Öldürme", "Parry", "Block", "Yendi", "Hasar"
        };

        float col = 70f;
        float tableWidth = col * headers.Length;
        float left = panel.x + (panel.width - tableWidth) * 0.5f;

        GUIStyle head = Centered(smallStyle);
        GUIStyle cell = Centered(tinyStyle);

        for (int c = 0; c < headers.Length; c++)
            GUI.Label(new Rect(left + c * col, y, col, 16f), headers[c], head);

        y += 17f;

        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        GUI.DrawTexture(new Rect(left, y, tableWidth, 1f), Texture2D.whiteTexture);
        GUI.color = old;

        y += 3f;

        IReadOnlyList<RunStats.StageRecord> stages = s.Stages;

        float bottom = panel.yMax - 30f;

        int maxRows =
            Mathf.Max(1, Mathf.Min(maxStageRows, Mathf.FloorToInt((bottom - y) / 14f)));

        int first = Mathf.Max(0, stages.Count - maxRows);

        for (int i = first; i < stages.Count; i++)
        {
            RunStats.StageRecord r = stages[i];

            string[] values =
            {
                r.stage + (r.cleared ? "" : " (öldü)"),
                RunStats.FormatTime(r.time),
                r.kills.ToString(),
                r.parries.ToString(),
                r.blocks.ToString(),
                r.hitsTaken.ToString(),
                r.damageTaken.ToString()
            };

            for (int c = 0; c < values.Length; c++)
                GUI.Label(new Rect(left + c * col, y, col, 14f), values[c], cell);

            y += 14f;
        }
    }

    // =========================================================
    // YARDIMCILAR
    // =========================================================

    private Health cachedPlayerHealth;

    private Health PlayerHealth()
    {
        if (cachedPlayerHealth == null)
        {
            PlayerController p = FindFirstObjectByType<PlayerController>();

            if (p != null)
                cachedPlayerHealth = p.GetComponent<Health>();
        }

        return cachedPlayerHealth;
    }

    // =========================================================
    // STİLLER
    // =========================================================

    private void EnsureStyles()
    {
        if (titleStyle != null && panelTexture != null)
            return;

        titleStyle = Make(16, FontStyle.Bold, Color.white);
        titleStyle.richText = true;

        labelStyle = Make(14, FontStyle.Normal, Color.white);
        labelStyle.richText = true;

        smallStyle = Make(12, FontStyle.Normal, Soft);
        smallStyle.richText = true;

        tinyStyle = Make(11, FontStyle.Normal, new Color(1f, 0.93f, 0.7f, 0.85f));
        tinyStyle.richText = true;

        charmStyle = Make(12, FontStyle.Normal, new Color(1f, 0.95f, 0.8f));
        charmStyle.richText = true;
        charmStyle.clipping = TextClipping.Clip;

        legendStyle = Make(11, FontStyle.Normal, Color.white);
        legendStyle.richText = true;
        legendStyle.wordWrap = true;

        riposteStyle = Make(18, FontStyle.Bold, Gold);
        riposteStyle.alignment = TextAnchor.MiddleCenter;

        bannerStyle = Make(24, FontStyle.Bold, Color.white);
        bannerStyle.alignment = TextAnchor.MiddleCenter;
        bannerStyle.richText = true;

        bigTitleStyle = Make(28, FontStyle.Bold, Gold);
        bigTitleStyle.alignment = TextAnchor.MiddleCenter;

        richCentered = Make(12, FontStyle.Normal, Color.white);
        richCentered.richText = true;
        richCentered.alignment = TextAnchor.MiddleCenter;

        dimTexture = Solid(new Color(0f, 0f, 0f, 0.6f));

        sideFadeTexture = HorizontalFade(new Color(0.02f, 0.03f, 0.04f), 0.85f);

        menuTitleStyle = Make(56, FontStyle.Bold, Gold);

        menuHintStyle = Make(11, FontStyle.Normal, new Color(1f, 1f, 1f, 0.6f));
        menuHintStyle.richText = true;

        panelTexture =
            Bordered(
                new Color(0.06f, 0.06f, 0.08f, 0.78f),
                new Color(1f, 1f, 1f, 0.18f)
            );

        cardTexture =
            Bordered(
                new Color(0.08f, 0.08f, 0.11f, 0.92f),
                new Color(1f, 0.82f, 0.3f, 0.35f)
            );

        cardHoverTexture =
            Bordered(
                new Color(0.14f, 0.12f, 0.08f, 0.95f),
                new Color(1f, 0.82f, 0.3f, 0.9f)
            );

        panelStyle = new GUIStyle();
        panelStyle.normal.background = panelTexture;
        panelStyle.border = new RectOffset(1, 1, 1, 1);

        cardStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            alignment = TextAnchor.UpperLeft,
            wordWrap = true,
            richText = true,
            padding = new RectOffset(12, 12, 10, 10),
            border = new RectOffset(1, 1, 1, 1)
        };

        ApplyButtonLook(cardStyle);

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter,
            richText = true,
            padding = new RectOffset(8, 8, 3, 3),
            border = new RectOffset(1, 1, 1, 1)
        };

        ApplyButtonLook(buttonStyle);

        // Ana menü maddeleri: arka plansız, büyük yazı.
        menuItemStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            richText = true
        };

        menuItemStyle.normal.textColor = Color.white;
        menuItemStyle.hover.textColor = Color.white;
        menuItemStyle.active.textColor = Color.white;
    }

    private void ApplyButtonLook(GUIStyle style)
    {
        style.normal.background = cardTexture;
        style.hover.background = cardHoverTexture;
        style.active.background = cardHoverTexture;
        style.focused.background = cardTexture;
        style.normal.textColor = Color.white;
        style.hover.textColor = Color.white;
        style.active.textColor = Color.white;
    }

    private static GUIStyle Make(int size, FontStyle fontStyle, Color color)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = size,
            fontStyle = fontStyle
        };

        style.normal.textColor = color;

        return style;
    }

    private static Texture2D Solid(Color color)
    {
        Texture2D tex = new Texture2D(1, 1);

        tex.SetPixel(0, 0, color);
        tex.Apply();

        return tex;
    }

    private static Texture2D Bordered(Color fill, Color border)
    {
        Texture2D tex = new Texture2D(3, 3);

        tex.filterMode = FilterMode.Point;

        for (int x = 0; x < 3; x++)
        {
            for (int y = 0; y < 3; y++)
            {
                bool edge = x == 0 || y == 0 || x == 2 || y == 2;
                tex.SetPixel(x, y, edge ? border : fill);
            }
        }

        tex.Apply();

        return tex;
    }

    // Soldan sağa sönen koyu şerit (ana menü arkası).
    private static Texture2D HorizontalFade(Color color, float maxAlpha)
    {
        const int w = 128;

        Texture2D tex = new Texture2D(w, 1, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int x = 0; x < w; x++)
        {
            float t = x / (float)(w - 1);
            float a = maxAlpha * (1f - Mathf.SmoothStep(0f, 1f, t));

            tex.SetPixel(x, 0, new Color(color.r, color.g, color.b, a));
        }

        tex.Apply();

        return tex;
    }

    private static GUIStyle RightAligned(GUIStyle source)
    {
        return new GUIStyle(source) { alignment = TextAnchor.MiddleRight };
    }

    private static GUIStyle Centered(GUIStyle source)
    {
        GUIStyle style = new GUIStyle(source)
        {
            alignment = TextAnchor.MiddleCenter
        };

        return style;
    }
}
