using UnityEngine;

public class GroundSlamState : IPlayerState
{
    private PlayerController player;
    private PlayerStateMachine sm;

    private bool impactTriggered;

    public GroundSlamState(PlayerController player, PlayerStateMachine sm)
    {
        this.player = player;
        this.sm = sm;
    }

    public void Enter()
    {
        impactTriggered = false;

        player.canControl = false;

        // slam başlarken horizontal momentum kes
        Vector2 vel = player.rb.linearVelocity;

        vel.x = 0f;
        vel.y = -player.impactSettings.slamSpeed;

        player.SetVelocity(vel);
    }

    public void Exit()
    {
        player.canControl = true;
    }

    public void Update()
    {
        // ---------------- DASH CANCEL ----------------
        if (player.dashPressed && player.dashCooldownTimer <= 0f)
        {
            sm.ChangeState(new DashState(player, sm));
            return;
        }

        // ---------------- IMPACT ----------------
        if (player.isGrounded && !impactTriggered)
        {
            impactTriggered = true;

            SlamImpact();

            sm.ChangeState(new GroundedState(player, sm));
        }
    }

    public void FixedUpdate()
    {
        // sürekli aşağı bastır
        Vector2 vel = player.rb.linearVelocity;

        vel.x = 0f;
        vel.y = -player.impactSettings.slamSpeed;

        player.SetVelocity(vel);
    }

    // =========================================================
    // IMPACT
    // =========================================================

    void SlamImpact()
    {
        player.inputLocked = true;
player.inputLockTimer = player.inputLockDuration;

        player.slamGroundLock = true;
        player.slamLockTimer = player.slamLockDuration;

        // 🔥 HARD STOP (micro bounce killer)
        player.rb.linearVelocity = Vector2.zero;
        player.SetVelocity(Vector2.zero);

        // freeze
        player.StartCoroutine(
            player.FreezeFrame(player.slamFreezeTime)
        );

        // camera shake
        if (player.impulseSource)
        {
            player.impulseSource.GenerateImpulse(1.5f);
        }

        // dust
        player.SpawnDust();

        // ---------------- ENEMY HIT ----------------
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            player.transform.position,
            player.impactSettings.slamDamageRadius,
            player.impactSettings.enemyLayer
        );

        foreach (Collider2D hit in hits)
        {
            Debug.Log("SLAM HIT: " + hit.name);
        }
    
}
}