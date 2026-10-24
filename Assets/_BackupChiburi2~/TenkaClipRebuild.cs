#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// RonniIdleKatana 'tenka' (chiburi + noto) klibini yeniden üretir:
///   1) Dönüş bitince tek seferlik, yukarıdan aşağı yarım ay savurma (eski küçük 'silkeleme' yerine).
///   2) Hareket boyunca bacaklar hafif açılır + hafif çömelme (kalça iner, bacaklar dışa döner, ayaklar yerinde kalır).
///   3) Kına tarafındaki kol (sol kol) kınayı tutar: el, kınanın eğilmesini / gövde hareketini izler (2 kemikli IK).
/// Kaynak her zaman ORİJİNAL klip yedeğidir (_backup_before_tenka_slash), yani tekrar çalıştırmak üst üste binmez.
/// Menü: Tools > Ronni > Rebuild Tenka Clip (slash)
/// </summary>
public static partial class TenkaClipRebuild
{
    // ===== Ayarlar (kaynak piksel / derece) =====
    public static int slashFrames = 24;                 // araya eklenen yeni kare sayısı
    public static float crouchDepth = 55f;              // kalça inişi
    public static float stanceSpread = 135f;             // her ayağın dışa açılması
    public static float impactDepthExtra = 25f;         // vuruş anında ekstra çömelme
    public static float impactSpreadExtra = 55f;
    public static float sayaDrop = 90f;                  // kına eğilirken kına (ve kılıç kolu) bu kadar aşağı iner: sol kol daha az bükülür
    public static float handSlide = 0.25f;              // kına tam eğikken el, kınanın dibine doğru bu oranda kayar (omuz bükülmesini azaltır)
    public static float leftHandHold = 1f;              // 0..1 sol elin kınaya yapışma gücü
    // Savurma pozları: (armR1.r, armR2.r, grip.r) derece
    public static Vector3 windUp = new Vector3(-170f, -5f, 90f);   // kol + kılıç tek hat (G~95 = bıçak ön kolla aynı yönde)
    public static Vector2 shoulderPull = new Vector2(70f, 0f);   // savurma boyunca sağ omuz/kol/kılıç gövdeye doğru (geri, +x) çekilir (px)
    public static Vector3 windUp2 = new Vector3(-175f, -6f, 90f);
    public static Vector3 impact = new Vector3(-62f, 0f, 40f);   // G küçük = bıçak koldan daha dikey (ucu yukarıda), batırma yok
    public static Vector3 overshoot = new Vector3(-58f, 0f, 38f);
    public static float eyeSquint = 1f;               // çömelme boyunca göz kısma (0 = yok, 1 = tamamen kapalı)
    public static float eyeClosedScale = 0.93f;       // kapalı göz: 1 - bu kadar dikey ölçek kalır (ince çizgi)
    public static float eyeImpactClose = 0f;        // vuruş anında ekstra kapanma
    public static float leftShoulderClamp = 14f;       // sol (kına) kolunun omuz sapması üst sınırı (derece): büyükse omuz sivri çıkar
    public static float leftSleeveFollow = 0.4f;       // sol kolluk omuz dönüşünün bu kadarını izler (azalınca omuz köşesi çıkmaz)
    public static float spinLean = 16f;                 // dönüş (spin) sırasında öne eğilme (derece); savurmaya girerken geri yaslanmaya yumuşakça döner
    public static float cutPower = 1.7f;                // kesme ivmesi (büyük = daha geç hızlanır)

    const string AnimsPath = "Assets/Sprites/RonniSkin/Resources/RonniIdleKatana/ronni_anims.json";
    const string RigPath = "Assets/Sprites/RonniSkin/Resources/RonniIdleKatana/ronni_rig.json";
    const string BackupDir = "_backup_before_tenka_slash";
    const string BackupAnims = "ronni_anims.IdleKatana.json";

    [Serializable] class Track { public string name; public float[] r; public float[] x; public float[] y; public float[] sx; public float[] sy; }
    [Serializable] class ClipD { public string name; public float duration; public int frames; public bool loop; public Track[] bones; }
    [Serializable] class FileD { public ClipD[] clips; }
    [Serializable] class BoneD { public string name; public string parent; public float x; public float y; }
    [Serializable] class RigD { public BoneD[] bones; }

    static readonly CultureInfo inv = CultureInfo.InvariantCulture;
    static readonly string[] Ch = { "r", "x", "y", "sx", "sy" };

    // ---- çalışma verisi
    static Dictionary<string, float[][]> N;             // yeni kanallar [bone][ch][frame]
    static List<string> order;
    static Dictionary<string, Vector2> restLocal;       // y-yukarı, ebeveyne göre
    static Dictionary<string, Vector2> restAbs;         // y-yukarı, mutlak
    static Dictionary<string, string> parentOf;
    static int NF;

    [MenuItem("Tools/Ronni/Rebuild Tenka Clip (slash)")]
    public static void Menu() { Debug.Log(Run(true)); }

    static float SS(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
    static Vector2 Rot(Vector2 v, float deg) { float a = deg * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a); return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c); }
    static float Wrap(float a) { a = Mathf.Repeat(a + 180f, 360f) - 180f; return a; }
    static string F(float v) { if (Mathf.Abs(v) < 0.00005f) v = 0f; return v.ToString("0.####", inv); }

    static float Old(Track t, int ch, int k)
    {
        float[] a = ch == 0 ? t.r : ch == 1 ? t.x : ch == 2 ? t.y : ch == 3 ? t.sx : t.sy;
        if (a == null || a.Length == 0) return ch >= 3 ? 1f : 0f;
        return a[Mathf.Clamp(k, 0, a.Length - 1)];
    }

    static float[][] Arr(string bone)
    {
        float[][] a;
        if (!N.TryGetValue(bone, out a))
        {
            a = new float[5][];
            for (int c = 0; c < 5; c++) { a[c] = new float[NF]; if (c >= 3) for (int i = 0; i < NF; i++) a[c][i] = 1f; }
            N[bone] = a; order.Add(bone);
        }
        return a;
    }

    static float Val(string bone, int ch, int i)
    {
        float[][] a;
        if (!N.TryGetValue(bone, out a)) return ch >= 3 ? 1f : 0f;
        return a[ch][i];
    }

    static Matrix4x4 LocalM(string b, int i)
    {
        Vector2 rl = restLocal[b];
        return Matrix4x4.TRS(new Vector3(rl.x + Val(b, 1, i), rl.y + Val(b, 2, i), 0f), Quaternion.Euler(0f, 0f, Val(b, 0, i)), new Vector3(Val(b, 3, i), Val(b, 4, i), 1f));
    }

    static Matrix4x4 World(string b, int i)
    {
        var m = LocalM(b, i);
        string p = parentOf[b];
        return string.IsNullOrEmpty(p) ? m : World(p, i) * m;
    }

    static float RotZ(Matrix4x4 m) { return Mathf.Atan2(m.m10, m.m00) * Mathf.Rad2Deg; }

    // iki kemikli IK: omuz P1, hedef T; üst kol vektörü e0, ön kol vektörü q0 (dinlenme), ebeveyn dönüşü rhoC. Dirsek sağa (dışa) bükülür.
    static void IK2(Vector2 P1, Vector2 T, Vector2 e0, Vector2 q0, float rhoC, out float R1, out float R2)
    {
        float l1 = e0.magnitude, l2 = q0.magnitude;
        Vector2 d = T - P1;
        float dist = Mathf.Clamp(d.magnitude, Mathf.Abs(l1 - l2) + 1f, (l1 + l2) * 0.9995f);
        float phi = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        float g = Mathf.Acos(Mathf.Clamp((l1 * l1 + dist * dist - l2 * l2) / (2f * l1 * dist), -1f, 1f)) * Mathf.Rad2Deg;
        float a1 = phi + g, a2 = phi - g;
        Vector2 E1 = P1 + Rot(Vector2.right * l1, a1), E2 = P1 + Rot(Vector2.right * l1, a2);
        float ang1 = E1.x >= E2.x ? a1 : a2;
        Vector2 E = P1 + Rot(Vector2.right * l1, ang1);
        Vector2 f = T - E;
        float ang2 = Mathf.Atan2(f.y, f.x) * Mathf.Rad2Deg;
        float e0a = Mathf.Atan2(e0.y, e0.x) * Mathf.Rad2Deg, q0a = Mathf.Atan2(q0.y, q0.x) * Mathf.Rad2Deg;
        R1 = Wrap(ang1 - e0a - rhoC);
        R2 = Wrap(ang2 - q0a - rhoC - R1);
    }

    // omuz kemiği (armX1) açısına göre kol kemeri (slX1) konum kayması: sleeve dirseği, kolun dirseğine rijit bağlı kalsın.
    static Vector2 SleeveOffset(Matrix4x4 chestW, float rhoC, string armShoulder, string armElbow, string slShoulder, string slElbow, float armR1, float slR1, Vector2 armOff = default(Vector2))
    {
        Vector2 e0 = restLocal[armElbow];
        Vector2 off = restAbs[slElbow] - restAbs[armElbow];
        Vector2 v12 = restAbs[slElbow] - restAbs[slShoulder];
        Vector3 P1 = chestW.MultiplyPoint3x4(restLocal[armShoulder]);
        Vector2 E = new Vector2(P1.x, P1.y) + Rot(armOff, rhoC) + Rot(e0, rhoC + armR1);
        Vector2 S2 = E + Rot(off, rhoC + armR1);
        Vector2 S1 = S2 - Rot(v12, rhoC + slR1);
        Vector3 loc = chestW.inverse.MultiplyPoint3x4(new Vector3(S1.x, S1.y, 0f));
        return new Vector2(loc.x - restLocal[slShoulder].x, loc.y - restLocal[slShoulder].y);
    }

    static void SolveLeg(string legB, string footB, float d, float sigma, int side, out float rotDeg, out float scaleY)
    {
        Vector2 P = restAbs[legB], A0 = restAbs[footB];
        Vector2 l0 = A0 - P;                       // (dx0, -L)
        float L = -l0.y, dx0 = l0.x;
        Vector2 Pn = new Vector2(P.x, P.y - d);
        Vector2 A = new Vector2(A0.x + side * sigma, A0.y);
        Vector2 v = A - Pn;
        float s = Mathf.Sqrt(Mathf.Max(1e-3f, v.sqrMagnitude - dx0 * dx0)) / L;
        float th = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg - Mathf.Atan2(-L * s, dx0) * Mathf.Rad2Deg;
        rotDeg = Wrap(th); scaleY = s;
    }

    static string ReadProject(string rel) { return File.ReadAllText(Path.GetFullPath(rel)); }

    public static string Run(bool write)
    {
        var rep = new StringBuilder();
        string root = Path.GetDirectoryName(Application.dataPath);
        string animsFull = Path.GetFullPath(AnimsPath);
        string backupFull = Path.Combine(root, BackupDir, BackupAnims);
        if (!File.Exists(backupFull))
        {
            Directory.CreateDirectory(Path.Combine(root, BackupDir));
            File.Copy(animsFull, backupFull);
            rep.AppendLine("yedek alındı: " + BackupDir + "/" + BackupAnims);
        }
        string curText = File.ReadAllText(animsFull);
        string srcText = File.ReadAllText(backupFull);

        // ---- kaynak (orijinal) tenka klibi
        var file = JsonUtility.FromJson<FileD>(srcText);
        ClipD oldClip = null;
        foreach (var c in file.clips) if (c.name == "tenka") oldClip = c;
        if (oldClip == null) return "HATA: yedekte 'tenka' klibi yok.";
        if (oldClip.frames != 90) return "HATA: kaynak tenka 90 kare değil (" + oldClip.frames + "); yedek bozuk olabilir.";

        // ---- rig (dinlenme iskeleti)
        var rig = JsonUtility.FromJson<RigD>(ReadProject(RigPath));
        restLocal = new Dictionary<string, Vector2>(); restAbs = new Dictionary<string, Vector2>(); parentOf = new Dictionary<string, string>();
        var absImg = new Dictionary<string, Vector2>();
        foreach (var b in rig.bones) { absImg[b.name] = new Vector2(b.x, b.y); parentOf[b.name] = b.parent ?? ""; }
        foreach (var b in rig.bones)
        {
            restAbs[b.name] = new Vector2(b.x, -b.y);
            restLocal[b.name] = string.IsNullOrEmpty(b.parent) ? new Vector2(b.x, -b.y) : new Vector2(b.x - absImg[b.parent].x, -(b.y - absImg[b.parent].y));
        }

        int S = slashFrames;
        NF = 37 + S + 42;
        float dt = 3.0f / 89f;
        float duration = (NF - 1) * dt;
        N = new Dictionary<string, float[][]>(); order = new List<string>();

        // ---- 1) taban: eski kareler + araya yumuşak geçiş
        foreach (var tr in oldClip.bones)
        {
            var a = Arr(tr.name);
            for (int i = 0; i < NF; i++)
                for (int c = 0; c < 5; c++)
                {
                    if (i <= 36) a[c][i] = Old(tr, c, i);
                    else if (i >= 37 + S) a[c][i] = Old(tr, c, 48 + (i - (37 + S)));
                    else { float t = SS((i - 36) / (float)(S + 1)); a[c][i] = Mathf.Lerp(Old(tr, c, 36), Old(tr, c, 48), t); }
                }
        }
        // yeni kemikler (bacaklar)
        foreach (var nb in new[] { "legR", "legL", "footR", "footL" }) Arr(nb);

        // ---- kural kontrolü: sağ kol kolluğu (slR1.x,y) eski karelerde konum kuralına uyuyor mu?
        {
            float maxErr = 0f;
            for (int i = 0; i < NF; i++)
            {
                if (i > 36 && i < 37 + S) continue;
                var cw = World("chest", i);
                Vector2 p = SleeveOffset(cw, RotZ(cw), "armR1", "armR2", "slR1", "slR2", Val("armR1", 0, i), Val("slR1", 0, i));
                maxErr = Mathf.Max(maxErr, Mathf.Abs(p.x - Val("slR1", 1, i)), Mathf.Abs(p.y - Val("slR1", 2, i)));
            }
            rep.AppendLine("kontrol: kolluk konum kuralı en büyük sapma = " + maxErr.ToString("0.0", inv) + " px (küçükse iskelet/eksen varsayımları doğru)");
        }

        // ---- 2) sağ kol savurması (armR1, armR2, grip)
        string[] ar = { "armR1", "armR2", "grip" };
        Vector3 p0 = new Vector3(Val("armR1", 0, 36), Val("armR2", 0, 36), Val("grip", 0, 36));
        Vector3 pE = new Vector3(Val("armR1", 0, 37 + S), Val("armR2", 0, 37 + S), Val("grip", 0, 37 + S));
        for (int s = 1; s <= S; s++)
        {
            int i = 36 + s;
            Vector3 pose;
            if (s <= 6) pose = Vector3.Lerp(p0, windUp, SS(s / 6f));
            else if (s <= 8) pose = Vector3.Lerp(windUp, windUp2, (s - 6) / 2f);
            else if (s <= 15) pose = Vector3.Lerp(windUp2, impact, Mathf.Pow((s - 8) / 7f, cutPower));
            else if (s <= 17) pose = Vector3.Lerp(impact, overshoot, SS((s - 15) / 2f));
            else pose = Vector3.Lerp(overshoot, pE, SS((s - 17) / (float)(S + 1 - 17)));
            for (int k = 0; k < 3; k++) Arr(ar[k])[0][i] = pose[k];
            float env = s <= 6 ? SS(s / 6f) : (s <= 17 ? 1f : 1f - SS((s - 17) / (float)(S + 1 - 17)));
            Arr("armR1")[1][i] = shoulderPull.x * env; Arr("armR1")[2][i] = shoulderPull.y * env;
        }

        // ---- 3a) dönüş (spin) boyunca öne eğilme
        for (int i = 1; i <= 36; i++)
        {
            float lean = spinLean * SS(i / 10f);
            Arr("chest")[0][i] += lean;
            Arr("belly")[0][i] += lean * 0.5f;
            Arr("head")[0][i] -= lean * 0.4f;
        }

        // ---- 3) gövde: geriye yaslanma -> vuruşta öne eğilme -> dinlenme
        for (int s = 1; s <= S; s++)
        {
            int i = 36 + s;
            float add;
            if (s <= 6) add = Mathf.Lerp(0f, -4f, SS(s / 6f));
            else if (s <= 8) add = -4f;
            else if (s <= 15) add = Mathf.Lerp(-4f, 7f, Mathf.Pow((s - 8) / 7f, cutPower));
            else if (s <= 18) add = Mathf.Lerp(7f, 6.5f, (s - 15) / 3f);
            else add = Mathf.Lerp(6.5f, 0f, SS((s - 18) / (float)(S + 1 - 18)));
            add += spinLean * (1f - SS(s / 6f));   // spin eğilmesi savurma hazırlığında yumuşakça çözülür
            Arr("chest")[0][i] += add;
            Arr("belly")[0][i] += add * 0.5f;
            Arr("head")[0][i] -= add * 0.4f;
        }

        // ---- 4) çömelme zarfı + bacaklar
        int relStart = 62, relEnd = 62 + 9;   // savurma bitince (ters tutuş başlarken) doğrulur; kına sokarken dik durur
        float[] eps = new float[NF], bump = new float[NF];
        for (int i = 0; i < NF; i++)
        {
            float e;
            if (i <= 12) e = SS(i / 12f);
            else if (i < relStart) e = 1f;
            else if (i < relEnd) e = 1f - SS((i - relStart) / (float)(relEnd - relStart));
            else e = 0f;
            eps[i] = e;
            float bc = 36 + 16.5f;                                  // vuruş anı (s ~ 16.5)
            bump[i] = SS(1f - Mathf.Abs(i - bc) / 6f) * e;
        }
        // gözler: hareket boyunca kısılır, vuruşta neredeyse kapanır (idle_gozA / idle_gozB ayrı göz parçaları, eyeA/eyeB kemiklerine bağlı; kaşlar idle_yuz'da kalır)
        for (int i = 0; i < NF; i++)
        {
            float closeAmt = Mathf.Clamp01(eyeSquint * eps[i] + eyeImpactClose * bump[i]);
            float sc = 1f - eyeClosedScale * closeAmt;   // gözler kapanır: göz sprite'ı (kaşlardan bağımsız) yukarıdan-aşağıdan bastırılır
            Arr("eyeA")[4][i] = sc; Arr("eyeB")[4][i] = sc;
        }
        float maxScaleDev = 0f, maxRot = 0f;
        for (int i = 0; i < NF; i++)
        {
            float d = crouchDepth * eps[i] + impactDepthExtra * bump[i];
            float sg = stanceSpread * eps[i] + impactSpreadExtra * bump[i];
            Arr("hip")[2][i] += -d;
            float rR, sR, rL, sL;
            SolveLeg("legR", "footR", d, sg, -1, out rR, out sR);
            SolveLeg("legL", "footL", d, sg, +1, out rL, out sL);
            Arr("legR")[0][i] = rR; Arr("legR")[4][i] = sR;
            Arr("legL")[0][i] = rL; Arr("legL")[4][i] = sL;
            Arr("footR")[1][i] = -sg; Arr("footL")[1][i] = sg;
            maxScaleDev = Mathf.Max(maxScaleDev, Mathf.Abs(1f - sR), Mathf.Abs(1f - sL));
            maxRot = Mathf.Max(maxRot, Mathf.Abs(rR), Mathf.Abs(rL));
        }
        rep.AppendLine("bacaklar: en büyük dönüş " + maxRot.ToString("0.0", inv) + "°, en büyük boy sıkışması " + (maxScaleDev * 100f).ToString("0.0", inv) + "%");

        // ---- 5) saç / haori gecikmesi (gövde dönüşünü geriden izler); sadece araya giren karelerde
        {
            string[] sec = { "pony1", "pony2", "pony3", "fringe1", "fringe2", "fringe3", "hB1", "hB2", "hB3", "hR1", "hR2", "hR3", "hL1", "hL2", "hL3" };
            float[] gain = { 1.2f, 1.8f, 2.4f, 0.5f, 0.8f, 1.0f, 0.6f, 0.9f, 1.2f, 0.6f, 0.9f, 1.2f, 0.6f, 0.9f, 1.2f };
            float lp = 0f;
            for (int i = 36; i <= 37 + S; i++)
            {
                float sig = RotZ(World("chest", i));
                if (i == 36) lp = sig; else lp = lp + 0.35f * (sig - lp);
                int s = i - 36;
                float w = s < 1 || s > S ? 0f : (s <= S - 6 ? 1f : 1f - SS((s - (S - 6)) / 6f));
                if (w <= 0f) continue;
                for (int k = 0; k < sec.Length; k++)
                    if (N.ContainsKey(sec[k])) Arr(sec[k])[0][i] += gain[k] * (lp - sig) * w;
            }
        }

        // ---- 6) sağ kol kolluğu (slR1/slR2) yeni karelerde kolu izler
        {
            float d36 = Val("slR1", 0, 36) - Val("armR1", 0, 36), d48 = Val("slR1", 0, 37 + S) - Val("armR1", 0, 37 + S);
            Func<float, float> f2 = r2 => 6f + 0.085f * r2;
            float c36 = Val("slR2", 0, 36) - f2(Val("armR2", 0, 36)), c48 = Val("slR2", 0, 37 + S) - f2(Val("armR2", 0, 37 + S));
            // uç düzeltmeleri için önce uç kareler için kural değerleri
            Vector2 a36, a48;
            {
                var cw = World("chest", 36); a36 = SleeveOffset(cw, RotZ(cw), "armR1", "armR2", "slR1", "slR2", Val("armR1", 0, 36), Val("slR1", 0, 36));
                int j = 37 + S; var cw2 = World("chest", j); a48 = SleeveOffset(cw2, RotZ(cw2), "armR1", "armR2", "slR1", "slR2", Val("armR1", 0, j), Val("slR1", 0, j));
            }
            Vector2 e36 = new Vector2(Val("slR1", 1, 36), Val("slR1", 2, 36)) - a36;
            Vector2 e48 = new Vector2(Val("slR1", 1, 37 + S), Val("slR1", 2, 37 + S)) - a48;
            for (int s = 1; s <= S; s++)
            {
                int i = 36 + s; float t = s / (float)(S + 1);
                float r1 = Val("armR1", 0, i);
                float slr1 = r1 + Mathf.Lerp(d36, d48, t);
                Arr("slR1")[0][i] = slr1;
                Arr("slR2")[0][i] = f2(Val("armR2", 0, i)) + Mathf.Lerp(c36, c48, t);
                var cw = World("chest", i);
                Vector2 p = SleeveOffset(cw, RotZ(cw), "armR1", "armR2", "slR1", "slR2", r1, slr1, new Vector2(Val("armR1", 1, i), Val("armR1", 2, i))) + Vector2.Lerp(e36, e48, t);
                // omuz boşluğu olmasın: savurma ortasında kolluğun KÖKÜ omuzda sabit kalır (kolla birlikte geri çekilir), dönüşü dirseğe doğru bakar
                float wF = s <= 5 ? SS(s / 5f) : (s <= 19 ? 1f : 1f - SS((s - 19) / (float)(S + 1 - 19)));
                {
                    float rho = RotZ(cw);
                    Vector2 armOff = new Vector2(Val("armR1", 1, i), Val("armR1", 2, i));
                    Vector3 P1 = cw.MultiplyPoint3x4(restLocal["armR1"]);
                    Vector2 E = new Vector2(P1.x, P1.y) + Rot(armOff, rho) + Rot(restLocal["armR2"], rho + r1);
                    Vector2 S2 = E + Rot(restAbs["slR2"] - restAbs["armR2"], rho + r1);
                    Vector3 S1w = cw.MultiplyPoint3x4(new Vector3(restLocal["slR1"].x + armOff.x, restLocal["slR1"].y + armOff.y, 0f));
                    Vector2 dir = S2 - new Vector2(S1w.x, S1w.y);
                    Vector2 v12 = restAbs["slR2"] - restAbs["slR1"];
                    float rF = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - Mathf.Atan2(v12.y, v12.x) * Mathf.Rad2Deg - rho;
                    p = Vector2.Lerp(p, armOff, wF);
                    Arr("slR1")[0][i] = slr1 + Wrap(rF - slr1) * wF;
                }
                Arr("slR1")[1][i] = p.x; Arr("slR1")[2][i] = p.y;
            }
        }

        // ---- 6b) kına eğildikçe aşağı iner; kılıç kolu + kolluk birlikte (ağız hizası bozulmasın)
        for (int i = 0; i < NF; i++)
        {
            float f = Mathf.Clamp01(Val("katana", 0, i) / 22f);
            Arr("katana")[2][i] += -sayaDrop * f; Arr("armR1")[2][i] += -sayaDrop * f; Arr("slR1")[2][i] += -sayaDrop * f;
        }

        // ---- 7) sol kol: kınayı tut (IK) + sol kolluk
        {
            Vector2 e0 = restLocal["armL2"], q0 = restAbs["armL_end"] - restAbs["armL2"];
            Vector3 Qrel3 = restAbs["armL_end"] - restAbs["katana"];
            float R10, R20;
            IK2(restAbs["armL1"], restAbs["armL_end"], e0, q0, 0f, out R10, out R20);
            float maxD1 = 0f, maxD2 = 0f;
            var oldL1 = (float[])Arr("armL1")[0].Clone(); var oldL2 = (float[])Arr("armL2")[0].Clone();
            var oldS1 = (float[])Arr("slL1")[0].Clone(); var oldS2 = (float[])Arr("slL2")[0].Clone();
            for (int i = 0; i < NF; i++)
            {
                var cw = World("chest", i); float rhoC = RotZ(cw);
                Vector3 P1 = cw.MultiplyPoint3x4(restLocal["armL1"]);
                float slide = 1f - handSlide * Mathf.Clamp01(Val("katana", 0, i) / 22f);
                Vector3 T = World("katana", i).MultiplyPoint3x4(Qrel3 * slide);
                float R1, R2;
                IK2(new Vector2(P1.x, P1.y), new Vector2(T.x, T.y), e0, q0, rhoC, out R1, out R2);
                float wHold = Mathf.Min(SS(i / 8f), SS((NF - 1 - i) / 10f));   // ilk/son karelerde idle ile birebir
                float d1 = Wrap(R1 - R10) * leftHandHold * wHold, d2 = Wrap(R2 - R20) * leftHandHold * wHold;
                d1 = Mathf.Clamp(d1, -leftShoulderClamp, leftShoulderClamp);
                maxD1 = Mathf.Max(maxD1, Mathf.Abs(d1)); maxD2 = Mathf.Max(maxD2, Mathf.Abs(d2));
                float nR1 = oldL1[i] + d1, nR2 = oldL2[i] + d2;
                Arr("armL1")[0][i] = nR1; Arr("armL2")[0][i] = nR2;
                float nS1 = oldS1[i] + d1 * leftSleeveFollow;
                Arr("slL1")[0][i] = nS1; Arr("slL2")[0][i] = oldS2[i] + 0.35f * d2;
                Vector2 pNew = SleeveOffset(cw, rhoC, "armL1", "armL2", "slL1", "slL2", nR1, nS1);
                Vector2 pOld = SleeveOffset(cw, rhoC, "armL1", "armL2", "slL1", "slL2", oldL1[i], oldS1[i]);
                Arr("slL1")[1][i] += pNew.x - pOld.x; Arr("slL1")[2][i] += pNew.y - pOld.y;
            }
            rep.AppendLine("sol kol IK: omuz sapması en çok " + maxD1.ToString("0.0", inv) + "°, dirsek sapması en çok " + maxD2.ToString("0.0", inv) + "°");
        }

        // ---- 8) efekt bayrakları (yeni kareler)
        {
            float[] fxTipSy = Arr("fxTip")[4], fxTipSx = Arr("fxTip")[3], gSx = Arr("fxGlint")[3], gSy = Arr("fxGlint")[4];
            for (int s = 1; s <= S; s++)
            {
                int i = 36 + s;
                float sm = 0f;
                if (s == 11) sm = 0.5f; else if (s >= 12 && s <= 16) sm = 0.85f; else if (s == 17) sm = 0.6f; else if (s == 18) sm = 0.3f;
                fxTipSy[i] = sm;
                fxTipSx[i] = (s == 16 || s == 17) ? 1f : 0f;
                gSx[i] = s == 14 ? 0.6f : s == 15 ? 1f : s == 16 ? 0.5f : 0f;
                gSy[i] = (s >= 10 && s <= 20) ? -1f : 1f;
            }
            // yeni karelerde sabit kalması gerekenler
            for (int s = 1; s <= S; s++)
            {
                int i = 36 + s;
                Arr("fxFlags")[3][i] = 1f; Arr("fxFlags")[4][i] = 0f; Arr("fxMouth")[3][i] = 0f;
                Arr("kblade")[3][i] = 1f; Arr("kblade")[4][i] = 1f;
            }
        }

        // ---- yaz
        var sb = new StringBuilder();
        sb.Append("{\"name\":\"tenka\",\"duration\":").Append(F(duration)).Append(",\"frames\":").Append(NF).Append(",\"loop\":false,\"bones\":[");
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

        // mevcut dosyada tenka klibinin yerini bul ve değiştir (diğer klipler aynen kalır)
        int s0 = curText.IndexOf("{\"name\":\"tenka\",\"duration\"", StringComparison.Ordinal);
        int e0i = curText.IndexOf("{\"name\":\"block\",\"duration\"", StringComparison.Ordinal);
        if (s0 >= 0) { foreach (System.Text.RegularExpressions.Match mm in System.Text.RegularExpressions.Regex.Matches(curText, "\\{\"name\":\"[A-Za-z0-9_]+\",\"duration\"")) if (mm.Index > s0 + 10) { if (mm.Index < e0i) e0i = mm.Index; break; } }   // tenka'dan sonra gelen ilk klip (attack1/unsheathe/guard_break araya girebilir)
        if (s0 < 0 || e0i < s0) return "HATA: mevcut dosyada tenka klibi bulunamadı.";
        int closeIdx = curText.LastIndexOf('}', e0i - 1);
        if (closeIdx < s0) return "HATA: tenka klip sonu bulunamadı.";
        string result = curText.Substring(0, s0) + sb.ToString() + curText.Substring(closeIdx + 1);

        float tb(float frame) { return frame * dt; }
        rep.AppendLine(string.Format(inv, "kare={0} süre={1:0.0000}s | sınırlar (klip sn): dönüş-sonu={2:0.0000} savurma-sonu={3:0.0000} ters-tutuş-sonu={4:0.0000} sokma-sonu={5:0.0000}",
            NF, duration, tb(36), tb(47.467f + S - 11), tb(57.85f + S - 11), tb(73.57f + S - 11)));
        rep.AppendLine("yeni dosya boyutu: " + result.Length + " (eski " + curText.Length + ")");

        if (write)
        {
            File.WriteAllText(animsFull, result, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(AnimsPath, ImportAssetOptions.ForceUpdate);
            rep.AppendLine("YAZILDI: " + AnimsPath);
        }
        else rep.AppendLine("(kuru çalışma: dosya yazılmadı)");
        return rep.ToString();
    }
}
#endif
