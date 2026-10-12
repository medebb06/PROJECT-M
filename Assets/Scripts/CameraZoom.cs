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
        // Z tuşuna basınca uzaklaşır, X tuşuna basınca normale döner
        if (Input.GetKeyDown(KeyCode.Z))
        {
            cam.orthographicSize = zoomedOutSize;
        }
        if (Input.GetKeyDown(KeyCode.X))
        {
            cam.orthographicSize = defaultSize;
        }
    }
}