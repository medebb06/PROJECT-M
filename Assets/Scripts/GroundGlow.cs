using UnityEngine;

/// <summary>
/// Zemin (cimen seridi) boydan boya isik: kameranin x'ini izler, y sabit.
/// Renk TimeOfDay'den (groundLight rengi, doygunlugu kontrol edilir); gece ayin mavisi, gun batiminda sicak.
/// [WORLD]/GroundGlow altindaki Band (zemin boyunca yumusak bant), Halo (cimen ustune sis) ve Edge (ince parlak cizgi) cocuklarini boyar.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(10060)]
public class GroundGlow : MonoBehaviour
{
    public float groundTopY = -4.89f;
    [Range(0f, 2f)] public float strength = 1f;
    [Range(0f, 1f)] public float bandAlpha = 0.34f;
    [Range(0f, 1f)] public float haloAlpha = 0.22f;
    [Range(0f, 1f)] public float edgeAlpha = 0.55f;
    [Range(0f, 1f)] public float whiten = 0.25f;
    public bool shimmer = true;

    private TimeOfDay tod;
    private Camera cam;
    private SpriteRenderer band, halo, edge, src;

    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (tod == null) tod = FindObjectOfType<TimeOfDay>();
        if (band == null) band = Find("Band");
        if (halo == null) halo = Find("Halo");
        if (edge == null) edge = Find("Edge");
        if (cam == null) return;

        transform.position = new Vector3(cam.transform.position.x, groundTopY, 0f);

        Color c = new Color(1f, 0.85f, 0.6f);
        float lum = 1f;
        if (src == null) { var o = GameObject.Find("Ground Light"); if (o != null) src = o.GetComponent<SpriteRenderer>(); }
        if (src != null)
        {
            Color g = src.color;
            float m = Mathf.Max(g.r, g.g, g.b, 0.01f);
            c = new Color(g.r / m, g.g / m, g.b / m);
            lum = Mathf.Clamp(g.a / 0.18f, 0.5f, 1.6f);
        }
        c = Color.Lerp(c, Color.white, whiten);

        float s = strength * lum;
        if (shimmer && Application.isPlaying) s *= 0.94f + 0.06f * Mathf.Sin(Time.time * 0.9f);

        Paint(band, c, bandAlpha * s);
        Paint(halo, c, haloAlpha * s);
        Paint(edge, c, edgeAlpha * s);
    }

    private SpriteRenderer Find(string n)
    {
        Transform t = transform.Find(n);
        return t != null ? t.GetComponent<SpriteRenderer>() : null;
    }

    private static void Paint(SpriteRenderer r, Color c, float a)
    {
        if (r != null) r.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(a));
    }
}
