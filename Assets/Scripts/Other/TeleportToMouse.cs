using UnityEngine;

public class TeleportToMouse : MonoBehaviour
{
    public Camera cam;
    public Rigidbody2D rb;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            Vector3 mousePos = Input.mousePosition;
            mousePos.z = 10f; // camera distance

            Vector3 worldPos = cam.ScreenToWorldPoint(mousePos);

            rb.linearVelocity = Vector2.zero;
            rb.position = worldPos;
        }
    }
}