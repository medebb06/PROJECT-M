using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// İNFAZ SİNEMATİĞİ: infaz başlayınca kamera yakınlaşır, zaman ağır çekime
/// girip normale döner, öldürme anında kısa bir sarsıntı olur, sonra kamera
/// yumuşakça eski haline açılır.
///
/// Kendi kendini kurar (sahneye bir şey eklemen gerekmez). Çağıranlar:
///   ExecuteCinematic.Begin();        // EnemyExecuteState.Enter
///   ExecuteCinematic.End(killed);    // EnemyExecuteState.FinishExecute
///
/// Ayarlar aşağıdaki sabitlerde. Zoom ortografik boyutla yapılır (2D).
/// Pixel Perfect Camera varsa efekt süresince geçici kapatılır (aksi halde
/// boyutu geri ezer), bitince açılır.
/// </summary>
public class ExecuteCinematic : MonoBehaviour
{
    // =========================================================
    // AYARLAR
    // =========================================================

    /// <summary>Ortografik boyut çarpanı. Küçük = daha yakın (0.72 ≈ %39 yakınlaşma).</summary>
    private const float ZoomFactor = 0.72f;

    private const float ZoomInTime = 0.14f;    // yakınlaşma süresi (gerçek sn)
    private const float ZoomOutTime = 0.45f;   // açılma süresi
    private const float LingerAfterKill = 0.22f; // öldürmeden sonra yakında bekleme
    private const float MaxHold = 2.5f;        // güvenlik: en fazla bu kadar yakında kal

    /// <summary>Ağır çekim başlangıç hızı (1 = ağır çekim yok).</summary>
    private const float SlowMoScale = 0.2f;
    private const float SlowMoDuration = 0.55f; // gerçek sn, sonra normale rampa

    /// <summary>Öldürme anı kamera sarsıntısı (0 = yok).</summary>
    private const float KillShake = 0.6f;

    // =========================================================
    // API
    // =========================================================

    public static void Begin()
    {
        if (!Application.isPlaying)
            return;

        Get().DoBegin();
    }

    public static void End(bool killed)
    {
        if (instance != null)
            instance.DoEnd(killed);
    }

    // =========================================================
    // İÇ
    // =========================================================

    private static ExecuteCinematic instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    private static ExecuteCinematic Get()
    {
        if (instance == null)
        {
            GameObject go = new GameObject("ExecuteCinematic");
            instance = go.AddComponent<ExecuteCinematic>();
        }

        return instance;
    }

    private Camera cam;
    private CinemachineCamera cm;

    private bool acquired;
    private bool active;

    private float t;              // 0 = normal, 1 = tam yakın
    private float baseSize;       // efekt başındaki ekran boyutu
    private float originalLensSize;
    private float beginTime;
    private float releaseAt = -1f;

    private readonly List<Behaviour> disabledByUs = new List<Behaviour>();

    private void Awake()
    {
        enabled = false;
    }

    private void DoBegin()
    {
        if (!acquired && !Acquire())
            return;

        active = true;
        releaseAt = -1f;
        beginTime = Time.unscaledTime;

        if (SlowMoScale < 0.999f)
            HitStop.RequestRamp(SlowMoDuration, SlowMoScale, 1f, 2f, 5);

        enabled = true;
    }

    private void DoEnd(bool killed)
    {
        if (!active || releaseAt >= 0f)
            return;

        releaseAt = Time.unscaledTime + LingerAfterKill;

        if (killed && KillShake > 0f && CameraShake.Instance != null)
            CameraShake.Instance.Shake(KillShake);
    }

    // Kamera + Cinemachine kamerasını bul, Pixel Perfect'i geçici kapat.
    private bool Acquire()
    {
        cam = Camera.main;

        if (cam == null || !cam.orthographic)
            return false;

        CinemachineBrain brain = cam.GetComponent<CinemachineBrain>();

        cm = brain != null ? brain.ActiveVirtualCamera as CinemachineCamera : null;

        if (cm == null)
            cm = FindFirstObjectByType<CinemachineCamera>();

        if (cm == null)
            return false;

        LensSettings lens = cm.Lens;

        originalLensSize = lens.OrthographicSize;

        // Ekranda şu an görünen boyut (Pixel Perfect varsa onun hesapladığı).
        baseSize = cam.orthographicSize;

        // Pixel Perfect Camera + Cinemachine eklentisi boyutu geri ezer: geçici kapat.
        disabledByUs.Clear();

        Behaviour pp = cam.GetComponent("PixelPerfectCamera") as Behaviour;
        Behaviour ppExt = cm.GetComponent("CinemachinePixelPerfect") as Behaviour;

        if (pp != null && pp.enabled)
        {
            pp.enabled = false;
            disabledByUs.Add(pp);
        }

        if (ppExt != null && ppExt.enabled)
        {
            ppExt.enabled = false;
            disabledByUs.Add(ppExt);
        }

        // Atlama olmasın: önce ekrandaki boyutu lens'e yaz.
        SetSize(baseSize);

        t = 0f;
        acquired = true;

        return true;
    }

    private void SetSize(float size)
    {
        LensSettings lens = cm.Lens;
        lens.OrthographicSize = size;
        cm.Lens = lens;
    }

    private void Update()
    {
        if (!acquired)
        {
            enabled = false;
            return;
        }

        // Sahne değişti / kamera yok oldu.
        if (cam == null || cm == null)
        {
            acquired = false;
            active = false;
            t = 0f;
            enabled = false;
            return;
        }

        if (active)
        {
            if (releaseAt >= 0f && Time.unscaledTime >= releaseAt)
                active = false;
            else if (Time.unscaledTime - beginTime > MaxHold)
                active = false;
        }

        float target = active ? 1f : 0f;
        float speed = 1f / (active ? ZoomInTime : ZoomOutTime);

        t = Mathf.MoveTowards(t, target, speed * Time.unscaledDeltaTime);

        float eased = t * t * (3f - 2f * t);

        SetSize(Mathf.Lerp(baseSize, baseSize * ZoomFactor, eased));

        if (!active && t <= 0f)
            Release();
    }

    private void Release()
    {
        if (cm != null)
            SetSize(originalLensSize);

        for (int i = 0; i < disabledByUs.Count; i++)
        {
            if (disabledByUs[i] != null)
                disabledByUs[i].enabled = true;
        }

        disabledByUs.Clear();

        acquired = false;
        active = false;
        t = 0f;
        enabled = false;
    }

    private void OnDisable()
    {
        // Obje kapanırsa Pixel Perfect açık kalmasın.
        if (!acquired)
            return;

        if (cm != null)
            SetSize(originalLensSize);

        for (int i = 0; i < disabledByUs.Count; i++)
        {
            if (disabledByUs[i] != null)
                disabledByUs[i].enabled = true;
        }

        disabledByUs.Clear();
        acquired = false;
        active = false;
        t = 0f;
    }
}
