using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KOŞULAR ARASI İLERLEME (kalıcı). PlayerPrefs'e JSON olarak kaydedilir.
///
/// Stat grind YOK: ilerleme ÇEŞİTLİLİK açar.
///  - Charm kilitleri: bazı charm'lar bir hedefe ulaşınca havuza girer
///    (CharmDefinition.unlockId).
///  - Zorluk (Isı) kademeleri: N. kademede kazanınca N+1 açılır.
///
/// Sıfırlamak için: Unity menüsü yok; ResetAll() çağır ya da PlayerPrefs'ten
/// "projectm_meta_v1" anahtarını sil.
/// </summary>
public static class MetaProgress
{
    private const string Key = "projectm_meta_v1";

    public const int MaxHeat = 5;

    // ---------------------------------------------------------
    // Kilit kimlikleri ve koşulları
    // ---------------------------------------------------------

    public const string UnlockAct2 = "act2";        // 2. perdeye ulaş
    public const string UnlockAct3 = "act3";        // 3. perdeye ulaş
    public const string UnlockWin = "win1";         // ilk zafer
    public const string UnlockExecute = "exec50";   // toplam 50 execute
    public const string UnlockParry = "parry150";   // toplam 150 parry

    // Arayüz için: kilidin nasıl açıldığı.
    public static string DescribeUnlock(string id)
    {
        switch (id)
        {
            case UnlockAct2: return "2. perdeye ulaş";
            case UnlockAct3: return "3. perdeye ulaş";
            case UnlockWin: return "Bir koşuyu kazan";
            case UnlockExecute: return "Toplam 50 execute";
            case UnlockParry: return "Toplam 150 parry";
            default: return "";
        }
    }

    [Serializable]
    private class Data
    {
        public int runs;
        public int wins;
        public int bestAct;
        public int totalExecutes;
        public int totalParries;
        public int highestHeatWon = -1;
        public int selectedHeat;
        public List<string> unlocked = new List<string>();
    }

    private static Data data;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration
    )]
    private static void ResetStatics()
    {
        data = null;
    }

    private static Data D
    {
        get
        {
            if (data == null)
                Load();

            return data;
        }
    }

    // ---------------------------------------------------------
    // Okuma
    // ---------------------------------------------------------

    public static int Runs => D.runs;
    public static int Wins => D.wins;
    public static int BestAct => D.bestAct;
    public static int TotalExecutes => D.totalExecutes;
    public static int TotalParries => D.totalParries;

    // Seçilebilecek en yüksek zorluk: N'de kazanınca N+1 açılır.
    public static int HeatUnlocked =>
        Mathf.Clamp(D.highestHeatWon + 1, 0, MaxHeat);

    public static int SelectedHeat
    {
        get => Mathf.Clamp(D.selectedHeat, 0, HeatUnlocked);
        set
        {
            D.selectedHeat = Mathf.Clamp(value, 0, HeatUnlocked);
            Save();
        }
    }

    public static bool IsUnlocked(string id)
    {
        return string.IsNullOrEmpty(id) || D.unlocked.Contains(id);
    }

    // ---------------------------------------------------------
    // Koşu sonu
    // ---------------------------------------------------------

    /// <summary>
    /// Koşu bitince çağrılır. Yeni açılan kilit kimliklerini döndürür.
    /// </summary>
    public static List<string> RecordRun(
        int actReached,
        bool won,
        int heat,
        int executes,
        int parries
    )
    {
        Data d = D;

        d.runs++;
        d.bestAct = Mathf.Max(d.bestAct, actReached);
        d.totalExecutes += Mathf.Max(0, executes);
        d.totalParries += Mathf.Max(0, parries);

        if (won)
        {
            d.wins++;
            d.highestHeatWon = Mathf.Max(d.highestHeatWon, heat);
        }

        List<string> newly = new List<string>();

        TryUnlock(UnlockAct2, d.bestAct >= 2, newly);
        TryUnlock(UnlockAct3, d.bestAct >= 3, newly);
        TryUnlock(UnlockWin, d.wins >= 1, newly);
        TryUnlock(UnlockExecute, d.totalExecutes >= 50, newly);
        TryUnlock(UnlockParry, d.totalParries >= 150, newly);

        Save();

        return newly;
    }

    private static void TryUnlock(string id, bool condition, List<string> newly)
    {
        if (!condition || D.unlocked.Contains(id))
            return;

        D.unlocked.Add(id);
        newly.Add(id);
    }

    // ---------------------------------------------------------
    // Kayıt
    // ---------------------------------------------------------

    private static void Load()
    {
        string json = PlayerPrefs.GetString(Key, "");

        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                data = JsonUtility.FromJson<Data>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("MetaProgress: kayıt okunamadı → " + e.Message);
                data = null;
            }
        }

        if (data == null)
            data = new Data();

        if (data.unlocked == null)
            data.unlocked = new List<string>();
    }

    private static void Save()
    {
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(D));
        PlayerPrefs.Save();
    }

    public static void ResetAll()
    {
        data = new Data();
        Save();
    }
}
