using UnityEngine;

/// <summary>
/// Karakterin altındaki yumuşak gölge. Zıplayınca yerde kalır, yükseldikçe küçülür ve solar.
/// (Tools > Lighting > Build Moonlit Atmosphere ekler.)
/// </summary>
[ExecuteAlways]
public class GroundShadow : MonoBehaviour
{
    public Transform owner;
    public LayerMask groundMask;
    [Range(0f, 1f)] public float baseAlpha = 0.55f;
    public float maxHeight = 5f;
    public Vector2 size = new Vector2(1.6f, 0.35f);

    private SpriteRenderer sr;

    private void OnEnable()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        if (owner == null || sr == null || sr.sprite == null)
            return;

        Vector2 from = (Vector2)owner.position + Vector2.up * 0.5f;
        RaycastHit2D hit = Physics2D.Raycast(from, Vector2.down, 30f, groundMask);

        float groundY = hit.collider != null ? hit.point.y : owner.position.y;
        float height = Mathf.Max(0f, owner.position.y - groundY);
        float k = Mathf.Clamp01(1f - height / Mathf.Max(0.01f, maxHeight));

        transform.position = new Vector3(owner.position.x, groundY + 0.02f, transform.position.z);

        // Ebeveyn ölçeği/çevrilmesi gölgeyi bozmasın.
        Vector3 ps = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        Vector2 sp = sr.sprite.bounds.size;
        float s = Mathf.Lerp(0.55f, 1f, k);

        transform.localScale = new Vector3(
            size.x * s / Mathf.Max(0.0001f, sp.x * Mathf.Abs(ps.x)),
            size.y * s / Mathf.Max(0.0001f, sp.y * Mathf.Abs(ps.y)),
            1f
        );

        Color c = sr.color;
        c.a = baseAlpha * k;
        sr.color = c;
    }
}
