using UnityEngine;

/// <summary>
/// EĞİMDE KAYMA: ayarlar + yardımcılar (asıl davranış SlideState'te).
///
/// Oyuncu eğimli bir zemine (Ground katmanı) değince o eğimin yönünde, yavaş başlayıp hızlanan bir
/// kaymaya girer. Kayarken yere mıknatıs gibi yapışık kalır (tepeden taşsa da havaya fırlamaz,
/// eğim değişse de zemini izler). Sadece zıplama ile kayma bırakılır (dash kapalı); hız zıplamada korunur. Ters tuşa basmak yavaşlatmaz.
///
/// Bu bileşeni Player'a ekle (eklenmemişse oyun başlarken otomatik eklenir). Değerleri inspector'dan ayarla.
/// Kayılacak zeminin katmanı Ground olmalı.
/// </summary>
[DisallowMultipleComponent]
public class SlopeSlide : MonoBehaviour
{
    [Header("Ne zaman kayar")]
    [Tooltip("Zemin bu açıdan (derece) dik ya da daha dikse kayılır. Düz zemin = 0°.")]
    public float minSlideAngle = 18f;
    [Tooltip("Bundan dik yüzeyler duvar sayılır, kayılmaz.")]
    public float maxSlideAngle = 75f;
    [Tooltip("Kayarken eğim bu açının altına inerse (düz zemin) kayma sürtünmeyle sönmeye başlar.")]
    public float flatAngle = 8f;

    [Header("Hız")]
    [Tooltip("Kayma başlarken en az bu hızla başlar (yavaş giriş). Koşarak/düşerek girersen mevcut hızın korunur.")]
    public float startSpeed = 4f;
    [Tooltip("Kaymanın en yüksek hızı (birim/sn). Koşu hızı 7.")]
    public float maxSpeed = 24f;
    [Tooltip("Hızlanma (birim/sn²). 45° eğimde tam değer; daha yatay eğimde azalır, daha dikte artar.")]
    public float acceleration = 30f;
    [Tooltip("Düz zemine inince (eğim bitti) yavaşlama (birim/sn²).")]
    public float runOutFriction = 14f;
    [Tooltip("Düz zeminde hız koşu hızının bu katına inince kayma biter, normal harekete dönülür.")]
    public float endSpeedRatio = 1.1f;
    [Tooltip("Kayma yönünün tersine (yokuş yukarı) giderken yavaşlama = hızlanma x bu.")]
    public float uphillDecelMultiplier = 1.5f;

    [Header("Mıknatıs (yere yapışma)")]
    [Tooltip("Ayağın altında bu mesafeye kadar zemin varsa yapışık kalır. Büyük = tepelerden daha zor kopar.")]
    public float stickDistance = 0.8f;
    [Tooltip("Yüzeye doğru sürekli çekim hızı (birim/sn). Çok büyük olursa titrer.")]
    public float stickSpeed = 4f;
    [Tooltip("Kaymaya GİRMEK için zemin ayağın en fazla bu kadar altında olmalı.")]
    public float enterDistance = 0.35f;

    [Header("Çıkış")]
    [Tooltip("Kayma bırakıldıktan sonra tekrar kaymaya girmeden önce bekleme (sn). Zıplayınca yapışmasın diye.")]
    public float reEnterDelay = 0.15f;
    [Tooltip("Kaymadan zıplayınca / kopunca yatay hızın korunduğu süre (sn).")]
    public float airCarryTime = 0.9f;
    [Tooltip("Bu sürede havada yatay hız sönümü x bu oran (küçük = momentum daha uzun korunur).")]
    [Range(0f, 1f)] public float airCarryDecelMul = 0.02f;

    private const float ProbeUp = 0.4f;

    /// <summary>Oyuncunun ayağının altındaki zemini tarar (kapsülün alt-orta noktasından aşağı).</summary>
    public static bool Probe(PlayerController p, float reach, out RaycastHit2D hit)
    {
        hit = default(RaycastHit2D);

        if (p == null || p.col == null || p.Movement == null)
            return false;

        Bounds b = p.col.bounds;
        Vector2 origin = new Vector2(b.center.x, b.min.y + ProbeUp);

        hit = Physics2D.Raycast(origin, Vector2.down, ProbeUp + Mathf.Max(0.05f, reach), p.Movement.groundMask);

        // distance 0: çıkış noktası zeminin içinde, normal güvenilmez.
        return hit.collider != null && hit.distance > 0.001f;
    }

    /// <summary>Uygunsa kaymayı başlatır. Grounded / Air durumlarından çağrılır.</summary>
    public static bool TryEnter(PlayerController p, PlayerStateMachine sm)
    {
        SlopeSlide cfg = p != null ? p.Slide : null;

        if (cfg == null || !cfg.enabled)
            return false;

        if (!p.canControl || p.isDashing || p.inputLocked || p.slamGroundLock)
            return false;

        if (ExecutionSlide.Active || p.IsDefending())
            return false;

        if (Time.time - SlideState.LastExitTime < cfg.reEnterDelay)
            return false;

        // Zıplayıp yükselirken yapışma.
        if (p.rb.linearVelocity.y > 0.5f)
            return false;

        RaycastHit2D hit;

        if (!Probe(p, cfg.enterDistance, out hit))
            return false;

        float angle = Vector2.Angle(hit.normal, Vector2.up);

        if (angle < cfg.minSlideAngle || angle > cfg.maxSlideAngle)
            return false;

        sm.ChangeState(new SlideState(p, sm, cfg));
        return true;
    }
}
