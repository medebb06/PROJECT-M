using UnityEngine;

public class SimpleSpriteVFX : MonoBehaviour
{
    public Sprite[] frames;
    public float fps = 12f;

    [Header("Fade Out")]
    public float fadeSpeed = 2f;

    private SpriteRenderer sr;
    private int i = 0;
    private float t = 0f;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();

        if (frames == null || frames.Length == 0)
        {
            Debug.LogError("NO FRAMES!");
            Destroy(gameObject);
            return;
        }

        sr.sprite = frames[0];
    }

    void Update()
    {
        if (frames == null || frames.Length == 0) return;

        // ---------------- FRAME ANIMATION ----------------
        t += Time.deltaTime;

        float frameTime = 1f / fps;

        while (t >= frameTime)
        {
            t -= frameTime;
            i++;

            if (i >= frames.Length)
            {
                // animasyon bitince fade baþlat
                StartCoroutine(FadeOut());
                enabled = false;
                return;
            }

            sr.sprite = frames[i];
        }

        // ---------------- SAFETY FADE (animasyon sýrasýnda bile hafif düþebilir) ----------------
        Color c = sr.color;
        c.a -= Time.deltaTime * (fadeSpeed * 0.7f);
        sr.color = c;
    }

    System.Collections.IEnumerator FadeOut()
    {
        Color c = sr.color;

        while (c.a > 0f)
        {
            c.a -= Time.deltaTime * fadeSpeed;
            sr.color = c;
            yield return null;
        }

        Destroy(gameObject);
    }
}