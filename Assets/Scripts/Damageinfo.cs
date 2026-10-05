using UnityEngine;

// Hasarın nereden geldiği. Charm'lar buna göre davranabilir
// (örneğin zehir kritik yapmaz, kritik sadece Attack/Slam'de çalışır).
public enum DamageSource
{
    Attack,   // oyuncunun kombo vuruşu
    Slam,     // ground slam
    Poison,   // zehir tiki (ileride)
    Other,
    Ability   // Q yeteneği (PlayerAbility)
}

// Oyuncunun düşmana vuracağı bir hasarın TAM tarifi.
// Çağıran taraf taban değerleri doldurur; stat'lar ve kritik
// PlayerDamage içinde uygulanır.
public struct DamageInfo
{
    public DamageSource source;

    // Kombo adımı (1-4). Slam gibi kombo dışı kaynaklarda 0.
    public int comboStep;

    // TABAN hasarlar (stat ve kritik uygulanmadan önce).
    // Denge kırık değilse balanceDamage, kırıksa healthDamage uygulanır.
    public int balanceDamage;
    public int healthDamage;

    // Vuruş yönü ve görsel efekt konumu.
    public Vector2 direction;
    public Vector3 hitPosition;

    // true ise kritik atılmaz (zehir tiki gibi).
    public bool disallowCrit;

    // Pipeline tarafından doldurulur.
    public EnemyController target;
}

// Pipeline'ın sonucu: tam olarak ne oldu?
public struct HitResult
{
    public bool hit;            // vuruş gerçekten bir şey yaptı mı
    public bool onBalance;      // true: denge katmanına, false: cana
    public int amount;          // uygulanan SON hasar (stat + kritik dahil)
    public bool critical;
    public bool brokeBalance;   // bu vuruş dengeyi kırdı mı
    public bool killed;         // bu vuruş düşmanı öldürdü mü
}