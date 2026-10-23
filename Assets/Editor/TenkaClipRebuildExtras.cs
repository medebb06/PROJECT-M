#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// RonniIdleKatana rig'ine 'unsheathe' (Q ile kılıç çekme, 0.5 sn) ve 'guard_break' (denge kırılması sendelemesi, 0.45 sn) klipleri ekler.
/// Amaç: bu iki animasyonun eski şapkalı RonniSkin rig'inde oynamasını bitirmek.
///   unsheathe  = tenka'nın kına sokma bölümünün TERSİ (sakate batto) + idle'a oturma.
///   guard_break = geriye savrulma + kılıç kolu havaya + çömelme, idle'a dönüş.
/// Menü: Tools > Ronni > Rebuild Unsheathe + GuardBreak Clips
/// </summary>
public static partial class TenkaClipRebuild
{
    [MenuItem("Tools/Ronni/Rebuild Unsheathe + GuardBreak Clips")]
    public static void MenuExtras() { Debug.Log(RunExtras(true)); }

    static float AngLerp(float a, float b, float t) { return Mathf.LerpAngle(a, b, t); }

    // klibi (isim) dosyada tenka'dan sonra, block'tan önce olacak şekilde koyar / varsa değiştirir
    static string PutClip(string cur, string clipName, string clipJson)
    {
        int blockIdx = cur.IndexOf("{\"name\":\"block\",\"duration\"", StringComparison.Ordinal);
        if (blockIdx < 0) throw new Exception("block klibi yok");
        string marker = "{\"name\":\"" + clipName + "\",\"duration\"";
        int idx = cur.IndexOf(marker, StringComparison.Ordinal);
        if (idx >= 0 && idx < blockIdx)
        {
            var starts = new List<int>();
            foreach (Match m in Regex.Matches(cur, "\\{\"name\":\"[A-Za-z0-9_]+\",\"duration\"")) starts.Add(m.Index);
            int next = blockIdx;
            foreach (int s in starts) if (s > idx) { next = s; break; }
            return cur.Substring(0, idx) + clipJson + "," + cur.Substring(next);
        }
        return cur.Substring(0, blockIdx) + clipJson + "," + cur.Substring(blockIdx);
    }

    static string ClipToJson(string name, float duration)
    {
        var sb = new StringBuilder();
        sb.Append("{\"name\":\"").Append(name).Append("\",\"duration\":").Append(F(duration)).Append(",\"frames\":").Append(NF).Append(",\"loop\":false,\"bones\":[");
        bool first = true;
        foreach (var bn in order)
        {
            if (!first) sb.Append(','); first = false;
            var a = N[bn];
            sb.Append("{\"name\":\"").Append(bn).Append('"');
            for (int c = 0; c < 5; c++)
            {
                sb.Append(",\"").Append(Ch[c]).Append("\":[");
                for (int i = 0; i < NF; i++) { if (i > 0) sb.Append(','); sb.Append(F(a[c][i])); }
                sb.Append(']');
            }
            sb.Append('}');
        }
        sb.Append("]}");
        return sb.ToString();
    }

    static void InitRigData()
    {
        var rig = JsonUtility.FromJson<RigD>(ReadProject(RigPath));
        restLocal = new Dictionary<string, Vector2>(); restAbs = new Dictionary<string, Vector2>(); parentOf = new Dictionary<string, string>();
        var absImg = new Dictionary<string, Vector2>();
        foreach (var b in rig.bones) { absImg[b.name] = new Vector2(b.x, b.y); parentOf[b.name] = b.parent ?? ""; }
        foreach (var b in rig.bones)
        {
            restAbs[b.name] = new Vector2(b.x, -b.y);
            restLocal[b.name] = string.IsNullOrEmpty(b.parent) ? new Vector2(b.x, -b.y) : new Vector2(b.x - absImg[b.parent].x, -(b.y - absImg[b.parent].y));
        }
    }

    public static string RunExtras(bool write)
    {
        var rep = new StringBuilder();
        string root = Path.GetDirectoryName(Application.dataPath);
        string animsFull = Path.GetFullPath(AnimsPath);
        string backupFull = Path.Combine(root, BackupDir, BackupAnims);
        if (!File.Exists(backupFull)) return "HATA: orijinal yedek yok.";
        string cur = File.ReadAllText(animsFull);
        var curFile = JsonUtility.FromJson<FileD>(cur);
        ClipD tk = null; foreach (var c in curFile.clips) if (c.name == "tenka") tk = c;
        if (tk == null || tk.frames < 100) return "HATA: güncel tenka klibi (103 kare) bulunamadı.";
        var origFile = JsonUtility.FromJson<FileD>(File.ReadAllText(backupFull));
        ClipD baseClip = null; foreach (var c in origFile.clips) if (c.name == "tenka") baseClip = c;
        InitRigData();
        const int fps = 60;

        // =================== unsheathe: tenka'nın kına sokma bölümünün tersi ===================
        {
            float dur = 0.5f; NF = Mathf.RoundToInt(dur * fps) + 1; float duration = (NF - 1) / (float)fps;
            N = new Dictionary<string, float[][]>(); order = new List<string>();
            float U1 = 0.64f; int srcLast = tk.frames - 1, srcMid = 61;
            foreach (var tr in tk.bones)
            {
                var a = Arr(tr.name);
                for (int i = 0; i < NF; i++)
                {
                    float u = i / (float)(NF - 1);
                    for (int c = 0; c < 5; c++)
                    {
                        float v;
                        if (u <= U1)
                        {
                            float s = Mathf.Lerp(srcLast, srcMid, u / U1);
                            int i0 = Mathf.FloorToInt(s), i1 = Mathf.Min(i0 + 1, srcLast); float f = s - i0;
                            v = c == 0 ? AngLerp(Old(tr, c, i0), Old(tr, c, i1), f) : Mathf.Lerp(Old(tr, c, i0), Old(tr, c, i1), f);
                        }
                        else
                        {
                            float k = SS((u - U1) / (1f - U1));
                            float v0 = Old(tr, c, srcMid), v1 = Old(tr, c, 0);
                            v = c == 0 ? AngLerp(v0, v1, k) : Mathf.Lerp(v0, v1, k);
                            // bayrak kanalları keskin: kına/ön bayrakları bu bölümde idle değerinde
                            if (tr.name == "fxFlags" || tr.name == "fxMouth" || tr.name == "fxTip" || tr.name == "fxGlint") v = Old(tr, c, 0);
                        }
                        a[c][i] = v;
                    }
                }
            }
            if (write) cur = PutClip(cur, "unsheathe", ClipToJson("unsheathe", duration));
            rep.AppendLine("unsheathe: " + NF + " kare, " + duration.ToString("0.00", inv) + " sn");
        }

        // =================== guard_break: sendeleme ===================
        {
            float dur = 0.45f; NF = Mathf.RoundToInt(dur * fps) + 1; float dt = 1f / fps, duration = (NF - 1) * dt;
            N = new Dictionary<string, float[][]>(); order = new List<string>();
            foreach (var tr in baseClip.bones)
            {
                var a = Arr(tr.name);
                for (int i = 0; i < NF; i++) for (int c = 0; c < 5; c++) a[c][i] = Old(tr, c, 0);
            }
            foreach (var nb in new[] { "legR", "legL", "footR", "footL" }) Arr(nb);
            float bR1 = Val("armR1", 0, 0), bR2 = Val("armR2", 0, 0), bG = Val("grip", 0, 0);
            float bL1 = Val("armL1", 0, 0), bL2 = Val("armL2", 0, 0), bSlR1 = Val("slR1", 0, 0), bSlR2 = Val("slR2", 0, 0), bSlL1 = Val("slL1", 0, 0), bSlL2 = Val("slL2", 0, 0);
            // t: 0 idle -> 0.06 darbe (geriye savrulma) -> 0.16 sendeleme -> 0.30 toparlanma -> 0.45 idle
            KF[] kChest = { new KF(0f, 0f, 0f), new KF(0.06f, -15f), new KF(0.16f, -9f), new KF(0.30f, -3f), new KF(0.45f, 0f, 0f) };
            KF[] kHead = { new KF(0f, 0f, 0f), new KF(0.06f, 12f), new KF(0.16f, 6f), new KF(0.30f, 2f), new KF(0.45f, 0f, 0f) };
            KF[] kHipX = { new KF(0f, 0f, 0f), new KF(0.06f, 110f), new KF(0.16f, 85f), new KF(0.30f, 30f), new KF(0.45f, 0f, 0f) };
            KF[] kD = { new KF(0f, 0f, 0f), new KF(0.06f, 45f), new KF(0.16f, 70f), new KF(0.30f, 30f), new KF(0.45f, 0f, 0f) };
            KF[] kFootF = { new KF(0f, 0f, 0f), new KF(0.06f, 70f), new KF(0.16f, 110f), new KF(0.30f, 40f), new KF(0.45f, 0f, 0f) };   // ön ayak geri kayar
            KF[] kFootB = { new KF(0f, 0f, 0f), new KF(0.06f, 150f), new KF(0.16f, 190f), new KF(0.30f, 70f), new KF(0.45f, 0f, 0f) };
            KF[] kR1 = { new KF(0f, bR1, 0f), new KF(0.06f, -40f), new KF(0.16f, -22f), new KF(0.30f, -6f), new KF(0.45f, bR1, 0f) };
            KF[] kR2 = { new KF(0f, bR2, 0f), new KF(0.06f, 35f), new KF(0.16f, 28f), new KF(0.30f, 8f), new KF(0.45f, bR2, 0f) };
            KF[] kG = { new KF(0f, bG, 0f), new KF(0.06f, -95f), new KF(0.16f, -45f), new KF(0.30f, -5f), new KF(0.45f, bG, 0f) };   // kılıç yukarı savrulur, sonra düşer
            KF[] kL1 = { new KF(0f, bL1, 0f), new KF(0.06f, 38f), new KF(0.16f, 24f), new KF(0.30f, 8f), new KF(0.45f, bL1, 0f) };
            KF[] kL2 = { new KF(0f, bL2, 0f), new KF(0.06f, 18f), new KF(0.16f, 12f), new KF(0.30f, 4f), new KF(0.45f, bL2, 0f) };
            for (int i = 0; i < NF; i++)
            {
                float t = i * dt, ch = Herm(kChest, t);
                Arr("chest")[0][i] += ch; Arr("belly")[0][i] += ch * 0.5f; Arr("head")[0][i] += Herm(kHead, t); Arr("neck")[0][i] += ch * 0.3f;
                float hx = Herm(kHipX, t), d = Herm(kD, t), fF = Herm(kFootF, t), fB = Herm(kFootB, t);
                Arr("hip")[1][i] += hx; Arr("hip")[2][i] += -d;
                float rR, sR, rL, sL;
                SolveLegXY("legR", "footR", hx, d, fF, out rR, out sR); SolveLegXY("legL", "footL", hx, d, fB, out rL, out sL);
                Arr("legR")[0][i] = rR; Arr("legR")[4][i] = sR; Arr("legL")[0][i] = rL; Arr("legL")[4][i] = sL; Arr("footR")[1][i] = fF; Arr("footL")[1][i] = fB;
                Arr("armR1")[0][i] = Herm(kR1, t); Arr("armR2")[0][i] = Herm(kR2, t); Arr("grip")[0][i] = Herm(kG, t);
                Arr("armL1")[0][i] = Herm(kL1, t); Arr("armL2")[0][i] = Herm(kL2, t);
                Arr("slL1")[0][i] = bSlL1 + (Arr("armL1")[0][i] - bL1); Arr("slL2")[0][i] = bSlL2 + 0.35f * (Arr("armL2")[0][i] - bL2);
            }
            // saç / haori gecikmesi
            string[] sec = { "pony1", "pony2", "pony3", "fringe1", "fringe2", "fringe3", "hB1", "hB2", "hB3", "hR1", "hR2", "hR3", "hL1", "hL2", "hL3" };
            float[] gain = { 1.6f, 2.2f, 3.0f, 0.7f, 1.0f, 1.3f, 0.9f, 1.2f, 1.6f, 0.9f, 1.2f, 1.6f, 0.9f, 1.2f, 1.6f };
            float lp = 0f;
            for (int i = 0; i < NF; i++)
            {
                float sig = RotZ(World("chest", i)) + Val("hip", 1, i) * 0.03f;
                lp = i == 0 ? sig : lp + 0.45f * (sig - lp);
                float w = Ramp(i * dt, 0f, 0.02f) * (1f - Ramp(i * dt, 0.36f, 0.45f));
                for (int k = 0; k < sec.Length; k++) if (N.ContainsKey(sec[k])) Arr(sec[k])[0][i] += gain[k] * (lp - sig) * w;
            }
            // sağ kolluk: kök omuzda, dirseğe bakar
            for (int i = 0; i < NF; i++)
            {
                float t = i * dt, r1 = Val("armR1", 0, i), r2 = Val("armR2", 0, i);
                var cw = World("chest", i); float rho = RotZ(cw);
                Vector2 armOff = new Vector2(Val("armR1", 1, i), Val("armR1", 2, i));
                Vector3 P1 = cw.MultiplyPoint3x4(restLocal["armR1"]);
                Vector2 E = new Vector2(P1.x, P1.y) + Rot(armOff, rho) + Rot(restLocal["armR2"], rho + r1);
                Vector2 S2 = E + Rot(restAbs["slR2"] - restAbs["armR2"], rho + r1);
                Vector3 S1w = cw.MultiplyPoint3x4(new Vector3(restLocal["slR1"].x + armOff.x, restLocal["slR1"].y + armOff.y, 0f));
                Vector2 dir = S2 - new Vector2(S1w.x, S1w.y);
                Vector2 v12 = restAbs["slR2"] - restAbs["slR1"];
                float rF = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - Mathf.Atan2(v12.y, v12.x) * Mathf.Rad2Deg - rho;
                float w = Ramp(t, 0.005f, 0.04f) * (1f - Ramp(t, 0.36f, 0.45f));
                float follow = bSlR1 + (r1 - bR1);
                Arr("slR1")[0][i] = follow + Wrap(rF - follow) * w;
                Arr("slR1")[1][i] = armOff.x * w; Arr("slR1")[2][i] = armOff.y * w;
                Arr("slR2")[0][i] = bSlR2 + 0.085f * (r2 - bR2);
            }
            for (int i = 0; i < NF; i++)
            {
                Arr("fxFlags")[3][i] = 0f; Arr("fxFlags")[4][i] = 0f; Arr("fxMouth")[3][i] = 0f; Arr("fxTip")[3][i] = 0f; Arr("fxTip")[4][i] = 0f; Arr("fxGlint")[3][i] = 0f; Arr("fxGlint")[4][i] = 1f;
                Arr("kblade")[3][i] = 1f; Arr("kblade")[4][i] = 1f;
            }
            if (write) cur = PutClip(cur, "guard_break", ClipToJson("guard_break", duration));
            rep.AppendLine("guard_break: " + NF + " kare, " + duration.ToString("0.00", inv) + " sn");
        }

        if (write)
        {
            File.WriteAllText(animsFull, cur, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(AnimsPath, ImportAssetOptions.ForceUpdate);
            rep.AppendLine("YAZILDI");
        }
        return rep.ToString();
    }
}
#endif
