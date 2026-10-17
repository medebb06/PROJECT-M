using UnityEngine;

// Tutorial sahnesi icin: oyuncu bu bolgeye duserse son checkpoint'e (yoksa fallbackSpawn'a) isinlanir.
// BoxCollider2D (Is Trigger) ile birlikte kullanilir.
[RequireComponent(typeof(BoxCollider2D))]
public class TutorialKillZone : MonoBehaviour
{
    public Vector3 fallbackSpawn;

    void Reset()
    {
        BoxCollider2D c = GetComponent<BoxCollider2D>();
        if (c != null) c.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController pc = other.GetComponentInParent<PlayerController>();
        if (pc == null) return;

        Vector3 target = TutorialCheckpoint.HasCurrent ? TutorialCheckpoint.Current : fallbackSpawn;
        target.z = pc.transform.position.z;
        pc.transform.position = target;

        Rigidbody2D rb = pc.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }
}
