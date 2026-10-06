using System.Collections.Generic;
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Boot()
    {
        Reset();
    }

    public static void Reset()
    {
        entries.Clear();
        postureBreaks = 0;
        reflectedHits = 0;
        phase2At = -1f;
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
    }
}
