using UnityEngine;

/// <summary>
/// ODAK BULANIKLIGI surucusu: oyuncu ve demirci net, gerisi hafif bulanik.
/// Sahnede herhangi bir objede durur. Renderer2D'de FocusBlurFeature ekli olmali.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(10080)]
public class FocusBlurDriver : MonoBehaviour
{
    public bool enabledBlur = true;
    [Range(0f, 1f)] public float strength = 0.9f;
    [Tooltip("Bulaniklik yaricapi (yari cozunurluk pikseli)."), Range(0.3f, 4f)] public float blurSize = 1.3f;
    [Header("Net alan (ekran orani; genislige gore)")]
    [Range(0.03f, 0.6f)] public float sharpWidth = 0.15f;
    [Range(0.05f, 1f)] public float falloffWidth = 0.45f;
    [Range(0.05f, 0.8f)] public float sharpHeight = 0.32f;
    [Range(0.1f, 1.5f)] public float falloffHeight = 0.75f;
    [Tooltip("Net kalacak objeler. Bos birakilirsa oyuncu (PlayerController) ve [NPC] otomatik bulunur.")]
    public Transform target0;
    public Transform target1;
    [Tooltip("Hedefin ayaklarindan govdesine dogru kayma (dunya birimi).")]
    public float yOffset = 0.9f;

    private void OnEnable() { FocusBlurFeature.Active = true; }
    private void OnDisable() { FocusBlurFeature.Active = false; }

    private float nextNpcFind;

    private void LateUpdate()
    {
        FocusBlurFeature.Active = enabledBlur && Application.isPlaying;
        FocusBlurFeature.Strength = strength;
        FocusBlurFeature.BlurSize = blurSize;
        FocusBlurFeature.Shape = new Vector4(sharpWidth, falloffWidth, sharpHeight, falloffHeight);

        Camera cam = Camera.main;
        if (cam == null) return;

        if (target0 == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) target0 = pc.transform;
        }
        if (target1 == null && Time.unscaledTime >= nextNpcFind)
        {
            nextNpcFind = Time.unscaledTime + 1f;
            var npc = GameObject.Find("[NPC]");
            if (npc != null) target1 = npc.transform;
        }

        FocusBlurFeature.Use0 = Set(cam, target0, ref FocusBlurFeature.Focus0);
        FocusBlurFeature.Use1 = Set(cam, target1, ref FocusBlurFeature.Focus1);
    }

    private bool Set(Camera cam, Transform t, ref Vector4 f)
    {
        if (t == null || !t.gameObject.activeInHierarchy) return false;
        Vector3 v = cam.WorldToViewportPoint(t.position + Vector3.up * yOffset);
        f = new Vector4(v.x, v.y, 0f, 0f);
        return true;
    }
}
