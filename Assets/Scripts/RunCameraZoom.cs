using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// KOŞARKEN KAMERA UZAKLAŞIR: oyuncu hızlandıkça görüş alanı hafifçe genişler, durunca yumuşakça geri gelir.
/// Kamerayı Cinemachine yönettiği için doğru yer onun lensidir: CinemachineCamera.Lens.OrthographicSize'ı oynatır,
/// Brain bunu kendisi kameraya uygular (başka script'le çakışma / ezilme olmaz).
/// Sahnede elle eklemene gerek yok: oyun başlayınca kendiliğinden oluşur.
/// </summary>
[DefaultExecutionOrder(-100)]
public class RunCameraZoom : MonoBehaviour
{
    [Tooltip("Tam hızda görüş alanı yüzde kaç büyür. 0.10 = %20 daha geniş.")]
    [Range(0f, 0.6f)] public float zoomAmount = 0.20f;

    [Tooltip("Bu yatay hızın altında hiç uzaklaşmaz.")]
    public float startSpeed = 3f;

    [Tooltip("Bu hızda tam uzaklaşır (koşu hızı).")]
    public float fullSpeed = 7f;

    [Tooltip("Uzaklaşma yumuşaklığı (sn). Büyük = daha yavaş, daha sinematik.")]
    public float smoothIn = 0.9f;
    [Tooltip("Yaklaşma (durunca geri dönüş) yumuşaklığı (sn).")]
    public float smoothOut = 0.6f;

    [Header("Bekleme Nefesi (idle'da uzun süre kalınca)")]
    [Tooltip("Girdi gelmeden bu kadar saniye geçince kamera nefes almaya başlar.")]
    public float idleDelay = 4f;
    [Tooltip("Nefes moduna yumuşak geçiş süresi (sn).")]
    public float breathFadeIn = 3.5f;
    [Tooltip("Girdi gelince normale dönüş süresi (sn).")]
    public float breathFadeOut = 0.7f;
    [Tooltip("Bekleme sırasında karaktere ne kadar yaklaşır. 0.07 = %7 daha yakın.")]
    [Range(0f, 0.3f)] public float idleZoomIn = 0.10f;
    [Tooltip("Nefes alıp vermeyle yakınlaşma titreşimi (oran).")]
    [Range(0f, 0.03f)] public float breathZoomWobble = 0.006f;
    [Tooltip("Nefes döngüsü süresi (sn).")]
    public float breathPeriod = 4.5f;
    [Tooltip("Kamera salınımı (dünya birimi, yatay / dikey).")]
    public Vector2 swayAmount = new Vector2(0.10f, 0.07f);
    [Tooltip("Hafif yalpalama (derece).")]
    public float swayRoll = 0.35f;
    public bool idleBreathing = true;

    [Tooltip("Ekranın sol üstünde durum yazısı göster (sorun giderme).")]
    public bool showDebug = true;

    [Header("Durum (sadece bilgi, Play'de güncellenir)")]
    [SerializeField] private string status = "";

    private PlayerController player;
    private CinemachineCamera vcam;
    private float baseOrtho = -1f;
    private float amount;
    private float vel;

    private string busyWhy = "";
    private string lastLoggedWhy = null;
    private bool forceBreath;
    private float nextBreathLog;
    private float idleTimer;
    private float breath;      // 0..1 nefes modu ağırlığı
    private float breathVel;
    private CinemachineCameraOffset camOffset;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<RunCameraZoom>() != null)
            return;

        new GameObject("RunCameraZoom").AddComponent<RunCameraZoom>();
    }

    private void OnDisable()
    {
        Set(1f);

        if (vcam != null)
        {
            LensSettings lens = vcam.Lens;
            lens.Dutch = 0f;
            vcam.Lens = lens;
        }

        if (camOffset != null)
            camOffset.Offset = Vector3.zero;
    }

    // Herhangi bir oyuncu girdisi var mı? (klavye, fare, kol)
    private static bool AnyInput()
    {
        if (Input.anyKey)
            return true;

        if (Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f)
            return true;

        Vector3 m = Input.mousePosition;

        // Fare hareketi (küçük titremeyi saymaz).
        bool moved = (m - lastMouse).sqrMagnitude > 9f;
        lastMouse = m;

        return moved || Input.GetAxisRaw("Mouse X") != 0f && Mathf.Abs(Input.GetAxisRaw("Mouse X")) > 0.5f;
    }

    private static Vector3 lastMouse;

    // ASIL SORUN: sahnede CinemachineCamera nesnesinin üzerinde kendi Camera + PixelPerfectCamera
    // bileşeni var ve ekrana o çiziliyor (Main Camera'nın üstünde, depth 0 > -1). Boyutunu
    // PixelPerfectCamera belirlediği için (21.6) Cinemachine lensini değiştirmek hiçbir şey yapmıyordu.
    // Çözüm: o kameranın PixelPerfectCamera'sını kapat (zaten sadece boyut hesaplıyordu: kırpma/snap/RT kapalı)
    // ve ortho boyutunu doğrudan biz yönet.
    private Camera ownCam;
    private float ownBase = -1f;
    private bool ownTried;
    private RunCameraRoll rollHelper;
    private float roll;

    private bool SetOwn(float k)
    {
        if (!ownTried && vcam != null)
        {
            ownTried = true;
            Camera c = vcam.GetComponent<Camera>();

            if (c != null && c.enabled)
            {
                ownCam = c;

                Behaviour pp = vcam.GetComponent("PixelPerfectCamera") as Behaviour;

                if (pp != null)
                    pp.enabled = false;

                ownBase = c.orthographicSize;

                if (ownBase < 0.5f)
                    ownBase = 21.6f;

                rollHelper = vcam.gameObject.GetComponent<RunCameraRoll>();

                if (rollHelper == null)
                    rollHelper = vcam.gameObject.AddComponent<RunCameraRoll>();

                Debug.Log("[RunCameraZoom] Ekrana çizilen kamera CinemachineCamera üstündeki Camera (ortho " + ownBase + "). PixelPerfectCamera'sı kapatıldı, zoom doğrudan yönetiliyor.");
            }
        }

        if (ownCam == null)
            return false;

        ownCam.orthographicSize = ownBase * k;

        if (rollHelper != null)
            rollHelper.roll = roll;

        return true;
    }

    private void Set(float k)
    {
        if (vcam == null || baseOrtho <= 0f)
            return;

        SetOwn(k);

        LensSettings lens = vcam.Lens;
        lens.OrthographicSize = baseOrtho * k;
        vcam.Lens = lens;
    }

    private float nextLog;

    private void OnGUI()
    {
        if (!showDebug)
            return;

        GUI.color = Color.white;
        GUI.Label(new Rect(10, 10, 900, 24), "RunCameraZoom: " + status);
    }

    private void Update()
    {
        if (player == null)
            player = FindFirstObjectByType<PlayerController>();

        if (vcam == null)
        {
            vcam = FindFirstObjectByType<CinemachineCamera>();

            if (vcam != null)
                baseOrtho = vcam.Lens.OrthographicSize;
        }

        if (vcam == null)
        {
            status = "CinemachineCamera bulunamadı";
            return;
        }

        float speed = 0f;

        if (player != null && player.rb != null)
            speed = Mathf.Abs(player.rb.linearVelocity.x);

        float target = Mathf.Clamp01(Mathf.InverseLerp(startSpeed, Mathf.Max(startSpeed + 0.1f, fullSpeed), speed));
        float smooth = target > amount ? smoothIn : smoothOut;
        amount = Mathf.SmoothDamp(amount, target, ref vel, smooth, Mathf.Infinity, Time.unscaledDeltaTime);

        float eased = amount * amount * (3f - 2f * amount);

        // ---- Bekleme nefesi ----
        bool inp = AnyInput();
        bool moving = speed > 0.35f;
        bool air = player != null && !player.isGrounded;
        busyWhy = player == null ? "oyuncu yok" : inp ? "girdi (tuş/fare/kol)" : moving ? "hareket" : air ? "havada" : "";
        // Test: B tuşu nefes modunu zorla aç/kapat (sebep ne olursa olsun).
        if (Input.GetKeyDown(KeyCode.B))
            forceBreath = !forceBreath;

        if (forceBreath)
            busyWhy = "";

        bool busy = busyWhy != "";

        if (busyWhy != lastLoggedWhy)
        {
            lastLoggedWhy = busyWhy;
            Debug.Log("[RunCameraZoom] bekleme engeli: " + (busyWhy == "" ? "YOK (sayaç işliyor)" : busyWhy));
        }

        if (busy)
            idleTimer = 0f;
        else
            idleTimer += Time.unscaledDeltaTime;

        float breathTarget = idleBreathing && (forceBreath || idleTimer >= idleDelay) ? 1f : 0f;
        float bs = breathTarget > breath ? breathFadeIn : breathFadeOut;
        breath = Mathf.SmoothDamp(breath, breathTarget, ref breathVel, bs, Mathf.Infinity, Time.unscaledDeltaTime);

        float bEase = breath * breath * (3f - 2f * breath);
        float tt = Time.unscaledTime;
        float phase = tt * Mathf.PI * 2f / Mathf.Max(0.5f, breathPeriod);

        // Yakınlaş + nefesle hafif ileri-geri.
        float zoomK = 1f + zoomAmount * eased
            - idleZoomIn * bEase
            + breathZoomWobble * bEase * Mathf.Sin(phase);

        Set(zoomK);

        // Salınım: konum ofseti + çok hafif yalpa (farklı frekanslar → organik).
        if (camOffset == null)
        {
            camOffset = vcam.GetComponent<CinemachineCameraOffset>();

            if (camOffset == null)
                camOffset = vcam.gameObject.AddComponent<CinemachineCameraOffset>();
        }

        camOffset.Offset = new Vector3(
            swayAmount.x * bEase * (Mathf.Sin(phase * 0.5f) + 0.4f * Mathf.Sin(phase * 1.3f + 1.7f)),
            swayAmount.y * bEase * (Mathf.Sin(phase * 0.8f + 0.9f) + 0.5f * Mathf.Sin(phase * 0.37f)),
            0f
        );

        LensSettings dl = vcam.Lens;
        roll = swayRoll * bEase * Mathf.Sin(phase * 0.43f + 0.6f);
        dl.Dutch = roll;
        vcam.Lens = dl;

        if (Time.unscaledTime >= nextLog && (speed > 0.5f || nextLog == 0f))
        {
            nextLog = Time.unscaledTime + 1.5f;
            Debug.Log($"[RunCameraZoom] player={(player != null)} rb={(player != null && player.rb != null)} vcam={vcam.name} | hız {speed:0.0} | miktar {amount:0.00} | taban {baseOrtho:0.0} | lens {vcam.Lens.OrthographicSize:0.00} | kamera {(Camera.main != null ? (ownCam != null ? ownCam.orthographicSize : Camera.main.orthographicSize) : 0f):0.00}");
        }

        if (breath > 0.02f && Time.unscaledTime >= nextBreathLog)
        {
            nextBreathLog = Time.unscaledTime + 1.0f;
            Camera mc = Camera.main;
            CinemachineBrain br = mc != null ? mc.GetComponent<CinemachineBrain>() : null;
            Debug.Log($"[RunCameraZoom] NEFES {breath:0.00} | istenen ortho {baseOrtho * zoomK:0.00} | vcam.Lens {vcam.Lens.OrthographicSize:0.00} | Camera.main {(mc != null ? mc.orthographicSize : 0f):0.00} | ofset {camOffset.Offset} | dutch {vcam.Lens.Dutch:0.00} | brain {(br != null ? br.name : "YOK")} canlı: {(br != null && br.ActiveVirtualCamera != null ? ((Component)br.ActiveVirtualCamera).name : "?")}");
        }

        status = $"bekleme {idleTimer:0.0}s engel [{busyWhy}] nefes {breath:0.00} | hız {speed:0.0} | miktar {amount:0.00} | taban {baseOrtho:0.0} | lens {vcam.Lens.OrthographicSize:0.00} | kamera {(Camera.main != null ? (ownCam != null ? ownCam.orthographicSize : Camera.main.orthographicSize) : 0f):0.00}";
    }
}

/// <summary>Cinemachine'in vcam transform'unu yazmasından SONRA hafif yalpa uygular.</summary>
[DefaultExecutionOrder(32000)]
public class RunCameraRoll : MonoBehaviour
{
    public float roll;

    private void LateUpdate()
    {
        if (Mathf.Abs(roll) < 0.0001f)
            return;

        Vector3 e = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(e.x, e.y, roll);
    }
}
