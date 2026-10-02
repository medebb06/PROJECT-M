using UnityEngine;

public class BuildCameraDebug : MonoBehaviour
{
    private void Start()
    {
        Camera cam = GetComponent<Camera>();

        if (cam == null)
        {
            Debug.LogError("CAMERA DEBUG: Camera component bulunamadı!");
            return;
        }

        Debug.Log(
            "BUILD CAMERA DEBUG -> START ORTHO: " +
            cam.orthographicSize
        );
    }

    private void Update()
    {
        Camera cam = GetComponent<Camera>();

        if (cam == null)
            return;

        if (Mathf.Abs(cam.orthographicSize - 5.625f) > 0.01f)
        {
            Debug.Log(
                "CAMERA DEĞİŞTİ -> ORTHO: " +
                cam.orthographicSize
            );
        }
    }
}