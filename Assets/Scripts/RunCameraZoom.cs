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

    [Header("Olay zoom'u (savaş anlarında)")]
    [Tooltip("Parry ve kalabalık zoom'unu açar/kapatır.")] public bool eventZoom = true;
    [Tooltip("Parry'de yaklaşma oranı. 0.05 = %5.")] [Range(0f, 0.2f)] public float parryZoom = 0.05f;
    [Tooltip("Dengeyi kıran (mükemmel) parry'de yaklaşma oranı.")] [Range(0f, 0.2f)] public float perfectParryZoom = 0.075f;
    [Tooltip("Parry zoom'unun girişi (sn): kısa = vuruş hissi.")] public float parryIn = 0.05f;
    [Tooltip("Parry'de yakın kalma süresi (sn).")] public float parryHold = 0.10f;
    [Tooltip("Parry zoom'undan geri dönüş (sn): yavaş = sinematik.")] public float parryOut = 0.40f;
    [Tooltip("Kalabalıkta uzaklaşma oranı. 0.10 = %10 daha geniş.")] [Range(0f, 0.4f)] public float crowdZoom = 0.10f;
    [Tooltip("Bu kadar ya da daha fazla düşman yakındaysa kamera açılır.")] public int crowdMinEnemies = 3;
    [Tooltip("Düşmanın 'yakın' sayıldığı mesafe (dünya birimi).")] public float crowdRadius = 14f;
    [Tooltip("Kalabalıkta açılma süresi (sn).")] public float crowdIn = 1.2f;
    [Tooltip("Kalabalık dağılınca geri dönüş süresi (sn).")] public float crowdOut = 1.8f;

    [Header("Bekleme Nefesi / Sinematik kamera (idle'da uzun süre kalınca)")]
    [Tooltip("Girdi gelmeden bu kadar saniye geçince kamera nefes almaya başlar.")]
    public float idleDelay = 3f;
    [Tooltip("Nefes moduna yumuşak geçiş süresi (sn).")]
    public float breathFadeIn = 3.5f;
    [Tooltip("Girdi gelince normale dönüş süresi (sn).")]
    public float breathFadeOut = 0.7f;
    [Tooltip("Bekleme sırasında karaktere ne kadar yaklaşır. 0.07 = %7 daha yakın.")]
    [Range(0f, 0.3f)] public float idleZoomIn = 0.12f;
    [Tooltip("Nefes alıp vermeyle yakınlaşma titreşimi (oran).")]
    [Range(0f, 0.05f)] public float breathZoomWobble = 0.016f;
    [Tooltip("Nefes döngüsü süresi (sn).")]
    public float breathPeriod = 3.8f;
    [Tooltip("Kamera salınımı (dünya birimi, yatay / dikey).")]
    public Vector2 swayAmount = new Vector2(0.55f, 0.42f);
    [Tooltip("Hafif yalpalama (derece).")]
    public float swayRoll = 2.2f;
    [Tooltip("Elle tutulan kamera titremesi (konum, dünya birimi): nefese eklenen hızlı, düzensiz sallantı.")]
    public float handheldPos = 0.14f;
    [Tooltip("Elle tutulan kamera titremesinin yalpası (derece).")]
    public float handheldRoll = 0.8f;
    [Tooltip("Elle tutulan titremenin hızı.")]
    public float handheldSpeed = 1.6f;
    [Tooltip("Sinematik kamera açıkken oynarken (girdi varken) de sallanma oranı. 0 = sadece beklerken, 1 = beklerkenki kadar.")]
    [Range(0f, 1f)] public float activeShake = 0.45f;
    public bool idleBreathing = true;
    [Tooltip("Kameranin taban gorus yuksekligi (ortho). Cozunurluk / Game view boyutundan BAGIMSIZ sabit. 18 = 888 piksellik pencerede eski gorunum, 21.6 = 1080p'de PixelPerfect gorunumu. 0 = eski davranis (ekran yuksekligine bagli).")]
    public float referenceOrtho = 18f;
    [Tooltip("Kamera en fazla ne kadar yaklasabilir (1 = hic yaklasmaz, 0.82 = en fazla %18). Nefes + giris sinematigi + odak + parry toplamini sinirlar.")]
    [Range(0.5f, 1f)] public float minZoomScale = 0.82f;

    [Header("Sinematik şeritler (letterbox)")]
    [Tooltip("Sinematik kamera açıkken ekranın üstüne ve altına siyah şerit çıkar.")] public bool letterbox = true;
    [Tooltip("Şerit yüksekliği (ekran yüksekliğine oran, her biri). 0.16 = %16.")] [Range(0f, 0.35f)] public float barHeight = 0.16f;
    [Tooltip("Şeritlerin açılıp kapanma süresi (sn).")] public float barSmooth = 0.5f;
    private float bars, barsVel;
    /// <summary>Ust sinematik seridin o anki piksel yuksekligi (HUD barlari asagi kayar).</summary>
    public static float TopBarPixels;
    /// <summary>Tutorial: sinematik kamerayi (seritler + yakin odak) zorla ac.</summary>
    public static bool ForceCinematic;
    /// <summary>Giris sahnesi: kamera bu dunya noktasina kayar (null = oyuncuyu takip).</summary>
    public static Transform FocusTarget;
    /// <summary>Odak sirasinda ek yaklasma (0.1 = %10 daha yakin).</summary>
    public static float FocusZoomIn = 0.12f;
    private float focusBlend, focusBlendVel;

    // Dis kaynakli kamera sarsintisi (dunya birimi): CameraShake/Impulse yeterince gorunmediginde dogrudan konum titretir.
    private static float kick;
    private static float kickSign = 1f;
    public static void AddShake(float worldUnits) { kick = Mathf.Max(kick, worldUnits); }
    public float forceZoomIn = 0.2f;

    [Tooltip("Ekranın sol üstünde durum yazısı göster (sorun giderme).")]
    [System.NonSerialized] public bool showDebug = false;   // sahnede/prefabta kayitli true degeri yoksayilir: sol ustteki debug yazisi kapali

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
    private float pulse, pulseVel, pulsePeak, pulseHold;   // parry zoom'u (yaklaşma)
    private float crowd, crowdVel, crowdTarget, nextCrowdScan;
    private readonly Collider2D[] crowdBuf = new Collider2D[32];
    private readonly EnemyBase[] crowdSeen = new EnemyBase[32];
    private ContactFilter2D crowdFilter; private bool crowdFilterInit;

    private void OnEnable()
    {
        CombatEvents.ParrySucceeded += OnParry;
    }

    private void OnParry(EnemyBase e, bool brokeBalance)
    {
        if (!eventZoom)
            return;

        pulsePeak = Mathf.Max(pulsePeak * 0.5f, brokeBalance ? perfectParryZoom : parryZoom);
        pulseHold = parryHold;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<RunCameraZoom>() != null)
            return;

        new GameObject("RunCameraZoom").AddComponent<RunCameraZoom>();
    }

    private void OnDisable()
    {
        CombatEvents.ParrySucceeded -= OnParry;
        Set(1f);

        if (vcam != null)
        {
            LensSettings lens = vcam.Lens;
            lens.Dutch = 0f;
            vcam.Lens = lens;
        }

        if (camOffset != null)
            camOffset.Offset = Vector3.zero;

        if (rollHelper != null)
        {
            rollHelper.posOffset = Vector3.zero;
            rollHelper.roll = 0f;
        }
    }

    // Herhangi bir oyuncu girdisi var mı? (klavye, fare, kol)
    private static bool AnyInput()
    {
        if (Input.anyKey || MobileInput.AnyTouch)
            return true;

        if (Mathf.Abs(MobileInput.GetAxisRaw("Horizontal")) > 0.1f || Mathf.Abs(MobileInput.GetAxisRaw("Vertical")) > 0.1f)
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

                // PixelPerfectCamera ortho'yu Game view yuksekligine gore verir (yukseklik/50): pencere kuculunce kamera YAKINLASIRDI.
                // Cozunurluge bagli kalmasin: sabit referans kullan (0 = eski davranis).
                ownBase = referenceOrtho > 0.5f ? referenceOrtho : c.orthographicSize;

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
        if (letterbox && bars > 0.002f && Event.current.type == EventType.Repaint)
        {
            float h = Screen.height * GameSettings.BarHeight * bars * (bars * (3f - 2f * bars) / Mathf.Max(bars, 0.0001f));
            int d = GUI.depth;
            GUI.depth = -10000;   // en önde: HUD dahil her şeyin üstünde
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, h), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, Screen.height - h, Screen.width, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.depth = d;
        }

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

        if (forceBreath || ForceCinematic)
            busyWhy = "";

        bool busy = busyWhy != "";

        if (busyWhy != lastLoggedWhy)
        {
            lastLoggedWhy = busyWhy;
            if (showDebug) Debug.Log("[RunCameraZoom] bekleme engeli: " + (busyWhy == "" ? "YOK (sayaç işliyor)" : busyWhy));
        }

        if (busy)
            idleTimer = 0f;
        else
            idleTimer += Time.unscaledDeltaTime;

        bool cinematic = (idleBreathing && GameSettings.CinematicCamera) || ForceCinematic;

        // Şeritler: sinematik açıkken görünür, menü açıkken (duraklatma/ana menü) gizlenir.
        float barTarget = letterbox && cinematic && !MainMenu.Active ? 1f : 0f;
        bars = Mathf.SmoothDamp(bars, barTarget, ref barsVel, Mathf.Max(0.05f, barSmooth), Mathf.Infinity, Time.unscaledDeltaTime);
        TopBarPixels = letterbox ? Screen.height * GameSettings.BarHeight * bars * (bars * (3f - 2f * bars) / Mathf.Max(bars, 0.0001f)) : 0f;

        // ---- Olay zoom'u: parry (yaklaş) + kalabalık (uzaklaş) ----
        if (eventZoom && player != null && Time.unscaledTime >= nextCrowdScan)
        {
            nextCrowdScan = Time.unscaledTime + 0.4f;
            int near = 0;
            Vector2 pp = player.transform.position;

            if (crowdFilterInit == false)
            {
                crowdFilter = new ContactFilter2D { useLayerMask = true, layerMask = LayerMask.GetMask("Enemy"), useTriggers = true };
                crowdFilterInit = true;
            }

            // Çöp üretmeyen sorgu (FindObjectsByType her seferinde dizi ayırıp takılma yapıyordu).
            int n = Physics2D.OverlapCircle(pp, crowdRadius, crowdFilter, crowdBuf);
            int seenCount = 0;

            for (int i = 0; i < n; i++)
            {
                EnemyBase en = crowdBuf[i] != null ? crowdBuf[i].GetComponentInParent<EnemyBase>() : null;

                if (en == null)
                    continue;

                bool dup = false;

                for (int k = 0; k < seenCount; k++)
                    if (crowdSeen[k] == en) { dup = true; break; }

                if (dup)
                    continue;

                if (seenCount < crowdSeen.Length)
                    crowdSeen[seenCount++] = en;

                Health eh = en.GetComponent<Health>();

                if (eh != null && eh.IsDead)
                    continue;

                near++;
            }

            // 3 düşmandan başlar, 5'te tam açılır.
            crowdTarget = near >= crowdMinEnemies ? Mathf.Clamp01((near - crowdMinEnemies + 1) / 3f) : 0f;
        }

        if (!eventZoom)
            crowdTarget = 0f;

        crowd = Mathf.SmoothDamp(crowd, crowdTarget, ref crowdVel, crowdTarget > crowd ? crowdIn : crowdOut, Mathf.Infinity, Time.unscaledDeltaTime);

        if (pulseHold > 0f)
            pulseHold -= Time.unscaledDeltaTime;

        float pulseTarget = pulseHold > 0f ? pulsePeak : 0f;
        pulse = Mathf.SmoothDamp(pulse, pulseTarget, ref pulseVel, pulseTarget > pulse ? parryIn : parryOut, Mathf.Infinity, Time.unscaledDeltaTime);

        if (pulseHold <= 0f && pulse < 0.0005f)
            pulsePeak = 0f;

        focusBlend = Mathf.SmoothDamp(focusBlend, FocusTarget != null ? 1f : 0f, ref focusBlendVel, 0.9f, Mathf.Infinity, Time.unscaledDeltaTime);
        if (FocusTarget == null && focusBlend < 0.001f) focusBlend = 0f;

        float breathTarget = cinematic && (forceBreath || ForceCinematic || idleTimer >= idleDelay) ? 1f : 0f;
        float bs = breathTarget > breath ? (ForceCinematic ? 0.5f : breathFadeIn) : breathFadeOut;
        breath = Mathf.SmoothDamp(breath, breathTarget, ref breathVel, bs, Mathf.Infinity, Time.unscaledDeltaTime);

        float bEase = breath * breath * (3f - 2f * breath);
        float tt = Time.unscaledTime;
        float phase = tt * Mathf.PI * 2f / Mathf.Max(0.5f, breathPeriod);

        // Yakınlaş + nefesle hafif ileri-geri.
        float zoomK = 1f + zoomAmount * eased
            + crowdZoom * crowd * crowd * (3f - 2f * crowd)
            - pulse
            - idleZoomIn * bEase
            - (ForceCinematic ? forceZoomIn * bEase : 0f)
            - FocusZoomIn * focusBlend * focusBlend * (3f - 2f * focusBlend)
            + breathZoomWobble * bEase * Mathf.Sin(phase);

        // Cok yakinlasmasin: tum "yaklasma" etkilerinin (nefes, giris sinematigi, odak, parry) toplami sinirlanir.
        zoomK = Mathf.Max(zoomK, minZoomScale);

        Set(zoomK);

        // Salınım: konum ofseti + çok hafif yalpa (farklı frekanslar → organik).
        if (camOffset == null)
        {
            camOffset = vcam.GetComponent<CinemachineCameraOffset>();

            if (camOffset == null)
                camOffset = vcam.gameObject.AddComponent<CinemachineCameraOffset>();
        }

        float sw = cinematic ? Mathf.Lerp(activeShake, 1f, bEase) : 0f;   // oynarken de sallan, beklerken tam
        float hn = tt * handheldSpeed;
        float hx = (Mathf.PerlinNoise(hn, 3.1f) - 0.5f) * 2f;
        float hy = (Mathf.PerlinNoise(7.7f, hn * 1.17f) - 0.5f) * 2f;
        float hr = (Mathf.PerlinNoise(hn * 0.83f, 11.3f) - 0.5f) * 2f;

        Vector3 shakeOff = new Vector3(
            swayAmount.x * sw * (Mathf.Sin(phase * 0.5f) + 0.4f * Mathf.Sin(phase * 1.3f + 1.7f)) + handheldPos * sw * hx,
            swayAmount.y * sw * (Mathf.Sin(phase * 0.8f + 0.9f) + 0.5f * Mathf.Sin(phase * 0.37f)) + handheldPos * sw * hy,
            0f
        );

        if (kick > 0.001f)
        {
            kick *= Mathf.Exp(-Time.unscaledDeltaTime * 6f);
            Vector2 kv = new Vector2(0f, Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI * 9f) * kick);
            shakeOff += new Vector3(kv.x, kv.y, 0f);
        }
        else
            kick = 0f;

        // Ekrana çizilen kamera vcam üstündeki Camera ise: Cinemachine yazdıktan SONRA doğrudan konuma ekle (kesin çalışır).
        if (rollHelper != null)
        {
            rollHelper.posOffset = shakeOff;
            if (FocusTarget != null) rollHelper.focusWorld = FocusTarget.position;
            rollHelper.focusBlend = focusBlend * focusBlend * (3f - 2f * focusBlend);
            camOffset.Offset = Vector3.zero;
        }
        else
            camOffset.Offset = shakeOff;

        LensSettings dl = vcam.Lens;
        roll = swayRoll * sw * Mathf.Sin(phase * 0.43f + 0.6f) + handheldRoll * sw * hr;
        dl.Dutch = roll;
        vcam.Lens = dl;

        if (showDebug && Time.unscaledTime >= nextLog && (speed > 0.5f || nextLog == 0f))
        {
            nextLog = Time.unscaledTime + 1.5f;
            Debug.Log($"[RunCameraZoom] player={(player != null)} rb={(player != null && player.rb != null)} vcam={vcam.name} | hız {speed:0.0} | miktar {amount:0.00} | taban {baseOrtho:0.0} | lens {vcam.Lens.OrthographicSize:0.00} | kamera {(Camera.main != null ? (ownCam != null ? ownCam.orthographicSize : Camera.main.orthographicSize) : 0f):0.00}");
        }

        if (showDebug && breath > 0.02f && Time.unscaledTime >= nextBreathLog)
        {
            nextBreathLog = Time.unscaledTime + 1.0f;
            Camera mc = Camera.main;
            CinemachineBrain br = mc != null ? mc.GetComponent<CinemachineBrain>() : null;
            Debug.Log($"[RunCameraZoom] NEFES {breath:0.00} | istenen ortho {baseOrtho * zoomK:0.00} | vcam.Lens {vcam.Lens.OrthographicSize:0.00} | Camera.main {(mc != null ? mc.orthographicSize : 0f):0.00} | ofset {camOffset.Offset} | dutch {vcam.Lens.Dutch:0.00} | brain {(br != null ? br.name : "YOK")} canlı: {(br != null && br.ActiveVirtualCamera != null ? ((Component)br.ActiveVirtualCamera).name : "?")}");
        }

        if (showDebug) status = $"bekleme {idleTimer:0.0}s engel [{busyWhy}] nefes {breath:0.00} | hız {speed:0.0} | miktar {amount:0.00} | taban {baseOrtho:0.0} | lens {vcam.Lens.OrthographicSize:0.00} | kamera {(Camera.main != null ? (ownCam != null ? ownCam.orthographicSize : Camera.main.orthographicSize) : 0f):0.00}";
    }
}

/// <summary>Cinemachine'in vcam transform'unu yazmasından SONRA hafif yalpa uygular.</summary>
[DefaultExecutionOrder(32000)]
public class RunCameraRoll : MonoBehaviour
{
    public float roll;
    public Vector3 posOffset;
    public Vector3 focusWorld;
    public float focusBlend;

    private void LateUpdate()
    {
        if (focusBlend > 0.0005f)
        {
            Vector3 p = transform.position;
            transform.position = Vector3.Lerp(p, new Vector3(focusWorld.x, focusWorld.y, p.z), focusBlend);
        }

        if (posOffset.sqrMagnitude > 1e-10f)
            transform.position += posOffset;

        if (Mathf.Abs(roll) < 0.0001f)
            return;

        Vector3 e = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(e.x, e.y, roll);
    }
}
