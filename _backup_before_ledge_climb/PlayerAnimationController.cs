using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    private PlayerController player;

    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Header("Parry Sonrası")]
    [Tooltip(
        "Parry animasyonu en az bu kadar oynadıktan sonra (normalize, 0..1) " +
        "yön tuşuna basılırsa hemen koşu/idle'a dönülür. Parry pozunda " +
        "takılı kalmayı önler.")]
    [Range(0f, 1f)]
    [SerializeField] private float parryCancelableAfter = 0.25f;

    public void Initialize(PlayerController controller)
    {
        player = controller;

        if (animator == null &&
            player.modelPivot != null)
        {
            animator =
                player.modelPivot.GetComponent<Animator>();
        }

        if (animator == null)
        {
            Debug.LogWarning(
                "PLAYER ANIMATION: " +
                "ModelPivot üzerinde Animator bulunamadı!"
            );
        }
    }

    public void UpdateAnimation()
    {
        if (animator == null)
            return;

        // Savunmadayken karakter yerinde duruyor. Yön tuşuna
        // basılı olsa bile Speed 0 gitsin; yoksa Parry/Block
        // animasyonları Idle'a dönemiyordu.
        float speed =
            player.IsDefending()
                ? 0f
                : Mathf.Abs(player.moveInput);

        animator.SetFloat(
            "Speed",
            speed
        );

        animator.SetBool(
            "Grounded",
            player.isGrounded
        );

        animator.SetFloat(
            "VerticalVelocity",
            player.rb != null
                ? player.rb.linearVelocity.y
                : 0f
        );

        animator.SetBool(
            "IsDashing",
            player.isDashing
        );

        RecoverStuckStates();
    }

    // =========================================================
    // TAKILI KALAN ANİMASYONLAR
    // =========================================================

    // Parry'nin Animator'daki tek çıkışı "Speed < 0.1 ve Grounded".
    // Yön tuşuna basılıyken bu koşul sağlanmadığı için karakter parry
    // pozunda animasyon bitene kadar (~1 sn) takılı kalıyordu.
    // Artık: savunma bittiyse ve oyuncu hareket etmek istiyorsa (ya da
    // havadaysa) kısa bir oynatmadan sonra hemen locomotion'a döner.
    private void RecoverStuckStates()
    {
        if (animator.IsInTransition(0))
            return;

        AnimatorStateInfo info =
            animator.GetCurrentAnimatorStateInfo(0);

        if (!info.IsName("Parry"))
            return;

        if (player.IsDefending())
            return;

        bool finished =
            info.normalizedTime >= 1f;

        bool wantsToMove =
            Mathf.Abs(player.moveInput) > 0.1f ||
            !player.isGrounded ||
            player.isDashing;

        if (
            finished ||
            (wantsToMove && info.normalizedTime >= parryCancelableAfter)
        )
        {
            ResetToLocomotion(0.05f);
        }
    }

    // Duruma göre Idle / Run / Fall'a döner.
    // Respawn sonrası Death'te takılı kalmayı da bu çözüyor
    // (Death state'inin Animator'da çıkışı yok).
    public void ResetToLocomotion(float fadeTime = 0f)
    {
        if (animator == null)
            return;

        animator.ResetTrigger("Land");
        animator.ResetTrigger("Death");
        animator.ResetTrigger("Parry");

        string target;

        if (!player.isGrounded)
            target = "Fall";
        else if (Mathf.Abs(player.moveInput) > 0.1f)
            target = "Run";
        else
            target = "Idle";

        if (fadeTime > 0f)
        {
            animator.CrossFadeInFixedTime(
                target,
                fadeTime,
                0,
                0f
            );
        }
        else
        {
            animator.Play(
                target,
                0,
                0f
            );
        }
    }

    public void PlayJump()
    {
        if (animator == null)
            return;

        animator.ResetTrigger("Land");

        animator.CrossFadeInFixedTime(
            "Jump",
            0.03f,
            0,
            0f
        );
    }

    public void PlayAttackAnimation(
        int attackStep
    )
    {
        if (animator == null)
            return;

        string stateName;

        switch (attackStep)
        {
            case 1:
                stateName = "Attack1";
                break;

            case 2:
                stateName = "Attack2";
                break;

            case 3:
                stateName = "Attack3";
                break;

            case 4:
                stateName = "Attack4";
                break;

            default:
                return;
        }

        animator.CrossFadeInFixedTime(
            stateName,
            0.035f,
            0,
            0f
        );
    }

    // Havada saldırı: Animator'da "AirAttack" / "DownAttack" state'i varsa
    // onu, yoksa yerdeki kombo animasyonlarını oynatır.
    public void PlayAirAttackAnimation(bool down, int step)
    {
        if (animator == null)
            return;

        string own = down ? "DownAttack" : "AirAttack";

        if (animator.HasState(0, Animator.StringToHash(own)))
        {
            animator.CrossFadeInFixedTime(own, 0.03f, 0, 0f);
            return;
        }

        PlayAttackAnimation(down ? 3 : (step <= 1 ? 1 : 2));
    }

    // Silah hızı: saldırı sırasında animator hızı (bitince 1).
    public void SetSpeed(float speed)
    {
        if (animator != null)
            animator.speed = Mathf.Max(0.05f, speed);
    }

    public void PlayDeathAnimation()
    {
        if (animator == null)
            return;

        animator.ResetTrigger("Land");

        animator.CrossFadeInFixedTime(
            "Death",
            0.03f,
            0,
            0f
        );
    }

    public void PlayParryAnimation()
    {
        if (animator == null)
            return;

        animator.ResetTrigger("Land");

        // Komboda arka arkaya parry: her seferinde baştan oynasın
        // (aynı state'e crossfade bazen yeniden başlatmıyordu).
        animator.PlayInFixedTime(
            "Parry",
            0,
            0f
        );
    }

    public void PlayBlockAnimation()
    {
        if (animator == null)
            return;

        animator.ResetTrigger("Land");

        animator.CrossFadeInFixedTime(
            "Block",
            0.03f,
            0,
            0f
        );
    }

    public void SetBlockingAnimation(
        bool blocking
    )
    {
        if (animator == null)
            return;

        animator.SetBool(
            "Blocking",
            blocking
        );

        if (blocking)
            PlayBlockAnimation();
    }

    public void PlayLandAnimation()
    {
        if (animator == null)
            return;

        animator.SetTrigger("Land");
    }
}