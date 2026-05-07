using UnityEngine;
using System.Collections;

public class DashState : IPlayerState
{
    private PlayerController player;
    private PlayerStateMachine sm;

    private float timer;
    private float dir;
    private float speed;
    private float fxTimer;

    private int originalLayer;

    public DashState(PlayerController player, PlayerStateMachine sm)
    {
        this.player = player;
        this.sm = sm;
    }

    public void Enter()
    {
        float speedFactor = Mathf.InverseLerp(0f, 20f, player.rb.linearVelocity.magnitude);
        player.audioPlayer.PlayDash(speedFactor);
        player.isDashing = true;
        player.canControl = false;
        player.isInvincible = true;
        player.isAttackLocked = true;

        timer = player.dashTime;

        dir = player.GetDashDirection();

        speed = player.dashDistance / player.dashTime;

        // collision layer save
        originalLayer = player.gameObject.layer;

        // dash layer
        player.gameObject.layer = LayerMask.NameToLayer("Dash");

        // dash başlangıcında vertical velocity temizle
        Vector2 vel = player.rb.linearVelocity;
        vel.y = 0f;

        player.SetVelocity(vel);

        fxTimer = 0f;

        SpawnGhost();
    }

    public void Exit()
    {
        player.isDashing = false;
        player.canControl = true;
        player.isInvincible = false;

        // restore layer
        player.gameObject.layer = originalLayer;

        player.dashCooldownTimer = player.dashCooldown;

        // dash çıkışında momentum koru
        Vector2 vel = player.rb.linearVelocity;

        vel.x = dir * player.moveSpeed * 0.9f;

        player.SetVelocity(vel);

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

        if (timer <= 0f)
        {
            if (player.isGrounded)
                sm.ChangeState(new GroundedState(player, sm));
            else
                sm.ChangeState(new AirState(player, sm));

            return;
        }

        HandleAfterImage();
    }

    public void FixedUpdate()
    {
        // dash boyunca stabil velocity
        Vector2 vel = player.rb.linearVelocity;

        vel.x = dir * speed;
        vel.y = 0f;

        player.SetVelocity(vel);
    }

    // =========================================================
    // AFTER IMAGE
    // =========================================================

    void HandleAfterImage()
    {
        fxTimer -= Time.deltaTime;

        if (fxTimer > 0f)
            return;

        fxTimer = player.afterImageSpacing;

        SpawnGhost();
    }

    void SpawnGhost()
    {
        if (!player.afterImagePrefab || !player.playerSprite)
            return;

        GameObject obj = Object.Instantiate(
            player.afterImagePrefab,
            player.transform.position,
            Quaternion.identity
        );

        AfterImage ghost = obj.GetComponent<AfterImage>();

        if (ghost != null)
        {
            ghost.Init(
                player.playerSprite.sprite,
                player.modelPivot.localScale,
                player.facingDir < 0
            );
        }
    }
}