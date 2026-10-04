// Koşu döngüsünün ortak tipleri. AYRI dosyada durmalı: Unity, dosya adıyla
// eşleşmeyen dosyalarda İLK sınıfı bileşen sanar (Runmanager.cs içinde
// ShopItem ilk sınıf olunca "missing ExtensionOfNativeClass" hatası çıkar).

public enum RunState
{
    Lobby,          // koşu başlamadan: zorluk seçimi
    Starting,       // koşu başlıyor
    Offer,          // charm seçimi (oyun duraklatılmış)
    ChoosingRoom,   // sıradaki odanın kapısını seç
    Fighting,       // dövüş odası sürüyor
    Cleared,        // dövüş odası temizlendi
    Shop,           // dükkan
    Rest,           // dinlenme
    Dead,           // öldü
    Victory         // son boss yenildi
}

public enum RoomType
{
    Fight,  // normal dövüş → charm
    Elite,  // güçlü düşman → 2 charm + bol altın
    Shop,   // altınla alışveriş
    Rest,   // iyileş ya da charm yükselt
    Boss    // perde sonu
}

public enum ShopItemKind
{
    Charm,
    Heal
}

public class ShopItem
{
    public ShopItemKind kind;
    public CharmDefinition charm;
    public int price;
    public bool sold;
}