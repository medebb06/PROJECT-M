using UnityEngine;

// Tek bir saldırının TÜM zamanlaması ve hasarı burada. Klip bu değerlere uydurulur.
[System.Serializable]
public class AttackDef
{
    public string name = "Slash";

    [Tooltip("Birden fazla uygun saldırı varsa seçilme ağırlığı.")]
    public float weight = 1f;

    [Header("Menzil (oyuncuya yatay mesafe)")]
    public float minRange = 0f;
    public float maxRange = 1.6f;

    [Header("Zamanlama (saniye)")]
    [Tooltip("Uyarı: vuruş anına kadar. Okunur ve SABİT kalmalı.")]
    public float windup = 0.55f;
    [Tooltip("Vuruş anından sonra aktif kalan süre (lunge burada oynar).")]
    public float active = 0.15f;
    [Tooltip("Vuruş sonrası savunmasız toparlanma.")]
    public float recovery = 0.6f;
    [Tooltip("Bu saldırı bittikten sonra tekrar seçilebilmesi için bekleme.")]
    public float cooldown = 0.8f;

    [Header("Vuruş")]
    public int damage = 10;
    public bool unblockable = false;
    [Tooltip("Vuruş anında oyuncu bu yatay mesafe içinde (önde) ise isabet eder.")]
    public float hitReach = 1.8f;
    public float hitHeight = 2.2f;
    public float knockbackForce = 6f;
    public float knockbackVertical = 2f;
    public float knockbackDuration = 0.25f;
    [Tooltip("Parry'de düşmana verilen denge hasarı çarpanı.")]
    public float parryBalanceMultiplier = 1f;

    [Header("Hareket (vuruş anında)")]
    [Tooltip("Aktif pencerede ileri atılma mesafesi (0 = yerinde).")]
    public float lungeDistance = 0f;
    public AnimationCurve lungeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Atılma oyuncunun bu kadar önünde biter; içinden/arkasına geçmez.")]
    public float lungeStopGap = 2f;

    [Header("Mermi (okçu) - 0 = yakın dövüş")]
    [Tooltip("0'dan büyükse saldırı Active başında mermi atar: yatay, düz çizgi, sabit hız. hitReach = uyarı çizgisinin uzunluğu. Parry mermiyi geri yansıtır.")]
    public float projectileSpeed = 0f;
    [Tooltip("Mermi çıkış yüksekliği (düşmanın ayağından).")]
    public float projectileHeight = 2.6f;
    public float projectileLife = 2.5f;
    public float projectileRadius = 0.35f;
    [Tooltip("Düşman ekranda değilse bu saldırı seçilmez.")]
    public bool requireOnScreen = true;

    [Header("Özel mekanikler")]
    [Tooltip("Windup başında düşman kaybolur, oyuncunun ARKASINA ışınlanır ve orada sabit durup odaklanır (windup'ın kalanı); sonra vurur.")]
    public bool teleportBehind = false;
    [Tooltip("Işınlanma sonrası oyuncunun arkasında durulacak mesafe.")]
    public float teleportGap = 2.4f;
    [Tooltip("Windup başında görünmez kalma süresi; bitince ışınlanır.")]
    public float teleportVanish = 0.3f;
    [Tooltip("Tek windup'ta art arda kaç vuruş (1 = normal). Her vuruş AYRI parry ister.")]
    [Min(1)] public int hits = 1;
    [Tooltip("Çoklu vuruşta vuruşlar arası süre (sn).")]
    public float hitInterval = 0.28f;
    [Tooltip("0 = oyuncunun standart sabit vuruş yüzdesi. >0 = bu saldırı oyuncunun MAX canının bu oranını götürür (0.4 = %40, ağır vuruş).")]
    [Range(0f, 1f)] public float hitPercent = 0f;
    [Tooltip("Bu saldırıdan sonraki nefeste düşman GERİ ÇEKİLMEZ, yerinde sabit bekler.")]
    public bool breathHold = false;
    [Tooltip("Bu saldırı bitince seri biter ve düşman nefese geçer.")]
    public bool endsBurst = false;
    [Tooltip("Düşman oyuncuyu ilk gördüğünde (ilk saldırı kararında) bunu öncelikle kullanır.")]
    public bool opener = false;

    [Header("Ağır saldırı (Heavy)")]
    [Tooltip("Windup + aktif sırasında oyuncu vuruşu sersemletmez / çatışma olmaz (posture kırılması yine sersemletir).")]
    public bool armored = false;
    [Tooltip("Uyarı yıldızı sarı olsun (parry'lenebilir ağır vuruş).")]
    public bool alertYellow = false;
    [Tooltip("Zıplayıp yere vurur: windup'ta iniş noktası yerde işaretlenir, havada gider, inince iki yana şok dalgası çıkar (zıplayarak/dash ile kaçılır, parry yok).")]
    public bool slamJump = false;
    [Tooltip("Şok dalgasının iniş noktasından iki yana menzili.")]
    public float slamRange = 7f;
    [Tooltip("Havada kalma süresi (sn).")]
    public float slamAirTime = 0.6f;
    public float slamHeight = 7f;      // tepe yüksekliği
    public float slamRise = 0.4f;      // yükseliş süresi
    public float slamHang = 0.12f;     // tepede asılı kalma
    public float slamFallSpeed = 45f;  // çakılma hızı
    [Tooltip("İniş noktası oyuncunun bu kadar önünde (düşman tarafında).")]
    public float slamGap = 2.5f;

    [Header("Devam saldırısı (kombo)")]
    [Tooltip("Bu saldırı isabet/ıska ile biterse (parry veya blok değil) bu isimdeki saldırı şansla ardından gelir; yine tam uyarılıdır.")]
    public string followUp = "";
    [Range(0f, 1f)] public float followUpChance = 0f;
    [Tooltip("Sadece devam saldırısı olarak kullanılır, normal seçimde çıkmaz.")]
    public bool followUpOnly = false;

    [Header("Animasyon")]
    [Tooltip("Boşsa EnemyAnimBridge standart durum adlarını kullanır (Windup/Attack/Recover).")]
    public string animWindup = "";
    public string animAttack = "";
    public string animRecover = "";
}
