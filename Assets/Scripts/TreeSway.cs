using UnityEngine;

/// Ağacı rüzgârla hafifçe sallar: taban (pivot BottomCenter) etrafında küçük açı.
/// Rüzgâr yönü WeatherDirector'ın yazdığı _ProjectM_WindDir global'inden gelir.
[ExecuteAlways]
public class TreeSway : MonoBehaviour
{
    public float angle = 0.8f;       // derece, genlik
    public float speed = 0.35f;      // Hz
    public float gustSpeed = 0.13f;  // Hz, esinti dalgası
    public float windBias = 1.3f;    // rüzgâr yönüne eğilme (derece)
    public float phase = -1f;        // -1: nesne konumundan türet (her ağaç farklı)

    void OnDisable() { transform.localRotation = Quaternion.identity; }

    void LateUpdate()
    {
        float ph = phase >= 0f ? phase : Mathf.Abs(transform.position.x) * 0.37f;
        float t = Application.isPlaying ? Time.time : (float)UnityEditor_Time();
        float wind = Shader.GetGlobalFloat("_ProjectM_WindDir");
        float gust = 0.65f + 0.35f * Mathf.Sin(t * gustSpeed * 6.283f + ph);
        float a = (Mathf.Sin(t * speed * 6.283f + ph) * 0.65f + Mathf.Sin(t * speed * 2.1f * 6.283f + ph * 1.7f) * 0.35f) * angle * gust
                  - wind * windBias;
        transform.localRotation = Quaternion.Euler(0, 0, a);
    }

    static double UnityEditor_Time()
    {
#if UNITY_EDITOR
        return UnityEditor.EditorApplication.timeSinceStartup;
#else
        return 0;
#endif
    }
}
