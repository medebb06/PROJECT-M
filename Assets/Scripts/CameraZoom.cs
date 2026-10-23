using UnityEngine;

public class CameraZoom : MonoBehaviour
{
    private Camera cam;

    [Header("Zoom Ayarları")]
    public float defaultSize = 5f;   // Normal kamera boyutu
    public float zoomedOutSize = 8f; // Uzaklaşmış kamera boyutu

    void Start()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = defaultSize;
    }

    void Update()
    {
    }
}