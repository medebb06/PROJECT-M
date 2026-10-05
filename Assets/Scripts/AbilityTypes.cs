using UnityEngine;

// Yetenek tipleri. YENİ tip eklenecekse SONUNA ekle (kayıtlarda sayı tutuluyor).
public enum AbilityType
{
    Shockwave,   // Şok Dalgası: çevredeki herkese denge + itme (sürülere karşı)
    ShadowStep,  // Gölge Adım: en yakın düşmanın arkasına ışınlan + denge vuruşu
    Frost,       // Buz Nefesi: düşman zamanı yavaşlar, öndekiler kesilir + denge
    Fire         // Alev Dalgası: öndekileri yakar (sersemlemişe çift)
}

/// <summary>
/// Yetenek bilgileri (ad, açıklama, renk, bekleme süresi, kilit kimliği).
/// Sayısal güç PlayerAbility'de; burada sadece tanım.
/// </summary>
public static class AbilityInfo
{
    public const int MaxLevel = 3;

    public static readonly AbilityType[] All =
    {
        AbilityType.Shockwave,
        AbilityType.ShadowStep,
        AbilityType.Frost,
        AbilityType.Fire
    };

    public static string Name(AbilityType type)
    {
        switch (type)
        {
            case AbilityType.Shockwave: return "Şok Dalgası";
            case AbilityType.ShadowStep: return "Gölge Adım";
            case AbilityType.Frost: return "Buz Nefesi";
            case AbilityType.Fire: return "Alev Dalgası";
            default: return type.ToString();
        }
    }

    // Kısa ikon harfi (HUD yuvası).
    public static string Glyph(AbilityType type)
    {
        switch (type)
        {
            case AbilityType.Shockwave: return "ŞOK";
            case AbilityType.ShadowStep: return "GÖL";
            case AbilityType.Frost: return "BUZ";
            case AbilityType.Fire: return "ALV";
            default: return "?";
        }
    }

    public static string Description(AbilityType type)
    {
        switch (type)
        {
            case AbilityType.Shockwave:
                return "Çevredeki TÜM düşmanlara denge hasarı + geri itme. Sürülere karşı.";
            case AbilityType.ShadowStep:
                return "En yakın düşmanın ARKASINA ışınlan + güçlü denge vuruşu. Kısa korunma.";
            case AbilityType.Frost:
                return "Düşmanların ZAMANI yavaşlar; öndekiler kesilir ve denge kaybeder.";
            case AbilityType.Fire:
                return "Öndeki düşmanları yakar (denge, sonra can). Sersemlemiş düşmana çift yanık.";
            default:
                return "";
        }
    }

    public static string LevelBonusText(AbilityType type)
    {
        return "Seviye başı: +%25 güç, −%10 bekleme";
    }

    public static Color Color(AbilityType type)
    {
        switch (type)
        {
            case AbilityType.Shockwave: return new Color(0.75f, 0.85f, 1f);
            case AbilityType.ShadowStep: return new Color(0.7f, 0.45f, 1f);
            case AbilityType.Frost: return new Color(0.45f, 0.9f, 1f);
            case AbilityType.Fire: return new Color(1f, 0.55f, 0.2f);
            default: return UnityEngine.Color.white;
        }
    }

    // Taban bekleme süresi (sn).
    public static float BaseCooldown(AbilityType type)
    {
        switch (type)
        {
            case AbilityType.Shockwave: return 10f;
            case AbilityType.ShadowStep: return 6f;
            case AbilityType.Frost: return 13f;
            case AbilityType.Fire: return 9f;
            default: return 10f;
        }
    }

    // Kalıcı gelişimde kilit kimliği. Şok Dalgası ve Gölge Adım baştan açık (boş).
    public static string UnlockId(AbilityType type)
    {
        switch (type)
        {
            case AbilityType.Frost: return "ab_frost";
            case AbilityType.Fire: return "ab_fire";
            default: return "";
        }
    }
}
