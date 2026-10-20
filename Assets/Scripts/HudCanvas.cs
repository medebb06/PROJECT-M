using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD barlari icin ortak, EKRANA GORE konumlanan cizim katmani.
/// IMGUI (OnGUI) yerine kendi Screen Space Overlay Canvas'ini kullanir: Game view'da diger UI'nin altinda kalmaz,
/// ana menu canvas'larinin (sortingOrder 30000+) ise altinda kalir. Cozunurluk / en-boy orani / safe area
/// degisince her karede yeniden hesaplanir (cache yok).
/// Kullanim: LateUpdate'te HudCanvas.Fill(guiRect, renk). guiRect IMGUI koordinatlidir (sol-ust orijin, piksel).
/// </summary>
public class HudCanvas : MonoBehaviour
{
    public const int SortingOrder = 20000;   // MainMenu (30000+) altinda, normal oyun UI'sinin ustunde

    static HudCanvas inst;

    /// <summary>Savasta degilken HUD otomatik gizlenir. Kapatmak icin false yap.</summary>
    public static bool AutoHide = true;
    /// <summary>Savas bittikten sonra HUD bu kadar saniye daha gorunur kalir.</summary>
    public static float HideDelay = 3f;
    /// <summary>Dusman bu mesafeden yakinsa savas sayilir (dunya birimi).</summary>
    public static float CombatRange = 28f;

    CanvasGroup group;
    float lastCombat = -999f;
    float nextScan;
    Health playerHealth;
    int lastPlayerHp = -1;
    Canvas canvas;
    RectTransform root;
    readonly List<Image> pool = new List<Image>(48);
    readonly List<int> stamp = new List<int>(48);
    int used, usedFrame = -1;
    bool loggedDraw;

    static HudCanvas Get()
    {
        if (inst != null) return inst;
        if (!Application.isPlaying) return null;

        var go = new GameObject("HudCanvas");
        DontDestroyOnLoad(go);
        inst = go.AddComponent<HudCanvas>();

        inst.canvas = go.AddComponent<Canvas>();
        inst.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        inst.canvas.sortingOrder = SortingOrder;
        inst.canvas.pixelPerfect = false;
        inst.group = go.AddComponent<CanvasGroup>();
        inst.group.interactable = false;
        inst.group.blocksRaycasts = false;
        inst.group.alpha = 0f;
        Debug.Log("[HudCanvas] kuruldu: ekran " + Screen.width + "x" + Screen.height + ", safeArea " + Screen.safeArea + ", sortingOrder " + SortingOrder);

        var rootGo = new GameObject("Root", typeof(RectTransform));
        rootGo.transform.SetParent(go.transform, false);
        inst.root = rootGo.GetComponent<RectTransform>();
        inst.root.anchorMin = Vector2.zero;
        inst.root.anchorMax = Vector2.one;
        inst.root.offsetMin = inst.root.offsetMax = Vector2.zero;
        return inst;
    }

    void Update()
    {
        if (group == null) return;

        if (!AutoHide) { group.alpha = Mathf.MoveTowards(group.alpha, 1f, Time.unscaledDeltaTime * 3f); return; }

        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 0.25f;
            PlayerController pc = FindObjectOfType<PlayerController>();
            if (pc != null)
            {
                Vector3 pp = pc.transform.position;
                var enemies = FindObjectsOfType<EnemyController>();
                for (int i = 0; i < enemies.Length; i++)
                {
                    EnemyController e = enemies[i];
                    if (e == null || !e.gameObject.activeInHierarchy) continue;
                    Health eh = e.GetComponent<Health>();
                    if (eh != null && eh.CurrentHealth <= 0) continue;
                    if (Mathf.Abs(e.transform.position.x - pp.x) <= CombatRange) { lastCombat = Time.unscaledTime; break; }
                }

                // Oyuncu hasar aldiysa / iyilestiyse (iksir) kisa sure goster
                if (playerHealth == null) playerHealth = pc.GetComponent<Health>();
                if (playerHealth != null)
                {
                    int hp = playerHealth.CurrentHealth;
                    if (lastPlayerHp >= 0 && hp != lastPlayerHp) lastCombat = Time.unscaledTime;
                    lastPlayerHp = hp;
                }
            }
        }

        bool show = Time.unscaledTime - lastCombat <= HideDelay;
        group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * (show ? 3f : 1.2f));
    }

    void OnEnable() { Canvas.willRenderCanvases += HideUnused; }
    void OnDisable() { Canvas.willRenderCanvases -= HideUnused; }

    // Bu karede cizilmeyen dikdortgenleri gizle (willRenderCanvases: tum LateUpdate'lerden sonra).
    void HideUnused()
    {
        int f = Time.frameCount;
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null && stamp[i] != f && pool[i].enabled)
                pool[i].enabled = false;
        }
    }

    /// <summary>Ekranin guvenli alani (IMGUI koordinati: sol-ust orijin). Cukur/kose olan cihazlarda HUD icerde kalir.</summary>
    public static Rect SafeArea()
    {
        Rect sa = Screen.safeArea;
        if (sa.width < 1f || sa.height < 1f)
            return new Rect(0f, 0f, Screen.width, Screen.height);
        return new Rect(sa.x, Screen.height - sa.yMax, sa.width, sa.height);
    }

    /// <summary>HUD olcegi: 1080 yuksekligine gore, mobil carpani dahil.</summary>
    public static float Scale()
    {
        return Screen.height / 1080f * MobileInput.HudMul;
    }

    /// <summary>IMGUI koordinatli (sol-ust orijin) dolu dikdortgen cizer. Bu kare icin gecerlidir.</summary>
    public static void Fill(Rect r, Color c)
    {
        HudCanvas h = Get();
        if (h == null || h.root == null) return;
        h.Draw(r, c);
    }

    void Draw(Rect r, Color c)
    {
        int f = Time.frameCount;
        if (usedFrame != f) { usedFrame = f; used = 0; }

        Image img;
        if (used < pool.Count)
        {
            img = pool[used];
        }
        else
        {
            var g = new GameObject("r" + used, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            g.transform.SetParent(root, false);
            img = g.GetComponent<Image>();
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;   // sol-alt
            rt.pivot = Vector2.zero;
            pool.Add(img);
            stamp.Add(-1);
        }

        if (!loggedDraw) { loggedDraw = true; Debug.Log("[HudCanvas] ilk cizim: rect " + r + ", renk " + c + ", TopBarPixels " + RunCameraZoom.TopBarPixels + ", false " + false); }
        stamp[used] = f;
        img.enabled = c.a > 0.003f && r.width > 0.01f && r.height > 0.01f;
        img.color = c;
        var t = img.rectTransform;
        t.anchoredPosition = new Vector2(r.x, Screen.height - r.yMax);   // IMGUI y -> canvas y
        t.sizeDelta = new Vector2(r.width, r.height);
        used++;
    }
}
