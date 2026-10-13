using UnityEngine;

/// <summary>
/// ÖN PLAN SALINIMI: kameraya yakın bitkiler (büyük siyah yapraklar/otlar) kökünden çok hafif, çok yavaş sallanır.
/// Shader'dan bağımsızdır: objeyi alt-orta noktasından birkaç derece döndürür.
/// Bir "esinti" gelip geçer: salınım zaman zaman biraz güçlenir, sonra sakinleşir.
/// ParallaxEffect'ten sonra çalışır, onunla çakışmaz (sadece kendi farkını uygular). Sadece Play'de.
/// </summary>
[DefaultExecutionOrder(10025)]
[DisallowMultipleComponent]
public class ForegroundSway : MonoBehaviour
{
    [Tooltip("En fazla kaç derece sallanır. Ön plan büyük göründüğü için küçük tut (0.6 - 1.8).")]
    [Range(0f, 5f)] public float angle = 1.1f;

    [Tooltip("Hız (devir/sn). 0.2 = ~5 sn'de bir salınım.")]
    [Range(0.05f, 1f)] public float speed = 0.22f;

    [Tooltip("Esinti etkisi: 0 = hep aynı genlik, 1 = sakin ve rüzgârlı anlar arasında belirgin fark.")]
    [Range(0f, 1f)] public float gustiness = 0.55f;

    private Quaternion baseRot;
    private Vector3 lastDelta;
    private Vector3 pivotLocal;
    private float phase;
    private float speedMul;
    private bool ready;

    private void OnEnable()
    {
        baseRot = transform.rotation;
        lastDelta = Vector3.zero;

        var sr = GetComponent<SpriteRenderer>();

        if (sr != null && sr.sprite != null)
        {
            Bounds b = sr.sprite.bounds;
            pivotLocal = new Vector3(b.center.x, b.min.y, 0f);   // alt-orta
        }
        else
            pivotLocal = Vector3.zero;

        Vector3 p = transform.position;
        float h = Mathf.Abs(Mathf.Sin(p.x * 12.9898f + p.y * 78.233f) * 43758.5453f) % 1f;
        phase = h * Mathf.PI * 2f;
        speedMul = Mathf.Lerp(0.85f, 1.2f, (h * 5.17f) % 1f);
        ready = true;
    }

    private void OnDisable()
    {
        if (!ready)
            return;

        transform.position -= lastDelta;
        transform.rotation = baseRot;
        lastDelta = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (!ready)
            return;

        // Önceki karenin etkisini geri al (parallax vb. konum değişimleri korunur).
        transform.position -= lastDelta;
        transform.rotation = baseRot;

        float t = Time.time * speed * speedMul * Mathf.PI * 2f + phase;
        float gust = 1f - gustiness + gustiness * (0.5f + 0.5f * Mathf.Sin(Time.time * 0.23f + phase * 0.5f) * Mathf.Sin(Time.time * 0.11f + 1.3f));
        float a = (Mathf.Sin(t) * 0.7f + Mathf.Sin(t * 2.1f + 1.1f) * 0.3f) * angle * gust;

        Vector3 pivotBefore = transform.TransformPoint(pivotLocal);
        transform.rotation = baseRot * Quaternion.Euler(0f, 0f, a);
        Vector3 pivotAfter = transform.TransformPoint(pivotLocal);

        lastDelta = pivotBefore - pivotAfter;
        lastDelta.z = 0f;
        transform.position += lastDelta;
    }
}
