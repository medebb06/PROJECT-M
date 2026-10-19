using UnityEngine;

/// <summary>
/// YUMUSAK ZEMIN GOLGESI. Karakterin ayagina konan elips; ziplayinca kuculur ve solar, yere inince geri gelir.
/// Zemin y'si: Start'taki ayak konumu (veya groundY). Sprite: shadow_soft.
/// </summary>
[ExecuteAlways]
public class ContactShadow : MonoBehaviour
{
    public Transform target;            // takip edilen (oyuncu); bos = sabit golge
    public float groundY = -5.16f;      // golgenin durdugu zemin
    public float baseWidth = 2.2f;
    public float baseHeight = 0.55f;
    [Range(0f, 1f)] public float opacity = 0.45f;
    public float fadeHeight = 3.5f;     // bu kadar yukseklikte golge tamamen kaybolur
    public float feetOffset = 0f;       // hedefin pivotundan ayaga mesafe (negatif = asagi)

    private SpriteRenderer sr;

    private void OnEnable() { sr = GetComponent<SpriteRenderer>(); }

    private void LateUpdate()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;

        float h = 0f;
        float x = transform.position.x;
        if (target != null)
        {
            x = target.position.x;
            h = Mathf.Max(0f, target.position.y + feetOffset - groundY);
        }
        float k = Mathf.Clamp01(1f - h / Mathf.Max(0.1f, fadeHeight));
        transform.position = new Vector3(x, groundY, transform.position.z);
        float s = Mathf.Lerp(0.6f, 1f, k);
        transform.localScale = new Vector3(baseWidth * s, baseHeight * s, 1f);
        Color c = sr.color; c.a = opacity * k; sr.color = c;
    }
}
