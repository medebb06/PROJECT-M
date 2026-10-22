using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Test sahnesi (EncounterTest) için sürekli düşman akını: farklı tipte düşmanlar iki yandan gelir,
// ölen düşman biraz can verir. Sahneye dokunmadan Resources/HordeMode prefab'ından kendini kurar.
// B = akını aç/kapa   N = hepsini öldür
public class HordeMode : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public string label = "Duelist";
        public GameObject prefab;
        public float weight = 1f;
    }

    public List<Entry> entries = new List<Entry>();

    [Header("Akın")]
    public bool running = true;
    public int startAlive = 4;           // başta aynı anda en fazla
    public int maxAliveCap = 10;         // zamanla çıkacağı üst sınır
    public float rampSeconds = 90f;      // üst sınıra çıkma süresi
    public float startInterval = 1.6f;   // doğma aralığı (sn)
    public float endInterval = 0.7f;
    public float spawnDistance = 16f;    // oyuncudan uzaklık (ekran dışı)
    public float spawnJitter = 4f;

    [Header("Ödül")]
    public int healOnKill = 8;           // ölen düşman başına can

    private float nextSpawn;
    private float startTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;   // R ile sahne yeniden yüklenince de akın kurulsun
        TryCreate();
    }

    private static void OnSceneLoaded(Scene s, LoadSceneMode m) { TryCreate(); }

    private static void TryCreate()
    {
        if (SceneManager.GetActiveScene().name != "EncounterTest") return;
        if (FindFirstObjectByType<HordeMode>() != null) return;
        GameObject prefab = Resources.Load<GameObject>("HordeMode");
        if (prefab == null) return;
        Instantiate(prefab).name = "HordeMode";

        // Sahnenin kendi encounter akışı akınla karışmasın.
        EncounterDirector dir = FindFirstObjectByType<EncounterDirector>();
        if (dir != null) dir.enabled = false;

        // Haritadaki görünmez kenar duvarları kalksın (dash / koşu sınırsız).
        foreach (GameObject go in FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go.name == "EdgeWall") go.SetActive(false);
    }

    private void OnEnable() { CombatEvents.EnemyKilled += OnKilled; startTime = Time.time; nextSpawn = Time.time + 2f; }
    private void OnDisable() { CombatEvents.EnemyKilled -= OnKilled; }

    private void OnKilled(EnemyBase e)
    {
        if (healOnKill <= 0) return;
        GameObject p = GameObject.FindWithTag("Player");
        if (p == null) return;
        Health h = p.GetComponent<Health>();
        if (h != null && !h.IsDead) h.Heal(healOnKill);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B)) running = !running;
        if (Input.GetKeyDown(KeyCode.N)) KillAll();
        if (!running || Time.time < nextSpawn || entries.Count == 0) return;

        float k = Mathf.Clamp01((Time.time - startTime) / Mathf.Max(1f, rampSeconds));
        int cap = Mathf.RoundToInt(Mathf.Lerp(startAlive, maxAliveCap, k));
        int alive = 0;
        for (int i = 0; i < EnemyBase.All.Count; i++)
        {
            EnemyBase b = EnemyBase.All[i];
            if (b != null && !b.IsDead) alive++;
        }
        nextSpawn = Time.time + Mathf.Lerp(startInterval, endInterval, k);
        if (alive >= cap) return;
        SpawnRandom();
    }

    private void SpawnRandom()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p == null) return;

        float total = 0f;
        for (int i = 0; i < entries.Count; i++) if (entries[i].prefab != null) total += Mathf.Max(0f, entries[i].weight);
        if (total <= 0f) return;
        float r = Random.value * total;
        Entry pick = null;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].prefab == null) continue;
            r -= Mathf.Max(0f, entries[i].weight);
            if (r <= 0f) { pick = entries[i]; break; }
        }
        if (pick == null) return;

        float side = Random.value < 0.5f ? -1f : 1f;
        float x = p.transform.position.x + side * (spawnDistance + Random.Range(0f, spawnJitter));
        Instantiate(pick.prefab, new Vector3(x, p.transform.position.y, 0f), Quaternion.identity);
    }

    private void KillAll()
    {
        for (int i = EnemyBase.All.Count - 1; i >= 0; i--)
        {
            EnemyBase b = EnemyBase.All[i];
            if (b == null) continue;
            Health h = b.GetComponent<Health>();
            if (h != null) h.TakeDamage(99999);
        }
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 600, 22), "AKIN: " + (running ? "AÇIK" : "KAPALI") + "   [B] aç/kapa   [N] hepsini öldür   ölen düşman +" + healOnKill + " can");
    }
}
