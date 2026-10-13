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

    [Tooltip("Ekranın sol üstünde durum yazısı göster (sorun giderme).")]
    public bool showDebug = true;

    [Header("Durum (sadece bilgi, Play'de güncellenir)")]
    [SerializeField] private string status = "";

    private PlayerController player;
    private CinemachineCamera vcam;
    private float baseOrtho = -1f;
    private float amount;
    private float vel;

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
    }

    private void Set(float k)
    {
        if (vcam == null || baseOrtho <= 0f)
            return;

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
        Set(1f + zoomAmount * eased);

        if (Time.unscaledTime >= nextLog && (speed > 0.5f || nextLog == 0f))
        {
            nextLog = Time.unscaledTime + 1.5f;
            Debug.Log($"[RunCameraZoom] player={(player != null)} rb={(player != null && player.rb != null)} vcam={vcam.name} | hız {speed:0.0} | miktar {amount:0.00} | taban {baseOrtho:0.0} | lens {vcam.Lens.OrthographicSize:0.00} | kamera {(Camera.main != null ? Camera.main.orthographicSize : 0f):0.00}");
        }

        status = $"hız {speed:0.0} | miktar {amount:0.00} | taban {baseOrtho:0.0} | lens {vcam.Lens.OrthographicSize:0.00} | kamera {(Camera.main != null ? Camera.main.orthographicSize : 0f):0.00}";
    }
}
