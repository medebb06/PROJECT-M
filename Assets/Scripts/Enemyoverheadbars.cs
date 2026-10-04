using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NORMAL DÜŞMANLARIN CAN + DENGE ÇUBUĞU, boss çubuğuyla aynı görünümde
/// ama düşmanın BAŞININ ÜSTÜNDE:
///   kırmızı = can, altın = denge (dolunca denge kırılır).
///   Vurunca azalan kısım kısa süre beyaz iz bırakır.
///   Denge kırıkken (execute fırsatı) denge çubuğu beyaz-altın yanıp söner.
///
/// Boss'ta gösterilmez (onun çubuğu ekranın üstünde, RunUI'da).
///
/// KURULUM YOK: oyun başında kendiliğinden oluşur. Ayar yapmak istersen
/// sahnede boş bir objeye ekle (o kullanılır).
/// Prefab'daki eski Slider çubuklarını (EnemyBalanceBar, HealthBarUI)
/// varsayılan olarak gizler; istemezsen 'Hide Old Enemy Bars' kapat.
/// </summary>
public class EnemyOverheadBars : MonoBehaviour
{
    [Header("Görünüm")]
    [Tooltip("RunUI ile aynı ölçek mantığı: 720p'ye göre × bu değer.")]
    public float uiScale = 0.7f;

    [Tooltip("Çubuk genişliği (720p referans pikseli).")]
    public float barWidth = 64f;

    public float healthHeight = 6f;
    public float balanceHeight = 3f;

    [Tooltip("Sprite'ın tepesinden yukarı mesafe (dünya birimi).")]
    public float worldOffset = 0.12f;

    public Color healthColor = new Color(0.85f, 0.18f, 0.15f);
    public Color balanceColor = new Color(1f, 0.8f, 0.25f);
    public Color trailColor = new Color(1f, 1f, 1f, 0.85f);
    public Color backColor = new Color(0f, 0f, 0f, 0.6f);

    [Header("Davranış")]
    [Tooltip("Açık: sadece hasar almış (ya da dengesi dolmaya başlamış) düşmanda göster.")]
    public bool onlyWhenEngaged = false;

    [Tooltip("Beyaz izin azalan değere yetişme hızı (oran/sn).")]
    public float trailSpeed = 0.8f;

    [Tooltip("Beyaz iz azalmaya başlamadan önce bekleme (sn).")]
    public float trailDelay = 0.35f;

    [Tooltip("Prefab'daki eski Slider çubuklarını gizle.")]
    public bool hideOldEnemyBars = true;

    private class Entry
    {
        public Health health;
        public EnemyBalance balance;
        public SpriteRenderer sprite;
        public float healthTrail = 1f;
        public float balanceTrail = 0f;
        public float lastHealth = 1f;
        public float trailHoldUntil;
        public bool engaged;
        public bool oldBarsHidden;
    }

    private static EnemyOverheadBars instance;

    private readonly Dictionary<EnemyController, Entry> entries =
        new Dictionary<EnemyController, Entry>();

    private readonly List<EnemyController> stale = new List<EnemyController>();

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration
    )]
    private static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad
    )]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<EnemyOverheadBars>() != null)
            return;

        new GameObject("EnemyOverheadBars").AddComponent<EnemyOverheadBars>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    // =========================================================
    // GÜNCELLEME (izler gerçek zamanla akar)
    // =========================================================

    private void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        float now = Time.unscaledTime;

        for (int i = 0; i < EnemyController.All.Count; i++)
        {
            EnemyController enemy = EnemyController.All[i];

            if (enemy == null)
                continue;

            Entry e = GetEntry(enemy);

            if (hideOldEnemyBars && !e.oldBarsHidden)
                HideOldBars(enemy, e);

            float hp = HealthPercent(e);
            float bal = e.balance != null ? e.balance.BalancePercent : 0f;

            if (hp < 0.999f || bal > 0.001f)
                e.engaged = true;

            // Can azaldıysa iz bir süre bekler, sonra yetişir.
            if (hp < e.lastHealth - 0.0001f)
                e.trailHoldUntil = now + trailDelay;

            e.lastHealth = hp;

            if (e.healthTrail < hp)
                e.healthTrail = hp;
            else if (now >= e.trailHoldUntil)
                e.healthTrail = Mathf.MoveTowards(e.healthTrail, hp, trailSpeed * dt);

            // Denge DOLARAK ilerler: iz, artan kısmı kısa süre beyaz gösterir
            // (yeni eklenen denge hasarı beyaz → altına döner).
            if (bal < e.balanceTrail)
                e.balanceTrail = bal;
            else
                e.balanceTrail = Mathf.MoveTowards(e.balanceTrail, bal, trailSpeed * 1.5f * dt);
        }

        // Yok olan düşmanları temizle.
        stale.Clear();

        foreach (KeyValuePair<EnemyController, Entry> pair in entries)
        {
            if (pair.Key == null)
                stale.Add(pair.Key);
        }

        for (int i = 0; i < stale.Count; i++)
            entries.Remove(stale[i]);
    }

    // =========================================================
    // ÇİZİM
    // =========================================================

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint)
            return;

        Camera cam = Camera.main;

        if (cam == null)
            return;

        // RunUI'nın (menüler) ARKASINDA kalsın.
        GUI.depth = 10;

        float scale = Screen.height / 720f * uiScale;

        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;

        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        BossController boss = BossController.Current;

        for (int i = 0; i < EnemyController.All.Count; i++)
        {
            EnemyController enemy = EnemyController.All[i];

            if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled)
                continue;

            // Boss'un çubuğu ekranın üstünde.
            if (boss != null && boss.gameObject == enemy.gameObject)
                continue;

            if (!entries.TryGetValue(enemy, out Entry e))
                continue;

            if (onlyWhenEngaged && !e.engaged)
                continue;

            DrawBars(cam, enemy, e, scale);
        }

        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
    }

    private void DrawBars(Camera cam, EnemyController enemy, Entry e, float scale)
    {
        Vector3 top = enemy.transform.position;

        if (e.sprite == null)
            e.sprite = FindMainRenderer(enemy);

        top.y =
            (e.sprite != null ? e.sprite.bounds.max.y : top.y + 1f) +
            worldOffset;

        Vector3 screen = cam.WorldToScreenPoint(top);

        // Kameranın arkasında / ekran dışında.
        if (screen.z < 0f)
            return;

        // Ekran koordinatı (alt-sol) → GUI (üst-sol), ölçeklenmiş.
        float cx = screen.x / scale;
        float cy = (Screen.height - screen.y) / scale;

        float w = barWidth;
        float x = cx - w * 0.5f;

        float totalH = healthHeight + 2f + balanceHeight;
        float y = cy - totalH;

        if (x + w < 0f || x > Screen.width / scale || y > Screen.height / scale || y + totalH < 0f)
            return;

        float hp = HealthPercent(e);

        // -------- CAN --------
        GUI.color = backColor;
        GUI.DrawTexture(new Rect(x - 1f, y - 1f, w + 2f, healthHeight + 2f), Texture2D.whiteTexture);

        // Beyaz iz (yeni kaybedilen can).
        if (e.healthTrail > hp)
        {
            GUI.color = trailColor;
            GUI.DrawTexture(new Rect(x, y, w * e.healthTrail, healthHeight), Texture2D.whiteTexture);
        }

        GUI.color = healthColor;
        GUI.DrawTexture(new Rect(x, y, w * hp, healthHeight), Texture2D.whiteTexture);

        y += healthHeight + 2f;

        // -------- DENGE --------
        float bal = e.balance != null ? e.balance.BalancePercent : 0f;
        bool broken = e.balance != null && e.balance.IsBroken;

        GUI.color = new Color(backColor.r, backColor.g, backColor.b, backColor.a * 0.85f);
        GUI.DrawTexture(new Rect(x - 1f, y - 1f, w + 2f, balanceHeight + 2f), Texture2D.whiteTexture);

        if (broken)
        {
            // Execute fırsatı: yanıp sönen dolu çubuk.
            bool flash = Mathf.Repeat(Time.unscaledTime * 8f, 1f) < 0.5f;

            GUI.color = flash ? Color.white : balanceColor;
            GUI.DrawTexture(new Rect(x, y, w, balanceHeight), Texture2D.whiteTexture);
            return;
        }

        // Altın = yerleşmiş denge, beyaz = yeni eklenen kısım.
        GUI.color = trailColor;
        GUI.DrawTexture(new Rect(x, y, w * bal, balanceHeight), Texture2D.whiteTexture);

        GUI.color = balanceColor;
        GUI.DrawTexture(new Rect(x, y, w * Mathf.Min(bal, e.balanceTrail), balanceHeight), Texture2D.whiteTexture);
    }

    // =========================================================
    // YARDIMCILAR
    // =========================================================

    private Entry GetEntry(EnemyController enemy)
    {
        if (entries.TryGetValue(enemy, out Entry e))
            return e;

        e = new Entry
        {
            health = enemy.GetComponent<Health>(),
            balance = enemy.GetComponent<EnemyBalance>(),
            sprite = FindMainRenderer(enemy)
        };

        e.lastHealth = HealthPercent(e);
        e.healthTrail = e.lastHealth;
        e.balanceTrail = e.balance != null ? e.balance.BalancePercent : 0f;

        entries.Add(enemy, e);

        return e;
    }

    private static float HealthPercent(Entry e)
    {
        if (e.health == null || e.health.MaxHealth <= 0)
            return 0f;

        return Mathf.Clamp01((float)e.health.CurrentHealth / e.health.MaxHealth);
    }

    private static SpriteRenderer FindMainRenderer(EnemyController enemy)
    {
        SpriteRenderer[] renderers = enemy.GetComponentsInChildren<SpriteRenderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];

            if (sr.enabled && sr.sprite != null)
                return sr;
        }

        return null;
    }

    // Prefab'daki eski world-space Slider çubuklarını kapat.
    private static void HideOldBars(EnemyController enemy, Entry e)
    {
        e.oldBarsHidden = true;

        EnemyBalanceBar[] balanceBars =
            enemy.GetComponentsInChildren<EnemyBalanceBar>(true);

        for (int i = 0; i < balanceBars.Length; i++)
            HideBarObject(enemy, balanceBars[i]);

        HealthBarUI[] healthBars =
            enemy.GetComponentsInChildren<HealthBarUI>(true);

        for (int i = 0; i < healthBars.Length; i++)
            HideBarObject(enemy, healthBars[i]);
    }

    private static void HideBarObject(EnemyController enemy, Component bar)
    {
        if (bar == null)
            return;

        // Çubuğun Canvas'ını kapat (düşmanın kendisi değilse).
        Canvas canvas = bar.GetComponentInParent<Canvas>(true);

        if (
            canvas != null &&
            canvas.transform != enemy.transform &&
            canvas.transform.IsChildOf(enemy.transform)
        )
        {
            canvas.gameObject.SetActive(false);
            return;
        }

        if (bar.transform != enemy.transform)
            bar.gameObject.SetActive(false);
    }
}