using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Graybox encounter akisi: oyuncu tetik noktasina gelince düsmanlar dogar, arena kilitlenir,
// hepsi ölünce kilit acilir ve siradaki encounter beklenir.
// Tuslar: R = sahneyi bastan yükle | K = aktif düsmanlari öldür (debug)
public class EncounterDirector : MonoBehaviour
{
    [System.Serializable]
    public class Spawn
    {
        public string label = "Duelist";
        public GameObject prefab;
        public EnemyDef def;
        [Tooltip("Encounter merkezine göre X ofseti. Negatif = sol, pozitif = sag.")]
        public float offsetX = 8f;
    }

    [System.Serializable]
    public class Encounter
    {
        public string label = "Duel";
        public string hint = "";
        public float triggerX = 20f;
        public float halfWidth = 22f;
        public List<Spawn> spawns = new List<Spawn>();
    }

    public List<Encounter> encounters = new List<Encounter>();
    public string groundLayerName = "Ground";

    [Tooltip("Açık: encounter'daki düşmanlar TEK TEK çıkar; biri ölünce sıradaki doğar. Kapalı: hepsi birden.")]
    public bool oneAtATime = true;
    [Tooltip("Bir düşman ölünce sıradakinin doğmasına kadar bekleme (gerçek sn).")]
    public float nextSpawnDelay = 0.7f;

    private readonly Queue<Spawn> pending = new Queue<Spawn>();
    private float nextSpawnAt;
    private int spawnCounter;
    private float spawnY, spawnZ;

    private int index;
    private bool active;
    private readonly List<GameObject> alive = new List<GameObject>();
    private readonly List<GameObject> walls = new List<GameObject>();
    private float clearedUntil;

    private Transform Player
    {
        get
        {
            GameObject p = GameObject.FindWithTag("Player");
            return p != null ? p.transform : null;
        }
    }

    private void Start()
    {
        // Sahne kenar duvarlarını görünür yap (görünmez duvara çarpma olmasın).
        foreach (GameObject go in GameObject.FindObjectsOfType<GameObject>())
            if (go.name == "EdgeWall" && go.transform.Find("WallVisual") == null)
                AddWallVisual(go.transform, new Color(0.7f, 0.7f, 0.75f, 0.6f));
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) { SceneManager.LoadScene(SceneManager.GetActiveScene().path); return; }
        if (Input.GetKeyDown(KeyCode.K)) KillActive();

        Transform p = Player;
        if (p == null || index >= encounters.Count) return;
        Encounter e = encounters[index];

        if (!active)
        {
            if (p.position.x >= e.triggerX) Begin(e, p);
            return;
        }

        for (int i = alive.Count - 1; i >= 0; i--)
        {
            GameObject go = alive[i];
            if (go == null) { alive.RemoveAt(i); continue; }
            EnemyBase b = go.GetComponent<EnemyBase>();
            if (b == null || b.IsDead) alive.RemoveAt(i);
        }
        if (alive.Count == 0)
        {
            if (pending.Count > 0)
            {
                if (nextSpawnAt <= 0f) nextSpawnAt = Time.unscaledTime + nextSpawnDelay;

                if (Time.unscaledTime >= nextSpawnAt)
                {
                    nextSpawnAt = 0f;
                    SpawnOne(e, pending.Dequeue());
                }
            }
            else End();
        }
    }

    private void Begin(Encounter e, Transform p)
    {
        active = true;
        alive.Clear();
        pending.Clear();
        spawnCounter = 0;
        nextSpawnAt = 0f;
        spawnY = p.position.y;
        spawnZ = p.position.z;

        foreach (Spawn s in e.spawns)
            if (s.prefab != null) pending.Enqueue(s);

        if (oneAtATime)
        {
            if (pending.Count > 0) SpawnOne(e, pending.Dequeue());
        }
        else
        {
            while (pending.Count > 0) SpawnOne(e, pending.Dequeue());
        }

        MakeWall(e.triggerX - e.halfWidth);
        MakeWall(e.triggerX + e.halfWidth);
    }

    private void SpawnOne(Encounter e, Spawn s)
    {
        Vector3 pos = new Vector3(e.triggerX + s.offsetX, spawnY, spawnZ);
        GameObject go = Instantiate(s.prefab, pos, Quaternion.identity);
        go.name = s.label + "_" + spawnCounter++;
        EnemyUnit u = go.GetComponent<EnemyUnit>();
        if (u != null && s.def != null) { u.def = s.def; u.ApplyDef(); }
        alive.Add(go);
    }

    private void End()
    {
        active = false;
        pending.Clear();
        foreach (GameObject w in walls) if (w != null) Destroy(w);
        walls.Clear();
        index++;
        clearedUntil = Time.unscaledTime + 3f;
    }

    private void MakeWall(float x)
    {
        GameObject w = new GameObject("EncounterWall");
        w.transform.position = new Vector3(x, 0f, 0f);
        int layer = LayerMask.NameToLayer(groundLayerName);
        if (layer >= 0) w.layer = layer;
        BoxCollider2D c = w.AddComponent<BoxCollider2D>();
        c.size = new Vector2(1f, 80f);
        AddWallVisual(w.transform, new Color(1f, 0.25f, 0.2f, 0.45f));
        walls.Add(w);
    }

    private static Sprite whiteSprite;
    private static Sprite WhiteSprite()
    {
        if (whiteSprite != null) return whiteSprite;
        Texture2D tex = new Texture2D(4, 4);
        Color[] px = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = Color.white;
        tex.SetPixels(px); tex.Apply();
        whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        return whiteSprite;
    }

    // Görünmez duvara çarpma hissi olmasın: kilit duvarı net, yarı saydam kırmızı bant.
    public static void AddWallVisual(Transform wall, Color col)
    {
        GameObject v = new GameObject("WallVisual");
        v.transform.SetParent(wall, false);
        SpriteRenderer sr = v.AddComponent<SpriteRenderer>();
        sr.sprite = WhiteSprite();
        v.transform.localScale = new Vector3(0.5f, 80f, 1f);
        sr.color = col;
        sr.sortingOrder = 500;
    }

    private void KillActive()
    {
        foreach (GameObject go in alive)
        {
            if (go == null) continue;
            Health h = go.GetComponent<Health>();
            if (h != null) h.TakeDamage(99999);
        }
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;   // Layout/Input olaylarında çizme (GC azalt)
        if (encounters.Count == 0) return;   // serbest test: kendi koyduğun düşmanlar
        GUIStyle st = new GUIStyle(GUI.skin.label);
        st.fontSize = 18;
        st.alignment = TextAnchor.UpperCenter;
        st.normal.textColor = Color.white;
        Rect r = new Rect(0, 8, Screen.width, 60);

        if (index >= encounters.Count)
            GUI.Label(r, "TUM ENCOUNTER'LAR BITTI  -  [R] bastan", st);
        else
        {
            Encounter e = encounters[index];
            string head = (index + 1) + "/" + encounters.Count + "  " + e.label;
            if (active) GUI.Label(r, head + "   (kalan: " + (alive.Count + pending.Count) + ")", st);
            else
            {
                string msg = Time.unscaledTime < clearedUntil && index > 0 ? "TEMIZ  ->  saga git\n" : "";
                GUI.Label(r, msg + "siradaki: " + head + (string.IsNullOrEmpty(e.hint) ? "" : "  -  " + e.hint), st);
            }
        }
        st.fontSize = 12;
        GUI.Label(new Rect(0, Screen.height - 22, Screen.width, 20), "[R] bastan  [K] aktif düsmanlari öldür", st);
    }
}
