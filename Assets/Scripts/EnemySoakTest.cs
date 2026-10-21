#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// DUSMAN AI SOAK TESTI (sadece editor, Play modunda).
/// Her dusman tipini tek basina ve karisik gruplar halinde, iki oyuncu davranisiyla dener:
///   Still = oyuncu kipirdamaz | Dance = oyuncu ileri-geri yurur ve vurur.
/// Yakaladigi sorunlar:
///   STALL_CHASE     : Chase durumunda >1.5 sn hizi 0 (menzile girememe kilidi; Leaper hatasi gibi)
///   HUG_NO_ATTACK   : oyuncuya <3.2 birimde >3 sn hic saldiri baslatmadi
///   NO_ATTACK_6S    : 6 sn boyunca hic saldiri baslatmadi (grupta bekleme mesru olabilir; tek basinaysa hata)
///   ALL_WHIFF       : >=3 saldiri basladi, oyuncu hareketsizken hic isabet etmedi
///   PUSHED          : oyuncu isabet basina >6 birim itildi (surekli itme)
///   FELL            : dusman zeminin cok altina dustu
/// Kullanim: Play'e gir (ana menuyu gec) -> Tools > Enemy Soak Test > Run All. Rapor Console'a ve proje kokune SoakReport.txt.
/// </summary>
[DefaultExecutionOrder(-999)]
public class EnemySoakTest : MonoBehaviour
{
    // ---------- menu ----------
    [MenuItem("Tools/Enemy Soak Test/Run All")]
    public static void RunAll() { Launch(""); }

    [MenuItem("Tools/Enemy Soak Test/Run All", true)]
    static bool RunAllValid() { return Application.isPlaying && FindFirstObjectByType<EnemySoakTest>() == null; }

    public static void Launch(string onlyKey)
    {
        if (!Application.isPlaying) { Debug.LogWarning("SOAK: once Play moduna gir."); return; }
        var go = new GameObject("EnemySoakTest");
        var s = go.AddComponent<EnemySoakTest>();
        s.onlyKey = onlyKey;
    }

    // ---------- ayarlar ----------
    public string onlyKey = "";          // "Leaper|Still" gibi: sadece o kosu
    const float StillTime = 8f, DanceTime = 12f;
    static readonly string[][] Scenarios =
    {
        new[]{"Duelist"}, new[]{"Quick"}, new[]{"Heavy"}, new[]{"Archer"}, new[]{"Leaper"},
        new[]{"Duelist","Quick"}, new[]{"Heavy","Archer"}, new[]{"Leaper","Duelist"},
        new[]{"Duelist","Quick","Archer"}, new[]{"Heavy","Leaper","Archer"},
        new[]{"Duelist","Quick","Heavy","Archer","Leaper"},
    };

    // ---------- durum ----------
    PlayerController pc; Health hp; Rigidbody2D prb;
    FieldInfo fHeld, fDown, fStick, fState;
    bool botOn, danceMode; float botT; float nextAtk;
    List<GameObject> live = new List<GameObject>();
    StringBuilder report = new StringBuilder();
    List<string> problems = new List<string>();
    Vector3 startPos;

    class Track
    {
        public string name; public GameObject go; public EnemyUnit eu; public Rigidbody2D rb;
        public float lastWindup, stallT, hugT; public bool wasWindup; public int attacks;
        public Dictionary<string,string> flags = new Dictionary<string,string>(); public string lastState = "";
        public float diedAt = -1f;
    }

    void Start()
    {
        var bf = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        fHeld = typeof(MobileInput).GetField("held", bf); fDown = typeof(MobileInput).GetField("downFrame", bf); fStick = typeof(MobileInput).GetField("stickX", bf);
        fState = typeof(EnemyUnit).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
        StartCoroutine(Main());
    }

    void Update()
    {
        if (!botOn || pc == null) return;
        float stick = 0f; bool atk = false;
        if (danceMode)
        {
            botT += Time.deltaTime;
            GameObject nearest = null; float nd = 1e9f;
            foreach (var g in live) { if (g == null || !g.activeInHierarchy) continue; float d = Mathf.Abs(g.transform.position.x - pc.transform.position.x); if (d < nd) { nd = d; nearest = g; } }
            if (nearest != null)
            {
                float dir = Mathf.Sign(nearest.transform.position.x - pc.transform.position.x);
                bool toward = ((int)(botT / 2.2f)) % 2 == 0;
                stick = toward ? dir : -dir;
                if (nd < 7f && botT >= nextAtk) { atk = true; nextAtk = botT + 0.55f; }
            }
        }
        fStick.SetValue(null, stick);
        var held = (bool[])fHeld.GetValue(null); var down = (int[])fDown.GetValue(null);
        int b = (int)MBtn.Attack; held[b] = atk; if (atk) down[b] = Time.frameCount;
    }

    IEnumerator Main()
    {
        MobileInput.ForceMobile = true;

        var mm = FindFirstObjectByType<MainMenu>(); if (mm != null) mm.gameObject.SetActive(false);
        var gw = GameObject.Find("GiantWarrior"); if (gw != null) gw.SetActive(false);
        pc = FindFirstObjectByType<PlayerController>(); hp = pc.GetComponent<Health>(); prb = pc.rb;
        pc.canControl = true; pc.isInvincible = false;
        startPos = pc.transform.position;

        report.AppendLine("ENEMY SOAK RAPORU  " + System.DateTime.Now);
        int runs = 0;
        foreach (var sc in Scenarios)
        {
            foreach (int mode in new[] { 0, 1 })
            {
                string key = string.Join("+", sc) + "|" + (mode == 0 ? "Still" : "Dance");
                if (onlyKey != "" && onlyKey != key) continue;
                yield return RunOne(sc, key, mode == 1, mode == 1 ? DanceTime : StillTime);
                runs++;
            }
        }
        report.AppendLine(); report.AppendLine("=== OZET: " + runs + " kosu, " + problems.Count + " sorun ===");
        foreach (var p in problems) report.AppendLine("SORUN: " + p);
        if (problems.Count == 0) report.AppendLine("Sorun yakalanmadi.");
        Debug.Log("SOAK RAPOR\n" + report);
        try { System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../SoakReport.txt"), report.ToString()); } catch { }
        botOn = false; fStick.SetValue(null, 0f); MobileInput.ForceMobile = false;
        Destroy(gameObject);
    }

    GameObject FindPrefab(string type)
    {
        foreach (var g in AssetDatabase.FindAssets("Graybox" + type + " t:Prefab"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (System.IO.Path.GetFileNameWithoutExtension(p) == "Graybox" + type) return AssetDatabase.LoadAssetAtPath<GameObject>(p);
        }
        return null;
    }

    IEnumerator RunOne(string[] types, string key, bool dance, float duration)
    {
        foreach (var g in live) if (g != null) Destroy(g);
        live.Clear(); botOn = false; fStick.SetValue(null, 0f);
        yield return null;
        pc.transform.position = startPos; prb.linearVelocity = Vector2.zero; hp.SetHealth(hp.MaxHealth);
        yield return new WaitForSeconds(0.4f);

        var tracks = new List<Track>();
        float x0 = startPos.x;
        for (int i = 0; i < types.Length; i++)
        {
            var pf = FindPrefab(types[i]); if (pf == null) { problems.Add(key + ": prefab yok " + types[i]); continue; }
            float sx = x0 + 14f + 3.5f * i;
            var hit = Physics2D.Raycast(new Vector2(sx, startPos.y + 6f), Vector2.down, 40f, LayerMask.GetMask("Ground"));
            float sy = hit.collider != null ? hit.point.y + 0.3f : startPos.y;
            var go = Instantiate(pf, new Vector3(sx, sy, 0f), Quaternion.identity); go.name = "Soak_" + types[i];
            live.Add(go);
            tracks.Add(new Track { name = types[i], go = go, eu = go.GetComponent<EnemyUnit>(), rb = go.GetComponent<Rigidbody2D>(), lastWindup = 0f });
        }
        botT = 0f; nextAtk = 0.5f; danceMode = dance; botOn = true;

        float t = 0f, playerHits = 0f, lastHp = hp.CurrentHealth, px0 = pc.transform.position.x, maxPush = 0f;
        float groundY = startPos.y - 12f;
        while (t < duration)
        {
            yield return null; t += Time.deltaTime;
            if (hp.CurrentHealth < lastHp) playerHits++;
            lastHp = hp.CurrentHealth;
            if (hp.CurrentHealth < hp.MaxHealth * 0.3f) { hp.SetHealth(hp.MaxHealth); lastHp = hp.CurrentHealth; }
            maxPush = Mathf.Max(maxPush, Mathf.Abs(pc.transform.position.x - px0));
            int alive = 0;
            foreach (var tr in tracks)
            {
                if (tr.go == null || !tr.go.activeInHierarchy) { if (tr.diedAt < 0f) tr.diedAt = t; continue; }
                alive++;
                string st = tr.eu != null && fState != null ? fState.GetValue(tr.eu).ToString() : "?";
                bool wind = st == "Windup";
                if (wind && !tr.wasWindup) { tr.attacks++; tr.lastWindup = t; }
                tr.wasWindup = wind; tr.lastState = st;
                float dist = Mathf.Abs(tr.go.transform.position.x - pc.transform.position.x);
                float vx = tr.rb != null ? Mathf.Abs(tr.rb.linearVelocity.x) : 1f;
                if (st == "Chase" && vx < 0.15f && dist > 2.5f) tr.stallT += Time.deltaTime; else tr.stallT = 0f;
                if (tr.stallT > 1.5f && dist < 14f && !tr.flags.ContainsKey("STALL")) tr.flags["STALL"] = "STALL_CHASE(dist=" + dist.ToString("F1") + ",t=" + t.ToString("F1") + ")";
                if (dist < 3.2f && !wind && st != "Active" && st != "Recover") tr.hugT += Time.deltaTime; else tr.hugT = 0f;
                if (tr.hugT > 3f && !tr.flags.ContainsKey("HUG")) tr.flags["HUG"] = "HUG_NO_ATTACK(state=" + st + ",t=" + t.ToString("F1") + ")";
                if (t - tr.lastWindup > 6f && dist < 14f && !tr.flags.ContainsKey("NOATK")) tr.flags["NOATK"] = "NO_ATTACK_6S(state=" + st + ",dist=" + dist.ToString("F1") + ")";
                if (tr.go.transform.position.y < groundY) tr.flags["FELL"] = "FELL";
            }
            if (alive == 0 && tracks.Count > 0) break;
        }
        botOn = false; fStick.SetValue(null, 0f);

        bool single = tracks.Count == 1;
        var sb = new StringBuilder();
        sb.Append("[" + key + "] sure=" + t.ToString("F1") + "s oyuncuIsabet=" + playerHits + " itilme=" + maxPush.ToString("F1"));
        foreach (var tr in tracks)
        {
            sb.Append(" | " + tr.name + ": saldiri=" + tr.attacks + (tr.diedAt >= 0f ? " oldu@" + tr.diedAt.ToString("F1") : ""));
            if (!dance && tr.attacks >= 3 && playerHits == 0f && single) tr.flags["ALL_WHIFF"] = "ALL_WHIFF";
            foreach (var f in tr.flags.Values)
            {
                bool legit = !single && f.StartsWith("NO_ATTACK_6S");
                string line = key + " -> " + tr.name + ": " + f + (legit ? " (grupta olabilir)" : "");
                sb.Append(" !" + f);
                if (!legit) problems.Add(line);
            }
        }
        if (!dance && single && playerHits > 0f && maxPush / (playerHits + 1f) > 6f) { problems.Add(key + " -> PUSHED itilme=" + maxPush.ToString("F1")); sb.Append(" !PUSHED"); }
        report.AppendLine(sb.ToString());
        Debug.Log("SOAK " + sb);

        foreach (var g in live) if (g != null) Destroy(g);
        live.Clear();
        yield return null;
    }
}
#endif
