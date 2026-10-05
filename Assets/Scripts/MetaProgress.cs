using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KOŞULAR ARASI İLERLEME (kalıcı). PlayerPrefs'e JSON olarak kaydedilir.
///
/// İlerleme:
///  - Charm kilitleri: bazı charm'lar bir hedefe ulaşınca havuza girer
///    (CharmDefinition.unlockId).
///  - Zorluk (Isı) kademeleri: N. kademede kazanınca N+1 açılır.
///  - ÖZ: koşu sonunda kazanılır; ana menüde DEMİRCİ'den kalıcı yükseltme
///    ve yetenek kilidi alınır (MetaUpgrades listesi).
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

        // --- Kalıcı gelişim (Öz) ---
        public int essence;
        public int totalEssence;
        public List<string> upgradeIds = new List<string>();
        public List<int> upgradeLevels = new List<int>();
        public int selectedAbility = -1;
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

    // ---------------------------------------------------------
    // ÖZ ve KALICI GELİŞİM (Demirci)
    // ---------------------------------------------------------

    public static int Essence => D.essence;
    public static int TotalEssence => D.totalEssence;

    public static int UpgradeLevel(string id)
    {
        int i = D.upgradeIds.IndexOf(id);

        return i >= 0 && i < D.upgradeLevels.Count ? D.upgradeLevels[i] : 0;
    }

    /// <summary>Bir sonraki seviyenin bedeli; en üst seviyedeyse -1.</summary>
    public static int NextCost(MetaUpgrade upgrade)
    {
        int level = UpgradeLevel(upgrade.id);

        if (level >= upgrade.costs.Length)
            return -1;

        return upgrade.costs[level];
    }

    public static bool TryBuy(MetaUpgrade upgrade)
    {
        int cost = NextCost(upgrade);

        if (cost < 0 || D.essence < cost)
            return false;

        if (!string.IsNullOrEmpty(upgrade.requires) && UpgradeLevel(upgrade.requires) <= 0)
            return false;

        D.essence -= cost;

        int i = D.upgradeIds.IndexOf(upgrade.id);

        if (i < 0)
        {
            D.upgradeIds.Add(upgrade.id);
            D.upgradeLevels.Add(1);
        }
        else
        {
            while (D.upgradeLevels.Count <= i)
                D.upgradeLevels.Add(0);

            D.upgradeLevels[i]++;
        }

        Save();

        return true;
    }

    public static void AddEssence(int amount)
    {
        if (amount <= 0)
            return;

        D.essence += amount;
        D.totalEssence += amount;

        Save();
    }

    /// <summary>Kilitli yeteneğin Demirci bedeli (kilit yoksa / bulunamazsa -1).</summary>
    public static int AbilityUnlockCost(AbilityType type)
    {
        MetaUpgrade[] all = MetaUpgrades.All;

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].isAbility && all[i].ability == type)
                return NextCost(all[i]);
        }

        return -1;
    }

    public static bool IsAbilityUnlocked(AbilityType type)
    {
        string id = AbilityInfo.UnlockId(type);

        return string.IsNullOrEmpty(id) || UpgradeLevel(id) > 0;
    }

    /// <summary>Menüde seçilen başlangıç yeteneği (-1 = seçilmedi).</summary>
    public static int SelectedAbility
    {
        get => D.selectedAbility;
        set
        {
            D.selectedAbility = value;
            Save();
        }
    }

    // Değerler (yükseltme seviyelerinden).
    public static int BonusMaxHealth => UpgradeLevel(MetaUpgrades.Health) * 10;
    public static float BonusPostHeal => UpgradeLevel(MetaUpgrades.PostHeal) * 0.03f;
    public static float ExecuteFillMultiplier => 1f + UpgradeLevel(MetaUpgrades.ExecuteFill) * 0.15f;
    public static float AbilityCooldownMultiplier => 1f - UpgradeLevel(MetaUpgrades.AbilityCooldown) * 0.08f;
    public static float ShopPriceMultiplier => 1f - UpgradeLevel(MetaUpgrades.ShopDiscount) * 0.08f;
    public static int StartGold => UpgradeLevel(MetaUpgrades.StartGold) * 25;
    public static float EssenceMultiplier => 1f + UpgradeLevel(MetaUpgrades.EssenceGain) * 0.1f;

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

        if (data.upgradeIds == null)
            data.upgradeIds = new List<string>();

        if (data.upgradeLevels == null)
            data.upgradeLevels = new List<int>();
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

/// <summary>Demirci'nin kalıcı yükseltmesi.</summary>
public class MetaUpgrade
{
    public string id;
    public string name;
    public string description;   // {0} = seviye başı değer metni
    public int[] costs;          // seviye başına bedel (uzunluk = max seviye)
    public string requires;      // önce alınması gereken (boş = yok)
    public bool isAbility;
    public AbilityType ability;
}

/// <summary>Kalıcı yükseltme listesi (sıra = menü sırası).</summary>
public static class MetaUpgrades
{
    public const string Health = "hp";
    public const string PostHeal = "postheal";
    public const string ExecuteFill = "execfill";
    public const string AbilityCooldown = "abilitycd";
    public const string ShopDiscount = "shop";
    public const string StartGold = "gold";
    public const string EssenceGain = "essence";

    public static readonly MetaUpgrade[] All =
    {
        new MetaUpgrade
        {
            id = Health, name = "Dayanıklılık",
            description = "Koşuya +10 max can ile başla (seviye başı).",
            costs = new[] { 30, 50, 80, 120, 170 }
        },
        new MetaUpgrade
        {
            id = PostHeal, name = "Nefeslenme",
            description = "Nöbet grubu temizlenince +%3 ek iyileşme (seviye başı).",
            costs = new[] { 40, 70, 110 }
        },
        new MetaUpgrade
        {
            id = ExecuteFill, name = "Cellat",
            description = "İnfaz barı %15 daha hızlı dolar (seviye başı).",
            costs = new[] { 40, 70, 110 }
        },
        new MetaUpgrade
        {
            id = AbilityCooldown, name = "Odaklanma",
            description = "Yetenek bekleme süresi −%8 (seviye başı).",
            costs = new[] { 50, 80, 120 }
        },
        new MetaUpgrade
        {
            id = ShopDiscount, name = "Pazarlık",
            description = "Dükkan fiyatları −%8 (seviye başı).",
            costs = new[] { 35, 60, 90 }
        },
        new MetaUpgrade
        {
            id = StartGold, name = "Kese",
            description = "Koşuya +25 altınla başla (seviye başı).",
            costs = new[] { 25, 45, 70 }
        },
        new MetaUpgrade
        {
            id = EssenceGain, name = "Öz Toplayıcı",
            description = "Koşu sonunda +%10 öz (seviye başı).",
            costs = new[] { 60, 100, 150 }
        },
        new MetaUpgrade
        {
            id = AbilityInfo.UnlockId(AbilityType.Frost), name = "Yetenek: Buz Nefesi",
            description = "Düşmanların zamanı yavaşlar, öndekiler donar.",
            costs = new[] { 90 }, isAbility = true, ability = AbilityType.Frost
        },
        new MetaUpgrade
        {
            id = AbilityInfo.UnlockId(AbilityType.Fire), name = "Yetenek: Alev Dalgası",
            description = "Öndeki düşmanları yakar; sersemlemiş düşmana çift yanık.",
            costs = new[] { 90 }, isAbility = true, ability = AbilityType.Fire
        }
    };
}
