using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// BAŞLANGIÇ EKRANI + R İLE YENİDEN BAŞLATMA. Sahneye hiçbir şey eklemek gerekmez:
/// oyun açılınca kendi kendine kurulur (RuntimeInitializeOnLoadMethod).
///   - Menü: BAŞLA / AYARLAR / ÇIKIŞ. W-S veya yön tuşları gezinir, Enter seçer, fare de çalışır.
///   - BAŞLA: ekran kararır, menü kapanır, oyun başlar (oyuncu menü boyunca kilitli bekler).
///   - Oyun içinde R: ekran kararır, sahne yeniden yüklenir (menü tekrar çıkmaz).
/// Font: Resources/UI/ProjectM_Serif_Bold (+Regular). Yoksa Unity'nin yerleşik fontu kullanılır.
/// </summary>
public static class GameBoot
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!Application.isPlaying)
            return;

        if (Object.FindFirstObjectByType<GameFlow>() == null)
        {
            GameObject flow = new GameObject("~GameFlow");
            Object.DontDestroyOnLoad(flow);
            flow.AddComponent<GameFlow>();
        }

        GameVignette.Ensure();

        ScreenFade.Instance.Set(1f);

        GameObject menu = new GameObject("~MainMenu");
        menu.AddComponent<MainMenu>();
    }
}

/// <summary>R ile yeniden başlatma.</summary>
public class GameFlow : MonoBehaviour
{
    private bool restarting;

    private void Update()
    {
        if (restarting || MainMenu.Active)
            return;

        if (Input.GetKeyDown(KeyCode.R))
            StartCoroutine(Restart());
        else if (Input.GetKeyDown(KeyCode.Escape))
            MainMenu.OpenPause();
    }

    /// <summary>Duraklatma menüsünden "ANA MENÜ": kararır, sahne yeniden yüklenir, başlangıç ekranı gelir.</summary>
    public void GoToMainMenu()
    {
        if (!restarting)
            StartCoroutine(ToMenu());
    }

    private IEnumerator ToMenu()
    {
        restarting = true;

        yield return ScreenFade.Instance.FadeTo(1f, 0.5f);

        MainMenu old = Object.FindFirstObjectByType<MainMenu>();
        if (old != null) Object.Destroy(old.gameObject);

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        yield return null;
        yield return null;

        ScreenFade.Instance.Set(1f);
        new GameObject("~MainMenu").AddComponent<MainMenu>();

        restarting = false;
    }

    private IEnumerator Restart()
    {
        restarting = true;

        yield return ScreenFade.Instance.FadeTo(1f, 0.35f);

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        // Yeni sahnenin kurulması için iki kare bekle.
        yield return null;
        yield return null;
        yield return new WaitForSecondsRealtime(0.15f);

        yield return ScreenFade.Instance.FadeTo(0f, 0.6f);

        restarting = false;
    }
}

/// <summary>Tüm ekranı kaplayan siyah örtü (sahne değişse de kalır).</summary>
public class ScreenFade : MonoBehaviour
{
    private static ScreenFade instance;
    private Image image;

    public static ScreenFade Instance
    {
        get
        {
            if (instance == null)
                Create();

            return instance;
        }
    }

    private static void Create()
    {
        GameObject go = new GameObject("~ScreenFade");
        DontDestroyOnLoad(go);

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;

        GameObject img = new GameObject("Black");
        img.transform.SetParent(go.transform, false);

        Image im = img.AddComponent<Image>();
        im.color = new Color(0f, 0f, 0f, 0f);
        im.raycastTarget = false;

        RectTransform rt = im.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        instance = go.AddComponent<ScreenFade>();
        instance.image = im;
    }

    public void Set(float a)
    {
        Color c = image.color;
        c.a = a;
        image.color = c;
    }

    public IEnumerator FadeTo(float target, float time)
    {
        float from = image.color.a;
        float t = 0f;

        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            Set(Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / time))));
            yield return null;
        }

        Set(target);
    }
}

public class MainMenu : MonoBehaviour
{
    public static bool Active { get; private set; }

    private enum Page { Main, Settings, Test, Time, Rain, Tools, Smoke }

    private struct Row
    {
        public string label;
        public string kanji;
        public int kind; // 0 = düğme, 1 = kaydırıcı, 2 = aç/kapa
    }

    private static readonly Row[] MainRows =
    {
        new Row { label = "BAŞLA", kanji = "開始", kind = 0 },
        new Row { label = "AYARLAR", kanji = "設定", kind = 0 },
        new Row { label = "ÇIKIŞ", kanji = "終了", kind = 0 },
    };

    private static readonly Row[] PauseRows =
    {
        new Row { label = "DEVAM ET", kanji = "再開", kind = 0 },
        new Row { label = "TEST", kanji = "試験", kind = 0 },
        new Row { label = "AYARLAR", kanji = "設定", kind = 0 },
        new Row { label = "ANA MENÜ", kanji = "題名", kind = 0 },
    };

    private static bool IsTestPage(Page p)
    {
        return p == Page.Test || p == Page.Time || p == Page.Rain || p == Page.Tools || p == Page.Smoke;
    }

    private Row[] CurRows()
    {
        if (IsTestPage(page))
            return testRowCache;

        return page == Page.Settings ? SettingsRows : (pauseMode ? PauseRows : MainRows);
    }

    private static readonly Row[] SettingsRows =
    {
        new Row { label = "SES", kanji = "音量", kind = 1 },
        new Row { label = "ARAYÜZ BOYUTU", kanji = "画面", kind = 1 },
        new Row { label = "KENAR KARARMA", kanji = "暗さ", kind = 1 },
        new Row { label = "KARARMA ALANI", kanji = "範囲", kind = 1 },
        new Row { label = "TAM EKRAN", kanji = "全画面", kind = 2 },
        new Row { label = "SİNEMATİK KAMERA", kanji = "映画", kind = 2 },
        new Row { label = "GERİ", kanji = "戻る", kind = 0 },
    };

    // ---- renkler ----
    private static readonly Color Ink = new Color(0.03f, 0.035f, 0.05f, 0.62f);
    private static readonly Color Paper = new Color(0.93f, 0.9f, 0.82f, 1f);
    private static readonly Color PaperDim = new Color(0.93f, 0.9f, 0.82f, 0.42f);
    private static readonly Color Gold = new Color(0.78f, 0.66f, 0.42f, 1f);
    private static readonly Color Vermilion = new Color(0.78f, 0.16f, 0.14f, 1f);

    private Font fontBold;
    private Font fontRegular;

    private Canvas canvas;
    private CanvasGroup titleGroup;
    private CanvasGroup menuGroup;
    private RectTransform menuRoot;

    private Page page = Page.Main;
    private int index;
    private bool starting;
    private float age;

    private PlayerController player;

    // duraklatma modu
    private bool pauseMode;
    private bool prevCanControl = true;
    private float prevTimeScale = 1f;
    private static bool nextIsPause;
    private Text titleText, subtitleText;
    private Image sealImage;

    public static void OpenPause()
    {
        if (Active)
            return;

        nextIsPause = true;
        new GameObject("~PauseMenu").AddComponent<MainMenu>();
    }

    // sayfa elemanları
    private RectTransform[] rowRects = new RectTransform[0];
    private Text[] labelTexts = new Text[0];
    private Text[] kanjiTexts = new Text[0];
    private Text[] valueTexts = new Text[0];
    private RectTransform[] trackRects = new RectTransform[0];
    private RectTransform[] fillRects = new RectTransform[0];
    private RectTransform[] knobRects = new RectTransform[0];
    private RectTransform diamondL;
    private RectTransform diamondR;

    private Sprite vignette;
    private Sprite white;

    private void Awake()
    {
        Active = true;
        pauseMode = nextIsPause;
        nextIsPause = false;

        fontBold = Resources.Load<Font>("UI/ProjectM_Serif_Bold");
        fontRegular = Resources.Load<Font>("UI/ProjectM_Serif_Regular");

        Font fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (fontBold == null) fontBold = fallback;
        if (fontRegular == null) fontRegular = fontBold;

        white = MakeWhite();
        vignette = MakeVignette();

        BuildCanvas();
        BuildPage(Page.Main);

        if (pauseMode)
        {
            age = 2f; // giriş animasyonu yok
            titleText.text = "DURAKLATILDI";
            subtitleText.text = "停　止";
            sealImage.gameObject.SetActive(false);

            prevTimeScale = Time.timeScale > 0.01f ? Time.timeScale : 1f;
            Time.timeScale = 0f;

            player = FindFirstObjectByType<PlayerController>();
            if (player != null) prevCanControl = player.canControl;
        }
        else
        {
            StartCoroutine(Intro());
        }
    }

    private void OnDestroy()
    {
        Active = false;
    }

    private IEnumerator Intro()
    {
        // Karanlıktan açılır.
        yield return ScreenFade.Instance.FadeTo(0f, 1.1f);
    }

    // =========================================================
    // KURULUM
    // =========================================================

    private void BuildCanvas()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(3840f, 2160f); // büyütünce menü küçülür (1920x1080 = eski boyut)
        scaler.matchWidthOrHeight = 0.5f;

        // Koyu örtü + kenar karartması.
        Image bg = NewImage("Ink", transform, Ink, null);
        Stretch(bg.rectTransform);

        Image vig = NewImage("Vignette", transform, new Color(1f, 1f, 1f, 1f), vignette);
        Stretch(vig.rectTransform);

        // Sinematik şeritler.
        Image top = NewImage("BarTop", transform, new Color(0f, 0f, 0f, 0.92f), null);
        top.rectTransform.anchorMin = new Vector2(0f, 1f);
        top.rectTransform.anchorMax = new Vector2(1f, 1f);
        top.rectTransform.pivot = new Vector2(0.5f, 1f);
        top.rectTransform.sizeDelta = new Vector2(0f, 88f);

        Image bottom = NewImage("BarBottom", transform, new Color(0f, 0f, 0f, 0.92f), null);
        bottom.rectTransform.anchorMin = new Vector2(0f, 0f);
        bottom.rectTransform.anchorMax = new Vector2(1f, 0f);
        bottom.rectTransform.pivot = new Vector2(0.5f, 0f);
        bottom.rectTransform.sizeDelta = new Vector2(0f, 88f);

        // ---- başlık ----
        GameObject tg = new GameObject("Title");
        tg.transform.SetParent(transform, false);
        titleGroup = tg.AddComponent<CanvasGroup>();
        RectTransform trt = tg.AddComponent<RectTransform>();
        Stretch(trt);

        Text jp = subtitleText = NewText("Subtitle", tg.transform, "影 の 刃", 28, Gold, TextAnchor.MiddleCenter, fontRegular);
        Place(jp.rectTransform, new Vector2(0f, 372f), new Vector2(900f, 70f));

        Text title = titleText = NewText("Name", tg.transform, "PROJECT M", 92, Paper, TextAnchor.MiddleCenter, fontBold);
        Place(title.rectTransform, new Vector2(-20f, 262f), new Vector2(1400f, 220f));
        AddShadow(title.gameObject, new Color(0f, 0f, 0f, 0.8f), new Vector2(0f, -6f));

        float pw = title.preferredWidth;

        // Kırmızı mühür.
        Image seal = sealImage = NewImage("Seal", tg.transform, Vermilion, null);
        Place(seal.rectTransform, new Vector2(-20f + pw * 0.5f + 78f, 292f), new Vector2(96f, 96f));
        seal.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -5f);

        Text sealText = NewText("SealText", seal.transform, "斬", 40, Paper, TextAnchor.MiddleCenter, fontBold);
        Stretch(sealText.rectTransform);

        // Ayraç.
        Image line = NewImage("Line", tg.transform, new Color(Gold.r, Gold.g, Gold.b, 0.55f), null);
        Place(line.rectTransform, new Vector2(0f, 150f), new Vector2(620f, 2f));

        Image dia = NewImage("LineDiamond", tg.transform, Vermilion, null);
        Place(dia.rectTransform, new Vector2(0f, 150f), new Vector2(14f, 14f));
        dia.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        // ---- menü ----
        GameObject mg = new GameObject("Menu");
        mg.transform.SetParent(transform, false);
        menuGroup = mg.AddComponent<CanvasGroup>();
        menuRoot = mg.AddComponent<RectTransform>();
        Stretch(menuRoot);

        // Alt ipucu + sürüm.
        Text hint = NewText("Hint", transform, "W / S  GEZİN        ENTER  SEÇ        R  YENİDEN BAŞLAT", 26, PaperDim, TextAnchor.MiddleCenter, fontRegular);
        Place(hint.rectTransform, new Vector2(0f, -470f), new Vector2(1600f, 40f));

        Text ver = NewText("Ver", transform, "DEMO", 24, PaperDim, TextAnchor.MiddleRight, fontRegular);
        ver.rectTransform.anchorMin = new Vector2(1f, 0f);
        ver.rectTransform.anchorMax = new Vector2(1f, 0f);
        ver.rectTransform.pivot = new Vector2(1f, 0f);
        ver.rectTransform.anchoredPosition = new Vector2(-40f, 100f);
        ver.rectTransform.sizeDelta = new Vector2(300f, 36f);

        titleGroup.alpha = 0f;
        menuGroup.alpha = 0f;
    }

    private void BuildPage(Page p)
    {
        page = p;
        index = 0;

        for (int i = menuRoot.childCount - 1; i >= 0; i--)
            Destroy(menuRoot.GetChild(i).gameObject);

        if (IsTestPage(p))
            BuildTestRows(p);

        Page prevPage = page; page = p; Row[] rows = CurRows(); page = prevPage;
        int n = rows.Length;

        rowRects = new RectTransform[n];
        labelTexts = new Text[n];
        kanjiTexts = new Text[n];
        valueTexts = new Text[n];
        trackRects = new RectTransform[n];
        fillRects = new RectTransform[n];
        knobRects = new RectTransform[n];

        float top = p == Page.Main ? 40f : 110f;
        float step = p == Page.Main ? -84f : -66f;
        bool compact = IsTestPage(p);

        if (compact)
        {
            // Çok satırlı test sayfaları: sıkı dizilim, kanji satırı yok.
            step = n > 8 ? -56f : -64f;
            top = (n - 1) * 0.5f * -step;
        }

        for (int i = 0; i < n; i++)
        {
            GameObject row = new GameObject("Row" + i);
            row.transform.SetParent(menuRoot, false);
            RectTransform rrt = row.AddComponent<RectTransform>();
            Place(rrt, new Vector2(0f, top + step * i), new Vector2(p == Page.Main ? 760f : 1100f, 96f));
            rowRects[i] = rrt;

            if (p == Page.Main)
            {
                Text label = NewText("Label", row.transform, rows[i].label, 28, PaperDim, TextAnchor.MiddleCenter, fontBold);
                Place(label.rectTransform, new Vector2(0f, 10f), new Vector2(760f, 76f));
                AddShadow(label.gameObject, new Color(0f, 0f, 0f, 0.8f), new Vector2(0f, -3f));
                labelTexts[i] = label;

                Text kj = NewText("Kanji", row.transform, rows[i].kanji, 20, PaperDim, TextAnchor.MiddleCenter, fontRegular);
                Place(kj.rectTransform, new Vector2(0f, -34f), new Vector2(400f, 34f));
                kanjiTexts[i] = kj;
            }
            else
            {
                Text label = NewText("Label", row.transform, rows[i].label, compact ? 26 : 28, PaperDim, TextAnchor.MiddleLeft, fontBold);
                Place(label.rectTransform, new Vector2(-250f, compact ? 0f : 8f), new Vector2(520f, 60f));
                AddShadow(label.gameObject, new Color(0f, 0f, 0f, 0.8f), new Vector2(0f, -3f));
                labelTexts[i] = label;

                if (!compact)
                {
                    Text kj = NewText("Kanji", row.transform, rows[i].kanji, 20, PaperDim, TextAnchor.MiddleLeft, fontRegular);
                    Place(kj.rectTransform, new Vector2(-250f, -30f), new Vector2(520f, 30f));
                    kanjiTexts[i] = kj;
                }

                if (rows[i].kind == 1)
                {
                    Image track = NewImage("Track", row.transform, new Color(1f, 1f, 1f, 0.16f), null);
                    Place(track.rectTransform, new Vector2(250f, 0f), new Vector2(Track, 8f));
                    trackRects[i] = track.rectTransform;

                    Image fill = NewImage("Fill", track.transform, Vermilion, null);
                    fill.rectTransform.anchorMin = new Vector2(0f, 0f);
                    fill.rectTransform.anchorMax = new Vector2(0f, 1f);
                    fill.rectTransform.pivot = new Vector2(0f, 0.5f);
                    fill.rectTransform.anchoredPosition = Vector2.zero;
                    fillRects[i] = fill.rectTransform;

                    Image knob = NewImage("Knob", track.transform, Paper, null);
                    knob.rectTransform.sizeDelta = new Vector2(22f, 22f);
                    knob.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    knobRects[i] = knob.rectTransform;

                    Text val = NewText("Value", row.transform, "", 26, Paper, TextAnchor.MiddleLeft, fontRegular);
                    Place(val.rectTransform, new Vector2(250f + Track * 0.5f + 80f, 0f), new Vector2(140f, 44f));
                    valueTexts[i] = val;
                }
                else if (rows[i].kind == 2 || rows[i].kind == 3)
                {
                    Text val = NewText("Value", row.transform, "", 28, Paper, TextAnchor.MiddleCenter, fontBold);
                    Place(val.rectTransform, new Vector2(250f, 0f), new Vector2(Track, 56f));
                    valueTexts[i] = val;
                }
            }
        }

        // Seçim işareti: iki kırmızı baklava.
        diamondL = NewImage("DiaL", menuRoot, Vermilion, null).rectTransform;
        diamondR = NewImage("DiaR", menuRoot, Vermilion, null).rectTransform;

        foreach (RectTransform d in new[] { diamondL, diamondR })
        {
            d.sizeDelta = new Vector2(16f, 16f);
            d.anchorMin = d.anchorMax = new Vector2(0.5f, 0.5f);
            d.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        RefreshValues();
    }

    private const float Track = 380f;

    // =========================================================
    // GÜNCELLEME
    // =========================================================

    private void Update()
    {
        age += Time.unscaledDeltaTime;

        Cursor.visible = true;

        // Giriş görünümleri.
        titleGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - 0.5f) / 1.0f));
        menuGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - 1.1f) / 0.8f));

        // Oyuncu menü boyunca kilitli.
        if (player == null)
            player = FindFirstObjectByType<PlayerController>();

        if (player != null && !starting)
            player.canControl = false;

        if (pauseMode && page == Page.Main && !starting && Input.GetKeyDown(KeyCode.Escape))
        {
            Resume();
            return;
        }

        if (starting || age < 1.2f)
            return;

        Row[] rows = CurRows();
        int n = rows.Length;

        // --- fare ---
        for (int i = 0; i < n; i++)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(rowRects[i], Input.mousePosition, null))
            {
                if (Input.mousePosition != lastMouse && dragRow < 0)
                    index = i;
            }
        }

        lastMouse = Input.mousePosition;

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            index = (index + n - 1) % n;

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            index = (index + 1) % n;

        bool confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        bool click = Input.GetMouseButtonDown(0) &&
            RectTransformUtility.RectangleContainsScreenPoint(rowRects[index], Input.mousePosition, null);

        if (page == Page.Main)
        {
            if (confirm || click)
                ActivateMain(index);
        }
        else if (IsTestPage(page))
        {
            UpdateTest(confirm, click);
        }
        else
        {
            UpdateSettings(confirm, click);
        }

        UpdateVisuals(rows);
    }

    private Vector3 lastMouse;

    private void ActivateMain(int i)
    {
        if (pauseMode)
        {
            if (i == 0) Resume();
            else if (i == 1) BuildPage(Page.Test);
            else if (i == 2) BuildPage(Page.Settings);
            else
            {
                GameSettings.Save();
                starting = true;
                FindFirstObjectByType<GameFlow>().GoToMainMenu();
            }

            return;
        }

        switch (i)
        {
            case 0:
                StartCoroutine(StartGame());
                break;

            case 1:
                BuildPage(Page.Settings);
                break;

            case 2:
                GameSettings.Save();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                break;
        }
    }

    private void Resume()
    {
        GameSettings.Save();
        Time.timeScale = prevTimeScale;

        if (player != null)
            player.canControl = prevCanControl;

        Active = false;
        Destroy(gameObject);
    }

    private void UpdateSettings(bool confirm, bool click)
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Back();
            return;
        }

        int dir = 0;

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) dir = -1;
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) dir = 1;

        switch (index)
        {
            case 0: // ses
                if (dir != 0)
                    GameSettings.Volume = GameSettings.Volume + dir * 0.05f;

                DragSlider(0, v => GameSettings.Volume = v, 0f, 1f);
                break;

            case 1: // arayüz boyutu
                if (dir != 0)
                    GameSettings.UiScale = GameSettings.UiScale + dir * 0.05f;

                DragSlider(1, v => GameSettings.UiScale = v, GameSettings.MinUiScale, GameSettings.MaxUiScale);
                break;

            case 2: // kenar kararma gücü
                if (dir != 0)
                    GameVignette.Intensity = GameVignette.Intensity + dir * 0.05f;

                DragSlider(2, v => GameVignette.Intensity = v, 0f, 1f);
                break;

            case 3: // kararma alanı
                if (dir != 0)
                    GameVignette.Size = GameVignette.Size + dir * 0.05f;

                DragSlider(3, v => GameVignette.Size = v, 0f, 1f);
                break;

            case 4: // tam ekran
                if (dir != 0 || confirm || click)
                    GameSettings.Fullscreen = !GameSettings.Fullscreen;
                break;

            case 5: // sinematik kamera (nefes alan, sallanan kamera)
                if (dir != 0 || confirm || click)
                    GameSettings.CinematicCamera = !GameSettings.CinematicCamera;
                break;

            case 6: // geri
                if (confirm || click)
                    Back();
                break;
        }

        RefreshValues();
    }

    private void DragSlider(int row, System.Action<float> set, float min, float max)
    {
        if (trackRects[row] == null)
            return;

        Vector2 local;

        bool ok = RectTransformUtility.ScreenPointToLocalPointInRectangle(trackRects[row], Input.mousePosition, null, out local);

        if (!ok)
            return;

        Rect r = trackRects[row].rect;

        // İz alanına (biraz taşmayla) basınca sürükleme başlar.
        if (Input.GetMouseButtonDown(0) &&
            local.x >= r.xMin - 24f && local.x <= r.xMax + 24f &&
            local.y >= r.yMin - 26f && local.y <= r.yMax + 26f)
        {
            dragRow = row;
        }

        if (dragRow == row && Input.GetMouseButton(0))
            set(Mathf.Lerp(min, max, Mathf.Clamp01((local.x - r.xMin) / r.width)));
    }

    private int dragRow = -1;

    private void LateUpdate()
    {
        if (!Input.GetMouseButton(0))
            dragRow = -1;
    }

    private void Back()
    {
        GameSettings.Save();
        BuildPage(Page.Main);
        index = pauseMode ? 2 : 1;
    }

    private void RefreshValues()
    {
        if (IsTestPage(page))
        {
            RefreshTestValues();
            return;
        }

        if (page != Page.Settings)
            return;

        float vol = GameSettings.Volume;
        SetSlider(0, vol, Mathf.RoundToInt(vol * 100f) + "%");

        float ui = GameSettings.UiScale;
        float t = Mathf.InverseLerp(GameSettings.MinUiScale, GameSettings.MaxUiScale, ui);
        SetSlider(1, t, Mathf.RoundToInt(ui * 100f) + "%");

        SetSlider(2, GameVignette.Intensity, Mathf.RoundToInt(GameVignette.Intensity * 100f) + "%");
        SetSlider(3, GameVignette.Size, Mathf.RoundToInt(GameVignette.Size * 100f) + "%");

        if (valueTexts[4] != null)
            valueTexts[4].text = Screen.fullScreen ? "AÇIK" : "KAPALI";

        if (valueTexts.Length > 5 && valueTexts[5] != null)
            valueTexts[5].text = GameSettings.CinematicCamera ? "AÇIK" : "KAPALI";
    }

    private void SetSlider(int row, float t, string text)
    {
        if (fillRects[row] == null)
            return;

        t = Mathf.Clamp01(t);
        fillRects[row].sizeDelta = new Vector2(Track * t, 0f);
        knobRects[row].anchorMin = knobRects[row].anchorMax = new Vector2(0f, 0.5f);
        knobRects[row].anchoredPosition = new Vector2(Track * t, 0f);

        if (valueTexts[row] != null)
            valueTexts[row].text = text;
    }

    private void UpdateVisuals(Row[] rows)
    {
        float breathe = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 3f);

        int cnt = Mathf.Min(rows.Length, labelTexts.Length);

        for (int i = 0; i < cnt; i++)
        {
            bool sel = i == index;

            if (labelTexts[i] != null)
            {
                labelTexts[i].color = Color.Lerp(labelTexts[i].color, sel ? Paper : PaperDim, 0.35f);
                labelTexts[i].rectTransform.localScale = Vector3.Lerp(labelTexts[i].rectTransform.localScale, Vector3.one * (sel ? 1.06f : 1f), 0.3f);
            }

            if (kanjiTexts[i] != null)
                kanjiTexts[i].color = Color.Lerp(kanjiTexts[i].color, sel ? Gold : PaperDim, 0.35f);
        }

        // Baklavalar seçili satırın yanında.
        if (diamondL != null && rowRects[index] != null)
        {
            float half = page == Page.Main
                ? labelTexts[index].preferredWidth * 0.5f + 52f
                : 540f;

            Vector2 c = rowRects[index].anchoredPosition;
            float y = c.y + (page == Page.Main ? 10f : 0f);

            diamondL.anchoredPosition = new Vector2(-half, y);
            diamondR.anchoredPosition = new Vector2(half, y);

            Vector3 s = Vector3.one * breathe;
            diamondL.localScale = s;
            diamondR.localScale = s;
        }
    }

    // =========================================================
    // TEST SAYFALARI (duraklatma menüsü > TEST)
    // =========================================================

    private class TestRow
    {
        public string label;
        public int kind;                 // 0 düğme, 1 kaydırıcı, 2 aç/kapa, 3 seçici
        public System.Func<float> get;
        public System.Action<float> set;
        public float min, max, step = 0.05f;
        public System.Func<float, string> fmt;
        public System.Action click;
        public System.Action<int> cycle;
        public System.Func<string> text;
    }

    private System.Collections.Generic.List<TestRow> testRows = new System.Collections.Generic.List<TestRow>();
    private Row[] testRowCache = new Row[0];
    private int quickIdx = 3;

    private static TimeOfDay Tod
    {
        get { return TimeOfDay.Instance != null ? TimeOfDay.Instance : FindFirstObjectByType<TimeOfDay>(); }
    }

    private static RainSystem Rain
    {
        get
        {
            if (RainSystem.Instance != null)
                return RainSystem.Instance;

            RainSystem r = FindFirstObjectByType<RainSystem>();
            return r != null ? r : new GameObject("RainSystem").AddComponent<RainSystem>();
        }
    }

    private static WaterSmoke Smoke
    {
        get
        {
            if (WaterSmoke.Instance != null)
                return WaterSmoke.Instance;

            WaterSmoke w = FindFirstObjectByType<WaterSmoke>();
            return w != null ? w : new GameObject("WaterSmoke").AddComponent<WaterSmoke>();
        }
    }

    private static string Hm(float h)
    {
        int m = Mathf.RoundToInt(Mathf.Repeat(h, 24f) * 60f);
        return (m / 60 % 24).ToString("00") + ":" + (m % 60).ToString("00");
    }

    private static TestRow Btn(string label, System.Action click)
    {
        return new TestRow { label = label, kind = 0, click = click };
    }

    private static TestRow Slider(string label, float min, float max, float step, System.Func<float> get, System.Action<float> set, System.Func<float, string> fmt)
    {
        return new TestRow { label = label, kind = 1, min = min, max = max, step = step, get = get, set = set, fmt = fmt };
    }

    private static TestRow Toggle(string label, System.Func<bool> get, System.Action<bool> set)
    {
        return new TestRow { label = label, kind = 2, get = () => get() ? 1f : 0f, set = v => set(v > 0.5f) };
    }

    private void BuildTestRows(Page p)
    {
        testRows.Clear();

        if (player == null)
            player = FindFirstObjectByType<PlayerController>();

        switch (p)
        {
            case Page.Test:
                testRows.Add(Btn("SAAT  /  GÖKYÜZÜ", () => BuildPage(Page.Time)));
                testRows.Add(Btn("YAĞMUR  /  ŞİMŞEK", () => BuildPage(Page.Rain)));
                testRows.Add(Btn("SU DUMANI", () => BuildPage(Page.Smoke)));
                testRows.Add(Btn("ARAÇLAR  (DUMMY ...)", () => BuildPage(Page.Tools)));
                testRows.Add(Btn("GERİ", () => { BuildPage(Page.Main); index = 1; }));
                break;

            case Page.Time:
                testRows.Add(Slider("SAAT", 0f, 24f, 0.25f,
                    () => Tod != null ? Mathf.Repeat(Tod.hour, 24f) : 0f,
                    v => { if (Tod != null) Tod.SetHour(Mathf.Min(v, 23.99f), true); },
                    v => Hm(v)));

                testRows.Add(Toggle("ZAMAN AKIŞI", () => Tod != null && Tod.autoCycle, v => { if (Tod != null) Tod.autoCycle = v; }));

                testRows.Add(Slider("GÜN UZUNLUĞU (dk)", 0.5f, 30f, 0.5f,
                    () => Tod != null ? Tod.dayLengthMinutes : 6f,
                    v => { if (Tod != null) Tod.dayLengthMinutes = Mathf.Max(0.5f, v); },
                    v => v.ToString("0.0") + " dk"));

                testRows.Add(new TestRow
                {
                    label = "HAZIR AYAR  < >",
                    kind = 3,
                    cycle = d => { if (Tod != null) Tod.StepPreset(d); },
                    text = () =>
                    {
                        TimeOfDay t = Tod;

                        if (t == null || t.keys == null || t.keys.Count == 0)
                            return "-";

                        float best = 99f;
                        string nm = "-";

                        foreach (TimeOfDay.TodKey k in t.keys)
                        {
                            float dd = Mathf.Abs(Mathf.DeltaAngle(k.hour * 15f, t.hour * 15f)) / 15f;

                            if (dd < best)
                            {
                                best = dd;
                                nm = k.name;
                            }
                        }

                        return nm;
                    }
                });

                string[] qn = { "GECE YARISI", "ŞAFAK", "SABAH", "ÖĞLE", "İKİNDİ", "GÜN BATIMI", "ALACA", "GECE" };
                float[] qh = { 0f, 5.5f, 8f, 12f, 16f, 18f, 19.5f, 22f };

                testRows.Add(new TestRow
                {
                    label = "ANİ ZAMAN  < >",
                    kind = 3,
                    cycle = d =>
                    {
                        if (Tod == null)
                            return;

                        quickIdx = (quickIdx + d + qh.Length) % qh.Length;
                        Tod.SetHour(qh[quickIdx], true);
                    },
                    text = () => qn[Mathf.Clamp(quickIdx, 0, qn.Length - 1)] + "  " + Hm(qh[Mathf.Clamp(quickIdx, 0, qh.Length - 1)])
                });

                testRows.Add(Btn("GERİ", () => BuildPage(Page.Test)));
                break;

            case Page.Rain:
                testRows.Add(Toggle("YAĞMUR", () => Rain.raining, v => Rain.raining = v));
                testRows.Add(Slider("ŞİDDET", 0f, 1f, 0.05f, () => Rain.intensity, v => Rain.intensity = v, v => Mathf.RoundToInt(v * 100f) + "%"));
                testRows.Add(Slider("ÇİZGİ UZUNLUĞU", 0.5f, 8f, 0.25f, () => Rain.streakLength, v => Rain.streakLength = v, v => v.ToString("0.0") + "x"));
                testRows.Add(Slider("RÜZGÂR", -8f, 8f, 0.5f,
                    () => Rain.windEnabled ? Rain.wind : 0f,
                    v => { Rain.wind = v; Rain.windEnabled = Mathf.Abs(v) > 0.05f; },
                    v => v.ToString("0.0")));
                testRows.Add(Toggle("ARKA KATMAN", () => Rain.backLayer, v => Rain.backLayer = v));
                testRows.Add(Toggle("ORTA KATMAN", () => Rain.midLayer, v => Rain.midLayer = v));
                testRows.Add(Toggle("ÖN KATMAN", () => Rain.frontLayer, v => Rain.frontLayer = v));
                testRows.Add(Toggle("SIÇRAMALAR",
                    () => Rain.groundSplashes || Rain.waterSplashes || Rain.characterSplashes,
                    v => { Rain.groundSplashes = v; Rain.waterSplashes = v; Rain.characterSplashes = v; }));
                testRows.Add(Toggle("ŞİMŞEK", () => Rain.lightning, v => Rain.lightning = v));
                testRows.Add(Toggle("YAĞMUR SESİ", () => Rain.rainSound, v => Rain.rainSound = v));
                testRows.Add(Slider("SES SEVİYESİ", 0f, 1f, 0.05f, () => Rain.rainVolume, v => Rain.rainVolume = v, v => Mathf.RoundToInt(v * 100f) + "%"));
                testRows.Add(Btn("ŞİMŞEK ÇAKTIR", () => Rain.StartBolt()));
                testRows.Add(Btn("GERİ", () => BuildPage(Page.Test)));
                break;

            case Page.Smoke:
                testRows.Add(Toggle("SU DUMANI", () => Smoke.smoke, v => Smoke.smoke = v));
                testRows.Add(Toggle("ARKA KATMAN", () => Smoke.backLayer, v => Smoke.backLayer = v));
                testRows.Add(Toggle("ÖN KATMAN", () => Smoke.frontLayer, v => Smoke.frontLayer = v));
                testRows.Add(Slider("MİKTAR", 0f, 3f, 0.1f, () => Smoke.amount, v => Smoke.amount = v, v => v.ToString("0.0") + "x"));
                testRows.Add(Slider("SAYDAMLIK (görünürlük)", 0f, 0.6f, 0.02f, () => Smoke.opacity, v => Smoke.opacity = v, v => Mathf.RoundToInt(v * 100f) + "%"));
                testRows.Add(Slider("BOYUT", 0.3f, 3f, 0.1f, () => Smoke.size, v => Smoke.size = v, v => v.ToString("0.0") + "x"));
                testRows.Add(Slider("HIZ", 0f, 3f, 0.1f, () => Smoke.speed, v => Smoke.speed = v, v => v.ToString("0.0") + "x"));
                testRows.Add(Slider("YATAY KAYMA", -2f, 2f, 0.1f, () => Smoke.drift, v => Smoke.drift = v, v => v.ToString("0.0")));
                testRows.Add(Btn("GERİ", () => BuildPage(Page.Test)));
                break;

            case Page.Tools:
                testRows.Add(Btn("DUMMY OLUŞTUR", SpawnDummy));
                testRows.Add(Btn("DUMMY'LERİ SİL", () =>
                {
                    foreach (TrainingDummy d in new System.Collections.Generic.List<TrainingDummy>(TrainingDummy.All))
                        if (d != null)
                            Destroy(d.gameObject);
                }));
                testRows.Add(Btn("DUMMY İSTATİSTİĞİNİ SIFIRLA", () =>
                {
                    foreach (TrainingDummy d in TrainingDummy.All)
                        if (d != null)
                            d.ResetStats();
                }));
                testRows.Add(Btn("CANI DOLDUR", () =>
                {
                    Health h = player != null ? player.GetComponent<Health>() : null;

                    if (h != null)
                        h.Heal(h.MaxHealth);
                }));
                testRows.Add(Slider("OYUN HIZI", 0.1f, 2f, 0.1f, () => prevTimeScale, v => prevTimeScale = Mathf.Max(0.05f, v), v => v.ToString("0.0") + "x"));
                testRows.Add(Toggle("KAMERA NEFESİ",
                    () => { RunCameraZoom z = FindFirstObjectByType<RunCameraZoom>(); return z != null && z.idleBreathing; },
                    v => { RunCameraZoom z = FindFirstObjectByType<RunCameraZoom>(); if (z != null) z.idleBreathing = v; }));
                testRows.Add(Btn("GERİ", () => BuildPage(Page.Test)));
                break;
        }

        testRowCache = new Row[testRows.Count];

        for (int i = 0; i < testRows.Count; i++)
            testRowCache[i] = new Row { label = testRows[i].label, kanji = "", kind = testRows[i].kind == 3 ? 2 : testRows[i].kind };
    }

    private void SpawnDummy()
    {
        if (player == null)
            player = FindFirstObjectByType<PlayerController>();

        Vector3 pos = Vector3.zero;

        if (player != null)
        {
            // Oyuncunun baktığı yöne doğru ~4 birim ileriye.
            float dir = player.transform.localScale.x < 0f ? -1f : 1f;
            pos = player.transform.position + Vector3.right * 4f * dir;
        }
        else if (Camera.main != null)
        {
            pos = Camera.main.transform.position;
        }

        if (TrainingDummy.Spawn(pos) == null)
            Debug.LogWarning("Dummy oluşturulamadı.");
    }

    private void UpdateTest(bool confirm, bool click)
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (page == Page.Test)
            {
                BuildPage(Page.Main);
                index = 1;
            }
            else
            {
                BuildPage(Page.Test);
            }

            return;
        }

        if (index < 0 || index >= testRows.Count)
            return;

        int dir = 0;

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) dir = -1;
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) dir = 1;

        TestRow r = testRows[index];
        int row = index;

        switch (r.kind)
        {
            case 0:
                if (confirm || click)
                    r.click();
                break;

            case 1:
                if (dir != 0)
                    r.set(Mathf.Clamp(r.get() + dir * r.step, r.min, r.max));

                DragSlider(row, v => r.set(v), r.min, r.max);
                break;

            case 2:
                if (dir != 0 || confirm || click)
                    r.set(r.get() > 0.5f ? 0f : 1f);
                break;

            case 3:
                if (dir != 0)
                    r.cycle(dir);
                else if (confirm || click)
                    r.cycle(1);
                break;
        }

        // Sayfa değişmiş olabilir (BuildPage); değilse değerleri tazele.
        if (index < testRows.Count && testRows[index] == r)
            RefreshTestValues();
    }

    private void RefreshTestValues()
    {
        for (int i = 0; i < testRows.Count && i < valueTexts.Length; i++)
        {
            TestRow r = testRows[i];

            if (r.kind == 1)
            {
                float v = r.get();
                SetSlider(i, Mathf.InverseLerp(r.min, r.max, v), r.fmt != null ? r.fmt(v) : v.ToString("0.00"));
            }
            else if (r.kind == 2 && valueTexts[i] != null)
            {
                valueTexts[i].text = r.get() > 0.5f ? "AÇIK" : "KAPALI";
            }
            else if (r.kind == 3 && valueTexts[i] != null)
            {
                valueTexts[i].text = r.text != null ? r.text() : "";
            }
        }
    }

    // =========================================================
    // OYUNU BAŞLAT
    // =========================================================

    private IEnumerator StartGame()
    {
        starting = true;

        yield return ScreenFade.Instance.FadeTo(1f, 1.1f);

        // Menüyü gizle, oyuncuyu bırak.
        canvas.enabled = false;

        if (player == null)
            player = FindFirstObjectByType<PlayerController>();

        if (player != null)
            player.canControl = true;

        yield return new WaitForSecondsRealtime(0.45f);

        Active = false;

        yield return ScreenFade.Instance.FadeTo(0f, 1.4f);

        Destroy(gameObject);
    }

    // =========================================================
    // YARDIMCILAR
    // =========================================================

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // Merkezden konumla (anchor = orta).
    private static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static Image NewImage(string name, Transform parent, Color color, Sprite sprite)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        Image im = go.AddComponent<Image>();
        im.color = color;
        im.sprite = sprite;
        im.raycastTarget = false;

        return im;
    }

    private static Text NewText(string name, Transform parent, string text, int size, Color color, TextAnchor anchor, Font font)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        Text t = go.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.supportRichText = false;

        return t;
    }

    private static void AddShadow(GameObject go, Color color, Vector2 distance)
    {
        Shadow sh = go.AddComponent<Shadow>();
        sh.effectColor = color;
        sh.effectDistance = distance;
    }

    private static Sprite MakeWhite()
    {
        Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        t.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        t.Apply();
        return Sprite.Create(t, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
    }

    // Kenarlara doğru kararan radyal gradyan.
    private static Sprite MakeVignette()
    {
        const int size = 256;

        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x / (size - 1f) - 0.5f) * 2f;
                float dy = (y / (size - 1f) - 0.5f) * 2f;
                float d = Mathf.Sqrt(dx * dx + dy * dy * 1.15f);
                float a = Mathf.SmoothStep(0.25f, 1.35f, d) * 0.88f;

                t.SetPixel(x, y, new Color(0f, 0f, 0f, a));
            }
        }

        t.Apply();

        return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}


/// <summary>
/// OYUN İÇİ KENAR KARARMASI (vinyet). Ayarlar menüsünden güç ve alan ayarlanır, PlayerPrefs'te saklanır.
/// Menü ve ekran kararmasının altında, oyunun üstünde çizilir.
/// </summary>
public class GameVignette : MonoBehaviour
{
    private static GameVignette instance;

    private const string KeyInt = "pm_vig_int";
    private const string KeySize = "pm_vig_size";

    public static float Intensity
    {
        get { return PlayerPrefs.GetFloat(KeyInt, 0.55f); }
        set { PlayerPrefs.SetFloat(KeyInt, Mathf.Clamp01(value)); Apply(); }
    }

    public static float Size
    {
        get { return PlayerPrefs.GetFloat(KeySize, 0.5f); }
        set { PlayerPrefs.SetFloat(KeySize, Mathf.Clamp01(value)); dirtyTex = true; Apply(); }
    }

    private static bool dirtyTex = true;

    private Image image;
    private Texture2D tex;
    private Sprite sprite;

    public static void Ensure()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject("~GameVignette");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<GameVignette>();
    }

    private void Awake()
    {
        instance = this;

        Canvas c = gameObject.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 100;

        GameObject g = new GameObject("Vig");
        g.transform.SetParent(transform, false);
        image = g.AddComponent<Image>();
        image.raycastTarget = false;

        RectTransform rt = image.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        Apply();
    }

    private static void Apply()
    {
        if (instance == null)
            return;

        if (dirtyTex)
        {
            instance.Rebuild();
            dirtyTex = false;
        }

        instance.image.color = new Color(1f, 1f, 1f, Intensity);
        instance.image.enabled = Intensity > 0.001f;
    }

    // Size: 0 = sadece köşeler, 1 = ekranın ortasına kadar uzanan kararma.
    private void Rebuild()
    {
        const int n = 128;

        if (tex == null)
        {
            tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
        }

        float inner = Mathf.Lerp(0.85f, 0.1f, Size);
        Color32[] px = new Color32[n * n];

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x / (n - 1f) - 0.5f) * 2f;
                float dy = (y / (n - 1f) - 0.5f) * 2f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                float a = Mathf.SmoothStep(inner * 0.7f, 1f, d);
                px[y * n + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply();

        if (sprite == null)
        {
            sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            image.sprite = sprite;
        }
    }
}
