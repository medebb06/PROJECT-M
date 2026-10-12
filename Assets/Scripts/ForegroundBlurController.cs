
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class ForegroundBlurController : MonoBehaviour
{
    [Header("Blur Material")]
    public Material blurMaterial;

    [Header("Objects To Blur")]
    public SpriteRenderer[] targetRenderers;

    [Header("Blur Settings")]
    [Range(0f, 5f)]
    public float blurAmount = 1.5f;

    private static readonly int BlurSizeID =
        Shader.PropertyToID("_BlurSize");

    private MaterialPropertyBlock propertyBlock;

    private void OnEnable()
    {
        ApplyBlur();
    }

    private void OnValidate()
    {
        if (isActiveAndEnabled)
            ApplyBlur();
    }

    [ContextMenu("Apply Blur")]
    public void ApplyBlur()
    {
        if (blurMaterial == null || targetRenderers == null)
            return;

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        foreach (SpriteRenderer spriteRenderer in targetRenderers)
        {
            if (spriteRenderer == null)
                continue;

            spriteRenderer.sharedMaterial = blurMaterial;

            propertyBlock.Clear();
            spriteRenderer.GetPropertyBlock(propertyBlock);

            propertyBlock.SetFloat(BlurSizeID, blurAmount);
            spriteRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
