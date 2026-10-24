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

    /// <summary>Shift'e yön tuşu olmadan basılınca yön girişi için beklenen süre (sn, gerçek zaman).</summary>
    public static float DashDirWindow = 0.07f;

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
            // Tampon en az kalan bekleme + pay kadar sürsün: bekleme tamponu aşarsa dash tuşu yutulmasın.
            player.dashBufferTimer = Mathf.Max(player.dashBufferTime, player.minInputBuffer, player.dashCooldownTimer + 0.1f);

            // Basış anındaki yön tuşu (yoksa sıfır: dash başlarken canlı tuşa / baktığın yöne bakar).
            float pressAxis = MobileInput.GetAxisRaw("Horizontal");
            player.dashPressDir = Mathf.Abs(pressAxis) > 0.1f ? Mathf.Sign(pressAxis) : 0f;

            // Yön tuşu yoksa çok kısa bekle: shift'ten hemen sonra basılan yön (dash + sağ) dash'in yönü olsun.
            player.dashDirWait = player.dashPressDir == 0f ? DashDirWindow : 0f;
        }
        else if (player.dashBufferTimer > 0f)
        {
            // Tampon sürerken yön tuşuna sonradan basıldıysa (sola + dash neredeyse aynı anda) en son yönü al.
            float axisNow = MobileInput.GetAxisRaw("Horizontal");
            if (Mathf.Abs(axisNow) > 0.1f)
            {
                player.dashPressDir = Mathf.Sign(axisNow);
                player.dashDirWait = 0f;
            }

            if (player.dashDirWait > 0f)
                player.dashDirWait -= Time.unscaledDeltaTime;

            // Gerçek zaman: hit-stop tamponu eritmesin.
            player.dashBufferTimer -= Time.unscaledDeltaTime;

            if (player.dashBufferTimer <= 0f)
            {
                player.dashPressDir = 0f;
                player.dashDirWait = 0f;
            }
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
            player.dashDirWait <= 0f &&
            !(LockDashWhileParrying && defense != null && defense.IsParrying);
    }
}