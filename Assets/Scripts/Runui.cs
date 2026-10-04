using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Koşu arayüzü (prototip, OnGUI): bölüm bilgisi, charm listesi,
/// charm seçim ekranı ve koşu sonu ekranı (istatistik özeti).
/// Ek asset gerektirmez. Oyun duraklatılmışken de çalışır.
///
/// Seçim: 1/2/3 tuşları ya da tıklama.  Yeniden başla: Enter.
/// TAB (basılı tut): canlı istatistik paneli.
/// Boyut: 'UI Scale' (Inspector). RunManager kendiliğinden ekler.
/// </summary>
public class RunUI : MonoBehaviour
{
    [Header("Boyut")]
    [Tooltip("Tüm arayüzün boyutu. 1 = büyük, 0.7 = kompakt (önerilen).")]
    [Range(0.4f, 1.5f)]
    [SerializeField] private float uiScale = 0.7f;

    [Header("Davranış")]
    [Tooltip("Basılı tutunca canlı istatistik paneli açılır.")]
    [SerializeField] private KeyCode liveStatsKey = KeyCode.Tab;

    [Tooltip("Koşu sonu tablosunda gösterilecek en fazla bölüm (sonuncular).")]
    [SerializeField] private int maxStageRows = 8;

    [Tooltip("Alttaki build satırı (kritik / denge / can çarpanları).")]
    [SerializeField] private bool showBuildStats = true;

    // ---------------------------------------------------------
    // Stiller
    // ---------------------------------------------------------

    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle smallStyle;
    private GUIStyle tinyStyle;
    private GUIStyle charmStyle;
    private GUIStyle cardStyle;
    private GUIStyle bannerStyle;
    private GUIStyle riposteStyle;
    private GUIStyle richCentered;
    private GUIStyle legendStyle;
    private GUIStyle panelStyle;

    private Texture2D dimTexture;
    private Texture2D panelTexture;
    private Texture2D cardTexture;
    private Texture2D cardHoverTexture;

    private static readonly Color Gold = new Color(1f, 0.82f, 0.3f);
    private static readonly Color Soft = new Color(1f, 1f, 1f, 0.75f);

    // Saldırı sonucu renkleri (bar ve lejant).
    private static readonly Color ColParry = new Color(1f, 0.82f, 0.2f);
    private static readonly Color ColBlock = new Color(0.4f, 0.6f, 1f);
    private static readonly Color ColDash = new Color(0.3f, 0.9f, 0.9f);
    private static readonly Color ColMissed = new Color(0.62f, 0.62f, 0.62f);
    private static readonly Color ColInterrupted = new Color(1f, 0.5f, 0.2f);
    private static readonly Color ColIFrame = new Color(0.72f, 0.5f, 1f);
    private static readonly Color ColHit = new Color(0.95f, 0.25f, 0.25f);

    private const float Pad = 8f;

    // =========================================================
    // GİRİŞ
    // =========================================================

    private void Update()
    {
        RunManager run = RunManager.Instance;

        if (run == null)
            return;

        if (run.State == RunState.Offer)
        {
            for (int i = 0; i < 9; i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
                    run.Choose(i);
            }
        }
        else if (run.State == RunState.Dead)
        {
            if (
                Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter)
            )
            {
                run.RequestRestart();
            }
        }
    }

    // =========================================================
    // ÇİZİM
    // =========================================================

    private void OnGUI()
    {
        RunManager run = RunManager.Instance;

        if (run == null || run.Inventory == null)
            return;

        // 720p referans × uiScale. Sanal ekran: width × height.
        float scale = Screen.height / 720f * uiScale;

        GUI.matrix =
            Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        float width = Screen.width / scale;
        float height = Screen.height / scale;

        EnsureStyles();

        DrawHud(run, width, height);

        switch (run.State)
        {
            case RunState.Offer:
                DrawOffer(run, width, height);
                break;

            case RunState.Fighting:
                if (Time.unscaledTime < run.WaveBannerUntil)
                    DrawBanner(run.WaveBannerText, width, height);
                break;

            case RunState.Cleared:
                DrawBanner(
                    "BÖLÜM " + run.Stage + " TEMİZLENDİ",
                    width,
                    height
                );
                break;

            case RunState.Dead:
                DrawDead(run, width, height);
                break;
        }

        if (
            run.State != RunState.Dead &&
            run.State != RunState.Offer &&
            Input.GetKey(liveStatsKey)
        )
        {
            DrawLiveStats(run, width);
        }
    }

    // =========================================================
    // HUD (sol üst kompakt panel)
    // =========================================================

    private void DrawHud(RunManager run, float width, float height)
    {
        IReadOnlyList<CharmInventory.Entry> entries =
            run.Inventory.Entries;

        float panelWidth = 210f;
        float lineH = 15f;
        float panelHeight = 44f + entries.Count * lineH + (entries.Count > 0 ? 6f : 0f);

        Rect panel = new Rect(Pad, Pad, panelWidth, panelHeight);

        GUI.Box(panel, GUIContent.none, panelStyle);

        string stageText = "BÖLÜM " + run.Stage;

        if (run.WaveCount > 1)
            stageText += "  <size=11><color=#BBBBBB>dalga " + run.Wave + "/" + run.WaveCount + "</color></size>";

        GUI.Label(
            new Rect(panel.x + 8f, panel.y + 4f, panelWidth - 16f, 20f),
            stageText,
            titleStyle
        );

        GUI.Label(
            new Rect(panel.x + 8f, panel.y + 24f, panelWidth - 16f, 16f),
            "Düşman: " + run.AliveEnemies,
            smallStyle
        );

        float y = panel.y + 44f;

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

    // Build'in toplam etkisi: charm'ların ve riposte'un birleşik sonucu.
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

    // Parry sonrası güçlenmiş vuruş hakları (üst orta, küçük).
    private void DrawRiposte(float width)
    {
        if (!ParryRiposte.IsActive)
            return;

        GUI.Label(
            new Rect(0, 10f, width, 24f),
            "RİPOSTE " + new string('●', Mathf.Max(0, ParryRiposte.HitsLeft)),
            riposteStyle
        );
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

        GUI.Label(
            new Rect(x, y, w, 18f),
            s.HeaderLine() + "   " + s.KillsLine(),
            smallStyle
        );

        y += 22f;

        y = DrawOutcomeBlock(new Rect(x, y, w, 0f), "NORMAL SALDIRI", s.Normal, false);
        y += 4f;
        y = DrawOutcomeBlock(new Rect(x, y, w, 0f), "ENGELLENEMEZ", s.Unblockable, true);
        y += 4f;

        GUI.Label(
            new Rect(x, y, w, 32f),
            s.DefenseLine() + "\n" +
            "Alınan " + s.DamageTotal +
            "  (normal " + s.DamageNormal +
            ", engellenemez " + s.DamageUnblockable + ")" +
            "   İyileşme " + s.Healed,
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

        float cardWidth = 220f;
        float cardHeight = 190f;
        float gap = 14f;

        float total =
            offers.Count * cardWidth +
            Mathf.Max(0, offers.Count - 1) * gap;

        float x = (width - total) * 0.5f;
        float cardY = (height - cardHeight) * 0.5f;

        string heading =
            run.IsStartOffer
                ? "BAŞLANGIÇ CHARM'INI SEÇ"
                : run.IsBonusOffer
                    ? "KUSURSUZ BÖLÜM: BONUS CHARM"
                    : "BİR CHARM SEÇ";

        GUI.Label(
            new Rect(0, cardY - 46f, width, 34f),
            heading,
            bannerStyle
        );

        for (int i = 0; i < offers.Count; i++)
        {
            CharmDefinition def = offers[i];

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

            string text =
                "<size=11><color=#888888>" + (i + 1) + "</color></size>  " +
                "<b>" + def.displayName + "</b>\n" +
                "<size=11><color=#9AD1FF>" + level + "</color></size>\n\n" +
                "<size=12>" + def.description + "</size>\n\n" +
                "<size=11><color=#FFD54A>" + effect + "</color></size>";

            if (
                GUI.Button(
                    new Rect(x, cardY, cardWidth, cardHeight),
                    text,
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

    // =========================================================
    // BANNER
    // =========================================================

    private void DrawBanner(string text, float width, float height)
    {
        float bannerH = 40f;
        float y = height * 0.28f;

        // İnce şerit arka plan.
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.DrawTexture(new Rect(0, y, width, bannerH), Texture2D.whiteTexture);
        GUI.color = old;

        GUI.Label(new Rect(0, y, width, bannerH), text, bannerStyle);
    }

    // =========================================================
    // KOŞU SONU
    // =========================================================

    private void DrawDead(RunManager run, float width, float height)
    {
        GUI.DrawTexture(new Rect(0, 0, width, height), dimTexture);

        RunStats s = run.Stats;

        float panelWidth = Mathf.Min(680f, width - 2f * Pad);
        float panelHeight = Mathf.Min(height - 2f * Pad, 560f);

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

        GUI.Label(new Rect(panel.x, y, panelWidth, 34f), "KOŞU BİTTİ", bannerStyle);

        y += 38f;

        if (s == null || !s.HasResult)
        {
            GUI.Label(
                new Rect(panel.x, y, panelWidth, 20f),
                "Ulaştığın bölüm: " + run.Stage,
                Centered(labelStyle)
            );

            DrawRestartHint(panel);
            return;
        }

        GUI.Label(
            new Rect(panel.x, y, panelWidth, 18f),
            s.HeaderLine() + "     " + s.KillsLine(),
            Centered(smallStyle)
        );

        y += 18f;

        GUI.Label(
            new Rect(panel.x, y, panelWidth, 18f),
            "<color=#FF8A80>" + s.DeathText() + "</color>",
            richCentered
        );

        y += 26f;

        y = DrawOutcomeBlock(new Rect(x, y, w, 0f), "NORMAL SALDIRILARA CEVAP", s.Normal, false);
        y += 4f;
        y = DrawOutcomeBlock(new Rect(x, y, w, 0f), "ENGELLENEMEZ SALDIRILARA CEVAP", s.Unblockable, true);
        y += 6f;

        GUIStyle line = Centered(tinyStyle);

        GUI.Label(new Rect(panel.x, y, panelWidth, 15f), s.DefenseLine(), line);
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

        y += 24f;

        DrawStageTable(s, panel, y);

        DrawRestartHint(panel);
    }

    private void DrawRestartHint(Rect panel)
    {
        GUI.Label(
            new Rect(panel.x, panel.yMax - 24f, panel.width, 18f),
            "Yeniden başlamak için <b>ENTER</b>",
            richCentered
        );
    }

    // Başlık + yığılmış bar + renkli lejant. Bitiş y'sini döndürür.
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
            float x = bar.x;

            x = Segment(x, bar, o.parried, total, ColParry);
            x = Segment(x, bar, o.blocked, total, ColBlock);
            x = Segment(x, bar, o.dashed, total, ColDash);
            x = Segment(x, bar, o.missed, total, ColMissed);
            x = Segment(x, bar, o.interrupted, total, ColInterrupted);
            x = Segment(x, bar, o.iFrame, total, ColIFrame);
            Segment(x, bar, o.hit, total, ColHit);
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
            "Bölüm", "Süre", "Öldürme", "Parry", "Block", "Yendi", "Hasar"
        };

        float col = 70f;
        float tableWidth = col * headers.Length;
        float left = panel.x + (panel.width - tableWidth) * 0.5f;

        GUIStyle head = Centered(smallStyle);
        GUIStyle cell = Centered(tinyStyle);

        for (int c = 0; c < headers.Length; c++)
        {
            GUI.Label(new Rect(left + c * col, y, col, 16f), headers[c], head);
        }

        y += 17f;

        // İnce ayırıcı çizgi.
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
            {
                GUI.Label(new Rect(left + c * col, y, col, 14f), values[c], cell);
            }

            y += 14f;
        }
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

        richCentered = Make(12, FontStyle.Normal, Color.white);
        richCentered.richText = true;
        richCentered.alignment = TextAnchor.MiddleCenter;

        // Arka planlar: 1px açık kenarlıklı koyu panel (9-slice).
        dimTexture = Solid(new Color(0f, 0f, 0f, 0.6f));

        panelTexture =
            Bordered(
                new Color(0.06f, 0.06f, 0.08f, 0.72f),
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

        cardStyle.normal.background = cardTexture;
        cardStyle.hover.background = cardHoverTexture;
        cardStyle.active.background = cardHoverTexture;
        cardStyle.focused.background = cardTexture;
        cardStyle.normal.textColor = Color.white;
        cardStyle.hover.textColor = Color.white;
        cardStyle.active.textColor = Color.white;
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

    // 3x3: kenarlar 'border', orta 'fill' (GUIStyle.border = 1 ile 9-slice).
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

    private static GUIStyle Centered(GUIStyle source)
    {
        GUIStyle style = new GUIStyle(source)
        {
            alignment = TextAnchor.MiddleCenter
        };

        return style;
    }
}