
using UnityEngine;

public class ParallaxEffect : MonoBehaviour
{
    [Header("Parallax Settings")]
    [Range(0f, 1f)]
    public float parallaxX = 0.5f;

    [Range(0f, 1f)]
    public float parallaxY = 0.5f;

    [Header("Camera")]
    public Transform cameraTransform;

    private Vector3 lastCameraPosition;
    private bool isInitialized;

    private void Start()
    {
        if (cameraTransform == null)
        {
            Camera mainCamera = Camera.main;

            if (mainCamera != null)
                cameraTransform = mainCamera.transform;
        }

        if (cameraTransform == null)
        {
            Debug.LogError(
                "ParallaxEffect: Kamera bulunamadi!", this
            );

            enabled = false;
            return;
        }

        lastCameraPosition = cameraTransform.position;
        isInitialized = true;
    }

    private void LateUpdate()
    {
        if (!isInitialized || cameraTransform == null)
            return;

        Vector3 cameraDelta =
            cameraTransform.position - lastCameraPosition;

        transform.position += new Vector3(
            cameraDelta.x * parallaxX,
            cameraDelta.y * parallaxY,
            0f
        );

        lastCameraPosition = cameraTransform.position;
    }
}
