using UnityEngine;

/// <summary>
/// Bulut akışı: rüzgâr yönünde, sürekli ve tek yönlü (ileri-geri salınım yok).
///   - Hız bulut katmanına göre verilir (uzak yavaş, yakın hızlı) — Tools > Lighting > Build Cloud Layers.
///   - Kameradan çok uzaklaşan bulut diğer uca taşınır (sonsuz akış). Taşıma ekran dışında olur.
///   - Dikeyde çok hafif, yavaş bir süzülme (birikmez, geri dönmez).
/// Konumu "ekleyerek" değiştirir: ParallaxEffect ile birlikte çalışır. Sadece Play'de.
/// </summary>
public class CloudDrift : MonoBehaviour
{
    [Tooltip("Akış hızı (birim/sn). Katmana göre ayarlanır.")]
    public float speed = 0.25f;

    [Tooltip("+1 = soldan sağa, -1 = sağdan sola.")]
    public float direction = 1f;

    [Tooltip("Dikey süzülme genliği (birim).")]
    public float bobAmplitude = 0.1f;

    [Tooltip("Bir süzülme dönüşü kaç saniye sürer.")]
    public float bobPeriod = 55f;

    [Tooltip("Kameraya bu kadar yatay uzaklığı geçen bulut diğer tarafa taşınır (görüş alanından büyük olmalı).")]
    public float wrapHalfSpan = 75f;

    private float phase;
    private float lastBob;
    private float t0;
    private Camera cam;

    private void OnEnable()
    {
        phase = Mathf.Repeat(transform.position.x * 0.37f + transform.position.y * 1.3f, Mathf.PI * 2f);

        // Bulutlar aynı anda aynı hareketi yapmasın.
        bobPeriod *= Mathf.Lerp(0.8f, 1.3f, Mathf.Repeat(phase * 0.618f, 1f));

        t0 = Time.time;
        lastBob = Bob(0f);
    }

    private float Bob(float t)
    {
        return Mathf.Sin(t * Mathf.PI * 2f / Mathf.Max(5f, bobPeriod) + phase) * bobAmplitude;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        float bob = Bob(Time.time - t0);
        float dy = bob - lastBob;
        lastBob = bob;

        Vector3 p = transform.position;
        p.x += direction * speed * dt;
        p.y += dy;

        if (cam == null)
            cam = Camera.main;

        if (cam != null)
        {
            float rel = p.x - cam.transform.position.x;

            if (rel > wrapHalfSpan) p.x -= 2f * wrapHalfSpan;
            else if (rel < -wrapHalfSpan) p.x += 2f * wrapHalfSpan;
        }

        transform.position = p;
    }
}
