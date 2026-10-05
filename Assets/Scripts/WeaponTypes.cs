using UnityEngine;

// Silah tipleri. YENİ tip eklenecekse SONUNA ekle (kayıtlarda sayı tutuluyor).
public enum WeaponType
{
    Sword,       // Kılıç: dengeli (eski kombo)
    Daggers,     // Hançerler: çok hızlı, kısa, parry sonrası uzun karşılık
    Spear,       // Mızrak: uzun menzil, ileri atılan ilk vuruş
    Greatsword   // Büyük Kılıç: yavaş, dengeyi ezer, 4. vuruş şok dalgası
}

/// <summary>
/// Silah tanımları. Ayrı sprite GEREKMEZ: aynı oyuncu animasyonları silahın
/// hızına göre oynar, vuruş anında silahın renginde / boyunda kodla çizilen
/// bir kesik izi çıkar (PlayerWeapon).
/// </summary>
public static class WeaponInfo
{
    public static readonly WeaponType[] All =
    {
        WeaponType.Sword,
        WeaponType.Daggers,
        WeaponType.Spear,
        WeaponType.Greatsword
    };

    public static string Name(WeaponType t)
    {
        switch (t)
        {
            case WeaponType.Daggers: return "Hançerler";
            case WeaponType.Spear: return "Mızrak";
            case WeaponType.Greatsword: return "Büyük Kılıç";
            default: return "Kılıç";
        }
    }

    public static string Description(WeaponType t)
    {
        switch (t)
        {
            case WeaponType.Daggers:
                return "Çok hızlı, kısa menzil, az hasar. +%15 kritik. Parry sonrası karşılık (riposte) +2 vuruş.";
            case WeaponType.Spear:
                return "Uzun menzil. Kombonun ilk vuruşu ileri atılır. Düşmanı mesafede tut.";
            case WeaponType.Greatsword:
                return "Yavaş ama dengeyi EZER, çok can hasarı. 4. vuruş çevreye şok dalgası.";
            default:
                return "Dengeli. Hız, menzil ve hasar ortada.";
        }
    }

    public static Color Color(WeaponType t)
    {
        switch (t)
        {
            case WeaponType.Daggers: return new Color(0.55f, 1f, 0.6f);
            case WeaponType.Spear: return new Color(1f, 0.9f, 0.45f);
            case WeaponType.Greatsword: return new Color(1f, 0.5f, 0.35f);
            default: return new Color(0.8f, 0.9f, 1f);
        }
    }

    // Saldırı süresi çarpanı (küçük = hızlı).
    public static float Duration(WeaponType t)
    {
        switch (t)
        {
            case WeaponType.Daggers: return 0.68f;
            case WeaponType.Spear: return 1.1f;
            case WeaponType.Greatsword: return 1.45f;
            default: return 1f;
        }
    }

    // Vuruş kutusu genişliği (menzil) çarpanı.
    public static float Reach(WeaponType t)
    {
        switch (t)
        {
            case WeaponType.Daggers: return 0.8f;
            case WeaponType.Spear: return 1.7f;
            case WeaponType.Greatsword: return 1.35f;
            default: return 1f;
        }
    }

    // Vuruş kutusu yüksekliği çarpanı.
    public static float Height(WeaponType t)
    {
        switch (t)
        {
            case WeaponType.Spear: return 0.8f;
            case WeaponType.Greatsword: return 1.3f;
            default: return 1f;
        }
    }

    // Vuruşun ileri hareketi çarpanı.
    public static float Lunge(WeaponType t)
    {
        switch (t)
        {
            case WeaponType.Daggers: return 0.9f;
            case WeaponType.Spear: return 1.3f;
            case WeaponType.Greatsword: return 0.8f;
            default: return 1f;
        }
    }

    public static float Balance(WeaponType t)
    {
        switch (t)
        {
            case WeaponType.Daggers: return 0.6f;
            case WeaponType.Spear: return 0.95f;
            case WeaponType.Greatsword: return 1.9f;
            default: return 1f;
        }
    }

    public static float Health(WeaponType t)
    {
        switch (t)
        {
            case WeaponType.Daggers: return 0.65f;
            case WeaponType.Greatsword: return 1.7f;
            default: return 1f;
        }
    }

    // Kalıcı gelişimde kilit kimliği. Kılıç ve Hançer baştan açık (boş).
    public static string UnlockId(WeaponType t)
    {
        switch (t)
        {
            case WeaponType.Spear: return "wp_spear";
            case WeaponType.Greatsword: return "wp_great";
            default: return "";
        }
    }
}
