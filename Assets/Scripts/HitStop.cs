using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Oyundaki TÜM zaman yavaşlatma / dondurma işleri buradan geçer.
///
/// Eskiden CombatImpactFeedback, PlayerController.FreezeFrame,
/// PlayerCombatController.DoHitStop ve eski HitStop birbirinden
/// habersiz Time.timeScale'i değiştiriyordu; biri bitince diğerinin
/// efektini yarıda kesip zamanı 1'e çekebiliyordu.
///
/// Kullanım:
///     HitStop.Request(0.06f, 0.05f);          // 0.06 sn, %5 hızda
///     HitStop.Request(0.04f);                 // 0.04 sn tam dondurma
///     HitStop.Request(0.12f, 0.01f, 10);      // yüksek öncelik
///
/// Kurallar:
///  - Aynı anda birden fazla istek aktifse EN YAVAŞ olan geçerlidir.
///  - Aktif bir isteğin önceliğinden DÜŞÜK öncelikli yeni istek yok sayılır
///    (ör. balance break sırasında normal vuruş hit-stop'u karışmaz).
///  - Hiç istek kalmayınca zaman, hit-stop başlamadan önceki
///    değerine geri döner (ileride pause menüsü eklersen bozulmaz).
///    DÜZELTME: o değer 0 ise (oyun menüde duraklatılmışken gelen istek)
///    1'e döner; aksi halde oyun sonsuza kadar donuk kalıyordu.
///  - Sahnede HitStop yoksa kendiliğinden oluşturulur.
/// </summary>
public class HitStop : MonoBehaviour
{
    private struct StopRequest
    {
        public float endTime;   // Time.unscaledTime cinsinden
        public float timeScale;
        public int priority;

        // Rampa isteği: ağır başla, zamanla normale dön.
        public bool ramp;
        public float startTime;
        public float duration;
        public float startScale;
        public float endScale;
        public float power;
    }

    private static HitStop instance;

    private readonly List<StopRequest> requests =
        new List<StopRequest>();

    private bool applied;
    private float baseTimeScale = 1f;

    // Editor'de "Enter Play Mode" (domain reload kapalı) ayarında
    // static değişken oyunlar arasında kalmasın.
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration
    )]
    private static void ResetStatics()
    {
        instance = null;
    }

    private static HitStop Instance
    {
        get
        {
            if (instance == null)
            {
                instance =
                    FindFirstObjectByType<HitStop>();

                if (instance == null)
                {
                    GameObject obj =
                        new GameObject("HitStop");

                    instance =
                        obj.AddComponent<HitStop>();
                }
            }

            return instance;
        }
    }

    // =========================================================
    // PUBLIC API
    // =========================================================

    public static void Request(
        float duration,
        float timeScale = 0f,
        int priority = 0
    )
    {
        if (!Application.isPlaying)
            return;

        if (duration <= 0f)
            return;

        Instance.AddRequest(
            duration,
            Mathf.Clamp01(timeScale),
            priority
        );
    }

    /// <summary>
    /// Ağır başlayıp zamanla normale dönen yavaşlatma (slow-mo rampası).
    ///
    ///   scale(t) = Lerp(startScale, endScale, t ^ power),  t = 0..1
    ///
    /// duration GERÇEK zaman saniyesidir (timeScale'den etkilenmez).
    /// power 1 = düz, 2 = yavaş kalır sonra hızlanır, 3 = daha da geç hızlanır.
    /// </summary>
    public static void RequestRamp(
        float duration,
        float startScale,
        float endScale = 1f,
        float power = 2f,
        int priority = 0
    )
    {
        if (!Application.isPlaying)
            return;

        if (duration <= 0f)
            return;

        Instance.AddRamp(
            duration,
            Mathf.Clamp01(startScale),
            Mathf.Clamp01(endScale),
            Mathf.Max(0.1f, power),
            priority
        );
    }

    // Eski API uyumluluğu: tam dondurma.
    public static void Stop(float duration)
    {
        Request(duration, 0f, 0);
    }

    // Tüm aktif hit-stop'ları iptal eder (ör. sahne değişimi, ölüm ekranı).
    public static void ClearAll()
    {
        if (instance == null)
            return;

        instance.requests.Clear();
        instance.Apply();
    }

    // =========================================================
    // INTERNAL
    // =========================================================

    private void Awake()
    {
        if (
            instance != null &&
            instance != this
        )
        {
            // Sahneye elle koyulmuş ikinci bir kopya varsa
            // sadece bu bileşen kaldırılır.
            Destroy(this);
            return;
        }

        instance = this;
    }

    private void AddRequest(
        float duration,
        float timeScale,
        int priority
    )
    {
        PurgeExpired();

        if (priority < HighestActivePriority())
            return;

        StopRequest request =
            new StopRequest
            {
                endTime =
                    Time.unscaledTime + duration,

                timeScale =
                    timeScale,

                priority =
                    priority
            };

        requests.Add(request);

        Apply();
    }

    private void AddRamp(
        float duration,
        float startScale,
        float endScale,
        float power,
        int priority
    )
    {
        PurgeExpired();

        if (priority < HighestActivePriority())
            return;

        float now =
            Time.unscaledTime;

        requests.Add(
            new StopRequest
            {
                ramp = true,
                startTime = now,
                endTime = now + duration,
                duration = duration,
                startScale = startScale,
                endScale = endScale,
                power = power,
                timeScale = startScale,
                priority = priority
            }
        );

        Apply();
    }

    private static float EvaluateScale(
        StopRequest request,
        float now
    )
    {
        if (!request.ramp)
            return request.timeScale;

        float t =
            Mathf.Clamp01(
                (now - request.startTime) /
                Mathf.Max(0.0001f, request.duration)
            );

        return Mathf.Lerp(
            request.startScale,
            request.endScale,
            Mathf.Pow(t, request.power)
        );
    }

    private void Update()
    {
        // Update, timeScale 0 iken de çalışır.
        if (requests.Count == 0)
            return;

        PurgeExpired();

        Apply();
    }

    private void PurgeExpired()
    {
        float now =
            Time.unscaledTime;

        for (
            int i = requests.Count - 1;
            i >= 0;
            i--
        )
        {
            if (requests[i].endTime <= now)
                requests.RemoveAt(i);
        }
    }

    private int HighestActivePriority()
    {
        int highest = int.MinValue;

        for (int i = 0; i < requests.Count; i++)
        {
            if (requests[i].priority > highest)
                highest = requests[i].priority;
        }

        return highest;
    }

    private void Apply()
    {
        if (requests.Count == 0)
        {
            if (applied)
            {
                Time.timeScale = baseTimeScale;
                applied = false;
            }

            return;
        }

        if (!applied)
        {
            // Zaman o an 0 ise (menü duraklatması / başka bir dondurma)
            // dönüş değeri olarak 0'ı hatırlama: oyun donuk kalırdı.
            baseTimeScale =
                Time.timeScale > 0.001f
                    ? Time.timeScale
                    : 1f;

            applied = true;
        }

        float now =
            Time.unscaledTime;

        float slowest = 1f;

        for (int i = 0; i < requests.Count; i++)
        {
            float scale =
                EvaluateScale(requests[i], now);

            if (scale < slowest)
                slowest = scale;
        }

        Time.timeScale = slowest;
    }

    private void OnDisable()
    {
        // Obje kapanırken zaman asla 0'da takılı kalmasın.
        requests.Clear();

        Apply();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
