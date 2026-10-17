using UnityEngine;

// LEDGE CLIMB: kenara yetişince hızlı, otomatik tırmanma (Dead Cells tarzı).
//  - Havada kenara doğru basarken, duvarda kayarken ya da duvar zıplamasıyla
//    kenara çarpınca tetiklenir (PlayerMovement.TryGetLedge).
//  - İki faz: önce duvar boyunca yukarı, sonra kenarın üstüne. Toplam süre
//    PlayerMovement.ledgeClimbTime. Bu sürede yerçekimi kapalıdır.
//  - Aşağı (S) basılıyken kenar yakalanmaz.
public class LedgeClimbState : IPlayerState
{
    private readonly PlayerController player;
    private readonly PlayerStateMachine sm;
    private readonly int dir;

    private Vector2 start;
    private Vector2 mid;
    private readonly Vector2 stand;

    private float t;
    private float duration;
    private float split;
    private bool finished;

    private float originalGravityScale;
    private bool gravityOverridden;

    public LedgeClimbState(
        PlayerController player,
        PlayerStateMachine sm,
        int dir,
        Vector2 standPos
    )
    {
        this.player = player;
        this.sm = sm;
        this.dir = dir;
        stand = standPos;
    }

    // Uygunsa tırmanmayı başlatır. dir = 0 ise yön tuşundan alınır.
    public static bool TryStart(
        PlayerController player,
        PlayerStateMachine sm,
        int dir
    )
    {
        if (player == null || player.rb == null)
            return false;

        if (!player.canControl || player.inputLocked)
            return false;

        if (player.isGrounded || player.isDashing || player.slamGroundLock)
            return false;

        // Aşağı basılıysa bırak (kenardan aşağı inmek isteyebilir).
        if (player.verticalInput < -0.5f)
            return false;

        if (dir == 0)
        {
            if (player.moveInput > 0.1f) dir = 1;
            else if (player.moveInput < -0.1f) dir = -1;
            else return false;
        }

        Vector2 standPos;

        if (!player.Movement.TryGetLedge(dir, out standPos))
            return false;

        sm.ChangeState(
            new LedgeClimbState(
                player,
                sm,
                dir,
                standPos
            )
        );

        return true;
    }

    public void Enter()
    {
        PlayerMovement movement = player.Movement;

        PlayerCombatController combat =
            player.GetComponent<PlayerCombatController>();

        if (combat != null)
            combat.CancelAttack();

        movement.isWallSliding = false;
        movement.jumpRising = false;
        movement.wallJumpBlockedDirection = 0;
        movement.wallJumpBufferCounter = 0f;

        start = player.rb.position;
        mid = new Vector2(start.x, stand.y);

        float d1 = Mathf.Abs(mid.y - start.y);
        float d2 = Mathf.Abs(stand.x - mid.x);

        split = (d1 + d2) > 0.001f ? d1 / (d1 + d2) : 0.5f;

        // Kısa tırmanmalar daha da kısa sürsün (alçak kenar = anlık).
        duration = Mathf.Max(0.06f, movement.ledgeClimbTime);

        originalGravityScale = player.rb.gravityScale;
        player.rb.gravityScale = 0f;
        gravityOverridden = true;

        player.rb.linearVelocity = Vector2.zero;

        player.facingDir = dir;

        if (player.playerSprite != null)
            player.playerSprite.flipX = player.facingDir < 0f;

        player.PlayLedgeClimbAnimation();

        t = 0f;
        finished = false;
    }

    public void Exit()
    {
        RestoreGravity();

        player.Movement.ledgeCooldownTimer =
            player.Movement.ledgeRegrabCooldown;
    }

    private void RestoreGravity()
    {
        if (gravityOverridden && player.rb != null)
            player.rb.gravityScale = originalGravityScale;

        gravityOverridden = false;
    }

    public void Update()
    {
        if (!finished)
            return;

        PlayerMovement movement = player.Movement;

        RestoreGravity();

        // Yöne basılıysa koşarak devam et.
        float vx = 0f;

        if (player.moveInput * dir > 0.1f)
            vx = dir * movement.moveSpeed * movement.ledgeExitSpeedRatio;

        player.SetVelocity(new Vector2(vx, 0f));

        if (player.isGrounded)
            sm.ChangeState(new GroundedState(player, sm));
        else
            sm.ChangeState(new AirState(player, sm));
    }

    public void FixedUpdate()
    {
        if (finished)
            return;

        t += Time.fixedDeltaTime;

        float n = Mathf.Clamp01(t / duration);

        // Hızlı başla, yumuşak bitir.
        float e = 1f - (1f - n) * (1f - n);

        Vector2 p;

        if (e < split)
        {
            float k = split > 0.0001f ? e / split : 1f;
            p = Vector2.Lerp(start, mid, k);
        }
        else
        {
            float k = (1f - split) > 0.0001f ? (e - split) / (1f - split) : 1f;
            p = Vector2.Lerp(mid, stand, k);
        }

        player.rb.linearVelocity = Vector2.zero;
        player.rb.MovePosition(p);

        if (n >= 1f)
            finished = true;
    }
}
