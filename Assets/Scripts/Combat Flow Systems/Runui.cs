using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Koşu arayüzü (prototip, OnGUI): bölüm bilgisi, charm listesi,
/// charm seçim ekranı ve koşu sonu ekranı (istatistik özeti).
/// Ek asset gerektirmez. Oyun duraklatılmışken de çalışır.
///
/// Seçim: 1/2/3 tuşları ya da tıklama.  Yeniden başla: Enter.
/// TAB (basılı tut): canlı istatistik paneli.
/// RunManager kendiliğinden ekler.
/// </summary>
public class RunUI : MonoBehaviour
{
    [Tooltip("Basılı tutunca canlı istatistik paneli açılır.")]
    [SerializeField] private KeyCode liveStatsKey = KeyCode.Tab;

    [Tooltip("Koşu sonu tablosunda gösterilecek en fazla bölüm (sonuncular).")]
    [SerializeField] private int maxStageRows = 9;

    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle smallStyle;
    private GUIStyle cardStyle;
    private GUIStyle bannerStyle;
    private GUIStyle riposteStyle;
    private GUIStyle richCentered;
    private GUIStyle panelStyle;
    private GUIStyle legendStyle;

    private Texture2D dimTexture;

    // Saldırı sonucu renkleri (bar ve lejant).
    private static readonly Color ColParry = new Color(1f, 0.82f, 0.2f);
    private static readonly Color ColBlock = new Color(0.4f, 0.6f, 1f);
    private static readonly Color ColDash = new Color(0.3f, 0.9f, 0.9f);
    private static readonly Color ColMissed = new Color(0.62f, 0.62f, 0.62f);
    private static readonly Color ColInterrupted = new Color(1f, 0.5f, 0.2f);
    private static readonly Color ColIFrame = new Color(0.72f, 0.5f, 1f);
    private static readonly Color ColHit = new Color(0.95f, 0.25f, 0.25f);

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

    private void OnGUI()
    {
        RunManager run = RunManager.Instance;

        if (run == null || run.Inventory == null)
            return;

        // Ekran boyutundan bağımsız: 720 piksel yüksekliğe göre ölçekle.
        float scale = Screen.height / 720f;

        GUI.matrix =
            Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        float width = Screen.width / scale;
        float height = 720f;

        EnsureStyles();

        DrawHud(run);

        switch (run.State)
        {
            case RunState.Offer:
                DrawOffer(run, width, height);
                break;

            case RunState.Fighting:
                // Yeni dalga başlarken kısa duyuru.
                if (Time.unscaledTime < run.WaveBannerUntil)
                {
                    DrawBanner(
                        run.WaveBannerText,
                        width,
                        height
                    );
                }
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

        // Canlı istatistik (ölüm ekranında zaten tam özet var).
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
    // HUD
    // =========================================================

    private void DrawHud(RunManager run)
    {
        string stageText = "BÖLÜM " + run.Stage;

        if (run.WaveCount > 1)
            stageText += "   •   DALGA " + run.Wave + "/" + run.WaveCount;

        GUI.Label(
            new Rect(16, 12, 600, 30),
            stageText,
            titleStyle
        );

        GUI.Label(
            new Rect(16, 44, 400, 24),
            "Düşman: " + run.AliveEnemies,
            labelStyle
        );

        IReadOnlyList<CharmInventory.Entry> entries =
            run.Inventory.Entries;

        float y = 78f;

        for (int i = 0; i < entries.Count; i++)
        {
            CharmInventory.Entry e = entries[i];

            string stacks =
                e.stacks > 1 ? "  x" + e.stacks : "";

            GUI.Label(
                new Rect(16, y, 420, 22),
                e.definition.displayName + stacks,
                smallStyle
            );

            y += 20f;
        }

        DrawBuildStats(run);

        DrawRiposte();
    }

    // Build'in toplam etkisi: charm'ların ve riposte'un birleşik sonucu.
    private void DrawBuildStats(RunManager run)
    {
        PlayerStats stats = PlayerStats.Current;

        if (stats == null)
            return;

        string text =
            "Kritik %" + Mathf.RoundToInt(stats.CritChance * 100f) +
            " (x" + stats.CritMultiplier.ToString("0.0") + ")" +
            "    Denge x" +
            stats.Get(StatType.BalanceDamage, 1f).ToString("0.00") +
            "    Can x" +
            stats.Get(StatType.HealthDamage, 1f).ToString("0.00") +
            "    [TAB] istatistik";

        GUI.Label(
            new Rect(16, 686, 900, 24),
            text,
            smallStyle
        );
    }

    // Parry sonrası güçlenmiş vuruş hakları.
    private void DrawRiposte()
    {
        if (!ParryRiposte.IsActive)
            return;

        float width = Screen.width / (Screen.height / 720f);

        GUI.Label(
            new Rect(0, 96, width, 40),
            "RİPOSTE  " + new string('●', Mathf.Max(0, ParryRiposte.HitsLeft)),
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

        float panelWidth = 470f;
        float x = width - panelWidth - 16f;
        float y = 12f;

        GUI.DrawTexture(
            new Rect(x - 10f, y - 6f, panelWidth + 20f, 340f),
            dimTexture
        );

        GUI.Label(
            new Rect(x, y, panelWidth, 26),
            s.HeaderLine() + "   " + s.KillsLine(),
            panelStyle
        );

        y += 34f;

        y = DrawOutcomeBlock(
            new Rect(x, y, panelWidth, 0f),
            "NORMAL SALDIRI",
            s.Normal,
            false
        );

        y += 6f;

        y = DrawOutcomeBlock(
            new Rect(x, y, panelWidth, 0f),
            "ENGELLENEMEZ",
            s.Unblockable,
            true
        );

        y += 8f;

        GUI.Label(
            new Rect(x, y, panelWidth, 40),
            s.DefenseLine() + "\n" +
            "Alınan " + s.DamageTotal +
            "  (normal " + s.DamageNormal +
            ", engellenemez " + s.DamageUnblockable + ")" +
            "   İyileşme " + s.Healed,
            panelStyle
        );
    }

    // =========================================================
    // CHARM SEÇİMİ
    // =========================================================

    private void DrawOffer(RunManager run, float width, float height)
    {
        GUI.DrawTexture(
            new Rect(0, 0, width, height),
            dimTexture
        );

        string heading =
            run.IsStartOffer
                ? "BAŞLANGIÇ CHARM'INI SEÇ"
                : "BİR CHARM SEÇ";

        GUI.Label(
            new Rect(0, 90, width, 50),
            heading,
            bannerStyle
        );

        IReadOnlyList<CharmDefinition> offers = run.Offers;

        float cardWidth = 300f;
        float cardHeight = 260f;
        float gap = 24f;

        float total =
            offers.Count * cardWidth +
            (offers.Count - 1) * gap;

        float x = (width - total) * 0.5f;
        float cardY = 190f;

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
                "<b>" + (i + 1) + ".  " + def.displayName + "</b>\n" +
                "<size=14><color=#9AD1FF>" + level + "</color></size>\n\n" +
                def.description + "\n\n" +
                "<color=#FFD54A>" + effect + "</color>";

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
            new Rect(0, cardY + cardHeight + 24f, width, 30),
            "1 / 2 / 3 tuşları ya da tıkla",
            smallStyleCentered()
        );
    }

    // =========================================================
    // BANNER / ÖLÜM
    // =========================================================

    private void DrawBanner(string text, float width, float height)
    {
        GUI.Label(
            new Rect(0, height * 0.35f, width, 60),
            text,
            bannerStyle
        );
    }

    private void DrawDead(RunManager run, float width, float height)
    {
        GUI.DrawTexture(
            new Rect(0, 0, width, height),
            dimTexture
        );

        GUI.Label(
            new Rect(0, 30, width, 56),
            "KOŞU BİTTİ",
            bannerStyle
        );

        RunStats s = run.Stats;

        // İstatistik yoksa eski basit ekran.
        if (s == null || !s.HasResult)
        {
            GUI.Label(
                new Rect(0, 100, width, 30),
                "Ulaştığın bölüm: " + run.Stage,
                centered(labelStyle)
            );

            DrawRestartHint(width, height);
            return;
        }

        float contentWidth = Mathf.Min(900f, width - 32f);
        float left = (width - contentWidth) * 0.5f;
        float y = 92f;

        // --- Başlık satırları ---

        GUI.Label(
            new Rect(0, y, width, 26),
            s.HeaderLine() + "      " + s.KillsLine(),
            centered(labelStyle)
        );

        y += 28f;

        GUI.Label(
            new Rect(0, y, width, 24),
            "<color=#FF8A80>" + s.DeathText() + "</color>",
            richCentered
        );

        y += 36f;

        // --- Saldırılara cevap ---

        y = DrawOutcomeBlock(
            new Rect(left, y, contentWidth, 0f),
            "NORMAL SALDIRILARA CEVAP",
            s.Normal,
            false
        );

        y += 8f;

        y = DrawOutcomeBlock(
            new Rect(left, y, contentWidth, 0f),
            "ENGELLENEMEZ SALDIRILARA CEVAP",
            s.Unblockable,
            true
        );

        y += 10f;

        // --- Sayılar ---

        GUI.Label(
            new Rect(0, y, width, 22),
            s.DefenseLine(),
            smallStyleCentered()
        );

        y += 22f;

        GUI.Label(
            new Rect(0, y, width, 22),
            s.DamageTakenLine(),
            smallStyleCentered()
        );

        y += 22f;

        GUI.Label(
            new Rect(0, y, width, 22),
            s.DamageDealtLine(),
            smallStyleCentered()
        );

        y += 22f;

        GUI.Label(
            new Rect(0, y, width, 22),
            "Build: " + (s.BuildText.Length > 0 ? s.BuildText : "(charm yok)"),
            smallStyleCentered()
        );

        y += 32f;

        // --- Bölüm tablosu ---

        DrawStageTable(s, width, y);

        DrawRestartHint(width, height);
    }

    private void DrawRestartHint(float width, float height)
    {
        GUI.Label(
            new Rect(0, height - 44f, width, 34),
            "Yeniden başlamak için ENTER",
            centered(labelStyle)
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
            new Rect(area.x, y, area.width, 22),
            title + "  (" + total + ")",
            labelStyle
        );

        y += 24f;

        Rect bar = new Rect(area.x, y, area.width, 16f);

        Color old = GUI.color;

        // Arka plan
        GUI.color = new Color(1f, 1f, 1f, 0.12f);
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

        y += 20f;

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

        // Dar alanda (TAB paneli) iki satıra kayar.
        float legendHeight =
            legendStyle.CalcHeight(new GUIContent(legend), area.width);

        GUI.Label(
            new Rect(area.x, y, area.width, legendHeight),
            legend,
            legendStyle
        );

        return y + legendHeight + 4f;
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
        GUI.DrawTexture(
            new Rect(x, bar.y, w, bar.height),
            Texture2D.whiteTexture
        );

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
            RunStats.Pct(count, total) + "</color>    ";
    }

    private void DrawStageTable(RunStats s, float width, float y)
    {
        string[] headers =
        {
            "Bölüm", "Süre", "Öldürme", "Parry", "Block", "Yendi", "Hasar"
        };

        float col = 90f;
        float tableWidth = col * headers.Length;
        float left = (width - tableWidth) * 0.5f;

        GUIStyle head = centered(labelStyle);
        GUIStyle cell = centered(smallStyle);

        for (int c = 0; c < headers.Length; c++)
        {
            GUI.Label(
                new Rect(left + c * col, y, col, 22),
                headers[c],
                head
            );
        }

        y += 24f;

        IReadOnlyList<RunStats.StageRecord> stages = s.Stages;

        int first =
            Mathf.Max(0, stages.Count - Mathf.Max(1, maxStageRows));

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
                GUI.Label(
                    new Rect(left + c * col, y, col, 20),
                    values[c],
                    cell
                );
            }

            y += 19f;
        }
    }

    // =========================================================
    // STİLLER
    // =========================================================

    private void EnsureStyles()
    {
        if (titleStyle != null)
            return;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 26,
            fontStyle = FontStyle.Bold
        };

        titleStyle.normal.textColor = Color.white;

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18
        };

        labelStyle.normal.textColor = Color.white;

        smallStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15
        };

        smallStyle.normal.textColor = new Color(1f, 0.93f, 0.6f);

        panelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            wordWrap = true
        };

        panelStyle.normal.textColor = Color.white;

        legendStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            richText = true,
            wordWrap = true
        };

        legendStyle.normal.textColor = Color.white;

        richCentered = new GUIStyle(labelStyle)
        {
            richText = true,
            alignment = TextAnchor.MiddleCenter
        };

        cardStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 17,
            alignment = TextAnchor.UpperLeft,
            wordWrap = true,
            richText = true,
            padding = new RectOffset(16, 16, 14, 14)
        };

        riposteStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 30,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        riposteStyle.normal.textColor = new Color(1f, 0.82f, 0.2f);

        bannerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 40,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        bannerStyle.normal.textColor = Color.white;

        dimTexture = new Texture2D(1, 1);
        dimTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
        dimTexture.Apply();
    }

    private GUIStyle smallStyleCentered()
    {
        return centered(smallStyle);
    }

    private static GUIStyle centered(GUIStyle source)
    {
        GUIStyle style = new GUIStyle(source);

        style.alignment = TextAnchor.MiddleCenter;

        return style;
    }
}