using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// İNFAZ SİNEMATİĞİ. İki aşama:
///
///   ODAK (E basılı tutulurken): kamera yavaşça yaklaşır, oyuncu ile düşmanın ORTASINA kayar,
///       siyah sinema şeritleri açılır. (Zamanı PlayerFinisher yavaşlatır.)
///   VURUŞ (bırakınca): kamera hızla en yakına geçer, ağır çekim sürer; öldürme anında sarsıntı +
///       kısa dondurma, sonra zaman yumuşakça normale döner ve kamera eski haline açılır.
///
/// Kendi kendini kurar. Çağıranlar:
///   ExecuteCinematic.BeginFocus(player, enemy); SetFocusProgress(0..1); CancelFocus();   // PlayerFinisher
///   ExecuteCinematic.Begin();  ExecuteCinematic.End(killed);                              // EnemyExecuteState
///
/// Zoom ortografik boyutla yapılır (2D). Pixel Perfect Camera varsa efekt süresince geçici kapatılır.
/// Cinemachine "Follow" hedefi efekt boyunca iki karakterin ortasındaki bir noktaya çevrilir ve sonunda geri verilir.
/// </summary>
public class ExecuteCinematic : MonoBehaviour
{
    // =========================================================
    // AYARLAR
    // =========================================================

    /// <summary>Ortografik boyut çarpanları (küçük = daha yakın).</summary>
    private const float FocusZoomStart = 0.92f;
    private const float FocusZoomEnd = 0.74f;
    private const float StrikeZoom = 0.58f;

    /// <summary>Çarpan değişim hızları (1/sn, gerçek zaman).</summary>
    private const float FocusRate = 0.9f;
    private const float StrikeRate = 5f;
    private const float ReleaseRate = 2.2f;

    private const float LingerAfterKill = 0.45f;  // öldürmeden sonra yakında bekleme
    private const float MaxStrikeHold = 2.5f;     // güvenlik
    private const float FocusTimeout = 0.5f;      // PlayerFinisher haber vermezse odak biter

    /// <summary>Vuruş anı ağır çekim (süre gerçek sn).</summary>
    private const float StrikeSlowScale = 0.1f;
    private const float StrikeSlowTime = 0.6f;

    /// <summary>Öldürme anı: dondurma + sarsıntı, sonra zaman normale döner.</summary>
    private const float KillFreeze = 0.16f;
    private const float KillShake = 0.8f;
    private const float RecoverTime = 1.0f;

    /// <summary>Sinema şeritlerinin ekran yüksekliğine oranı (tam zoom'da, her biri).</summary>
    private const float BarFraction = 0.11f;

    // =========================================================
    // API
    // =========================================================

    public static void BeginFocus(Transform player, Transform enemy)
    {
        if (!Application.isPlaying)
            return;

        Get().DoBeginFocus(player, enemy);
    }

    public static void SetFocusProgress(float progress01)
    {
        if (instance != null)
            instance.DoFocusProgress(progress01);
    }

    public static void CancelFocus()
    {
        if (instance != null)
            instance.DoCancelFocus();
    }

    /// <summary>Vuruş aşaması başlar (EnemyExecuteState.Enter).</summary>
    public static void Begin()
    {
        if (!Application.isPlaying)
            return;

        Get().DoBeginStrike();
    }

    public static void End(bool killed)
    {
        if (instance != null)
            instance.DoEnd(killed);
    }

    // =========================================================
    // İÇ
    // =========================================================

    private enum Mode { None, Focus, Strike }

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
    private CinemachineBrain brain;

    private bool acquired;
    private Mode mode = Mode.None;

    private float zoom = 1f;          // 1 = normal
    private float baseSize;
    private float originalLensSize;
    private float focusProgress;
    private float lastPing;
    private float strikeBegin;
    private float releaseAt = -1f;

    private Transform playerT;
    private Transform enemyT;
    private Vector3 lastEnemyPos;

    private Transform focusPoint;
    private Transform originalFollow;
    private bool originalIgnoreTimeScale;

    private readonly System.Collections.Generic.List<Behaviour> disabledByUs =
        new System.Collections.Generic.List<Behaviour>();

    private void Awake()
    {
        enabled = false;
    }

    // ---------------- Başlat / bitir ----------------

    private void DoBeginFocus(Transform player, Transform enemy)
    {
        playerT = player;
        enemyT = enemy;

        if (enemy != null)
            lastEnemyPos = enemy.position;

        if (!acquired && !Acquire())
            return;

        mode = Mode.Focus;
        focusProgress = 0f;
        lastPing = Time.unscaledTime;
        releaseAt = -1f;

        enabled = true;
    }

    private void DoFocusProgress(float p)
    {
        if (mode != Mode.Focus)
            return;

        focusProgress = Mathf.Clamp01(p);
        lastPing = Time.unscaledTime;
    }

    private void DoCancelFocus()
    {
        if (mode == Mode.Focus)
            mode = Mode.None;
    }

    private void DoBeginStrike()
    {
        if (playerT == null)
        {
            PlayerController pc = FindFirstObjectByType<PlayerController>();

            if (pc != null)
                playerT = pc.transform;
        }

        if (!acquired && !Acquire())
            return;

        mode = Mode.Strike;
        strikeBegin = Time.unscaledTime;
        releaseAt = -1f;

        // Geçiş boyunca ağır çekim sürsün.
        HitStop.Request(StrikeSlowTime, StrikeSlowScale, 9);

        enabled = true;
    }

    private void DoEnd(bool killed)
    {
        if (mode != Mode.Strike || releaseAt >= 0f)
            return;

        releaseAt = Time.unscaledTime + LingerAfterKill;

        // Önce yavaş yavaş normale dönen rampa, sonra (daha yüksek öncelikle) kısa dondurma.
        HitStop.RequestRamp(RecoverTime, 0.14f, 1f, 2f, 9);

        if (killed)
        {
            HitStop.Request(KillFreeze, 0.02f, 10);

            if (KillShake > 0f && CameraShake.Instance != null)
                CameraShake.Instance.Shake(KillShake);
        }
    }

    // ---------------- Kamera ----------------

    private bool Acquire()
    {
        cam = Camera.main;

        if (cam == null || !cam.orthographic)
            return false;

        brain = cam.GetComponent<CinemachineBrain>();

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

        // Cinemachine geçişleri/sönümü ağır çekimde de gerçek zamanla aksın.
        if (brain != null)
        {
            originalIgnoreTimeScale = brain.IgnoreTimeScale;
            brain.IgnoreTimeScale = true;
        }

        // Kamera hedefi: iki karakterin ortası.
        originalFollow = cm.Follow;

        if (focusPoint == null)
        {
            GameObject go = new GameObject("ExecuteFocusPoint");
            go.hideFlags = HideFlags.HideInHierarchy;
            focusPoint = go.transform;
        }

        Vector3 startPos =
            originalFollow != null
                ? originalFollow.position
                : (playerT != null ? playerT.position : cam.transform.position);

        focusPoint.position = startPos;

        cm.Follow = focusPoint;

        // Atlama olmasın: önce ekrandaki boyutu lens'e yaz.
        SetSize(baseSize);

        zoom = 1f;
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
            mode = Mode.None;
            zoom = 1f;
            enabled = false;
            return;
        }

        float now = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;

        // Zaman aşımları.
        if (mode == Mode.Focus && now - lastPing > FocusTimeout)
            mode = Mode.None;

        if (mode == Mode.Strike)
        {
            if (releaseAt >= 0f && now >= releaseAt)
                mode = Mode.None;
            else if (now - strikeBegin > MaxStrikeHold)
                mode = Mode.None;
        }

        // Zoom hedefi.
        float targetZoom;
        float rate;

        switch (mode)
        {
            case Mode.Focus:
                targetZoom = Mathf.Lerp(FocusZoomStart, FocusZoomEnd, focusProgress);
                rate = FocusRate;
                break;

            case Mode.Strike:
                targetZoom = StrikeZoom;
                rate = StrikeRate;
                break;

            default:
                targetZoom = 1f;
                rate = ReleaseRate;
                break;
        }

        zoom = Mathf.MoveTowards(zoom, targetZoom, rate * dt);

        SetSize(baseSize * zoom);

        UpdateFocusPoint(dt);

        if (mode == Mode.None && zoom >= 0.999f)
            Release();
    }

    // Kamera hedefini iki karakterin ortasına (bitince eski hedefe) doğru kaydır.
    private void UpdateFocusPoint(float dt)
    {
        if (focusPoint == null)
            return;

        if (enemyT != null)
            lastEnemyPos = enemyT.position;

        Vector3 desired;

        if (mode == Mode.None && originalFollow != null)
        {
            desired = originalFollow.position;
        }
        else
        {
            Vector3 a = playerT != null ? playerT.position : focusPoint.position;

            desired = (a + lastEnemyPos) * 0.5f + Vector3.up * 0.3f;
        }

        desired.z = focusPoint.position.z;

        focusPoint.position =
            Vector3.Lerp(focusPoint.position, desired, 1f - Mathf.Exp(-9f * dt));
    }

    private void Release()
    {
        Restore();

        acquired = false;
        mode = Mode.None;
        zoom = 1f;
        enabled = false;
    }

    private void Restore()
    {
        if (cm != null)
        {
            SetSize(originalLensSize);

            // Eski takip hedefini geri ver.
            cm.Follow = originalFollow;
        }

        if (brain != null)
            brain.IgnoreTimeScale = originalIgnoreTimeScale;

        for (int i = 0; i < disabledByUs.Count; i++)
        {
            if (disabledByUs[i] != null)
                disabledByUs[i].enabled = true;
        }

        disabledByUs.Clear();
    }

    private void OnDisable()
    {
        // Obje kapanırsa Pixel Perfect / takip hedefi açık kalmasın.
        if (!acquired)
            return;

        Restore();

        acquired = false;
        mode = Mode.None;
        zoom = 1f;
    }

    private void OnDestroy()
    {
        if (focusPoint != null)
            Destroy(focusPoint.gameObject);
    }

    // ---------------- Sinema şeritleri ----------------

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint)
            return;

        float bar = Mathf.Clamp01((1f - zoom) / 0.3f);

        if (bar <= 0.001f)
            return;

        float h = Screen.height * BarFraction * bar;

        Color old = GUI.color;

        GUI.depth = -100;
        GUI.color = Color.black;

        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, h), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0f, Screen.height - h, Screen.width, h), Texture2D.whiteTexture);

        GUI.color = old;
    }
}
