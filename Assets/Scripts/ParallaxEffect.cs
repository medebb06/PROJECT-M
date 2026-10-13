using UnityEngine;

/// <summary>
/// PARALLAX — katmanı kameraya göre kaydırır.
///
///  factor  1   = kameraya yapışık (gökyüzü gibi, hiç kaymaz)
///  factor  0.5 = uzak (dünyanın yarı hızında kayar)
///  factor  0   = oyun düzlemi (zemin ile aynı)
///  factor <0   = ön plan (kameradan HIZLI geçer, derinlik hissi verir)
///
/// Konum her karede MUTLAK hesaplanır: ofset = (kamera - referans) * factor.
///   - Hata birikmez; Play'de factor'ü değiştirince anında doğru yere gider (canlı ayar).
///   - Sadece kendi ofset FARKINI uyguladığı için CloudDrift / animasyon gibi
///     başka hareketlerle birlikte çalışır.
///   - Cinemachine kamerayı LateUpdate'te taşır; bu bileşen ondan SONRA çalışır
///     (titreme / bir kare gecikme yok).
///
/// Referans:
///   PlacedPosition = obje, kamera tam önüne geldiğinde SAHNEDE KOYDUĞUN yerde görünür.
///                    Tekil objeler (ağaç, çalı) için doğrusu bu.
///   CameraStart    = katman, oyunun başladığı kameraya göre dizilir.
///                    Ekrana yakın katmanlar (bulut) ve grup konteynerleri için.
/// Dikey referans her zaman başlangıç kamerasıdır.
///
/// Hazır oranlar: Tools > Parallax > Apply Depth Presets.
/// </summary>
[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public class ParallaxEffect : MonoBehaviour
{
    public enum Reference { PlacedPosition, CameraStart }

    [Tooltip("Yatay kayma. 1 = kameraya yapışık, 0 = zemin, negatif = ön plan.")]
    [Range(-1f, 1f)] public float parallaxX = 0.5f;

    [Tooltip("Dikey kayma. Genelde yatayın yarısı kadar olmalı (zıplarken katmanlar zeminden kopmasın).")]
    [Range(-1f, 1f)] public float parallaxY = 0.25f;

    public Reference reference = Reference.PlacedPosition;

    [Tooltip("Boş bırakılırsa Main Camera.")]
    public Transform cameraTransform;

    private Vector2 refCam;
    private Vector2 appliedOffset;
    private bool initialized;

    private void OnEnable()
    {
        initialized = false;
        appliedOffset = Vector2.zero;
    }

    private void OnDisable()
    {
        // Kapatılınca sahnedeki yerine dönsün (yeniden açılınca kaymış kalmasın).
        if (initialized)
            transform.position -= (Vector3)appliedOffset;

        initialized = false;
        appliedOffset = Vector2.zero;
    }

    private bool Init()
    {
        if (cameraTransform == null)
        {
            Camera c = Camera.main;

            if (c == null)
                return false;

            cameraTransform = c.transform;
        }

        Vector3 cam = cameraTransform.position;
        Vector3 me = transform.position;

        refCam = new Vector2(reference == Reference.PlacedPosition ? me.x : cam.x, cam.y);
        appliedOffset = Vector2.zero;
        initialized = true;
        return true;
    }

    private void LateUpdate()
    {
        if (!initialized && !Init())
            return;

        if (cameraTransform == null)
        {
            initialized = false;
            return;
        }

        Vector3 cam = cameraTransform.position;

        Vector2 target = new Vector2(
            (cam.x - refCam.x) * parallaxX,
            (cam.y - refCam.y) * parallaxY
        );

        Vector2 d = target - appliedOffset;

        if (d.sqrMagnitude > 0f)
        {
            transform.position += new Vector3(d.x, d.y, 0f);
            appliedOffset = target;
        }
    }

    /// <summary>Referansı şu anki kameraya/konuma göre yeniden al (sahne geçişi, ışınlanma sonrası).</summary>
    public void Rebase()
    {
        if (initialized)
            transform.position -= (Vector3)appliedOffset;

        initialized = false;
        appliedOffset = Vector2.zero;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Kamera 10 birim sağa gidince bu obje nereye kayar: sarı ok.
        Vector3 p = transform.position;
        Vector3 to = p + new Vector3(10f * parallaxX, 0f, 0f);
        Gizmos.color = parallaxX < 0f ? new Color(1f, 0.4f, 0.3f) : new Color(1f, 0.85f, 0.2f);
        Gizmos.DrawLine(p, to);
        Gizmos.DrawWireSphere(to, 0.25f);
    }
#endif
}
