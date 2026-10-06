/// <summary>
/// DEMO SADELEŞTİRME ANAHTARLARI (tek yer).
///
/// Demo 1 için sistemler KAPALI (false). Kod silinmedi: geri açmak için
/// ilgili satırı true yap. Sahnedeki Inspector değerlerinden bağımsızdır.
///
///   Weapons              : koşu başı silah seçimi (hep Kılıç)
///   Shop                 : dükkan (kapı seçeneklerinde çıkmaz)
///   Abilities            : Q yeteneği + yetenek seçimi
///   ProceduralGeneration : rastgele harita + orman (biome) teması
///                          (orman teması sadece bu üretici tarafından kurulur)
///   MetaProgression      : öz, Demirci/kalıcı gelişim, ısı, kalıcı kilitler
///   Upgrades             : dinlenmede charm güçlendirme
/// </summary>
public static class GameFeatures
{
    public static readonly bool Weapons = false;
    public static readonly bool Shop = false;
    public static readonly bool Abilities = false;
    public static readonly bool ProceduralGeneration = false;
    public static readonly bool MetaProgression = false;
    public static readonly bool Upgrades = false;
}
