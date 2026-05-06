using UnityEngine;

public class SimpleSpriteVFX : MonoBehaviour
{
    public Sprite[] frames;
    public float fps = 12f;

    private SpriteRenderer sr;
    private int i = 0;
    private float t = 0f;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();

        Debug.Log("PREFAB NAME: " + gameObject.name);
        Debug.Log("FRAMES SIZE: " + frames.Length);
        if (frames == null || frames.Length == 0)
        {
            Debug.LogError("NO FRAMES!");
            Destroy(gameObject);
            return;
        }

        sr.sprite = frames[0];
        sr.color = Color.white;
    }
    void Start()
    {
        Debug.Log("VFX SPAWNED");
    }
    void Update()
    {
        if (frames == null || frames.Length == 0) return;

        t += Time.deltaTime;

        float frameTime = 1f / fps;

        while (t >= frameTime)
        {
            t -= frameTime;
            i++;

            if (i >= frames.Length)
            {
                Destroy(gameObject);
                return;
            }

            sr.sprite = frames[i];
        }
    }
}