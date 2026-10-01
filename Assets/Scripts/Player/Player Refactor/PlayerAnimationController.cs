using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    private PlayerController player;

    [Header("Animator")]
    [SerializeField] private Animator animator;

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

        float speed =
            Mathf.Abs(player.moveInput);

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

        animator.CrossFadeInFixedTime(
            "Parry",
            0.03f,
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