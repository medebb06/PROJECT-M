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
