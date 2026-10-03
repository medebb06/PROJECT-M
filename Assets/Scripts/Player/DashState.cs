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

        // FIX: audioPlayer atanmamışsa NullReferenceException atıyordu.
        if (player.audioPlayer != null)
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

        // FIX: "Dash" layer'ı projede yoksa NameToLayer -1 döner
        // ve gameObject.layer = -1 hata verir.
        int dashLayer = LayerMask.NameToLayer("Dash");

        if (dashLayer >= 0)
        {
            player.gameObject.layer = dashLayer;
        }
        else
        {
            Debug.LogWarning(
                "DashState: 'Dash' layer'ı bulunamadı! " +
                "Project Settings > Tags and Layers'tan ekle " +
                "ve Collision Matrix'i ayarla."
            );
        }

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
            // FIX: Yön artık gerçekten kullanılan kaynaktan
            // (SpriteRenderer.flipX) okunuyor.
            bool flipX =
                player.playerSprite.flipX;

            Vector3 ghostScale =
                player.modelPivot != null
                    ? player.modelPivot.localScale
                    : Vector3.one;

            // Scale'i her zaman pozitif tut,
            // yön bilgisi flipX'te.
            ghostScale.x =
                Mathf.Abs(ghostScale.x);

            ghost.Init(
                player.playerSprite.sprite,
                ghostScale,
                flipX
            );
        }
    }
}