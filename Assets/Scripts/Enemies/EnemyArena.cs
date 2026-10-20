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
        for (int i = 0; i < 4; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) && i < entries.Count) { selected = i; Spawn(i, 1); }

        if (Input.GetKeyDown(KeyCode.Alpha5)) Spawn(selected, 3);
        if (Input.GetKeyDown(KeyCode.Alpha6)) Spawn(selected, 5);
        if (Input.GetKeyDown(KeyCode.Alpha7)) Spawn(selected, 8);
        if (Input.GetKeyDown(KeyCode.Tab) && entries.Count > 0) selected = (selected + 1) % entries.Count;
        if (Input.GetKeyDown(KeyCode.K)) KillAll();
        if (Input.GetKeyDown(KeyCode.H)) HealAll();
        if (Input.GetKeyDown(KeyCode.G)) showLabels = !showLabels;
    }

    public void Spawn(int index, int count)
    {
        Transform p = Player;
        if (p == null || index < 0 || index >= entries.Count || entries[index].prefab == null) return;
        Entry e = entries[index];

        // Oyuncunun baktığı yönün önüne; kalabalıkta iki yana açılarak.
        for (int n = 0; n < count; n++)
        {
            float side = (n % 2 == 0) ? 1f : -1f;
            float step = (n / 2) * groupSpacing;
            float x = p.position.x + side * (spawnDistance + step);
            Vector3 pos = new Vector3(x, p.position.y, p.position.z);
            GameObject go = Instantiate(e.prefab, pos, Quaternion.identity);
            go.name = e.label + "_" + spawned.Count;
            EnemyUnit u = go.GetComponent<EnemyUnit>();
            if (u != null && e.def != null) { u.def = e.def; u.ApplyDef(); }
            spawned.Add(go);
        }
    }

    public void KillAll()
    {
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
        GUI.Label(new Rect(10, 10, 700, 22),
            "ARENA  [1-4] doğur  [5/6/7] 3/5/8 adet  [Tab] tip: " +
            (entries.Count > 0 ? entries[selected].label : "-") +
            "  [K] öldür  [H] doldur  [G] etiket   Aktif: " + EnemyBase.All.Count);

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
