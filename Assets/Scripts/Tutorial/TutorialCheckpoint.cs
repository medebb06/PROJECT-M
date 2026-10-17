using UnityEngine;

// Tutorial sahnesi icin: oyuncu tetikleyiciye girince son checkpoint olarak kaydedilir.
// BoxCollider2D (Is Trigger) ile birlikte kullanilir. Mevcut sistemlere dokunmaz.
[RequireComponent(typeof(BoxCollider2D))]
public class TutorialCheckpoint : MonoBehaviour
{
    public static Vector3 Current;
    public static bool HasCurrent;

    [Tooltip("Oyuncunun dogacagi nokta. Bos birakilirsa bu objenin konumu kullanilir.")]
    public Transform respawnPoint;

    void Reset()
    {
        BoxCollider2D c = GetComponent<BoxCollider2D>();
        if (c != null) c.isTrigger = true;
    }

    void OnEnable()
    {
        // Sahne yeniden yuklenince eski checkpoint kalmasin
        HasCurrent = false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;
        Current = respawnPoint != null ? respawnPoint.position : transform.position;
        HasCurrent = true;
    }
}
