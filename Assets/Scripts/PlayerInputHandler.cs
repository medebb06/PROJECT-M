using UnityEngine;

/// <summary>
/// Oyuncu girişi. TAMPONLAR (zıplama, dash) kilitliyken de kaydedilir:
/// hasar sersemlemesi / saldırı sırasında basılan tuş kaybolmaz, kilit
/// bitince ilk fırsatta çalışır.
/// </summary>
public class PlayerInputHandler : MonoBehaviour
{
    private PlayerController player;

    public void Initialize(PlayerController controller)
    {
        player = controller;
    }

    public void ReadInput()
    {
        // =====================================================
        // TAMPONLAR (kilitliyken de)
        // =====================================================

        if (Input.GetKeyDown(KeyCode.Space))
        {
            player.Movement.jumpBufferCounter =
                player.Movement.jumpBufferTime;

            player.Movement.wallJumpBufferCounter =
                player.Movement.wallJumpBufferTime;
        }

        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            player.dashBufferTimer = Mathf.Max(player.dashBufferTime, player.minInputBuffer);
        }
        else if (player.dashBufferTimer > 0f)
        {
            // Gerçek zaman: hit-stop tamponu eritmesin.
            player.dashBufferTimer -= Time.unscaledDeltaTime;
        }

        // Dash bekleme süresi kilitliyken de akar.
        if (player.dashCooldownTimer > 0f)
        {
            player.dashCooldownTimer -=
                Time.deltaTime;
        }

        // =====================================================
        // KİLİT
        // =====================================================

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
        // HAREKET
        // =====================================================

        player.moveInput =
            Input.GetAxisRaw("Horizontal");

        player.verticalInput =
            Input.GetAxisRaw("Vertical");

        player.jumpHeld =
            Input.GetKey(KeyCode.Space);

        // =====================================================
        // DASH (tamponlu): DashState.Enter tamponu tüketir.
        // =====================================================

        player.dashPressed =
            player.dashBufferTimer > 0f;
    }
}