using UnityEngine;

public class AfterImage : MonoBehaviour
{
    public SpriteRenderer sr;
    public float fadeSpeed = 2f;

    Color color;

    public void Init(Sprite sprite, Vector3 scale, bool flipX)
    {
        sr.sprite = sprite;

        transform.localScale = scale;
        sr.flipX = flipX;

        color = sr.color;
    }

    void Update()
    {
        color.a -= fadeSpeed * Time.deltaTime;
        sr.color = color;

        if (color.a <= 0)
            Destroy(gameObject);
    }
}