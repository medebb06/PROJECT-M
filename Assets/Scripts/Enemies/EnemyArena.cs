using System.Collections.Generic;
using UnityEngine;

// Tuşlar: P = sonsuz düşman döngüsü | O = yetenek barı hep dolu | I = ölümsüzlük (hepsi aç/kapa)
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
    [Tooltip("Açık: sürekli rastgele tipte düşman, sırayla sağdan/soldan doğar; öldürdükçe can dolar. [P] ile aç/kapa. Oyun başında KAPALI başlar.")]
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
        TickEndless();
        TickFocusTest();
        if (MainMenu.Active) return;

        // P: sonsuz düşman döngüsü | O: yetenek barı hep dolu | I: ölümsüzlük (hepsi aç/kapa)
        if (Input.GetKeyDown(KeyCode.P) && !Charms.KuroyoruMode) ToggleEndless();
        if (Input.GetKeyDown(KeyCode.O)) ToggleInfiniteBar();
        if (Input.GetKeyDown(KeyCode.I)) ToggleGod();
    }

    private bool godMode;

    private void OnDisable()
    {
        PlayerDamageReceiver.GodMode = false;
    }

    private void ToggleEndless()
    {
        endless = !endless;
        focusTest = false;
        queue.Clear();
        if (endless)
        {
            kills = 0;
            aliveEndless = 0;
            nextEndlessAt = 0f;
            endlessSide = -1;   // ilk düşman sağdan
            endlessOver = false;
        }
        else
        {
            KillAll();
            aliveEndless = 0;
        }
    }

    private void ToggleGod()
    {
        godMode = !godMode;
        PlayerDamageReceiver.GodMode = godMode;
        Transform p = Player;
        Health ph = p != null ? p.GetComponent<Health>() : null;
        if (ph != null)
        {
            ph.godMode = godMode;
            if (godMode && !ph.IsDead) ph.Heal(ph.MaxHealth);
        }
    }

    [Tooltip("Arena açılınca sahnedeki yağmuru (RainSystem) kapat. Y tuşu ile açılıp kapanır.")]
    public bool disableRain = false;
    private bool rainOn = true;

    private void Start()
    {
        if (disableRain) SetRain(false);
        if (Charms.KuroyoruMode) BeginKuroyoru();   // KUROYORU'da R ile yeniden başlayınca mod devam eder
    }

    [Header("Kuroyoru modu")]
    [Tooltip("Kaç öldürmede bir charm seçimi çıkar.")]
    public int kuroyoruKillsPerPick = 20;
    [Tooltip("Mod başlayınca ilk düşmanın gelmesine kadar bekleme (gerçek sn).")]
    public float kuroyoruStartGrace = 2.5f;

    // Sonsuz mod + tek vuruşta ölüm + her 20 öldürmede +%2 charm seçimi.
    public void BeginKuroyoru()
    {
        KillAll();   // (mod bayrağı henüz kapalı: eski düşmanlar sayılmasın)
        Charms.ClearStacks();
        Charms.KuroyoruMode = true;
        focusTest = false;
        queue.Clear();
        endless = true;
        kills = 0;
        aliveEndless = 0;
        nextEndlessAt = Time.unscaledTime + kuroyoruStartGrace;
        endlessSide = -1;
        endlessOver = false;
    }

    private System.Collections.IEnumerator OpenPickerSoon()
    {
        yield return new WaitForSecondsRealtime(0.6f);   // son öldürmenin efekti bitsin
        Transform p = Player;
        Health ph = p != null ? p.GetComponent<Health>() : null;
        if (Charms.KuroyoruMode && (ph == null || !ph.IsDead)) KuroyoruPicker.Open();
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
        if (KuroyoruPicker.IsOpen) return;
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
        if (Charms.KuroyoruMode && kuroyoruKillsPerPick > 0 && kills % kuroyoruKillsPerPick == 0)
            StartCoroutine(OpenPickerSoon());
        if (Charms.KuroyoruMode) return;   // tek vuruşta öldüğümüz için can doldurmaya gerek yok
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
        if (Event.current.type != EventType.Repaint || MainMenu.Active) return;

        bool bar = PlayerAbility.Instance != null && PlayerAbility.Instance.focusInfinite;
        if (!endless && !godMode && !bar) return;

        GUIStyle big = new GUIStyle(GUI.skin.label);
        big.fontSize = 22;
        big.alignment = TextAnchor.UpperCenter;
        big.normal.textColor = Color.white;

        string line = "";
        if (endless && Charms.KuroyoruMode)
        {
            int per = Mathf.Max(1, kuroyoruKillsPerPick);
            line += "KUROYORU   Öldürme: " + kills + "   |   sonraki charm: " + (per - kills % per) + (endlessOver ? "   -  öldün, [R] ile yeniden başla" : "");
        }
        else if (endless) line += "SONSUZ MOD   Öldürme: " + kills + (endlessOver ? "   -  öldün, [R] ile yeniden başla" : "");
        if (bar) line += (line.Length > 0 ? "   |   " : "") + "BAR FULL";
        if (godMode) line += (line.Length > 0 ? "   |   " : "") + "ÖLÜMSÜZ";
        GUI.Label(new Rect(0, 8, Screen.width, 32), line, big);
    }
}
