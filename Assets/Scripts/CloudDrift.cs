using UnityEngine;

/// <summary>
/// Bulutu çok hafifçe salındırır: yatayda yavaş gidip gelir, dikeyde azıcık süzülür.
/// Her bulut farklı hız ve fazda hareket eder. Konumu "ekleyerek" değiştirdiği için
/// ParallaxEffect ile birlikte sorunsuz çalışır. Sadece Play'de.
/// </summary>
public class CloudDrift : MonoBehaviour
{
    [Tooltip("Yatay salınım genişliği (birim).")]
    public float amplitudeX = 0.6f;
    [Tooltip("Dikey süzülme (birim).")]
    public float amplitudeY = 0.08f;
    [Tooltip("Bir gidip gelme kaç saniye sürer.")]
    public float period = 40f;
    [Tooltip("Rastgele faz (0 = otomatik, konuma göre).")]
    public float phase;

    private Vector2 lastOffset;
    private float t0;

    private void OnEnable()
    {
        if (phase == 0f)
            phase = Mathf.Repeat(transform.position.x * 0.37f + transform.position.y * 1.3f, Mathf.PI * 2f);

        // Bulutlar aynı hızda gitmesin.
        period *= Mathf.Lerp(0.8f, 1.25f, Mathf.Repeat(phase * 0.618f, 1f));

        t0 = Time.time;
        lastOffset = Offset(0f);
    }

    private Vector2 Offset(float t)
    {
        float w = Mathf.PI * 2f / Mathf.Max(1f, period);
        return new Vector2(
            Mathf.Sin(t * w + phase) * amplitudeX,
            Mathf.Sin(t * w * 0.63f + phase * 1.7f) * amplitudeY
        );
    }

    private void Update()
    {
        Vector2 o = Offset(Time.time - t0);
        Vector2 d = o - lastOffset;
        lastOffset = o;

        transform.position += new Vector3(d.x, d.y, 0f);
    }
}
