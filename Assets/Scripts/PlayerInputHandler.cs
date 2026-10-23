using UnityEngine;

/// <summary>
/// Oyuncu girişi. TAMPONLAR (zıplama, dash) kilitliyken de kaydedilir:
/// hasar sersemlemesi / saldırı sırasında basılan tuş kaybolmaz, kilit
/// bitince ilk fırsatta çalışır.
/// </summary>
public class PlayerInputHandler : MonoBehaviour
{
    private PlayerController player;
    private PlayerDefenseController defense;

    /// <summary>Parry penceresi acikken (basarili olana ya da bitene kadar) dash baslamaz; tus tamponda bekler.</summary>
    public static bool LockDashWhileParrying = false;   // dash parry'yi de böler

    public void Initialize(PlayerController controller)
    {
        player = controller;
        defense = controller.GetComponent<PlayerDefenseController>();
    }

    public void ReadInput()
    {
        // =====================================================
        // TAMPONLAR (kilitliyken de)
        // =====================================================

        if (MobileInput.GetKeyDown(KeyCode.Space))
        {
            // Dash / kilit sırasında basılan zıplama kaybolmasın: dash, saldırı ve parry ile aynı tampon.
            float jb = player.Movement.jumpBufferTime;
            if (player.isDashing || !player.canControl || player.inputLocked)
                jb = Mathf.Max(jb, player.minInputBuffer);

            player.Movement.jumpBufferCounter = jb;

            player.Movement.wallJumpBufferCounter =
                player.Movement.wallJumpBufferTime;
        }

        if (MobileInput.GetKeyDown(KeyCode.LeftShift))
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
            MobileInput.GetAxisRaw("Horizontal");

        player.verticalInput =
            MobileInput.GetAxisRaw("Vertical");

        player.jumpHeld =
            MobileInput.GetKey(KeyCode.Space);

        // =====================================================
        // DASH (tamponlu): DashState.Enter tamponu tüketir.
        // =====================================================

        player.dashPressed =
            player.dashBufferTimer > 0f &&
            !(LockDashWhileParrying && defense != null && defense.IsParrying);
    }
}