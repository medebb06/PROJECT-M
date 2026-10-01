using UnityEngine;

public class PlayerDeathState : IPlayerState
{
    private PlayerController player;
    private PlayerStateMachine sm;

    private PlayerCombatController combatController;
    private SpriteRenderer[] renderers;
    private float[] originalAlphas;

    private float fadeTimer;
    private float fadeDuration = 0.35f;

    private bool fadeComplete;

    public PlayerDeathState(
        PlayerController player,
        PlayerStateMachine sm
    )
    {
        this.player = player;
        this.sm = sm;
    }

    public void Enter()
    {
        PlayerMovement movement =
            player.Movement;

        fadeTimer = 0f;
        fadeComplete = false;

        // ========================================
        // INPUT KİLİTLE
        // ========================================

        player.canControl = false;
        player.inputLocked = true;

        player.moveInput = 0f;
        player.verticalInput = 0f;

        player.jumpHeld = false;
        player.dashPressed = false;
        player.slamPressed = false;

        movement.jumpBufferCounter = 0f;
        movement.coyoteCounter = 0f;
        movement.wallJumpBufferCounter = 0f;

        player.inputLockTimer = 0f;

        // ========================================
        // INVINCIBILITY
        // ========================================

        player.isInvincible = true;

        // ========================================
        // COMBAT KİLİTLE
        // ========================================

        player.isAttackLocked = true;

        combatController =
            player.GetComponent<
                PlayerCombatController
            >();

        if (combatController != null)
        {
            combatController.enabled = false;
        }

        // ========================================
        // PHYSICS KİLİTLE
        // ========================================

        if (player.rb != null)
        {
            player.rb.linearVelocity =
                Vector2.zero;

            player.rb.angularVelocity =
                0f;

            player.rb.simulated = false;
        }

        // ========================================
        // DEATH ANIMATION
        // ========================================

        player.PlayDeathAnimation();

        // ========================================
        // PLAYER GÖRSELLERİ
        // ========================================

        renderers =
            player.GetComponentsInChildren<
                SpriteRenderer
            >(true);

        originalAlphas =
            new float[
                renderers.Length
            ];

        for (
            int i = 0;
            i < renderers.Length;
            i++
        )
        {
            if (renderers[i] == null)
                continue;

            originalAlphas[i] =
                renderers[i].color.a;
        }
    }

    public void Update()
    {
        if (fadeComplete)
            return;

        fadeTimer +=
            Time.deltaTime;

        float t =
            Mathf.Clamp01(
                fadeTimer /
                fadeDuration
            );

        float alpha =
            Mathf.Lerp(
                1f,
                0f,
                t
            );

        for (
            int i = 0;
            i < renderers.Length;
            i++
        )
        {
            if (renderers[i] == null)
                continue;

            Color color =
                renderers[i].color;

            color.a =
                originalAlphas[i] *
                alpha;

            renderers[i].color =
                color;
        }

        if (t >= 1f)
        {
            fadeComplete = true;
        }
    }

    public void FixedUpdate()
    {
        // Rigidbody simulated false olduğu için
        // fizik tarafından hareket ettirilemez.
    }

    public void Exit()
    {
        // ========================================
        // PHYSICS
        // ========================================

        if (player.rb != null)
        {
            player.rb.simulated = true;

            player.rb.linearVelocity =
                Vector2.zero;

            player.rb.angularVelocity =
                0f;

            player.rb.WakeUp();
        }

        // ========================================
        // INPUT
        // ========================================

        player.canControl = true;
        player.inputLocked = false;
        player.inputLockTimer = 0f;

        player.moveInput = 0f;
        player.verticalInput = 0f;

        player.jumpHeld = false;
        player.dashPressed = false;
        player.slamPressed = false;

        // ========================================
        // COMBAT
        // ========================================

        player.isAttackLocked = false;
        player.isInvincible = false;

        if (combatController != null)
        {
            combatController.enabled = true;
        }

        // ========================================
        // MOVEMENT RESET
        // ========================================

        PlayerMovement movement =
            player.Movement;

        if (movement != null)
        {
            movement.jumpBufferCounter = 0f;
            movement.coyoteCounter = 0f;
            movement.wallJumpBufferCounter = 0f;
            movement.ResetAirData();
        }

        fadeComplete = false;
        fadeTimer = 0f;
    }
}