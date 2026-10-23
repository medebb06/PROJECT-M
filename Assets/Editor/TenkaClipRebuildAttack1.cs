#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// RonniIdleKatana rig'ine 'attack1' klibini ekler (prototip): el çizimi Attack1 kareleri yerine rig ile oynar.
/// Akış: çömel + kılıcı geriye al -> yatay kesim (vuruş anı ~0.115 sn = combat attack1HitTime 0.35 * 0.33 sn) -> yukarı yay (baş üstü) -> bekle -> idle'a dön.
/// Ayarlar: aşağıdaki a1* alanları. Menü: Tools > Ronni > Rebuild Attack1 Rig Clip
/// </summary>
public static partial class TenkaClipRebuild
{
    // ===== Attack1 ayarları =====
    public static float a1Dur = 0.9f;                 // toplam klip süresi (sn); saldırı 0.33 sn, kalanı toparlanma
    public static float a1CrouchScale = 1.7f;           // çömelme/açılma şiddeti çarpanı
    public static float a1LungeScale = 1.15f;            // öne atılma çarpanı

    struct KF { public float t, v, m; public KF(float t, float v, float m = float.NaN) { this.t = t; this.v = v; this.m = m; } }

    static float KTan(KF[] k, int i)
    {
        if (!float.IsNaN(k[i].m)) return k[i].m;
        if (i == 0 || i == k.Length - 1) return 0f;
        return (k[i + 1].v - k[i - 1].v) / (k[i + 1].t - k[i - 1].t);
    }

    // Hermite: anahtarlardan yumuşak geçer; m verilmezse komşulardan hız çıkarılır (hız kesilmez), uçlarda 0.
    static float Herm(KF[] k, float x)
    {
        if (x <= k[0].t) return k[0].v;
        if (x >= k[k.Length - 1].t) return k[k.Length - 1].v;
        int i = 0; while (i < k.Length - 2 && x > k[i + 1].t) i++;
        float h = k[i + 1].t - k[i].t, u = (x - k[i].t) / h, u2 = u * u, u3 = u2 * u;
        return (2 * u3 - 3 * u2 + 1) * k[i].v + (u3 - 2 * u2 + u) * h * KTan(k, i) + (-2 * u3 + 3 * u2) * k[i + 1].v + (u3 - u2) * h * KTan(k, i + 1);
    }

    static float Ramp(float x, float a, float b) { return b <= a ? (x >= b ? 1f : 0f) : Mathf.Clamp01((x - a) / (b - a)); }

    static void SolveLegXY(string legB, string footB, float hipDx, float d, float ankleDx, out float rotDeg, out float scaleY)
    {
        Vector2 P = restAbs[legB], A0 = restAbs[footB];
        Vector2 l0 = A0 - P;
        float L = -l0.y, dx0 = l0.x;
        Vector2 Pn = new Vector2(P.x + hipDx, P.y - d);
        Vector2 A = new Vector2(A0.x + ankleDx, A0.y);
        Vector2 v = A - Pn;
        float s = Mathf.Sqrt(Mathf.Max(1e-3f, v.sqrMagnitude - dx0 * dx0)) / L;
        float th = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg - Mathf.Atan2(-L * s, dx0) * Mathf.Rad2Deg;
        rotDeg = Wrap(th); scaleY = s;
    }

    [MenuItem("Tools/Ronni/Rebuild Attack1 Rig Clip")]
    public static void MenuAttack1() { Debug.Log(RunAttack1(true)); }

    public static string RunAttack1(bool write)
    {
        var rep = new StringBuilder();
        string root = Path.GetDirectoryName(Application.dataPath);
        string animsFull = Path.GetFullPath(AnimsPath);
        string backupFull = Path.Combine(root, BackupDir, BackupAnims);
        if (!File.Exists(backupFull)) return "HATA: orijinal yedek yok (_backup_before_tenka_slash).";
        var file = JsonUtility.FromJson<FileD>(File.ReadAllText(backupFull));
        ClipD baseClip = null;
        foreach (var c in file.clips) if (c.name == "tenka") baseClip = c;
        if (baseClip == null) return "HATA: yedekte tenka klibi yok.";

        // ---- rig
        var rig = JsonUtility.FromJson<RigD>(ReadProject(RigPath));
        restLocal = new Dictionary<string, Vector2>(); restAbs = new Dictionary<string, Vector2>(); parentOf = new Dictionary<string, string>();
        var absImg = new Dictionary<string, Vector2>();
        foreach (var b in rig.bones) { absImg[b.name] = new Vector2(b.x, b.y); parentOf[b.name] = b.parent ?? ""; }
        foreach (var b in rig.bones)
        {
            restAbs[b.name] = new Vector2(b.x, -b.y);
            restLocal[b.name] = string.IsNullOrEmpty(b.parent) ? new Vector2(b.x, -b.y) : new Vector2(b.x - absImg[b.parent].x, -(b.y - absImg[b.parent].y));
        }

        const int fps = 60;
        NF = Mathf.RoundToInt(a1Dur * fps) + 1;
        float dt = 1f / fps, duration = (NF - 1) * dt;
        N = new Dictionary<string, float[][]>(); order = new List<string>();

        // ---- taban: orijinal tenka klibinin ilk karesi (kılıç çekik idle duruşu) sabit
        foreach (var tr in baseClip.bones)
        {
            var a = Arr(tr.name);
            for (int i = 0; i < NF; i++) for (int c = 0; c < 5; c++) a[c][i] = Old(tr, c, 0);
        }
        foreach (var nb in new[] { "legR", "legL", "footR", "footL" }) Arr(nb);
        float baseR1 = Val("armR1", 0, 0), baseR2 = Val("armR2", 0, 0), baseG = Val("grip", 0, 0);
        float baseSlR2 = Val("slR2", 0, 0), baseSlR1 = Val("slR1", 0, 0);

        // ---- eğriler (t sn). Sağ kol: (armR1.r, armR2.r, grip.r). Bıçak açısı A = R1+R2+G (0 = kılıç ileri yatay, 175 = arkaya, -105 = yukarı/arkaya)
        float cr = a1CrouchScale, lg = a1LungeScale;
        KF[] kR1 = { new KF(0f, baseR1, 0f), new KF(0.065f, 30f, 0f), new KF(0.12f, -80f), new KF(0.21f, -160f), new KF(0.30f, -166f, 0f), new KF(0.85f, baseR1, 0f) };
        KF[] kR2 = { new KF(0f, baseR2, 0f), new KF(0.065f, 15f, 0f), new KF(0.12f, 0f), new KF(0.21f, -5f), new KF(0.30f, -8f, 0f), new KF(0.85f, baseR2, 0f) };
        KF[] kG = { new KF(0f, baseG, 0f), new KF(0.065f, 130f, 0f), new KF(0.12f, 80f), new KF(0.21f, 60f), new KF(0.30f, 52f, 0f), new KF(0.85f, baseG, 0f) };
        KF[] kChest = { new KF(0f, 0f, 0f), new KF(0.065f, -7f, 0f), new KF(0.12f, 9f), new KF(0.21f, 3f), new KF(0.30f, 0f, 0f), new KF(0.85f, 0f, 0f) };
        KF[] kHipX = { new KF(0f, 0f, 0f), new KF(0.065f, 60f * lg, 0f), new KF(0.12f, -90f * lg), new KF(0.21f, -130f * lg), new KF(0.30f, -130f * lg, 0f), new KF(0.85f, 0f, 0f) };
        KF[] kD = { new KF(0f, 0f, 0f), new KF(0.065f, 70f * cr, 0f), new KF(0.12f, 115f * cr), new KF(0.21f, 130f * cr), new KF(0.30f, 120f * cr, 0f), new KF(0.85f, 0f, 0f) };
        KF[] kFootF = { new KF(0f, 0f, 0f), new KF(0.065f, -60f * cr, 0f), new KF(0.12f, -170f * cr), new KF(0.21f, -260f * cr), new KF(0.30f, -260f * cr, 0f), new KF(0.85f, 0f, 0f) };
        KF[] kFootB = { new KF(0f, 0f, 0f), new KF(0.065f, 50f * cr, 0f), new KF(0.12f, 120f * cr), new KF(0.21f, 170f * cr), new KF(0.30f, 170f * cr, 0f), new KF(0.85f, 0f, 0f) };

        float maxLegDev = 0f;
        for (int i = 0; i < NF; i++)
        {
            float t = i * dt;
            Arr("armR1")[0][i] = Herm(kR1, t); Arr("armR2")[0][i] = Herm(kR2, t); Arr("grip")[0][i] = Herm(kG, t);
            float ch = Herm(kChest, t);
            Arr("chest")[0][i] += ch; Arr("belly")[0][i] += ch * 0.5f; Arr("head")[0][i] -= ch * 0.4f;
            float hx = Herm(kHipX, t), d = Herm(kD, t), fF = Herm(kFootF, t), fB = Herm(kFootB, t);
            Arr("hip")[1][i] += hx; Arr("hip")[2][i] += -d;
            float rR, sR, rL, sL;
            SolveLegXY("legR", "footR", hx, d, fF, out rR, out sR);
            SolveLegXY("legL", "footL", hx, d, fB, out rL, out sL);
            Arr("legR")[0][i] = rR; Arr("legR")[4][i] = sR; Arr("legL")[0][i] = rL; Arr("legL")[4][i] = sL;
            Arr("footR")[1][i] = fF; Arr("footL")[1][i] = fB;
            maxLegDev = Mathf.Max(maxLegDev, Mathf.Abs(1f - sR), Mathf.Abs(1f - sL));
        }
        rep.AppendLine("bacak boy sıkışması en çok " + (maxLegDev * 100f).ToString("0.0", inv) + "%");

        // ---- saç / haori / kuyruk gecikmesi
        {
            string[] sec = { "pony1", "pony2", "pony3", "fringe1", "fringe2", "fringe3", "hB1", "hB2", "hB3", "hR1", "hR2", "hR3", "hL1", "hL2", "hL3" };
            float[] gain = { 1.4f, 2.0f, 2.8f, 0.6f, 0.9f, 1.2f, 0.8f, 1.1f, 1.5f, 0.8f, 1.1f, 1.5f, 0.8f, 1.1f, 1.5f };
            float lp = 0f;
            for (int i = 0; i < NF; i++)
            {
                float sig = RotZ(World("chest", i)) + Val("hip", 1, i) * 0.02f;
                lp = i == 0 ? sig : lp + 0.4f * (sig - lp);
                float w = Ramp(i * dt, 0f, 0.03f) * (1f - Ramp(i * dt, 0.7f, 0.88f));
                for (int k = 0; k < sec.Length; k++) if (N.ContainsKey(sec[k])) Arr(sec[k])[0][i] += gain[k] * (lp - sig) * w;
            }
        }

        // ---- sağ kolluk: kökü omuzda sabit, dirseğe doğru bakar; hareketsizken orijinal duruş
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
            float w = Ramp(t, 0.005f, 0.05f) * (1f - Ramp(t, 0.72f, 0.88f));
            float follow = baseSlR1 + (r1 - baseR1);
            Arr("slR1")[0][i] = follow + Wrap(rF - follow) * w;
            Arr("slR1")[1][i] = armOff.x * w; Arr("slR1")[2][i] = armOff.y * w;
            Arr("slR2")[0][i] = baseSlR2 + 0.085f * (r2 - baseR2);
        }

        // ---- sol kol: kınayı tutar (IK)
        {
            Vector2 e0 = restLocal["armL2"], q0 = restAbs["armL_end"] - restAbs["armL2"];
            Vector3 Qrel3 = restAbs["armL_end"] - restAbs["katana"];
            float R10, R20;
            IK2(restAbs["armL1"], restAbs["armL_end"], e0, q0, 0f, out R10, out R20);
            var oldL1 = (float[])Arr("armL1")[0].Clone(); var oldL2 = (float[])Arr("armL2")[0].Clone();
            var oldS1 = (float[])Arr("slL1")[0].Clone(); var oldS2 = (float[])Arr("slL2")[0].Clone();
            float maxD1 = 0f, maxD2 = 0f;
            for (int i = 0; i < NF; i++)
            {
                var cw = World("chest", i); float rhoC = RotZ(cw);
                Vector3 P1 = cw.MultiplyPoint3x4(restLocal["armL1"]);
                Vector3 T = World("katana", i).MultiplyPoint3x4(Qrel3);
                float R1, R2;
                IK2(new Vector2(P1.x, P1.y), new Vector2(T.x, T.y), e0, q0, rhoC, out R1, out R2);
                float wHold = Mathf.Min(SS(i / 6f), SS((NF - 1 - i) / 8f));
                float d1 = Mathf.Clamp(Wrap(R1 - R10) * wHold, -25f, 25f), d2 = Wrap(R2 - R20) * wHold;
                maxD1 = Mathf.Max(maxD1, Mathf.Abs(d1)); maxD2 = Mathf.Max(maxD2, Mathf.Abs(d2));
                float nR1 = oldL1[i] + d1, nR2 = oldL2[i] + d2, nS1 = oldS1[i] + d1;
                Arr("armL1")[0][i] = nR1; Arr("armL2")[0][i] = nR2;
                Arr("slL1")[0][i] = nS1; Arr("slL2")[0][i] = oldS2[i] + 0.35f * d2;
                Vector2 pNew = SleeveOffset(cw, rhoC, "armL1", "armL2", "slL1", "slL2", nR1, nS1);
                Vector2 pOld = SleeveOffset(cw, rhoC, "armL1", "armL2", "slL1", "slL2", oldL1[i], oldS1[i]);
                Arr("slL1")[1][i] += pNew.x - pOld.x; Arr("slL1")[2][i] += pNew.y - pOld.y;
            }
            rep.AppendLine("sol kol IK: omuz en çok " + maxD1.ToString("0.0", inv) + "°, dirsek en çok " + maxD2.ToString("0.0", inv) + "°");
        }

        // ---- efekt bayrakları
        for (int i = 0; i < NF; i++)
        {
            float t = i * dt;
            Arr("fxFlags")[3][i] = (t >= 0.08f && t <= 0.72f) ? 1f : 0f;     // el + kabza gövdenin önünde
            Arr("fxFlags")[4][i] = 0f;
            Arr("fxMouth")[3][i] = 0f;
            Arr("fxTip")[3][i] = 0f;
            Arr("fxTip")[4][i] = 0.9f * Ramp(t, 0.07f, 0.1f) * (1f - Ramp(t, 0.19f, 0.26f));   // dönüş izi (smear): kesim + yukarı yay
            Arr("fxGlint")[3][i] = (t >= 0.11f && t <= 0.13f) ? 0.9f : 0f;
            Arr("fxGlint")[4][i] = 1f;
            Arr("kblade")[3][i] = 1f; Arr("kblade")[4][i] = 1f;
        }

        // ---- yaz
        var sb = new StringBuilder();
        sb.Append("{\"name\":\"attack1\",\"duration\":").Append(F(duration)).Append(",\"frames\":").Append(NF).Append(",\"loop\":false,\"bones\":[");
        bool firstB = true;
        foreach (var name in order)
        {
            if (!firstB) sb.Append(','); firstB = false;
            var a = N[name];
            sb.Append("{\"name\":\"").Append(name).Append('"');
            for (int c = 0; c < 5; c++)
            {
                sb.Append(",\"").Append(Ch[c]).Append("\":[");
                for (int i = 0; i < NF; i++) { if (i > 0) sb.Append(','); sb.Append(F(a[c][i])); }
                sb.Append(']');
            }
            sb.Append('}');
        }
        sb.Append("]}");

        string cur = File.ReadAllText(animsFull);
        int blockIdx = cur.IndexOf("{\"name\":\"block\",\"duration\"", StringComparison.Ordinal);
        if (blockIdx < 0) return "HATA: block klibi bulunamadı.";
        int tenkaIdx = cur.IndexOf("{\"name\":\"tenka\",\"duration\"", StringComparison.Ordinal);
        int a1Idx = cur.IndexOf("{\"name\":\"attack1\",\"duration\"", Math.Max(0, tenkaIdx), StringComparison.Ordinal);
        string result;
        if (a1Idx > tenkaIdx && a1Idx < blockIdx) result = cur.Substring(0, a1Idx) + sb + "," + cur.Substring(blockIdx);
        else result = cur.Substring(0, blockIdx) + sb + "," + cur.Substring(blockIdx);
        rep.AppendLine(string.Format(inv, "attack1: kare={0} süre={1:0.000}s, dosya {2} -> {3}", NF, duration, cur.Length, result.Length));
        if (write)
        {
            File.WriteAllText(animsFull, result, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(AnimsPath, ImportAssetOptions.ForceUpdate);
            rep.AppendLine("YAZILDI");
        }
        return rep.ToString();
    }
}
#endif
