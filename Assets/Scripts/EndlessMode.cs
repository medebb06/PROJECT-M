using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Sonsuz mod bayrağı. Ana menüdeki "SONSUZ MOD" açar, ana menüye dönünce / normal BAŞLA kapanır.
/// R ile yeniden başlatınca açık kalır.</summary>
public static class EndlessMode
{
    public static bool Active;
}

/// <summary>
/// SONSUZ MOD: düşmanlar sürekli doğar (zamanla artan baskı), ölen düşmandan can küreleri düşer.
/// Amaç: ölmeden ne kadar kesebileceğini görmek. Sağ üstte kesim sayısı + süre; ölünce özet ve R ile tekrar.
/// Oyun açılınca kendiliğinden kurulur; sahneye ekleme gerekmez.
/// </summary>
public class EndlessManager : MonoBehaviour
{
    [Header("Doğma")]
    [Tooltip("Başlangıçta aynı anda en fazla düşman sayısı.")]
    public int startMaxAlive = 4;
    public int hardMaxAlive = 12;
    [Tooltip("Kaç saniyede bir üst sınır +1 artsın.")]
    public float maxAliveRampSeconds = 12f;
    [Tooltip("Doğma aralığı: başta bu, zamanla en aza iner (sn).")]
    public float startInterval = 2.0f;
    public float minInterval = 0.6f;
    [Tooltip("Aralığın en aza inmesi için geçen süre (sn).")]
    public float intervalRampSeconds = 60f;
    [Tooltip("Her kademenin süresi (sn).")] public float levelSeconds = 20f;
    [Tooltip("Toplam kademe sayısı (sonuncuda en sık doğma).")] public int levelCount = 6;
    public float firstSpawnDelay = 1.2f;
    [Tooltip("Yoğunluk: aynı anda en fazla düşman sayısı bu çarpanla azalır.")]
    [Range(0.2f, 1f)] public float maxAliveMul = 0.5f;
    [Tooltip("Yoğunluk: doğma aralığı bu çarpanla uzar.")]
    [Range(1f, 4f)] public float intervalMul = 1.8f;
    private float nextSide;
    [Tooltip("Oyuncudan bu kadar uzakta (ekranın dışında) doğar; kamera genişliğine eklenir.")]
    public float spawnExtraDistance = 2.5f;

    static EndlessManager instance;

    PlayerController player;
    Health playerHealth;
    GameObject prefab;
    bool prefabSearched;

    float runTime, spawnTimer;
    int kills;
    bool wasRunning, over;
    int best;
    GUIStyle big, mid, small;

    const string BestKey = "endless_best_kills";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindAnyObjectByType<EndlessManager>() != null)
            return;

        var go = new GameObject("EndlessManager");
        DontDestroyOnLoad(go);
        go.AddComponent<EndlessManager>();
    }

    void Awake()
    {
        instance = this;
        best = PlayerPrefs.GetInt(BestKey, 0);
    }

    void OnEnable()
    {
        CombatEvents.EnemyKilled += OnKilled;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        CombatEvents.EnemyKilled -= OnKilled;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m) { ResetRun(); }

    void ResetRun()
    {
        runTime = 0f;
        spawnTimer = firstSpawnDelay;
        kills = 0;
        over = false;
        wasRunning = false;
        player = null;
        playerHealth = null;
    }

    void OnKilled(EnemyController e)
    {
        if (EndlessMode.Active && !over) kills++;
    }

    bool Running()
    {
        return EndlessMode.Active && !MainMenu.Active;
    }

    void Update()
    {
        if (!Running())
        {
            wasRunning = false;
            return;
        }

        if (!wasRunning) { wasRunning = true; if (runTime <= 0f) spawnTimer = firstSpawnDelay; }

        if (player == null)
        {
            player = FindFirstObjectByType<PlayerController>();
            if (player != null) playerHealth = player.GetComponent<Health>();
            if (player == null) return;
        }

        if (!over && playerHealth != null && playerHealth.IsDead)
        {
            over = true;

            if (kills > best)
            {
                best = kills;
                PlayerPrefs.SetInt(BestKey, best);
                PlayerPrefs.Save();
            }
        }

        if (over) return;

        float dt = Time.deltaTime;
        runTime += dt;
        spawnTimer -= dt;

        if (spawnTimer > 0f) return;

        // KADEME KADEME: her 'levelSeconds' saniyede bir seviye atlar; sıklık ve üst sınır basamak basamak artar.
        int level = Mathf.Min(levelCount, Mathf.FloorToInt(runTime / Mathf.Max(1f, levelSeconds)));
        float k = levelCount > 0 ? (float)level / levelCount : 1f;
        int maxAlive = Mathf.Min(hardMaxAlive, startMaxAlive + Mathf.RoundToInt((hardMaxAlive - startMaxAlive) * k));
        maxAlive = Mathf.Max(1, Mathf.RoundToInt(maxAlive * maxAliveMul));
        spawnTimer = Mathf.Lerp(startInterval, minInterval, k) * intervalMul * Random.Range(0.9f, 1.1f);

        if (AliveCount() >= maxAlive) return;

        Spawn();
    }

    int AliveCount()
    {
        int n = 0;
        for (int i = 0; i < EnemyController.All.Count; i++)
        {
            var e = EnemyController.All[i];
            if (e != null && !e.IsDead) n++;
        }
        return n;
    }

    GameObject FindPrefab()
    {
        if (prefab != null) return prefab;
        if (prefabSearched) return null;
        prefabSearched = true;

        prefab = Resources.Load<GameObject>("Endless/Duelist");

        if (prefab == null)
        {
            var sp = FindFirstObjectByType<EnemySpawner>();
            if (sp != null && sp.enemyPrefab != null) prefab = sp.enemyPrefab;
        }

        if (prefab == null)
            Debug.LogError("Sonsuz mod: düşman prefab'ı bulunamadı (Resources/Endless/Duelist).");

        return prefab;
    }

    void Spawn()
    {
        GameObject p = FindPrefab();
        if (p == null || player == null) return;

        Camera cam = Camera.main;
        float half = cam != null ? cam.orthographicSize * cam.aspect : 11f;
        float dist = half + spawnExtraDistance;

        // Düzenli sağ-sol-sağ-sol: sıra her doğuşta değişir.
        if (nextSide == 0f) nextSide = Random.value < 0.5f ? -1f : 1f;
        float firstSide = nextSide;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            float side = (attempt % 2 == 0) ? firstSide : -firstSide;
            float x = player.transform.position.x + side * (dist + Random.Range(0f, 3f));

            if (!FindGround(x, player.transform.position.y + 8f, out Vector2 pos))
                continue;

            GameObject go = Instantiate(p, new Vector3(pos.x, pos.y + 0.6f, 0f), Quaternion.identity);
            go.name = p.name + " (endless)";
            nextSide = -side;

            var ec = go.GetComponent<EnemyController>();
            if (ec != null) ec.alwaysHunt = true;
            return;
        }
    }

    static bool FindGround(float x, float fromY, out Vector2 pos)
    {
        pos = Vector2.zero;

        RaycastHit2D[] hits = Physics2D.RaycastAll(new Vector2(x, fromY), Vector2.down, 40f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D c = hits[i].collider;

            if (c == null || c.isTrigger) continue;
            if (c.GetComponentInParent<EnemyController>() != null || c.GetComponentInParent<PlayerController>() != null) continue;
            if (c.GetComponentInParent<Health>() != null) continue;

            pos = hits[i].point;
            return true;
        }

        return false;
    }

    // ------------------------------------------------------------- HUD

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || !Running()) return;

        float s = Screen.height / 1080f;

        if (big == null)
        {
            big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold };
            mid = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold };
            small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight };
        }

        big.fontSize = Mathf.RoundToInt(28 * s);
        mid.fontSize = Mathf.RoundToInt(14 * s);
        small.fontSize = Mathf.RoundToInt(11 * s);

        int sec = Mathf.FloorToInt(runTime);
        string time = (sec / 60).ToString("00") + ":" + (sec % 60).ToString("00");

        Rect r = new Rect(Screen.width - 640f * s - 36f * s, 22f * s, 640f * s, 70f * s);
        Shadowed(r, kills.ToString(), big, new Color(1f, 0.93f, 0.9f, 0.95f), s);

        Rect r2 = new Rect(r.x, r.y + 64f * s, r.width, 34f * s);
        Shadowed(r2, "KESİM   " + time, mid, new Color(1f, 1f, 1f, 0.6f), s);

        Rect r3 = new Rect(r.x, r2.y + 34f * s, r.width, 28f * s);
        Shadowed(r3, "EN İYİ  " + Mathf.Max(best, kills), small, new Color(1f, 1f, 1f, 0.38f), s);

        if (over)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            big.alignment = TextAnchor.MiddleCenter; mid.alignment = TextAnchor.MiddleCenter;
            Rect c = new Rect(0, Screen.height * 0.5f - 120f * s, Screen.width, 90f * s);
            Shadowed(c, kills + " KESİM", big, new Color(1f, 0.93f, 0.9f, 1f), s);
            Shadowed(new Rect(0, c.y + 84f * s, Screen.width, 40f * s), time + "     EN İYİ  " + best, mid, new Color(1f, 1f, 1f, 0.7f), s);
            Shadowed(new Rect(0, c.y + 140f * s, Screen.width, 40f * s), "R  TEKRAR DENE", mid, new Color(0.95f, 0.45f, 0.4f, 0.95f), s);
            big.alignment = TextAnchor.UpperRight; mid.alignment = TextAnchor.UpperRight;
        }
    }

    static void Shadowed(Rect r, string text, GUIStyle st, Color col, float s)
    {
        Color keep = st.normal.textColor;
        st.normal.textColor = new Color(0f, 0f, 0f, col.a * 0.7f);
        Rect o = r; o.x += 2f * s; o.y += 2f * s;
        GUI.Label(o, text, st);
        st.normal.textColor = col;
        GUI.Label(r, text, st);
        st.normal.textColor = keep;
    }
}
