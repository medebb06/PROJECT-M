using UnityEngine;

public class PlayerExecuteState : IPlayerState
{
    private PlayerController player;
    private PlayerStateMachine sm;

    private Vector2 startPosition;
    private Vector2 targetPosition;

    private float timer;
    private float duration;

    private float originalGravityScale;

    public PlayerExecuteState(
        PlayerController player,
        PlayerStateMachine sm,
        Vector2 targetPosition,
        float duration
    )
    {
        this.player = player;
        this.sm = sm;
        this.targetPosition = targetPosition;
        this.duration = duration;
    }

    public void Enter()
    {
        startPosition =
            player.rb.position;

        timer = 0f;

        originalGravityScale =
            player.rb.gravityScale;

        player.rb.gravityScale = 0f;

        player.canControl = false;
        player.isInvincible = true;
        player.isDashing = true;

        player.SetVelocity(
            Vector2.zero
        );
    }

    public void Update()
    {
        // Execute sırasında input yok.
    }

    public void FixedUpdate()
    {
        timer +=
            Time.fixedDeltaTime;

        float t =
            Mathf.Clamp01(
                timer / duration
            );

        float smoothT =
            Mathf.SmoothStep(
                0f,
                1f,
                t
            );

        Vector2 nextPosition =
            Vector2.Lerp(
                startPosition,
                targetPosition,
                smoothT
            );

        player.rb.MovePosition(
            nextPosition
        );

        if (t >= 1f)
        {
            Finish();
        }
    }

    private void Finish()
    {
        player.rb.gravityScale =
            originalGravityScale;

        player.isDashing = false;
        player.isInvincible = false;
        player.canControl = true;

        player.SetVelocity(
            Vector2.zero
        );

        if (player.IsGrounded())
        {
            sm.ChangeState(
                new GroundedState(
                    player,
                    sm
                )
            );
        }
        else
        {
            sm.ChangeState(
                new AirState(
                    player,
                    sm
                )
            );
        }
    }

    public void Exit()
    {
        player.rb.gravityScale =
            originalGravityScale;

        player.isDashing = false;
        player.isInvincible = false;
        player.canControl = true;
    }
}