using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// BOSS DENEME İSTATİSTİĞİ: hangi saldırı kaç kez vurdu, ne kadar hasar verdi,
/// kaç kez öldürdü. Oyuncu ölünce ya da boss ölünce konsola özet yazılır.
/// Dengeleme için: hiç vurmayan saldırı çok kolay, hep öldüren çok zordur.
/// </summary>
public static class BossStats
{
    private class Entry
    {
        public int hits;
        public int damage;
        public int kills;
    }

    private static readonly Dictionary<string, Entry> entries =
        new Dictionary<string, Entry>();

    public static int postureBreaks;
    public static int reflectedHits;
    public static float phase2At = -1f;

    // Oyuncu davranışı (sadece boss'a karşı).
    public static int parries;
    public static int dodges;
    public static readonly int[] executes = new int[4]; // [1..3] = harcanan parça

    public static int finalBlows;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Boot()
    {
        Reset();

    }

    private static bool IsBoss(EnemyController e)
    {
        return e != null && e.GetComponent<BossController>() != null;
    }

    public static void RecordExecute(int segs)
    {
        executes[Mathf.Clamp(segs, 1, 3)]++;
    }

    public static void Reset()
    {
        entries.Clear();
        postureBreaks = 0;
        reflectedHits = 0;
        phase2At = -1f;
        parries = 0;
        dodges = 0;
        Array.Clear(executes, 0, executes.Length);
        finalBlows = 0;
    }

    public static void Record(string skill, int damage, bool lethal)
    {
        if (string.IsNullOrEmpty(skill))
            skill = "?";

        if (!entries.TryGetValue(skill, out Entry e))
        {
            e = new Entry();
            entries[skill] = e;
        }

        e.hits++;
        e.damage += damage;

        if (lethal)
            e.kills++;
    }

    public static void Print(string title, float fightSeconds)
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("===== BOSS ÖZETİ: " + title + " =====");

        sb.AppendLine(
            "Süre: " + Mathf.RoundToInt(fightSeconds) + " sn" +
            (phase2At >= 0f ? "  (faz 2: " + Mathf.RoundToInt(phase2At) + ". sn)" : "") +
            "   Denge kırma: " + postureBreaks +
            "   Yansıtılan ok: " + reflectedHits
        );

        sb.AppendLine(
            "Parry: " + parries + "   Kusursuz kaçış: " + dodges +
            "   Ölümcül vuruş: " + finalBlows
        );

        BossController bc = BossController.Current;

        if (bc != null && bc.Health != null)
        {
            sb.AppendLine(
                "Boss canı (bitişte): %" + Mathf.RoundToInt(bc.HealthPercent * 100f) +
                "   Denge: %" + Mathf.RoundToInt(bc.BalancePercent * 100f) +
                (bc.InPhase2 ? "   (faz 2)" : "")
            );
        }

        if (entries.Count == 0)
            sb.AppendLine("Oyuncu hiç hasar almadı.");

        foreach (KeyValuePair<string, Entry> kv in entries)
        {
            sb.AppendLine(
                "  " + kv.Key + ": " + kv.Value.hits + " isabet, " +
                kv.Value.damage + " hasar, " + kv.Value.kills + " öldürme"
            );
        }

        Debug.Log(sb.ToString());

        WriteToFile(sb.ToString());
    }

    // Özeti dosyaya ekle: Assets/Scripts/BossLog.txt (editörde), yoksa persistentDataPath.
    private static void WriteToFile(string text)
    {
        try
        {
            string path =
                Application.isEditor
                    ? Path.Combine(Application.dataPath, "Scripts", "BossLog.txt")
                    : Path.Combine(Application.persistentDataPath, "BossLog.txt");

            File.AppendAllText(
                path,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n" + text + "\n"
            );
        }
        catch (Exception ex)
        {
            Debug.LogWarning("BossLog yazılamadı: " + ex.Message);
        }
    }
}
