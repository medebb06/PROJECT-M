using UnityEngine;

public class PlayerInputHandler : MonoBehaviour
{
    private PlayerController player;

    public void Initialize(PlayerController controller)
    {
        player = controller;
    }

    public void ReadInput()
    {
        if (
            player.inputLocked ||
            !player.canControl
        )
        {
            player.moveInput = 0f;
            player.verticalInput = 0f;
            player.jumpHeld = false;
            player.dashPressed = false;

            return;
        }

        // =====================================================
        // MOVEMENT INPUT
        // =====================================================

        player.moveInput =
            Input.GetAxisRaw("Horizontal");

        player.verticalInput =
            Input.GetAxisRaw("Vertical");

        // =====================================================
        // JUMP HELD
        // =====================================================

        player.jumpHeld =
            Input.GetKey(KeyCode.Space);

        // =====================================================
        // JUMP BUFFER
        // =====================================================

        if (Input.GetKeyDown(KeyCode.Space))
        {
            player.Movement.jumpBufferCounter =
                player.Movement.jumpBufferTime;
        }

        // =====================================================
        // WALL JUMP BUFFER
        // =====================================================

        if (Input.GetKeyDown(KeyCode.Space))
        {
            player.Movement.wallJumpBufferCounter =
                player.Movement.wallJumpBufferTime;
        }

        // =====================================================
        // DASH
        // =====================================================

        player.dashPressed =
            Input.GetKeyDown(
                KeyCode.LeftShift
            );

        // =====================================================
        // DASH COOLDOWN
        // =====================================================

        if (player.dashCooldownTimer > 0f)
        {
            player.dashCooldownTimer -=
                Time.deltaTime;
        }
    }
}