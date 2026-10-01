using UnityEngine;

public class FinisherTargetHighlight : MonoBehaviour
{
    [Header("Outline")]
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField] private float outlineSize = 0.035f;

    [Header("Sorting")]
    [SerializeField] private int sortingOrderOffset = -1;

    private SpriteRenderer sourceRenderer;
    private SpriteRenderer[] outlineRenderers;

    private void Awake()
    {
        sourceRenderer = GetComponent<SpriteRenderer>();

        if (sourceRenderer == null)
            sourceRenderer = GetComponentInChildren<SpriteRenderer>();

        if (sourceRenderer == null)
        {
            Debug.LogWarning("FINISHER HIGHLIGHT: SpriteRenderer bulunamadı.");
            return;
        }

        CreateOutline();
        SetHighlighted(false);
    }

    private void CreateOutline()
    {
        outlineRenderers = new SpriteRenderer[8];

        Vector2[] directions =
        {
            new Vector2( 1f,  0f),
            new Vector2(-1f,  0f),
            new Vector2( 0f,  1f),
            new Vector2( 0f, -1f),

            new Vector2( 1f,  1f).normalized,
            new Vector2(-1f,  1f).normalized,
            new Vector2( 1f, -1f).normalized,
            new Vector2(-1f, -1f).normalized
        };

        for (int i = 0; i < outlineRenderers.Length; i++)
        {
            GameObject outlineObject = new GameObject("FinisherOutline_" + i);

            outlineObject.transform.SetParent(transform);
            outlineObject.transform.localPosition = directions[i] * outlineSize;
            outlineObject.transform.localRotation = Quaternion.identity;
            outlineObject.transform.localScale = Vector3.one;

            SpriteRenderer renderer =
                outlineObject.AddComponent<SpriteRenderer>();

            renderer.sprite = sourceRenderer.sprite;
            renderer.color = outlineColor;

            renderer.flipX = sourceRenderer.flipX;
            renderer.flipY = sourceRenderer.flipY;

            renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            renderer.sortingOrder =
                sourceRenderer.sortingOrder + sortingOrderOffset;

            outlineRenderers[i] = renderer;
        }
    }

    private void LateUpdate()
    {
        if (sourceRenderer == null || outlineRenderers == null)
            return;

        for (int i = 0; i < outlineRenderers.Length; i++)
        {
            if (outlineRenderers[i] == null)
                continue;

            outlineRenderers[i].sprite = sourceRenderer.sprite;
            outlineRenderers[i].flipX = sourceRenderer.flipX;
            outlineRenderers[i].flipY = sourceRenderer.flipY;
            outlineRenderers[i].color = outlineColor;

            outlineRenderers[i].sortingLayerID =
                sourceRenderer.sortingLayerID;

            outlineRenderers[i].sortingOrder =
                sourceRenderer.sortingOrder + sortingOrderOffset;
        }
    }

    public void SetHighlighted(bool highlighted)
    {
        if (outlineRenderers == null)
            return;

        for (int i = 0; i < outlineRenderers.Length; i++)
        {
            if (outlineRenderers[i] != null)
                outlineRenderers[i].enabled = highlighted;
        }
    }
}