using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boss1 arayuzu: ekranin ust ortasinda ince KIRMIZI can cizgisi, hemen altinda SARI posture (denge) cizgisi.
/// Boss1Enemy runtime'da olusturur; hicbir sahne/prefab kurulumu gerekmez.
/// Can azalinca koyu kirmizi "gecikmeli" parca yavasca erir. Denge kirilinca posture cizgisi beyaza doner.
/// </summary>
public class Boss1Hud : MonoBehaviour
{
    const float BarWidth = 760f;
    const float HealthHeight = 8f;
    const float PostureHeight = 6f;

    Health health;
    EnemyBalance balance;
    RectTransform hpFill, hpGhost, postFill;
    Image postImage;
    CanvasGroup group;
    float hpShown = 1f, hpGhostShown = 1f, postShown;
    float deadTimer;

    static readonly Color PostureColor = new Color(1f, 0.82f, 0.12f, 1f);

    public static Boss1Hud Create(Health h, EnemyBalance b)
    {
        GameObject go = new GameObject("Boss1HUD");
        Boss1Hud hud = go.AddComponent<Boss1Hud>();
        hud.Build(h, b);
        return hud;
    }

    void Build(Health h, EnemyBalance b)
    {
        health = h;
        balance = b;

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        group = gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        hpGhost = MakeBar("Can", -28f, HealthHeight, new Color(0f, 0f, 0f, 0.65f), new Color(0.45f, 0.05f, 0.05f, 1f), out RectTransform back, out Image dummyA);
        // Ayni arka plan uzerine asil kirmizi dolgu.
        hpFill = MakeFill(back, new Color(0.9f, 0.08f, 0.08f, 1f), out Image dummyB);

        MakeBar("Posture", -40f, PostureHeight, new Color(0f, 0f, 0f, 0.65f), PostureColor, out RectTransform pback, out postImage);
        postFill = (RectTransform)postImage.transform;
    }

    // Arka plan + ilk dolgu olusturur; ilk dolgunun RectTransform'unu dondurur.
    RectTransform MakeBar(string name, float y, float height, Color bg, Color fill, out RectTransform back, out Image fillImage)
    {
        GameObject bgGo = new GameObject(name + "_bg", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(transform, false);
        Image bgImg = bgGo.GetComponent<Image>();
        bgImg.color = bg;
        bgImg.raycastTarget = false;
        back = (RectTransform)bgGo.transform;
        back.anchorMin = back.anchorMax = new Vector2(0.5f, 1f);
        back.pivot = new Vector2(0.5f, 1f);
        back.anchoredPosition = new Vector2(0f, y);
        back.sizeDelta = new Vector2(BarWidth, height);

        return MakeFill(back, fill, out fillImage);
    }

    RectTransform MakeFill(RectTransform parent, Color color, out Image img)
    {
        GameObject go = new GameObject("fill", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        RectTransform rt = (RectTransform)go.transform;
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    static void SetFill(RectTransform rt, float pct)
    {
        rt.anchorMax = new Vector2(Mathf.Clamp01(pct), 1f);
        rt.offsetMax = Vector2.zero;
    }

    void Update()
    {
        if (health == null)
        {
            Destroy(gameObject);
            return;
        }

        float dt = Time.unscaledDeltaTime;

        float hp = health.MaxHealth > 0 ? (float)health.CurrentHealth / health.MaxHealth : 0f;
        hpShown = Mathf.MoveTowards(hpShown, hp, dt * 2.5f);
        hpGhostShown = hpGhostShown > hp ? Mathf.MoveTowards(hpGhostShown, hp, dt * 0.15f) : hp;

        SetFill(hpFill, hpShown);
        SetFill(hpGhost, hpGhostShown);

        if (balance != null)
        {
            postShown = Mathf.MoveTowards(postShown, balance.BalancePercent, dt * 3f);
            SetFill(postFill, postShown);
            postImage.color = balance.IsBroken ? Color.white : PostureColor;
        }

        if (health.IsDead)
        {
            deadTimer += dt;
            group.alpha = Mathf.Clamp01(1f - deadTimer / 1.2f);
        }
    }
}
