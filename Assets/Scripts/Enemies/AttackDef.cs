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

    [Header("Animasyon")]
    [Tooltip("Boşsa EnemyAnimBridge standart durum adlarını kullanır (Windup/Attack/Recover).")]
    public string animWindup = "";
    public string animAttack = "";
    public string animRecover = "";
}
