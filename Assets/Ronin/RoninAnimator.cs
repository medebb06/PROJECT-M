using UnityEngine;
using Spine.Unity;

// Ronin karakterinin idle / run animasyonlarını yönetir.
// SkeletonAnimation olan objeye ekleyin. Rigidbody2D bulursa hızına göre kendisi
// idle <-> run geçişi yapar; bulamazsa koddan SetRunning(true/false) çağırın.
public class RoninAnimator : MonoBehaviour
{
    public SkeletonAnimation skeletonAnimation;
    [Tooltip("Boş bırakılırsa bu objede veya üst objelerde aranır.")]
    public Rigidbody2D body;
    public string idleAnimation = "idle";
    public string runAnimation = "run";
    [Tooltip("Bu hızın üstünde run oynar.")]
    public float runSpeedThreshold = 0.1f;
    [Tooltip("Karakter çizimde sola bakıyor; sağa gidince yatayda çevrilir.")]
    public bool faceMovementDirection = true;

    string current;

    void Awake()
    {
        if (!skeletonAnimation) skeletonAnimation = GetComponent<SkeletonAnimation>();
        if (!body) body = GetComponentInParent<Rigidbody2D>();
    }

    void Start()
    {
        Play(idleAnimation);
    }

    void Update()
    {
        if (!body) return;
        float vx = body.linearVelocity.x;
        bool moving = Mathf.Abs(vx) > runSpeedThreshold;
        SetRunning(moving);
        if (faceMovementDirection && moving)
        {
            Vector3 s = transform.localScale;
            s.x = Mathf.Abs(s.x) * (vx > 0 ? -1f : 1f);
            transform.localScale = s;
        }
    }

    public void SetRunning(bool running)
    {
        Play(running ? runAnimation : idleAnimation);
    }

    void Play(string animationName)
    {
        if (animationName == current || skeletonAnimation == null) return;
        current = animationName;
        skeletonAnimation.AnimationState.SetAnimation(0, animationName, true);
    }
}
