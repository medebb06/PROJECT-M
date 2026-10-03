using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Koşu arayüzü (prototip, OnGUI): bölüm bilgisi, charm listesi,
/// charm seçim ekranı ve koşu sonu ekranı. Ek asset gerektirmez.
/// Oyun duraklatılmışken de çalışır.
///
/// Seçim: 1/2/3 tuşları ya da tıklama.  Yeniden başla: Enter.
/// RunManager kendiliğinden ekler.
/// </summary>
public class RunUI : MonoBehaviour
{
    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle smallStyle;
    private GUIStyle cardStyle;
    private GUIStyle bannerStyle;

    private Texture2D dimTexture;

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
            new Rect(0, height * 0.28f, width, 60),
            "KOŞU BİTTİ",
            bannerStyle
        );

        GUI.Label(
            new Rect(0, height * 0.28f + 64f, width, 36),
            "Ulaştığın bölüm: " + run.Stage,
            centered(labelStyle)
        );

        IReadOnlyList<CharmInventory.Entry> entries =
            run.Inventory.Entries;

        string build = "";

        for (int i = 0; i < entries.Count; i++)
        {
            CharmInventory.Entry e = entries[i];

            build +=
                e.definition.displayName +
                (e.stacks > 1 ? " x" + e.stacks : "") +
                (i < entries.Count - 1 ? "   •   " : "");
        }

        if (build.Length == 0)
            build = "(charm yok)";

        GUI.Label(
            new Rect(0, height * 0.28f + 104f, width, 60),
            "Build: " + build,
            centered(smallStyle)
        );

        GUI.Label(
            new Rect(0, height * 0.28f + 190f, width, 40),
            "Yeniden başlamak için ENTER",
            centered(labelStyle)
        );
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

        cardStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 17,
            alignment = TextAnchor.UpperLeft,
            wordWrap = true,
            richText = true,
            padding = new RectOffset(16, 16, 14, 14)
        };

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