#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ronni modeli oyun başlayınca (Awake'te) kodla oluşturulduğu için sahnede görünmez.
/// Bu gizmo, Edit modunda Player'ın nerede durduğunu gösterir:
/// collider çerçevesi, ayak çizgisi, yön oku ve isim etiketi.
/// Sahne dosyasına hiçbir şey eklemez (sadece Scene görünümünde çizilir).
/// </summary>
public static class PlayerSceneGizmo
{
    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
    static void Draw(PlayerController pc, GizmoType type)
    {
        if (Application.isPlaying)
            return;

        Collider2D col = pc.GetComponent<Collider2D>();

        if (col == null)
            col = pc.GetComponentInChildren<Collider2D>();

        Vector3 pos = pc.transform.position;

        Color c = new Color(0.3f, 0.9f, 1f, 0.9f);
        Gizmos.color = c;

        if (col != null && !col.isTrigger)
        {
            Bounds b = col.bounds;

            Gizmos.DrawWireCube(b.center, b.size);

            // Ayak çizgisi
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.95f);
            Gizmos.DrawLine(
                new Vector3(b.min.x - 0.4f, b.min.y, 0f),
                new Vector3(b.max.x + 0.4f, b.min.y, 0f)
            );

            // Yön oku (sağa)
            Gizmos.color = c;
            Vector3 head = new Vector3(b.max.x + 0.3f, b.center.y, 0f);
            Gizmos.DrawLine(head, head + Vector3.right * 0.8f);
            Gizmos.DrawLine(head + Vector3.right * 0.8f, head + new Vector3(0.55f, 0.2f, 0f));
            Gizmos.DrawLine(head + Vector3.right * 0.8f, head + new Vector3(0.55f, -0.2f, 0f));

            Handles.Label(new Vector3(b.center.x, b.max.y + 0.3f, 0f), "PLAYER (Ronni oyunda çıkar)");
        }
        else
        {
            Gizmos.DrawWireSphere(pos, 0.4f);
            Handles.Label(pos + Vector3.up * 0.6f, "PLAYER (collider yok)");
        }
    }
}
#endif
