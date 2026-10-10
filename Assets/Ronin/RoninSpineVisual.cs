using UnityEngine;
using Spine.Unity;

// Player'ın sprite'ı yerine Ronin (Spine) karakterini gösterir.
//
// Kurulum: Ronin Spine objesini Player > ModelPivot'un ALTINA koyun ve bu
// script'i ona ekleyin. Gerisini kendisi bulur.
//
// Nasıl çalışır:
//  - Player'ın SpriteRenderer'ı ve Animator'ı çalışmaya devam eder (saldırı
//    zamanlamaları, animasyon event'leri bozulmasın diye). Sprite sadece
//    ekranda çizilmez (forceRenderingOff).
//  - Animator hangi state'teyse ona karşılık gelen Ronin animasyonu oynar.
//    Ronin'de karşılığı olmayan state'lerde (Jump, Attack, ...) fallback oynar.
//  - Sprite'ın yönü (flipX), rengi/alfa'sı (blink, hasar flaşı, ölüm fade'i) ve
//    sıralaması (sorting layer/order) her kare Ronin'e kopyalanır.
[DefaultExecutionOrder(1000)]
public class RoninSpineVisual : MonoBehaviour
{
    [Header("Kaynak (boş bırakılırsa otomatik bulunur)")]
    public SpriteRenderer sourceSprite;
    public Animator sourceAnimator;
    public Collider2D sourceCollider;
    public SkeletonAnimation skeletonAnimation;

    [Header("Görünüm")]
    public bool hideSourceSprite = true;
    [Tooltip("Ronin çizimde sola bakıyor.")]
    public bool artFacesLeft = true;
    public bool copyColor = true;
    public bool copySorting = true;
    public int sortingOrderOffset = 0;

    [Header("Yerleşim (Start'ta bir kez)")]
    [Tooltip("Ayakları collider'ın altına hizala.")]
    public bool alignFeetToCollider = true;
    [Tooltip("Boyu collider yüksekliğine göre ayarla.")]
    public bool fitHeightToCollider = true;
    [Tooltip("Ronin'in boyu = collider yüksekliği x bu değer.")]
    public float heightMultiplier = 1.25f;
    [Tooltip("Ronin'in çizimdeki görünen boyu (SkeletonData Scale 0.0025 iken).")]
    public float roninArtHeight = 1.08f;
    public Vector2 extraOffset = Vector2.zero;

    [Header("Animator state -> Ronin animasyonu")]
    public string idleState = "Idle";
    public string runState = "Run";
    public string idleAnimation = "idle";
    public string runAnimation = "run";
    [Tooltip("Ronin'de karşılığı olmayan state'lerde oynar.")]
    public string fallbackAnimation = "idle";

    string current;
    MeshRenderer meshRenderer;

    void Awake()
    {
        if (!skeletonAnimation) skeletonAnimation = GetComponent<SkeletonAnimation>();
        meshRenderer = GetComponent<MeshRenderer>();

        PlayerController player = GetComponentInParent<PlayerController>();
        if (!sourceSprite && player) sourceSprite = player.playerSprite;
        if (!sourceCollider && player) sourceCollider = player.col;
        if (!sourceAnimator && player && player.modelPivot)
            sourceAnimator = player.modelPivot.GetComponent<Animator>();
        if (!sourceAnimator) sourceAnimator = GetComponentInParent<Animator>();

        // Aynı objede eski basit animatör script'i varsa çakışmasın.
        RoninAnimator old = GetComponent<RoninAnimator>();
        if (old) old.enabled = false;
    }

    void Start()
    {
        if (sourceSprite && hideSourceSprite) sourceSprite.forceRenderingOff = true;
        FitToCollider();
        Play(idleAnimation);
    }

    void OnDisable()
    {
        // Ronin kapatılırsa eski sprite geri görünsün.
        if (sourceSprite) sourceSprite.forceRenderingOff = false;
    }

    void OnEnable()
    {
        if (sourceSprite && hideSourceSprite && current != null) sourceSprite.forceRenderingOff = true;
    }

    void FitToCollider()
    {
        if (!sourceCollider) return;
        Bounds b = sourceCollider.bounds;

        if (fitHeightToCollider && roninArtHeight > 0.0001f)
        {
            float worldScale = b.size.y * heightMultiplier / roninArtHeight;
            Vector3 parentScale = transform.parent ? transform.parent.lossyScale : Vector3.one;
            float px = Mathf.Abs(parentScale.x) < 0.0001f ? 1f : Mathf.Abs(parentScale.x);
            float py = Mathf.Abs(parentScale.y) < 0.0001f ? 1f : Mathf.Abs(parentScale.y);
            transform.localScale = new Vector3(worldScale / px, worldScale / py, 1f);
        }

        if (alignFeetToCollider)
        {
            transform.position = new Vector3(b.center.x + extraOffset.x, b.min.y + extraOffset.y, transform.position.z);
        }
    }

    void LateUpdate()
    {
        if (!skeletonAnimation) return;
        var skeleton = skeletonAnimation.Skeleton;

        if (sourceSprite && skeleton != null)
        {
            if (hideSourceSprite) sourceSprite.forceRenderingOff = true;

            // Yön: sprite flipX = sola bakıyor.
            bool facingLeft = sourceSprite.flipX;
            skeleton.ScaleX = (facingLeft == artFacesLeft) ? 1f : -1f;

            if (copyColor)
            {
                Color c = sourceSprite.color;
                skeleton.SetColor(c.r, c.g, c.b, c.a);
            }

            if (copySorting && meshRenderer)
            {
                meshRenderer.sortingLayerID = sourceSprite.sortingLayerID;
                meshRenderer.sortingOrder = sourceSprite.sortingOrder + sortingOrderOffset;
            }
        }

        Play(PickAnimation());
    }

    string PickAnimation()
    {
        if (!sourceAnimator || !sourceAnimator.isActiveAndEnabled) return idleAnimation;

        AnimatorStateInfo info = sourceAnimator.IsInTransition(0)
            ? sourceAnimator.GetNextAnimatorStateInfo(0)
            : sourceAnimator.GetCurrentAnimatorStateInfo(0);

        if (info.IsName(runState)) return runAnimation;
        if (info.IsName(idleState)) return idleAnimation;
        return fallbackAnimation;
    }

    void Play(string animationName)
    {
        if (string.IsNullOrEmpty(animationName) || animationName == current) return;
        current = animationName;
        skeletonAnimation.AnimationState.SetAnimation(0, animationName, true);
    }
}
