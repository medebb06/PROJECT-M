using UnityEngine;

public class PlayerRespawnTest : MonoBehaviour
{
    [Header("Respawn")]
    [SerializeField] private KeyCode respawnKey = KeyCode.R;

    private Vector2 startPosition;

    private PlayerController player;
    private PlayerCombatController combatController;
    private Health health;
    private SpriteRenderer[] renderers;

    void Awake()
    {
        startPosition = transform.position;

        player = GetComponent<PlayerController>();
        combatController = GetComponent<PlayerCombatController>();
        health = GetComponent<Health>();

        renderers =
            GetComponentsInChildren<SpriteRenderer>(true);
    }

    void Update()
    {
        if (Input.GetKeyDown(respawnKey))
        {
            Respawn();
        }
    }

    private void Respawn()
    {
        // ========================================
        // RIGIDBODY
        // ========================================

        if (player != null && player.rb != null)
        {
            // Önce fiziği tekrar aç.
            player.rb.simulated = true;

            // Hızı temizle.
            player.rb.linearVelocity = Vector2.zero;

            // Başlangıç pozisyonuna git.
            player.rb.position = startPosition;

            // Physics'i yeni pozisyona hemen senkronize et.
            Physics2D.SyncTransforms();

            // Rigidbody'yi uyandır.
            player.rb.WakeUp();
        }
        else
        {
            transform.position = startPosition;

            Physics2D.SyncTransforms();
        }

        // ========================================
        // HEALTH
        // ========================================

        if (health != null)
        {
            health.Revive();
        }

        // ========================================
        // PLAYER CONTROLLER RESET
        // ========================================

        if (player != null)
        {
            player.canControl = true;
            player.isInvincible = false;
            player.isDashing = false;
            player.isAttackLocked = false;

            player.moveInput = 0f;
            player.verticalInput = 0f;

            player.jumpHeld = false;
            player.dashPressed = false;
            player.slamPressed = false;

            player.jumpBufferCounter = 0f;
            player.coyoteCounter = 0f;
            player.jumpConsumed = false;

            player.inputLocked = false;
            player.inputLockTimer = 0f;

            player.slamGroundLock = false;
            player.slamLockTimer = 0f;

            player.dashCooldownTimer = 0f;

            // Grounded kontrolünün temiz başlamasını sağla.
            player.isGrounded = false;

            // State machine'i sıfırdan GroundedState'e al.
            if (player.stateMachine != null)
            {
                player.stateMachine.ChangeState(
                    new GroundedState(
                        player,
                        player.stateMachine
                    )
                );
            }
        }

        // ========================================
        // COMBAT
        // ========================================

        if (combatController != null)
        {
            combatController.enabled = true;
        }

        // ========================================
        // SPRITE
        // ========================================

        if (renderers != null)
        {
            foreach (SpriteRenderer sr in renderers)
            {
                if (sr == null)
                    continue;

                Color color = sr.color;
                color.a = 1f;
                sr.color = color;
            }
        }

        Debug.Log("PLAYER RESPAWNED");
    }
}