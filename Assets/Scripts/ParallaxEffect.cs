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

    [Header("Derinlikten hesapla (gerçek perspektif modeli)")]
    [Tooltip("Açıkken X/Y oranları aşağıdaki derinlikten otomatik hesaplanır: oran = derinlik / (derinlik + odak). " +
             "Tek bir anlamlı ayar: derinlik. 0 = oyun düzlemi, + = arkada (uzak), - = kameraya yakın (ön plan).")]
    public bool driveFromDepth = false;
    [Tooltip("Oyun düzleminden uzaklık (dünya birimi). 18 = arka çalı şeridi, 100+ = bulut.")]
    public float depth = 0f;
    [Tooltip("Perspektif odak mesafesi. Büyütürsen bütün katmanlar kameraya daha az bağlanır.")]
    public float focal = 12f;
    [Tooltip("Dikey oran = yatay oran x bu. Zıplarken katmanlar zeminden kopmasın diye yatayın yarısı civarı.")]
    [Range(0f, 1f)] public float yRatio = 0.5f;

    [Tooltip("Boş bırakılırsa Main Camera.")]
    public Transform cameraTransform;

    [Header("Sabit referans kamera (yukarı çıkan map için)")]
    [Tooltip("Açıkken referans, oyunun başladığı kameradan DEĞİL aşağıdaki sabit köy-başlangıç konumundan alınır. " +
             "Böylece oyuncuyu plato/checkpoint gibi yüksekte başlatsan da katmanlar (dağ, bulut, ağaç) aynı yerde görünür.")]
    public bool fixedReferenceCamera = true;
    [Tooltip("Katmanların 'doğru' göründüğü kamera konumu (köy başlangıcı).")]
    public Vector2 referenceCamPos = new Vector2(-16.95f, 0.65f);

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!driveFromDepth)
            return;

        float d = depth + focal;
        float f = Mathf.Abs(d) < 0.01f ? 1f : depth / d;
        parallaxX = Mathf.Clamp(f, -1f, 1f);
        parallaxY = Mathf.Clamp(parallaxX * yRatio, -1f, 1f);
    }
#endif

    private Vector2 refCam;
    private Vector2 appliedOffset;
    private bool initialized;

    private void OnEnable()
    {
        initialized = false;
        appliedOffset = Vector2.zero;
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += OnBeginCamera;
    }

    // Kamera ÇİZİLMEDEN hemen önce, kameranın SON konumuna göre yeniden hizala
    // (Cinemachine bazen LateUpdate'ten sonra çalışır -> bir kare geriden gelip titretiyordu).
    // Konum mutlak hesaplandığı için tekrar çağırmak güvenli.
    private void OnBeginCamera(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != null && cam == Camera.main)
            LateUpdate();
    }

    private void OnDisable()
    {
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= OnBeginCamera;

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

        float refX = reference == Reference.PlacedPosition ? me.x : (fixedReferenceCamera ? referenceCamPos.x : cam.x);
        float refY = fixedReferenceCamera ? referenceCamPos.y : cam.y;
        refCam = new Vector2(refX, refY);
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
