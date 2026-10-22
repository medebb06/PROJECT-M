using System.Collections.Generic;
using UnityEngine;

// Bir düşman tipinin tüm verisi. Yeni düşman = prefab + bir EnemyDef.
[CreateAssetMenu(menuName = "Project M/Enemy Def", fileName = "EnemyDef")]
public class EnemyDef : ScriptableObject
{
    public string displayName = "Duelist";

    [Header("Dayanıklılık")]
    public int maxHealth = 60;
    public int maxBalance = 40;
    [Tooltip("Kararlı saldırı sırasında vuruş saldırıyı kesmez (poise).")]
    public bool superArmor = true;
    public float staggerDuration = 2.2f;
    [Tooltip("Denge vuruşunda (kırmadan) kısa tökezleme süresi.")]
    public float balanceHitStun = 0.12f;

    [Header("Hareket")]
    public float chaseSpeed = 4.5f;
    public float standbySpeedMultiplier = 0.7f;
    [Tooltip("Oyuncuyu fark etme mesafesi.")]
    public float sightRange = 14f;
    public float arrivalTolerance = 0.2f;
    [Tooltip("Hız değişim ivmesi (birim/sn²). Yüksek = çevik, düşük = ağır/okunur.")]
    public float acceleration = 70f;
    [Tooltip("Hasar yeme / saldırı öncesi gerilme squash-stretch şiddeti. 0 = kapalı, 1 = normal.")]
    public float squashStrength = 1f;
    [Header("Kaçış dash'i (okçu)")]
    [Tooltip("0 = kapalı. Oyuncu bu mesafeden yakınsa (saldırıda değilken) düşman geriye hızlı dash atar.")]
    public float escapeDistance = 0f;
    public float escapeSpeed = 26f;
    public float escapeDuration = 0.28f;
    [Tooltip("> 0 ise kaçış kayarak değil ZIPLAYARAK olur (dikey hız). Havadayken yatay hız korunur, yere inince biter; escapeDuration en fazla süre.")]
    public float escapeJump = 0f;
    [Tooltip("İki kaçış arası bekleme (sn). Sürekli kaçmasın, arada yakalanabilsin.")]
    public float escapeCooldown = 3.5f;
    [Tooltip("Kaçışa başlarken bulunduğu yere tuzak bırakır.")]
    public bool escapeTrap = false;
    [Tooltip("Tuzak kaç sn sonra patlar.")]
    public float trapDelay = 2f;
    [Tooltip("Patlama menzili (iki yana, birim).")]
    public float trapRadius = 3.5f;
    [Tooltip("Patlama oyuncunun MAX canının bu oranını götürür.")]
    [Range(0f, 1f)] public float trapHitPercent = 0.3f;
    [Tooltip("Düz vuruşla sersemletildikten sonra bu süre (sn) yeniden sersemletilemez; vuruş spamı düşmanı kilitleyip saldırılarını iptal etmesin. Parry/posture kırılması etkilemez.")]
    public float hitStunCooldown = 1.0f;
    [Tooltip("Sekiro tarzı: düz vuruşları bloklar (parry sesi + kıvılcım, can gitmez, sersemlemez) posture kırılana kadar.")]
    public bool blockPlainHits = true;

    [Header("Çatışma (clash): oyuncunun saldırısı düşmanın saldırısıyla çarpışır")]
    [Tooltip("Windup'ın bu oranından sonra (ve Active'de, vuruş çözülmeden) gelen oyuncu saldırısı düşman saldırısıyla çatışır.")]
    [Range(0.3f, 1f)] public float clashWindupFraction = 0.7f;
    [Tooltip("Çatışmada düşman posture'ına gelen oran (MaxBalance x oran). Parry'den hafif.")]
    [Range(0f, 1f)] public float clashPostureFraction = 0.4f;
    public float clashStun = 0.35f;
    [Tooltip("Aynı düşmanla art arda çatışma kilidi (sn).")]
    public float clashCooldown = 0.6f;

    [Header("Seri / Nefes (saldırı ritmi)")]
    [Tooltip("Bir seride en az/en çok kaç saldırı (zincir vuruşları dahil). 0 = seri sistemi kapalı.")]
    public int burstMin = 0;
    public int burstMax = 0;
    [Tooltip("Seri bitince nefes süresi aralığı (sn). Nefeste düşman saldırı kararı vermez, geri çekilir.")]
    public float breathMin = 0f;
    public float breathMax = 0f;
    [Tooltip("Nefeste oyuncudan en az bu kadar uzaklaşır.")]
    public float breathDistance = 6f;
    [Range(0.2f, 1.5f)] public float breathMoveSpeed = 0.75f;

    [Header("Tempo baskısı: isabet alan düşman saldırı kararını geciktirir")]
    public float pressureDelay = 0.4f;
    [Tooltip("Üst üste isabet başına eklenen gecikme.")]
    public float pressureStep = 0.12f;
    public float pressureMax = 1.0f;
    [Tooltip("Gruptaki diğer düşmanların sıradaki vuruşuna eklenen gecikme.")]
    public float pressureGroupDelay = 0.3f;
    [Tooltip("Vuruş anında oyuncu hitReach'ten bu kadar uzakta olsa da isabet eder; ayrıca düşman vuruşta bu mesafeyi ileri adımlayarak kapatır. Oyuncunun küçük geri adımı/atak hareketi ıskalatmasın.")]
    public float reachForgiveness = 1.6f;
    [Tooltip("Windup'ta hedeften uzağa geriye eğilme (derece).")]
    public float leanBackDegrees = 16f;
    [Tooltip("Saldırı salımında hedefe doğru eğilme (derece).")]
    public float leanForwardDegrees = 9f;
    [Tooltip("Oyuncu bu mesafeden uzaktaysa koşu hızı bu çarpanla artar (uzaktan hızlı yaklaşma).")]
    public float sprintMultiplier = 1.4f;
    public float sprintDistance = 14f;

    [Header("Sıra bekleme (kalabalık)")]
    public float standbyDistance = 4.5f;
    public float standbySpacing = 1.5f;

    [Header("Tepki")]
    [Tooltip("Saldırıya geçmeden önce kısa duraksama (okunabilirlik).")]
    public float attackDecisionDelay = 0.15f;
    [Tooltip("İki saldırı arası minimum bekleme.")]
    public float attackCooldown = 0.6f;

    [Header("Geri tepme")]
    public float balanceHitKnockback = 2.5f;
    public float healthHitKnockback = 4f;
    public float blockKnockback = 3f;
    public float knockbackDeceleration = 25f;

    [Header("Karşı atılma (oyuncunun dash'ine cevap)")]
    [Tooltip("Boş değilse: oyuncu yakında dash atınca bu isimli saldırı (followUpOnly) şansla zorla seçilir.")]
    public string counterDashAttack = "";
    [Range(0f, 1f)] public float counterDashChance = 0.5f;
    public float counterDashCooldown = 7f;
    [Tooltip("Oyuncu dash attığında düşman en fazla bu yatay mesafedeyse tetiklenir.")]
    public float counterDashRange = 12f;

    [Header("Posture kullanımı")]
    [Tooltip("Kapalı: posture barı yok, düz vuruş doğrudan can götürür; parry düşmanı sersemletir (infaz fırsatı). Elite / boss için açık bırak.")]
    public bool usesPosture = true;

    [Header("Posture / can (boss gibi)")]
    [Tooltip("Düz vuruşlarla posture bar dolarken MAX canın bu oranı kadar can da gider (boss: 0.5). 0 = posture can götürmez.")]
    [Range(0f, 1f)] public float hitChipHealthRatio = 0.5f;
    [Tooltip("Sersemlemiş (posture kırık) düşmana TEK düz vuruşun götüreceği MAX can oranı (infaz gibi). 1 = tek vuruşta öldürür, 0 = kapalı.")]
    [Range(0f, 1f)] public float staggerHitHealthFraction = 1f;

    [Header("Çarpışma / itme")]
    [Tooltip("Açıkken düşman oyuncunun İÇİNDEN geçer; birbirini itmezler.")]
    public bool passThroughPlayer = true;
    [Tooltip("Oyuncunun vuruşlarının düşmanı geri itme çarpanı (0 = hiç itme).")]
    [Range(0f, 1f)] public float knockbackScale = 0.35f;

    [Header("Oyuncu etkileşimi")]
    [Tooltip("KULLANILMIYOR (eski). Parry artık parryPostureFraction ile çalışır.")]
    public int parryBalanceDamage = 18;
    [Tooltip("Tek parry posture barının bu kadarını doldurur (0.5 = 2 parry'de kırılır). Düz vuruştan çok daha hızlı olmalı.")]
    [Range(0.05f, 1f)] public float parryPostureFraction = 0.5f;
    public int blockPostureDamage = 8;

    [Header("Saldırılar")]
    public List<AttackDef> attacks = new List<AttackDef> { new AttackDef() };
}
