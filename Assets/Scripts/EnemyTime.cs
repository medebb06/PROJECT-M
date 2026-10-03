using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DÜŞMANLARA ÖZEL ZAMAN SAATİ.
///
/// Oyuncu ve dünya normal hızda akarken sadece düşmanları yavaşlatmak için.
/// (Unity'de tek bir global Time.timeScale var; oyuncuyu bundan ayrı tutmak
/// yerine düşmanlar kendi saatini kullanır.)
///
/// Düşman kodu şunları kullanır:
///   EnemyTime.DeltaTime   -> Time.deltaTime yerine (sayaçlar, yavaşlama)
///   EnemyTime.Now         -> Time.time yerine (zamanlama / takvim)
///   EnemyTime.Scale       -> hız çarpanı (hareket, Animator.speed)
///
/// Yavaşlama, ağır başlayıp zamanla normale dönen bir rampa olarak istenir:
///   scale(t) = Lerp(startScale, 1, SmoothStep(t) ^ power),  t = 0..1
///
/// SmoothStep sayesinde rampanın HEM başı HEM sonu yumuşaktır: zaman normale
/// dönerken ani bir sıçrama olmaz (düz t^power'da en sert hızlanma tam
/// yavaşlamanın bittiği anda olurdu). power: 1 = düz yumuşak geçiş,
/// 2 = bir süre daha ağır kalıp sonra hızlanır.
///
/// Önemli: Animator.speed aynı ölçekle yavaşladığı için animasyon düşman
/// sayaçlarıyla senkron kalır. Sesler yavaşlamaz (pitch değişmez).
///
/// NOT: FixedUpdate içinden çağırma; Now birikimi Update karesine bağlı.
/// </summary>
public static class EnemyTime
{
    private struct SlowRequest
    {
        public float startTime;   // Time.unscaledTime
        public float duration;    // gerçek zaman
        public float startScale;
        public float power;
    }

    private static readonly List<SlowRequest> requests =
        new List<SlowRequest>();

    private static float scale = 1f;
    private static float now;
    private static int lastFrame = -1;

    // En son yavaşlatma isteğinin gerçek zamanı (cooldown için).
    public static float LastRequestTime { get; private set; } = -999f;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration
    )]
    private static void ResetStatics()
    {
        requests.Clear();

        scale = 1f;
        now = 0f;
        lastFrame = -1;

        LastRequestTime = -999f;
    }

    // Düşman hız çarpanı (1 = normal).
    public static float Scale
    {
        get
        {
            Sync();
            return scale;
        }
    }

    // Time.deltaTime yerine: düşman sayaçları bunu kullanır.
    public static float DeltaTime =>
        Time.deltaTime * Scale;

    // Time.time yerine: düşman zamanlaması için birikimli saat.
    public static float Now
    {
        get
        {
            Sync();
            return now;
        }
    }

    /// <summary>
    /// Düşmanları yavaşlatır: startScale ile başlar, 'duration' (gerçek
    /// saniye) içinde 1'e döner. Birden fazla istek varsa en yavaşı geçerli.
    /// </summary>
    public static void RequestRamp(
        float duration,
        float startScale,
        float power = 2f
    )
    {
        if (duration <= 0f)
            return;

        Sync();

        float t = Time.unscaledTime;

        requests.Add(
            new SlowRequest
            {
                startTime = t,
                duration = duration,
                startScale = Mathf.Clamp(startScale, 0.05f, 1f),
                power = Mathf.Max(0.1f, power)
            }
        );

        LastRequestTime = t;

        // Aynı karede kalan düşmanlar da yeni ölçeği görsün.
        scale = Evaluate(t);
    }

    public static void Clear()
    {
        requests.Clear();

        scale = 1f;
    }

    // Karede ilk erişimde: süresi dolanları at, ölçeği hesapla, saati ilerlet.
    private static void Sync()
    {
        int frame = Time.frameCount;

        if (frame == lastFrame)
            return;

        lastFrame = frame;

        float t = Time.unscaledTime;

        for (int i = requests.Count - 1; i >= 0; i--)
        {
            if (t >= requests[i].startTime + requests[i].duration)
                requests.RemoveAt(i);
        }

        scale = Evaluate(t);

        now += Time.deltaTime * scale;
    }

    private static float Evaluate(float t)
    {
        float slowest = 1f;

        for (int i = 0; i < requests.Count; i++)
        {
            SlowRequest r = requests[i];

            float x =
                Mathf.Clamp01(
                    (t - r.startTime) /
                    Mathf.Max(0.0001f, r.duration)
                );

            // SmoothStep: başı ve sonu yumuşak (ani sıçrama yok).
            float eased =
                Mathf.Pow(
                    Mathf.SmoothStep(0f, 1f, x),
                    r.power
                );

            float s =
                Mathf.Lerp(
                    r.startScale,
                    1f,
                    eased
                );

            if (s < slowest)
                slowest = s;
        }

        return slowest;
    }
}