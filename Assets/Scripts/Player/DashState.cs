using UnityEngine;

public class DashState : IPlayerState
{
    PlayerController player;
    PlayerStateMachine sm;

    float timer;
    float dir;
    float speed;

    public DashState(PlayerController player, PlayerStateMachine sm)
    {
        this.player = player;
        this.sm = sm;
    }

    public void Enter()
    {
        player.isDashing = true;

        timer = player.dashTime;
        dir = player.GetDashDirection();
        speed = player.dashDistance / player.dashTime;

        player.afterImageTimer = 0f; // hemen spawn başlasın
    }

    public void Exit()
    {
        player.isDashing = false;
        player.dashCooldownTimer = player.dashCooldown;
    }

    public void Update()
    {
        timer -= Time.deltaTime;

        player.rb.linearVelocity = new Vector2(dir * speed, 0f);

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
        player.afterImageTimer -= Time.deltaTime;

        if (player.afterImageTimer > 0f)
            return;

        player.afterImageTimer = player.afterImageSpacing;

        SpawnGhost();
    }

    void SpawnGhost()
    {
        if (player.afterImagePrefab == null) return;

        GameObject obj = Object.Instantiate(
            player.afterImagePrefab,
            player.transform.position,
            Quaternion.identity
        );

        var ghost = obj.GetComponent<AfterImage>();

        ghost.Init(
            player.playerSprite.sprite,
            player.modelPivot.localScale,
            player.facingDir < 0
        );
    }

    public void FixedUpdate() { }
}