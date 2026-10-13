using UnityEngine;
using Spine.Unity;

// Bir SpriteRenderer'ın yerine Spine karakteri gösterir (NPC'ler için).
// Spine objesini sprite'lı objenin ALTINA koyun ve bu script'i ona ekleyin.
//  - Sprite gizlenir (forceRenderingOff); Animator vb. çalışmaya devam eder.
//  - Spine karakteri sprite'ın ayak hizasına ve boyuna oturtulur.
//  - Sprite'ın rengi/alfa'sı, yönü (flipX) ve sıralaması kopyalanır.
[DefaultExecutionOrder(1000)]
[ExecuteAlways]
public class SpineSpriteSwap : MonoBehaviour
{
    public SpriteRenderer sourceSprite;
    public SkeletonAnimation skeletonAnimation;
    public string loopAnimation = "idle";

    [Header("Görünüm")]
    public bool hideSourceSprite = true;
    public bool copyColor = true;
    public bool copySorting = true;
    public int sortingOrderOffset = 0;
    public bool copyFlip = false;
    [Tooltip("copyFlip açıkken: çizim sola bakıyorsa işaretleyin.")]
    public bool artFacesLeft = true;

    [Header("Yerleşim")]
    public bool fitToSprite = true;
    [Tooltip("Spine karakterinin boyu = sprite yüksekliği x bu değer.")]
    public float heightMultiplier = 1f;
    [Tooltip("Çizimdeki karakterin piksel boyu (samuray.png için 1267).")]
    public float artHeightPixels = 1267f;
    public Vector2 extraOffset = Vector2.zero;

    string current;
    MeshRenderer meshRenderer;

    void OnEnable()
    {
        if (!skeletonAnimation) skeletonAnimation = GetComponent<SkeletonAnimation>();
        if (!sourceSprite && transform.parent) sourceSprite = transform.parent.GetComponent<SpriteRenderer>();
        meshRenderer = GetComponent<MeshRenderer>();
        if (sourceSprite && hideSourceSprite) sourceSprite.forceRenderingOff = true;
        current = null;
    }

    void OnDisable()
    {
        if (sourceSprite) sourceSprite.forceRenderingOff = false;
    }

    void Start()
    {
        FitNow();
    }

    [ContextMenu("Sprite'a Oturt")]
    public void FitNow()
    {
        if (!fitToSprite || !sourceSprite || !sourceSprite.sprite) return;
        SkeletonRenderer sr = GetComponent<SkeletonRenderer>();
        float skelScale = (sr && sr.SkeletonDataAsset) ? sr.SkeletonDataAsset.scale : 0.01f;
        float artHeight = artHeightPixels * skelScale;
        if (artHeight <= 0.0001f) return;

        Bounds b = sourceSprite.bounds;
        float worldScale = b.size.y * heightMultiplier / artHeight;
        Vector3 ps = transform.parent ? transform.parent.lossyScale : Vector3.one;
        float px = Mathf.Abs(ps.x) < 0.0001f ? 1f : Mathf.Abs(ps.x);
        float py = Mathf.Abs(ps.y) < 0.0001f ? 1f : Mathf.Abs(ps.y);
        transform.localScale = new Vector3(worldScale / px, worldScale / py, 1f);
        transform.position = new Vector3(b.center.x + extraOffset.x, b.min.y + extraOffset.y, transform.position.z);
    }

    void LateUpdate()
    {
        if (!skeletonAnimation) return;

        if (sourceSprite)
        {
            if (hideSourceSprite) sourceSprite.forceRenderingOff = true;
            var skeleton = skeletonAnimation.Skeleton;
            if (skeleton != null)
            {
                if (copyFlip)
                    skeleton.ScaleX = (sourceSprite.flipX == artFacesLeft) ? 1f : -1f;
                if (copyColor)
                {
                    Color c = sourceSprite.color;
                    skeleton.SetColor(c.r, c.g, c.b, c.a);
                }
            }
            if (copySorting && meshRenderer)
            {
                meshRenderer.sortingLayerID = sourceSprite.sortingLayerID;
                meshRenderer.sortingOrder = sourceSprite.sortingOrder + sortingOrderOffset;
            }
        }

        if (Application.isPlaying && current != loopAnimation && !string.IsNullOrEmpty(loopAnimation))
        {
            current = loopAnimation;
            skeletonAnimation.AnimationState.SetAnimation(0, loopAnimation, true);
        }
    }
}
