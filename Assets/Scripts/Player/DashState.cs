using UnityEngine;
using System.Collections;

public class DashState : IPlayerState
{
    PlayerController player;
    PlayerStateMachine sm;

    float timer;
    float dir;
    float speed;
    float fxTimer;

    int originalLayer;

    public DashState(PlayerController player, PlayerStateMachine sm)
    {
        this.player = player;
        this.sm = sm;
    }

    public void Enter()
    {
        player.isDashing = true;
        player.canControl = false;
        player.isInvincible = true;
        player.isAttackLocked = true;

        timer = player.dashTime;
        dir = player.GetDashDirection();
        speed = player.dashDistance / player.dashTime;

        // 🔥 STORE LAYER
        originalLayer = player.gameObject.layer;

        // 🔥 DASH LAYER (Enemy ile etkileşmez)
        player.gameObject.layer = LayerMask.NameToLayer("Dash");

        fxTimer = 0f;
        SpawnGhost();
    }

    public void Exit()
    {
        player.isDashing = false;
        player.canControl = true;
        player.isInvincible = false;

        // 🔥 RESTORE LAYER
        player.gameObject.layer = originalLayer;

        player.dashCooldownTimer = player.dashCooldown;

        player.StartCoroutine(UnlockAttack());
    }

    IEnumerator UnlockAttack()
    {
        yield return null;
        player.isAttackLocked = false;
    }

    public void Update()
    {
        timer -= Time.deltaTime;

        player.rb.linearVelocity = new Vector2(dir * speed, player.rb.linearVelocity.y);

        HandleAfterImage();

        if (timer <= 0f)
        {
            if (player.isGrounded)
                sm.ChangeState(new GroundedState(player, sm));
            else
                sm.ChangeState(new AirState(player, sm));
        }
    }

    void HandleAfterImage()
    {
        fxTimer -= Time.deltaTime;

        if (fxTimer > 0f) return;

        fxTimer = player.afterImageSpacing;
        SpawnGhost();
    }

    void SpawnGhost()
    {
        if (!player.afterImagePrefab || !player.playerSprite) return;

        GameObject obj = Object.Instantiate(
            player.afterImagePrefab,
            player.transform.position,
            Quaternion.identity
        );

        var ghost = obj.GetComponent<AfterImage>();

        if (ghost != null)
        {
            ghost.Init(
                player.playerSprite.sprite,
                player.modelPivot.localScale,
                player.facingDir < 0
            );
        }
    }

    public void FixedUpdate() { }
}