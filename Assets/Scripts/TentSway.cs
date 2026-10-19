using UnityEngine;

/// Rüzgârla çadırı hafifçe sallar (taban etrafında dönüş + hafif nefes).
[ExecuteAlways]
public class TentSway : MonoBehaviour
{
    public float angle = 0.9f;      // derece
    public float speed = 0.9f;
    public float gustSpeed = 0.37f;
    public float breathe = 0.012f;  // ölçek titreşimi
    public float windBias = 1.2f;   // rüzgâr yönüne eğilme (derece)

    Vector3 baseScale;
    bool init;

    void OnEnable() { baseScale = transform.localScale; init = true; }
    void OnDisable() { transform.localRotation = Quaternion.identity; }

    void LateUpdate()
    {
        if (!init) OnEnable();
        float t = Application.isPlaying ? Time.time : (float)UnityEditor_Time();
        float wind = Shader.GetGlobalFloat("_ProjectM_WindDir");
        float gust = 0.6f + 0.4f * Mathf.Sin(t * gustSpeed * 6.283f);
        float a = (Mathf.Sin(t * speed * 6.283f) * 0.6f + Mathf.Sin(t * speed * 2.3f * 6.283f + 1.3f) * 0.4f) * angle * gust
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
