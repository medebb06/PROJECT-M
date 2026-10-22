using System.Collections.Generic;
using UnityEngine;

// Test arenası: düşman doğur, can/posture sıfırla, durumları ekranda gör.
// Tuşlar: 1-4 = giriş sırasındaki düşmanı doğur | 5 = seçili tipten 3 | 6 = 5 | 7 = 8
//         K = hepsini öldür | H = düşman canlarını doldur | G = etiketleri aç/kapa | Tab = tip seç
public class EnemyArena : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public string label = "Duelist";
        public GameObject prefab;
        public EnemyDef def;
    }

    public List<Entry> entries = new List<Entry>();
    public float spawnDistance = 7f;
    public float groupSpacing = 1.6f;
    public bool showLabels = true;

    [Tooltip("Açık: grup tuşlarında (6/7/8/9) düşmanlar TEK TEK çıkar; biri ölünce sıradaki doğar. [O] ile aç/kapa.")]
    public bool oneAtATime = true;
    [Tooltip("Bir düşman ölünce sıradakinin doğmasına kadar bekleme (gerçek sn).")]
    public float nextSpawnDelay = 0.7f;

    [Header("Sonsuz mod")]
    [Tooltip("Açık: sürekli rastgele tipte düşman, sırayla sağdan/soldan doğar; öldürdükçe can dolar. [T] ile aç/kapa. Oyun başında KAPALI başlar.")]
    public bool endless = false;
    public float endlessSpawnDistance = 20f;
    public float endlessSpawnGap = 1.2f;
    public int endlessStartMax = 2;
    public int endlessMaxCap = 5;
    public int killsPerExtraEnemy = 6;
    [Tooltip("Her öldürmede oyuncunun maks canının bu oranı kadar can dolar.")]
    public float healPerKillFraction = 0.2f;

    private int kills;
    private int aliveEndless;
    private float nextEndlessAt;
    private int endlessSide = 1;
    private bool endlessOver;

    private struct Pending { public int index; public float offset; }
    private readonly Queue<Pending> queue = new Queue<Pending>();
    private float nextSpawnAt;

    private int selected;
    private readonly List<GameObject> spawned = new List<GameObject>();

    private Transform Player
    {
        get
        {
            GameObject p = GameObject.FindWithTag("Player");
            return p != null ? p.transform : null;
        }
    }

    private void Update()
    {
        TickQueue();
        // [T] ışınlanma tuşu: eski sonsuz mod kısayolu kaldırıldı (test döngüsü artık [U]).
        TickEndless();
        if (Input.GetKeyDown(KeyCode.O)) oneAtATime = !oneAtATime;
        if (Input.GetKeyDown(KeyCode.U)) ToggleFocusTest();
        if (Input.GetKeyDown(KeyCode.I)) ToggleInfiniteBar();
        TickFocusTest();

        // 1-5: o tipi sağda doğur | Shift+tuş: SOLDA | Ctrl+tuş: UZAKTA (2x mesafe)
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        for (int i = 0; i < 5; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) && i < entries.Count) { selected = i; Spawn(i, 1, shift ? -1f : 1f, ctrl ? 2f : 1f); }
        if (Input.GetKeyDown(KeyCode.Y)) SetRain(!rainOn);

        if (Input.GetKeyDown(KeyCode.Alpha6)) Spawn(selected, 3);
        if (Input.GetKeyDown(KeyCode.Alpha7)) Spawn(selected, 5);
        if (Input.GetKeyDown(KeyCode.Alpha8)) Spawn(selected, 8);
        if (Input.GetKeyDown(KeyCode.Alpha9)) SpawnPair();
        if (Input.GetKeyDown(KeyCode.Tab) && entries.Count > 0) selected = (selected + 1) % entries.Count;
        if (Input.GetKeyDown(KeyCode.K)) KillAll();
        if (Input.GetKeyDown(KeyCode.H)) HealAll();
        if (Input.GetKeyDown(KeyCode.G)) showLabels = !showLabels;
    }

    [Tooltip("Arena açılınca sahnedeki yağmuru (RainSystem) kapat. Y tuşu ile açılıp kapanır.")]
    public bool disableRain = true;
    private bool rainOn = true;

    private void Start()
    {
        if (disableRain) SetRain(false);
    }

    private void SetRain(bool on)
    {
        rainOn = on;
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            if (go.name == "RainSystem" && go.scene.IsValid()) go.SetActive(on);
    }

    public void Spawn(int index, int count, float firstSide = 1f, float distMul = 1f)
    {
        Transform p = Player;
        if (p == null || index < 0 || index >= entries.Count || entries[index].prefab == null) return;
        Entry e = entries[index];

        // Oyuncunun baktığı yönün önüne; kalabalıkta iki yana açılarak.
        for (int n = 0; n < count; n++)
        {
            float side = (n % 2 == 0) ? firstSide : -firstSide;
            float step = (n / 2) * groupSpacing;
            float off = side * (spawnDistance * distMul + step);

            // Tek tek mod: ilki hemen, kalanlar sırada bekler (biri ölünce doğar).
            if (oneAtATime && n > 0)
            {
                queue.Enqueue(new Pending { index = index, offset = off });
                continue;
            }

            SpawnOne(index, p.position.x + off);
        }
    }

    // Ideal karşılaşma: seçili tip YAKIN (sağda), listedeki sonraki tip UZAK (solda).
    public void SpawnPair()
    {
        Transform p = Player;
        if (p == null || entries.Count == 0) return;
        SpawnOne(selected, p.position.x + spawnDistance * 0.8f);

        if (oneAtATime)
            queue.Enqueue(new Pending { index = (selected + 1) % entries.Count, offset = -spawnDistance * 2f });
        else
            SpawnOne((selected + 1) % entries.Count, p.position.x - spawnDistance * 2f);
    }

    private GameObject SpawnOne(int index, float x)
    {
        Transform p = Player;
        if (p == null || index < 0 || index >= entries.Count || entries[index].prefab == null) return null;
        Entry e = entries[index];
        GameObject go = Instantiate(e.prefab, new Vector3(x, p.position.y, p.position.z), Quaternion.identity);
        go.name = e.label + "_" + spawned.Count;
        EnemyUnit u = go.GetComponent<EnemyUnit>();
        if (u != null && e.def != null) { u.def = e.def; u.ApplyDef(); }
        spawned.Add(go);
        return go;
    }

    // Tek tek mod: ekranda canlı düşman kalmayınca sıradakini doğur.
    private void TickQueue()
    {
        if (queue.Count == 0) return;

        for (int i = 0; i < EnemyBase.All.Count; i++)
        {
            EnemyBase b = EnemyBase.All[i];

            if (b != null && !b.IsDead)
            {
                nextSpawnAt = 0f;
                return;
            }
        }

        if (nextSpawnAt <= 0f) nextSpawnAt = Time.unscaledTime + nextSpawnDelay;

        if (Time.unscaledTime < nextSpawnAt) return;

        nextSpawnAt = 0f;

        Transform p = Player;

        if (p == null) { queue.Clear(); return; }

        Pending s = queue.Dequeue();
        SpawnOne(s.index, p.position.x + s.offset);
    }

    private void TickEndless()
    {
        if (!endless || entries.Count == 0) return;
        Transform p = Player;
        if (p == null) return;
        Health ph = p.GetComponent<Health>();
        if (ph != null && ph.IsDead) { endlessOver = true; return; }
        endlessOver = false;

        int max = Mathf.Min(endlessMaxCap, endlessStartMax + kills / Mathf.Max(1, killsPerExtraEnemy));
        if (aliveEndless >= max) return;
        if (Time.unscaledTime < nextEndlessAt) return;
        nextEndlessAt = Time.unscaledTime + endlessSpawnGap;

        // gecerli (prefab'i olan) rastgele tip
        int idx = -1;
        for (int tries = 0; tries < 10; tries++)
        {
            int c = Random.Range(0, entries.Count);
            if (entries[c].prefab != null) { idx = c; break; }
        }
        if (idx < 0) return;

        endlessSide = -endlessSide;   // sag, sol, sag, sol...
        GameObject go = SpawnOne(idx, p.position.x + endlessSide * endlessSpawnDistance);
        if (go == null) return;

        aliveEndless++;
        Health eh = go.GetComponent<Health>();
        if (eh != null) eh.OnDeath += OnEndlessKill;
        else aliveEndless--;
    }

    private void OnEndlessKill()
    {
        aliveEndless = Mathf.Max(0, aliveEndless - 1);
        kills++;
        Transform p = Player;
        if (p == null) return;
        Health ph = p.GetComponent<Health>();
        if (ph != null && !ph.IsDead)
            ph.Heal(Mathf.Max(1, Mathf.RoundToInt(ph.MaxHealth * healPerKillFraction)));
    }

// ---------------------------------------------------------
    // ODAK TESTİ [U]: sonsuz döngü, düşmanlar 2'şer gelir (biri sağdan, biri soldan),
    // yetenek barı hep DOLU. Tekrar [U] ile kapanır.
    // ---------------------------------------------------------
    [Header("Odak testi [U]")]
    public bool focusTest = false;
    [Tooltip("Bir çift ölünce yeni çiftin gelmesine kadar bekleme (gerçek sn).")]
    public float focusTestPairDelay = 0.8f;
    public float focusTestSpawnDistance = 14f;
    private float focusTestNextAt;

    private void ToggleFocusTest()
    {
        focusTest = !focusTest;
        if (focusTest) focusTestWave = 0;
        endless = false;
        queue.Clear();
        focusTestNextAt = 0f;

        Transform p = Player;
        }

    // [I]: yetenek barı hep dolu (aç/kapa).
    private void ToggleInfiniteBar()
    {
        Transform p = Player;
        PlayerAbility ab = p != null ? PlayerAbility.Ensure(p.gameObject) : PlayerAbility.Instance;
        if (ab == null) return;

        ab.focusInfinite = !ab.focusInfinite;
        if (ab.focusInfinite) ab.focusCharge = ab.focusMax;
    }

private void TickFocusTest()
    {
        if (!focusTest || entries.Count == 0) return;

        Transform p = Player;
        if (p == null) return;

        Health ph = p.GetComponent<Health>();
        if (ph != null && ph.IsDead) return;

        // (Bar dolumu artık [I] tuşunda.)

        // Canlı düşman varsa bekle.
        for (int i = 0; i < EnemyBase.All.Count; i++)
        {
            EnemyBase b = EnemyBase.All[i];
            if (b != null && !b.IsDead) { focusTestNextAt = 0f; return; }
        }

        if (focusTestNextAt <= 0f) focusTestNextAt = Time.unscaledTime + focusTestPairDelay;
        if (Time.unscaledTime < focusTestNextAt) return;
        focusTestNextAt = 0f;

        // Test hiç bitmesin: her çiftte can dolsun.
        if (ph != null) ph.Heal(ph.MaxHealth);

        // Sıradaki karşılaşma (liste bitince başa döner).
        Encounter enc = Encounters[focusTestWave % Encounters.Length];
        focusTestWave++;
        focusTestCurrent = enc.name;

        int r = FindEntry(enc.right);
        int l = FindEntry(enc.left);
        if (r < 0) r = RandomValidEntry();
        if (l < 0) l = r;
        if (r < 0) return;

        SpawnOne(r, p.position.x + enc.rightDist);    // sağdan
        SpawnOne(l, p.position.x - enc.leftDist);     // soldan
    }

    // Karşılaşma tasarımı: kolaydan zora, her çift farklı bir şeyi sınar. Mesafeler farklı: iki düşman AYNI ANDA gelmez, biri önce ulaşır.
    private struct Encounter
    {
        public string name, right, left;
        public float rightDist, leftDist;
        public Encounter(string name, string right, float rightDist, string left, float leftDist)
        { this.name = name; this.right = right; this.rightDist = rightDist; this.left = left; this.leftDist = leftDist; }
    }

    private static readonly Encounter[] Encounters =
    {
        new Encounter("Isınma: Duelist + Duelist",           "Duelist", 12f, "Duelist", 16f),
        new Encounter("Hız: Quick + Duelist",                 "Quick",   12f, "Duelist", 15f),
        new Encounter("Yavaş tehdit: Heavy + Duelist",        "Heavy",   14f, "Duelist", 11f),
        new Encounter("Çifte hız: Quick + Quick",             "Quick",   12f, "Quick",   17f),
        new Encounter("Atlayan: Leaper + Duelist",            "Leaper",  13f, "Duelist", 12f),
        new Encounter("Menzilli: Archer + Heavy",             "Archer",  19f, "Heavy",   12f),
        new Encounter("Baskı: Heavy + Quick",                 "Heavy",   13f, "Quick",   16f),
        new Encounter("İki cephe: Leaper + Archer",           "Leaper",  12f, "Archer",  20f),
        new Encounter("Final: Heavy + Leaper",                "Heavy",   12f, "Leaper",  14f),
    };

    private int focusTestWave;
    private string focusTestCurrent = "";

    private int FindEntry(string label)
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].prefab != null && string.Equals(entries[i].label, label, System.StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    private int RandomValidEntry()
    {
        for (int tries = 0; tries < 10; tries++)
        {
            int c = Random.Range(0, entries.Count);
            if (entries[c].prefab != null) return c;
        }
        return -1;
    }


    public void KillAll()
    {
        queue.Clear();
        for (int i = EnemyBase.All.Count - 1; i >= 0; i--)
        {
            EnemyBase b = EnemyBase.All[i];
            if (b == null) continue;
            Health h = b.GetComponent<Health>();
            if (h != null) h.TakeDamage(99999);
        }
        spawned.Clear();
    }

    public void HealAll()
    {
        foreach (EnemyBase b in EnemyBase.All)
        {
            if (b == null) continue;
            EnemyUnit u = b as EnemyUnit;
            if (u != null) u.ResetVitals();
        }
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;   // Layout/Input olaylarında çizme (GC azalt)
        string keys = "";
        for (int i = 0; i < entries.Count && i < 5; i++) keys += (i + 1) + "=" + entries[i].label + "  ";
        GUI.Label(new Rect(10, 10, 1100, 22),
            "ARENA  " + keys + "| Shift=sol  Ctrl=uzak | [6/7/8] 3/5/8 adet  [9] çift  [Tab] tip: " +
            (entries.Count > 0 ? entries[selected].label : "-") +
            "  [K] öldür  [H] doldur  [G] etiket  [Y] yağmur  [O] tek tek: " + (oneAtATime ? "AÇIK" : "kapalı") + "   Aktif: " + EnemyBase.All.Count + (queue.Count > 0 ? "  Sırada: " + queue.Count : ""));

        if (focusTest)
        {
            GUIStyle fb = new GUIStyle(GUI.skin.label);
            fb.fontSize = 22;
            fb.alignment = TextAnchor.UpperCenter;
            fb.normal.textColor = Color.white;
            GUI.Label(new Rect(0, 8, Screen.width, 32), "ODAK TESTİ  [U] kapat   [I] bar hep dolu: " + (PlayerAbility.Instance != null && PlayerAbility.Instance.focusInfinite ? "AÇIK" : "kapalı") + "   |   " + (focusTestWave > 0 ? focusTestWave + ". " + focusTestCurrent : "hazırlanıyor"), fb);
        }

        if (endless)
        {
            GUIStyle big = new GUIStyle(GUI.skin.label);
            big.fontSize = 22;
            big.alignment = TextAnchor.UpperCenter;
            big.normal.textColor = Color.white;
            GUI.Label(new Rect(0, 8, Screen.width, 32), "SONSUZ MOD   Öldürme: " + kills + (endlessOver ? "   -  öldün, [R] ile yeniden başla" : ""), big);
        }

        PlayerPosture pp = FindFirstObjectByType<PlayerPosture>();
        if (pp != null)
            GUI.Label(new Rect(10, 32, 500, 22), "Oyuncu posture: " + pp.CurrentPosture + "/" + pp.MaxPosture + (pp.IsBroken ? "  [KIRIK - blok yok]" : ""));

        if (!showLabels || Camera.main == null) return;
        foreach (EnemyBase b in EnemyBase.All)
        {
            EnemyUnit u = b as EnemyUnit;
            if (u == null) continue;
            Vector3 sp = Camera.main.WorldToScreenPoint(u.transform.position + Vector3.up * 3.2f);
            if (sp.z < 0f) continue;
            Health h = u.GetComponent<Health>();
            EnemyBalance bal = u.GetComponent<EnemyBalance>();
            string t = u.DebugState + "\n" +
                       (h != null ? "HP " + h.CurrentHealth + "/" + h.MaxHealth : "") +
                       (bal != null ? "  PO " + bal.CurrentBalance + "/" + bal.MaxBalance : "");
            GUI.Label(new Rect(sp.x - 70, Screen.height - sp.y, 160, 40), t);
        }
    }
}
