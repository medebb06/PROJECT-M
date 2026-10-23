#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using RonniSkin;

/// <summary>Play modunda oyuncu durumlarını sırayla tetikler, hangi rig'lerin göründüğünü kaydeder (eski şapkalı rig avı). Geçici araç.</summary>
[InitializeOnLoad]
public static class RigAudit
{
    class Step { public string label; public float dur; public Action start; }
    static List<Step> steps = new List<Step>();
    static int idx = -1; static double stepT0; static bool running;
    static Dictionary<string, Dictionary<string, int>> shown = new Dictionary<string, Dictionary<string, int>>();
    static Dictionary<string, int> frames = new Dictionary<string, int>();
    static Dictionary<string, double[]> firstLast = new Dictionary<string, double[]>();
    static Dictionary<string, string> lastKey = new Dictionary<string, string>();
    static List<string> timeline = new List<string>();
    static RonniPlayerBridge b; static PlayerPosture posture;
    public static string Report = "";

    static RigAudit() { EditorApplication.update += Tick; }

    static void Press(int btn, bool hold = false)
    {
        var bf = BindingFlags.Static | BindingFlags.NonPublic;
        var down = (int[])typeof(MobileInput).GetField("downFrame", bf).GetValue(null);
        var held = (bool[])typeof(MobileInput).GetField("held", bf).GetValue(null);
        down[btn] = Time.frameCount + 1; held[btn] = hold;
    }
    static void Release(int btn) { var held = (bool[])typeof(MobileInput).GetField("held", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null); held[btn] = false; }
    static void Stick(float x) { typeof(MobileInput).GetField("stickX", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, x); }

    public static void Start(string[] labels, string[] actions, float[] durs)
    {
        steps.Clear(); shown.Clear(); frames.Clear(); firstLast.Clear(); lastKey.Clear(); timeline.Clear();
        b = UnityEngine.Object.FindAnyObjectByType<RonniPlayerBridge>(); posture = UnityEngine.Object.FindAnyObjectByType<PlayerPosture>();
        MobileInput.ForceMobile = true;
        for (int i = 0; i < labels.Length; i++)
        {
            string a = actions[i]; var s = new Step { label = labels[i], dur = durs[i] };
            s.start = () => Do(a);
            steps.Add(s);
        }
        idx = -1; running = true; Report = "çalışıyor";
    }
    static void Do(string a)
    {
        // btn indexleri: Attack0 Parry1 Dash2 Jump3 Ability4 Execute5 Potion6 Pause7 Restart8; Q = Ability? (Map: KeyCode.Q)
        switch (a)
        {
            case "q": Press(MapIndex(KeyCode.Q)); break;
            case "attack": Press(0); break;
            case "parryhold": Press(1, true); break;
            case "parryrelease": Release(1); break;
            case "dash": Press(2); break;
            case "jump": Press(3); break;
            case "runR": Stick(1f); break;
            case "stop": Stick(0f); break;
            case "break": if (posture != null) posture.TakePostureDamage(100000); break;
            case "resetposture": if (posture != null) posture.ResetPosture(); break;
        }
    }
    static int MapIndex(KeyCode k)
    {
        var m = typeof(MobileInput).GetMethod("Map", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        return m == null ? 4 : (int)m.Invoke(null, new object[] { k });
    }
    static string Hats()
    {
        var sb = new StringBuilder();
        foreach (var n in new[] { "rig", "rigBL", "rigBR" })
        {
            var f = typeof(RonniPlayerBridge).GetField(n, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var r = f != null ? f.GetValue(b) as RonniSkinRig : null;
            if (r != null && r.IsBuilt && r.Shown) sb.Append(n + "(" + r.resourceFolder + ") ");
        }
        return sb.ToString();
    }
    static string Shown()
    {
        var sb = new StringBuilder();
        foreach (var r in b.GetComponentsInChildren<RonniSkinRig>(true)) if (r.IsBuilt && r.Shown) sb.Append(r.resourceFolder + " ");
        foreach (var fn in new[] { "rigBL", "rigBR", "rigRun", "rigIdle", "rigJump", "rigAtk", "rigDrawn" })
        {
            var f = typeof(RonniPlayerBridge).GetField(fn, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var r = f != null ? f.GetValue(b) as RonniSkinRig : null;
            if (r != null && r.IsBuilt && r.Shown && !sb.ToString().Contains(r.resourceFolder + " ")) sb.Append(r.resourceFolder + " ");
        }
        var fs = typeof(RonniPlayerBridge).GetField("frameSR", BindingFlags.Instance | BindingFlags.NonPublic);
        var sr = fs != null ? fs.GetValue(b) as SpriteRenderer : null;
        if (sr != null && sr.enabled && sr.sprite != null) sb.Append("frame:" + sr.sprite.name + " ");
        return sb.ToString().Trim();
    }
    static void Tick()
    {
        if (!running || !Application.isPlaying || b == null) return;
        double now = EditorApplication.timeSinceStartup;
        if (idx < 0 || now - stepT0 >= steps[idx].dur)
        {
            idx++;
            if (idx >= steps.Count)
            {
                running = false; Stick(0f); Release(1); MobileInput.ForceMobile = false;
                var sb = new StringBuilder();
                foreach (var s in steps)
                {
                    sb.AppendLine(s.label + " (" + frames[s.label] + " kare):");
                    foreach (var kv in shown[s.label]) sb.AppendLine("    " + kv.Key + "  x" + kv.Value);
                }
                foreach (var kv in firstLast) sb.AppendLine("HAT " + kv.Key + " ilk=" + kv.Value[0].ToString("0.00") + "s son=" + kv.Value[1].ToString("0.00") + "s");
                foreach (var tl2 in timeline) sb.AppendLine("TL " + tl2);
                Report = sb.ToString(); return;
            }
            stepT0 = now; frames[steps[idx].label] = 0; shown[steps[idx].label] = new Dictionary<string, int>();
            steps[idx].start();
            return;
        }
        string key = Shown(); if (key.Length == 0) key = "(hiçbiri)";
        { string tl = steps[idx].label; string last; if (!lastKey.TryGetValue(tl, out last) || last != key) { lastKey[tl] = key; timeline.Add(tl + " " + (now - stepT0).ToString("0.00") + "s  " + key); } }
        var d = shown[steps[idx].label]; int c; d.TryGetValue(key, out c); d[key] = c + 1; frames[steps[idx].label]++;
        if (key.Contains("RonniSkin ") || key.EndsWith("RonniSkin")) { string tk = steps[idx].label + "@" + key; double el = now - stepT0; double[] fl; if (!firstLast.TryGetValue(tk, out fl)) firstLast[tk] = new double[] { el, el }; else fl[1] = el; }
    }
}
#endif
